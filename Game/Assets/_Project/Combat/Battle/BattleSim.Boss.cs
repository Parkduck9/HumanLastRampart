using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Combat.PerfLab;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 우두머리: 성벽손 오우거 (Docs/GDD/Boss_WallhandOgre_v1.md, 사용자 확정 2026-10-26)
    // - 점화의 밤에만 한 마리. 본대가 시작(MainPhaseStart)된 뒤 60초에 전투 씨앗이 정한 공격 면의 들판에서 나와
    //   그 성벽에서 가장 약한 자리(성벽 부서짐 W39의 파공 칸 = 체력은 벽 하나라 가장 먼저 뚫리는 칸)로 걸어온다
    // - 빛줄기 밖: 화살·총은 정의 비율(unlitPointDamageMul)만 (범위 피해는 그대로). 빛 안: 100% + 빛 추가 피해, 대신 걷기 ×1.2, 느려짐 없음 — 피해 규칙은 ApplyBattleHitsJob, 걷기는 EnemyStepJob.StepBoss
    // - 성벽에 닿으면 사다리 없이 땅에서 바로 초당 0.5m로 오른다. 오르는 동안 범위 피해 ×2. 한 번 오르는 동안 최대 체력의 knockOffRatio(정의값) 이상 잃으면 손을 놓고 떨어져 6초 쓰러짐,
    //   다시 걸어와 오른다. 함정 폭발 범위 안이면 4초 쓰러짐
    // - 통로 위: 죽을 때까지 버틴다. 병사(OnWallAttack)와 그 성벽 체력(OnWallWallDamage)을 깎고, 모든 설정에서 1순위 표적, 늘 100% 피해
    // - 무작위는 전투 씨앗뿐 — 같은 전투를 다시 돌리면 같다. 우두머리는 일반 무리·최후 돌격·변형 단계 계산 밖에서 맨 끝 무리 하나로 붙는다
    public sealed partial class BattleSim
    {
        public const string BossDefId = "enemy_boss_wallhand";            // 요새 1
        public const string BossDefIdFortress2 = "enemy_boss_wallhand_2"; // 요새 2 (없으면 요새 1 정의)
        public const float BossSpawnSeconds = MainPhaseStart + 60f;       // 본대 시작 뒤 60초
        public const float BossWarnSeconds = PhaseWarnSeconds;            // 나오기 5초 전 단계 띠
        public const float BossSpawnReach = 85f;                          // 성벽 바깥 모서리에서 나오는 거리(m)
        const float BossFallDistance = 2f;                                // 떨어져 쓰러지는 자리 (성벽 중심선에서 CastleHalf + 이만큼)
        const string BossName = "성벽손 오우거";

        EnemyDef bossDef;
        int bossSlot = -1, bossGroup = -1, bossSide;
        float bossAlong;                  // 가려는 성벽 따라 자리(m, WallGeometry.Right 방향, 성벽 가운데 0)
        NativeArray<float> eDown;           // 우두머리 쓰러짐 남은 시간 (적 이동 작업이 줄인다)
        NativeArray<float> eTargetLateral;  // 우두머리가 가려는 성벽 따라 자리 (적별 배열 — 우두머리만 쓴다)
        bool bossTracking;                // 지금 오르는 중인 한 번의 등반을 지켜보는 중
        int bossClimbStartHp, bossPrevHp;
        bool bossSeenSpawn;

        public bool BossPresent => bossSlot >= 0;
        public int BossSlot => bossSlot;
        public bool BossSpawned => bossSlot >= 0 && bossSeenSpawn;
        public bool BossActive => bossSlot >= 0 && EnemyState.IsAlive(eState[bossSlot]);
        public bool BossKilled { get; private set; }
        public float BossKilledAt { get; private set; } = -1f;
        public float BossAppearedAt { get; private set; } = -1f;
        public bool BossReachedWalkway { get; private set; }
        public float BossReachedAt { get; private set; } = -1f;
        public int BossFalls { get; private set; }
        public float BossDownTotal { get; private set; }     // 이번 쓰러짐 전체 길이 (그리기가 누운 자세 단계를 가른다)
        public bool BossDownByFall { get; private set; }     // true = 성벽에서 떨어짐 (떨어짐 → 착지 → 누움 → 일어남), false = 함정 (넘어짐 → 누움 → 일어남)
        public int BossSide => bossSide;
        public float BossTargetAlong => bossAlong;
        public string BossTitle => BossName;
        public int BossMaxHp => bossSlot >= 0 ? eMaxHp[bossSlot] : 0;
        public int BossHp => bossSlot >= 0 ? eHp[bossSlot] : 0;
        public byte BossState => bossSlot >= 0 ? eState[bossSlot] : EnemyState.Waiting;
        public float BossDownLeft => bossSlot >= 0 ? eDown[bossSlot] : 0f;
        public bool BossOnWalkway => bossSlot >= 0 && eState[bossSlot] == EnemyState.OnWall;
        public bool BossClimbing => bossSlot >= 0 && eState[bossSlot] == EnemyState.Climbing;
        public bool BossLit => BossActive && CurrentBeam().Contains(ePos[bossSlot]);
        public Vector3 BossPosition => bossSlot >= 0 ? (Vector3)(float3)ePos[bossSlot] : Vector3.zero;

        // 가려는 성벽 자리 (통로 높이, 지도 표시용)
        public Vector3 BossTargetPoint
        {
            get
            {
                float3 n = WallGeometry.Outward(bossSide), r = WallGeometry.Right(bossSide);
                float3 p = n * CastleHalf + r * bossAlong;
                return new Vector3(p.x, WallHeight, p.z);
            }
        }

        // 정비 때 어디가 약한지 보고 함정을 깔 수 있게: 그 면의 가장 약한 자리 = 파공이 나는 칸의 가운데 (WallDamageRules, 전투 씨앗)
        public float BossSpotAlong(int side)
        {
            float from = BattleWallLayout.From(CastleHalf), to = BattleWallLayout.To(CastleHalf);
            int tiles = WallDamageRules.TileCount(from, to);
            return WallDamageRules.TileCenter(from, to, WallDamageRules.BreachTile(seed, side, tiles));
        }

        // 점화의 밤이고 정의가 있을 때만 (요새 2는 전용 정의, 없으면 요새 1 정의)
        EnemyDef ResolveBossDef(IDefinitionLookup defs)
        {
            if (!setup.IsIgnitionNight) return null;
            EnemyDef def = setup.FortressNumber >= 2 ? defs.Get<EnemyDef>(BossDefIdFortress2) : null;
            if (def == null) def = defs.Get<EnemyDef>(BossDefId);
            return def != null && def.IsBoss ? def : null;
        }

        // PlanAttack 뒤: 공격 면 중 하나를 씨앗으로 골라 맨 끝 무리로 붙인다 (일반 무리의 계획·난수는 그대로)
        void AddBossGroup(List<(WaveGroup group, int count)> groups)
        {
            var sides = new List<int>();
            foreach (GroupPlan plan in attackPlan) if (!sides.Contains(plan.Side)) sides.Add(plan.Side);
            if (sides.Count == 0) sides.Add(FrontSide);
            sides.Sort();
            var random = new Unity.Mathematics.Random((seed ^ 0xB0550B05u) | 1u);
            bossSide = sides[random.NextInt(0, sides.Count)];
            bossAlong = BossSpotAlong(bossSide);
            bossGroup = groups.Count;
            attackPlan.Add(new GroupPlan { Side = bossSide, SpawnAt = BossSpawnSeconds, Flank = false, Count = 1 });   // 우회 발견·전령 대상이 아니다 (나오기 전 단계 띠가 따로 알린다)
            groups.Add((new WaveGroup { Side = (WallSide)bossSide, EnemyDefId = bossDef.Id, Count = 1, SpawnAtSeconds = BossSpawnSeconds, Phase = WavePhase.Main }, 1));
            Array.Resize(ref groupDiscovered, groups.Count);
            groupDiscovered[bossGroup] = true;
        }

        // 우두머리 슬롯 값 (BuildEnemies에서 일반 값을 계산한 뒤 덮어쓴다)
        SpawnEntry ConfigureBossSlot(int slot, EnemyDef def, SpawnEntry spawn)
        {
            bossSlot = slot;
            eTier[slot] = 0;                 // 변형 단계 체력·속도 보너스·빛줄기 끄기 대상이 아니다
            eTierShown[slot] = 0;
            int hp = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, def.MaxHp) * (setup.Difficulty == Difficulty.Easy ? def.EasyHpMul : 1f)));
            eMaxHp[slot] = hp;
            eHp[slot] = hp;
            eSpeed[slot] = def.MoveSpeed;    // 속도 흔들림 없음
            eStop[slot] = CastleHalf + 0.6f; // 사다리 자리와 같은 성벽 바로 앞
            eSide[slot] = (byte)bossSide;
            eTargetLateral[slot] = bossAlong;
            spawn.Side = bossSide;
            spawn.Time = BossSpawnSeconds;
            spawn.Depth = CastleHalf + Mathf.Min(BossSpawnReach, FieldSize - CastleHalf - 5f);
            spawn.Lateral = bossAlong;
            return spawn;
        }

        // 나오기 5초 전부터 (단계 띠 "성벽손 오우거가 나타납니다 — ○쪽"). 이미 나왔으면 false
        public bool TryGetBossWarning(out int side, out float secondsLeft)
        {
            side = bossSide;
            secondsLeft = 0f;
            if (bossSlot < 0 || eState[bossSlot] != EnemyState.Waiting) return false;
            secondsLeft = BossSpawnSeconds - Elapsed;
            return secondsLeft <= BossWarnSeconds;
        }

        // BeginFrame, Spawn 뒤 (작업이 끝난 상태): 등장·통로 도착·처치 기록, 한 번 오르는 동안 잃은 체력으로 떨어뜨리기
        void UpdateBoss()
        {
            if (bossSlot < 0) return;
            byte state = eState[bossSlot];
            if (state == EnemyState.Waiting) return;
            int hpBeforeFrame = bossPrevHp > 0 ? bossPrevHp : eHp[bossSlot];   // 이 틱의 작업이 돌기 전 체력 (등반 시작 체력을 전환 틱에 맞추려고)
            bossPrevHp = eHp[bossSlot];
            if (!bossSeenSpawn)
            {
                bossSeenSpawn = true;
                BossAppearedAt = Elapsed;
                Notice?.Invoke($"{BossName}가 나타났습니다 — {SideNames[bossSide]}");
            }

            if (state == EnemyState.Dead)
            {
                bossTracking = false;
                if (!BossKilled)
                {
                    BossKilled = true;
                    BossKilledAt = Elapsed;
                    Notice?.Invoke($"{BossName}를 쓰러뜨렸습니다!");
                }

                return;
            }

            if (state == EnemyState.OnWall)
            {
                bossTracking = false;
                if (!BossReachedWalkway)
                {
                    BossReachedWalkway = true;
                    BossReachedAt = Elapsed;
                    Notice?.Invoke($"{BossName}가 {SideNames[bossSide]} 통로에 올라섰습니다!");
                }

                return;
            }

            if (state != EnemyState.Climbing)
            {
                bossTracking = false;
                return;
            }

            if (!bossTracking)
            {
                bossTracking = true;   // 이번 등반 시작 체력 = 걷기에서 오르기로 바뀐 틱이 시작될 때의 체력 (그 틱에 맞은 것도 센다)
                bossClimbStartHp = hpBeforeFrame;
            }

            float limit = bossDef.KnockOffRatio * eMaxHp[bossSlot];
            if (bossClimbStartHp - eHp[bossSlot] >= limit) BossFall(bossDef.KnockdownSeconds);
        }

        // 손을 놓고 떨어짐 (또는 오르는 중 함정): 성벽 앞 땅에 내려와 seconds 동안 쓰러짐. 그 뒤 걸어서 다시 성벽에 닿으면 처음부터 오른다
        void BossFall(float seconds)
        {
            float3 n = WallGeometry.Outward(bossSide);
            float3 p = ePos[bossSlot];
            p += n * (CastleHalf + BossFallDistance - math.dot(p, n));
            p.y = 0f;
            ePos[bossSlot] = p;
            eVel[bossSlot] = float3.zero;
            eState[bossSlot] = EnemyState.Walking;
            eClimb[bossSlot] = 0f;
            eDown[bossSlot] = math.max(eDown[bossSlot], seconds);
            BossDownTotal = eDown[bossSlot];
            BossDownByFall = true;
            bossTracking = false;
            BossFalls++;
            Notice?.Invoke("손을 놓쳤다!");
        }

        // 함정 폭발 (Explode에서): 반경 안이면 걷는 중에는 쓰러짐 +seconds, 오르는 중이면 떨어진다. 통로 위는 해당 없음
        void BossKnockdownByTrap(Vector3 center, float radius)
        {
            if (bossSlot < 0 || bossDef == null) return;
            byte state = eState[bossSlot];
            if (state != EnemyState.Walking && state != EnemyState.Climbing) return;
            float3 d = ePos[bossSlot] - new float3(center.x, 0f, center.z);
            if (d.x * d.x + d.z * d.z > radius * radius) return;
            if (state == EnemyState.Climbing) BossFall(bossDef.TrapKnockdownSeconds);
            else
            {
                if (eDown[bossSlot] <= 0f) { BossDownTotal = 0f; BossDownByFall = false; }   // 새 쓰러짐 (이미 쓰러져 있으면 누운 시간만 길어진다)
                eDown[bossSlot] += bossDef.TrapKnockdownSeconds;
                BossDownTotal += bossDef.TrapKnockdownSeconds;
                Notice?.Invoke($"{BossName}가 함정에 쓰러졌습니다!");
            }
        }

        // 결과 (BuildResult): 나온 적이 있으면 처치 여부와 걸린 시간(나온 뒤부터)
        void CopyBossResult(BattleResult result)
        {
            if (bossSlot < 0 || !bossSeenSpawn) return;
            result.BossPresent = true;
            result.BossDefeated = BossKilled;
            result.BossSeconds = Mathf.Max(0f, (BossKilled ? BossKilledAt : Elapsed) - BossAppearedAt);
            result.BossFalls = BossFalls;
        }

        // 전투 끝 보고 한 줄 (없으면 null)
        public string BossEndLine
        {
            get
            {
                if (bossSlot < 0 || !bossSeenSpawn) return null;
                float seconds = Mathf.Max(0f, (BossKilled ? BossKilledAt : Elapsed) - BossAppearedAt);
                string time = $"{(int)(seconds / 60f)}:{(int)(seconds % 60f):00}";
                return BossKilled ? $"{BossName} 처치 ({time})" : $"{BossName}는 쓰러뜨리지 못했습니다 ({time})";
            }
        }

        // 시험용
        public byte DebugState(int enemyIndex) => eState[enemyIndex];
        public Vector3 DebugPosition(int enemyIndex) => (Vector3)(float3)ePos[enemyIndex];
        public float DebugDown(int enemyIndex) => eDown[enemyIndex];
        public void DebugSetState(int enemyIndex, byte state) => eState[enemyIndex] = state;
        public void DebugSetHp(int enemyIndex, int hp) => eHp[enemyIndex] = hp;
        public void DebugSkipTo(float seconds) { if (seconds > Elapsed) Elapsed = seconds; }   // 사진용: 등장 직전으로
        public void DebugKnockdown(float left, float total, bool byFall) { eDown[bossSlot] = left; BossDownTotal = total; BossDownByFall = byFall; }   // 사진용: 쓰러짐 자세 (total 중 left 남음)
        public void DebugHitArea(int enemyIndex, int damage)
        {
            hitQueue.Enqueue(new HitEvent { Enemy = new EnemyHandle { Index = enemyIndex, Generation = eGen[enemyIndex] }, Damage = damage, Area = 1 });
        }

        // 시험용: 함정 폭발을 직접 일으킨다 (반경 안 우두머리 쓰러짐)
        public void DebugTrapBlast(Vector3 center, float radius) => BossKnockdownByTrap(center, radius);
    }
}
