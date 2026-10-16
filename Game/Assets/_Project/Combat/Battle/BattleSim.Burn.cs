using System.Collections.Generic;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.Definitions;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 불바다 화상 (K-035 → K-040 사용자 시험 플레이 3차: '불기둥을 뚫고 나오는 적도 있게, 데미지는 약하게, 몸에 불이 붙어 천천히')
    // - 불바다 위 직접 피해는 약하다(SpellDef.damage 초당). 0.5초 틱마다 적마다 피해를 소수까지 확률로 반올림 — 운 좋은 적은 덜 맞는다
    // - 한 불바다 안에서 처음 맞을 때 한 번만 SpellDef.burnChance 확률로 화상: burnSeconds 동안 초당 burnDamage.
    //   불바다를 벗어나도 계속 탄다. 다른 불바다에서 다시 걸리면 남은 시간·초당 피해 중 큰 쪽
    // - 비·소나기면 확률·직접 피해·화상 피해 모두 불 피해 배율(FireDamageMultiplier)만큼 준다
    // - 피해는 일반 사건 줄(HitEvent)로 — 보상·시체 더미 처리는 화살과 같다. 불바다는 적을 멈추거나 늦추지 않는다(걸어서 지나감)
    // - 그리기: 적 그림 붉은 깜빡임(WriteBattleEnemyInstancesJob) + 몸 위 작은 불꽃이 적을 따라다님(BattleView, torch_flame)
    // 투사체 출발점 (K-035, 7번): 병사 그림의 활·총 높이(키의 0.6)와 바깥쪽으로 조금, 대포는 포구 연기 자리(BattlePropView)와 같게
    public sealed partial class BattleSim
    {
        const float MuzzleHeightRatio = 0.6f;         // 병사 그림 높이(SoldierDrawSize)에 곱함
        const float MuzzleForwardDistance = 0.15f;    // 성벽 바깥쪽으로 (m)
        const float CannonMuzzleDistance = 0.7f;      // BattlePropView: 대포 자리 0.25 + 포구 연기 0.45
        const float CannonMuzzleLift = 0.12f;         // BattlePropView SmokeLift
        const float FlameSizeRatio = 0.6f;            // 불꽃 크기 = 적 그림 크기 × 이 값

        NativeArray<float> eBurn;                     // 화상 남은 시간 (0 = 없음). 그리기 작업이 읽는다
        float[] eBurnDps;
        float[] eBurnCarry;                           // 정수 피해로 나가고 남은 소수 부분
        int[] eFireRolled;                            // 이 적이 화상을 굴린 마지막 불바다 효과 번호 (SpellEffect.Serial)
        readonly List<int> burning = new List<int>();
        Unity.Mathematics.Random burnRandom;

        public int BurningCount => burning.Count;

        void InitBurn(Allocator p)
        {
            int n = math.max(1, enemyCount);
            eBurn = new NativeArray<float>(n, p);
            eBurnDps = new float[n];
            eBurnCarry = new float[n];
            eFireRolled = new int[n];
            burnRandom = new Unity.Mathematics.Random((seed ^ 0x5bd1e995u) | 1u);
        }

        // 불바다 틱 하나 (UpdateSpells에서, 작업이 끝난 뒤 — 지난 프레임 격자). tickDamage = 이번 틱 직접 피해(배율 적용, 소수 가능)
        void FireTick(SpellEffect effect, float tickDamage)
        {
            SpellDef spell = effect.Serial > 0 && effect.SpellIndex >= 0 && effect.SpellIndex < spells.Count ? spells[effect.SpellIndex] : null;
            float multiplier = FireDamageMultiplier;
            float chance = spell != null ? spell.BurnChance * multiplier : 0f;
            var c = new float3(effect.Position.x, 0f, effect.Position.z);
            float radius = effect.Radius;
            int2 minCell = grid.CellOf(c - new float3(radius, 0f, radius));
            int2 maxCell = grid.CellOf(c + new float3(radius, 0f, radius));
            float radiusSq = radius * radius;
            int whole = (int)math.floor(tickDamage);
            float part = tickDamage - whole;
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
                        if (d.x * d.x + d.z * d.z > radiusSq) continue;
                        int damage = whole + (burnRandom.NextFloat() < part ? 1 : 0);
                        if (damage > 0) hitQueue.Enqueue(new HitEvent { Enemy = new EnemyHandle { Index = e, Generation = eGen[e] }, Damage = damage });
                        if (chance <= 0f || eFireRolled[e] == effect.Serial) continue;
                        eFireRolled[e] = effect.Serial;   // 이 불바다에서는 한 번만 굴린다
                        if (burnRandom.NextFloat() < chance) Ignite(e, spell.BurnSeconds, spell.BurnDamage * multiplier);
                    }
                }
            }
        }

        void Ignite(int e, float seconds, float dps)
        {
            if (seconds <= 0f || dps <= 0f) return;
            if (eBurn[e] <= 0f) burning.Add(e);
            eBurn[e] = math.max(eBurn[e], seconds);
            eBurnDps[e] = math.max(eBurnDps[e], dps);
        }

        // BeginFrame에서 (작업이 끝난 뒤, UpdateSpells 다음)
        void UpdateBurns(float dt)
        {
            for (int k = burning.Count - 1; k >= 0; k--)
            {
                int e = burning[k];
                float left = eBurn[e] - dt;
                bool alive = EnemyState.IsAlive(eState[e]);
                if (alive)
                {
                    float carry = eBurnCarry[e] + eBurnDps[e] * math.min(dt, eBurn[e]);
                    int damage = (int)carry;
                    if (damage > 0)
                    {
                        hitQueue.Enqueue(new HitEvent { Enemy = new EnemyHandle { Index = e, Generation = eGen[e] }, Damage = damage });
                        carry -= damage;
                    }

                    eBurnCarry[e] = carry;
                }

                if (!alive || left <= 0f)
                {
                    eBurn[e] = 0f;
                    eBurnDps[e] = 0f;
                    eBurnCarry[e] = 0f;
                    burning[k] = burning[burning.Count - 1];
                    burning.RemoveAt(burning.Count - 1);
                }
                else
                {
                    eBurn[e] = left;
                }
            }
        }

        // 그리기 (작업이 끝난 뒤): 불타는 적마다 몸 위 불꽃 한 개. 보이지 않는 적은 건너뜀. 쓴 개수
        public int WriteBurnFlames(PerfLabInstance[] into, float time, int frames, float fps)
        {
            int count = 0;
            for (int k = 0; k < burning.Count && count < into.Length; k++)
            {
                int e = burning[k];
                PerfLabInstance body = enemyInstances[e];
                float size = body.TintSize.w;
                if (size <= 0f) continue;
                float3 feet = body.PosFrame.xyz;
                int frame = ((int)(time * fps + e * 0.37f) % frames + frames) % frames;
                into[count++] = new PerfLabInstance
                {
                    PosFrame = new float4(feet.x, feet.y + size * 0.35f, feet.z, frame),
                    TintSize = new float4(1f, 1f, 1f, size * FlameSizeRatio),
                };
            }

            return count;
        }

        void ReleaseBurn()
        {
            Release(ref eBurn);
        }
    }
}
