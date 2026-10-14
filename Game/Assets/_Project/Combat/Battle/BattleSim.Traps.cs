using System.Collections.Generic;
using HumanLastRampart.Core;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Traps;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 전투 중 함정 (CY4-C1, D-050, BattleTraps.md r3)
    // - 점화식: 지도에서 내 함정 표시를 누르면 FuseSeconds 뒤 실제 위치에서 폭발 → 피해 거리 안 모든 적에게 피해 한 번
    // - 불발 1%(눌러도 안 터지고 묻힌 채 남음), 저절로 1%(적이 처음 발동 거리에 닿을 때 한 번 판정)
    // - 터진 자리는 구덩이: 지나는 적이 느려진다(EnemyStepJob). 지도 표시는 터진 모습으로
    // - 결과: 터진 함정 ConsumedTrapIds, 많이 몰렸던 자리의 안 터진 함정은 낮은 확률로 밀려남 TrapMoves (T4)
    // 무작위는 전투 씨앗 + 함정 ID로 정해 같은 전투를 다시 돌리면 같다
    public sealed partial class BattleSim
    {
        public enum TrapState { Armed, Fusing, Exploded, Dud }

        public struct TrapInfo
        {
            public string TrapId;
            public bool Marked;
            public Vector3 Mark;        // 지도 표시 위치 (설치 때 남긴 곳)
            public Vector3 Position;    // 실제 위치 (그리기·시험용 — 지도에는 표시만 보인다)
            public TrapState State;
            public float DamageRadius;
            public float CraterRadius;
        }

        // T4: 안 터진 함정 6m 안에 적이 한때 20마리 이상 몰렸으면 10%로 2~5m 밀려남 (전투 끝에 한 번)
        const int TrapMoveCrowd = 20;
        const float TrapMoveChance = 0.1f;
        const float TrapMoveMin = 2f, TrapMoveMax = 5f;
        const float TrapCrowdRadius = 6f;

        sealed class TrapRun
        {
            public TrapPlacement Placement;
            public TrapDef Def;
            public Vector2 Pos;
            public TrapState State;
            public float Fuse;
            public bool AutoRolled;
            public int PeakCrowd;
        }

        readonly List<TrapRun> traps = new List<TrapRun>();
        NativeArray<float4> craters;
        int craterCount;
        TerrainDef terrainDef;

        public int TrapCount => traps.Count;
        // 이 지형의 언덕 (함정을 놓을 수 없는 곳, x·z 중심, y 반경) — 그리기용
        public IReadOnlyList<Vector3> Hills => terrainDef != null ? terrainDef.Hills : (IReadOnlyList<Vector3>)System.Array.Empty<Vector3>();

        void InitTraps(IDefinitionLookup defs, Allocator p)
        {
            terrainDef = defs.Get<TerrainDef>(setup.TerrainId);
            foreach (TrapPlacement placement in setup.Traps)
            {
                if (placement.TerrainId != setup.TerrainId) continue;   // 다른 지형 함정은 이 전투에서 발동·이동하지 않는다
                TrapDef def = defs.Get<TrapDef>(placement.TrapDefId);
                if (def == null)
                {
                    Debug.LogWarning($"[Battle] 함정 {placement.TrapId}의 종류 {placement.TrapDefId}를 찾지 못해 건너뜁니다.");
                    continue;
                }

                traps.Add(new TrapRun { Placement = placement, Def = def, Pos = new Vector2(placement.ActualX, placement.ActualZ) });
            }

            craters = new NativeArray<float4>(math.max(1, traps.Count), p);
        }

        public TrapInfo GetTrap(int index)
        {
            TrapRun t = traps[index];
            return new TrapInfo
            {
                TrapId = t.Placement.TrapId,
                Marked = t.Placement.Marked,
                Mark = new Vector3(t.Placement.MarkX, 0f, t.Placement.MarkZ),
                Position = new Vector3(t.Pos.x, 0f, t.Pos.y),
                State = t.State,
                DamageRadius = t.Def.DamageRadius,
                CraterRadius = t.Def.CraterRadius,
            };
        }

        // 함정 피해 거리 안의 살아 있는 적 수 (작업이 끝난 뒤에만 — 도는 중이면 0)
        public int EnemiesInTrapRange(int index)
        {
            if (scheduled || index < 0 || index >= traps.Count) return 0;
            return CountAlive(traps[index].Pos, traps[index].Def.DamageRadius, out _);
        }

        // 지도 표시를 눌러 점화 (표시가 없는 함정은 지도에서 누를 수 없다)
        public bool DetonateTrap(int index)
        {
            if (Ended || index < 0 || index >= traps.Count) return false;
            TrapRun t = traps[index];
            if (t.State != TrapState.Armed) return false;
            RunOrQueue(() => Ignite(t, manual: true));
            return true;
        }

        void Ignite(TrapRun t, bool manual)
        {
            if (t.State != TrapState.Armed) return;
            if (manual && Roll(t, "dud") < t.Def.DudChance)
            {
                t.State = TrapState.Dud;
                Notice?.Invoke("불발! 함정이 터지지 않았습니다");
                return;
            }

            t.State = TrapState.Fusing;
            t.Fuse = manual ? t.Def.FuseSeconds : 0f;
            Notice?.Invoke(manual ? "함정 점화!" : "함정이 저절로 터졌습니다");
        }

        float Roll(TrapRun t, string salt) => DeterministicRandom.From((int)seed, t.Placement.TrapId, salt).NextFloat();

        // BeginFrame에서 (작업이 끝난 뒤) — 점화 대기, 저절로 터짐, 몰림 기록
        void UpdateTraps(float dt)
        {
            foreach (TrapRun t in traps)
            {
                if (t.State == TrapState.Fusing)
                {
                    t.Fuse -= dt;
                    if (t.Fuse <= 0f) Explode(t);
                    continue;
                }

                if (t.State != TrapState.Armed && t.State != TrapState.Dud) continue;
                int crowd = CountAlive(t.Pos, TrapCrowdRadius, out float nearestSq);
                t.PeakCrowd = Mathf.Max(t.PeakCrowd, crowd);
                if (t.State == TrapState.Armed && !t.AutoRolled && nearestSq <= t.Def.TriggerRadius * t.Def.TriggerRadius)
                {
                    t.AutoRolled = true;   // 처음 닿을 때 한 번만
                    if (Roll(t, "auto") < t.Def.AutoChance)
                    {
                        Ignite(t, manual: false);
                        Explode(t);
                    }
                }
            }
        }

        void Explode(TrapRun t)
        {
            var center = new Vector3(t.Pos.x, 0f, t.Pos.y);
            int hit = DamageArea(center, t.Def.DamageRadius, t.Def.Damage);
            t.State = TrapState.Exploded;
            float r = t.Def.CraterRadius;
            craters[craterCount++] = new float4(t.Pos.x, t.Pos.y, r * r, t.Def.CraterSpeedMultiplier);
            effects.Add(new SpellEffect { Fire = false, Pending = false, Position = center, Radius = t.Def.DamageRadius, TimeLeft = 0.4f });
            fireTick.Add(0f);
            effectDamage.Add(0);
            Notice?.Invoke($"함정 폭발! 적 {hit}마리에 피해");
        }

        // 반경 안 살아 있는 적 수와 가장 가까운 거리² (지난 프레임 격자 — 작업이 끝난 뒤라 읽어도 안전)
        int CountAlive(Vector2 at, float radius, out float nearestSq)
        {
            var c = new float3(at.x, 0f, at.y);
            int2 minCell = grid.CellOf(c - new float3(radius, 0f, radius));
            int2 maxCell = grid.CellOf(c + new float3(radius, 0f, radius));
            float radiusSq = radius * radius;
            nearestSq = float.MaxValue;
            int count = 0;
            for (int z = minCell.y; z <= maxCell.y; z++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    int cell = grid.IndexOf(new int2(x, z));
                    int start = cellStart[cell];
                    int end = start + cellCount[cell];
                    for (int k = start; k < end; k++)
                    {
                        int e = sorted[k];
                        if (!EnemyState.IsAlive(eState[e])) continue;
                        float3 d = ePos[e] - c;
                        float distSq = d.x * d.x + d.z * d.z;
                        if (distSq > radiusSq) continue;
                        count++;
                        nearestSq = math.min(nearestSq, distSq);
                    }
                }
            }

            return count;
        }

        void AddTrapResults(BattleResult result)
        {
            foreach (TrapRun t in traps)
            {
                if (t.State == TrapState.Exploded)
                {
                    if (!result.ConsumedTrapIds.Contains(t.Placement.TrapId)) result.ConsumedTrapIds.Add(t.Placement.TrapId);
                    continue;
                }

                // 점화 중에 전투가 끝난 함정은 터진 것으로 보지 않고 그대로 남긴다
                if (t.PeakCrowd < TrapMoveCrowd || Roll(t, "move") >= TrapMoveChance) continue;
                var random = DeterministicRandom.From((int)seed, t.Placement.TrapId, "move_to");
                float angle = random.NextFloat() * Mathf.PI * 2f;
                float distance = Mathf.Lerp(TrapMoveMin, TrapMoveMax, random.NextFloat());
                Vector2 to = t.Pos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if (!ValidGround(to)) continue;   // 갈 곳이 마땅치 않으면 제자리 (C-008 r3)
                result.TrapMoves.Add(new TrapMove { TrapId = t.Placement.TrapId, NewX = to.x, NewZ = to.y });
            }
        }

        // 밀려난 자리가 성벽에 붙거나 성 안·언덕·들판 밖이 아닌가
        bool ValidGround(Vector2 p)
        {
            float fromWall = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) - CastleHalf;
            if (fromWall < (terrainDef != null ? terrainDef.TrapNear : 10f) * 0.5f || fromWall > FieldSize) return false;
            if (terrainDef != null)
            {
                foreach (Vector3 hill in terrainDef.Hills)
                {
                    if ((p - new Vector2(hill.x, hill.z)).sqrMagnitude <= hill.y * hill.y) return false;
                }
            }

            return true;
        }
    }
}
