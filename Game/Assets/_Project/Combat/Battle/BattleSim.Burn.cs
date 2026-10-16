using System.Collections.Generic;
using HumanLastRampart.Combat.PerfLab;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 불바다 화상 (K-035, 사용자 시험 플레이 2차 12-a) — 수치는 임시, 사용자 검토 전
    // - 불바다 피해 틱(0.5초)마다 맞은 적이 BurnChance 확률로 화상: BurnSeconds 동안 체력이 서서히 준다 (다시 걸리면 시간만 새로)
    // - 화상 초당 피해 = 그 불바다 초당 피해 × BurnDamageRatio. 비·소나기면 확률·피해 모두 불 피해 배율(FireDamageMultiplier)만큼 준다
    // - 피해는 일반 사건 줄(HitEvent)로 — 보상·시체 더미 처리는 화살·불바다와 같다
    // - 그리기: 적 그림 붉은 깜빡임(WriteBattleEnemyInstancesJob) + 몸 위 작은 불꽃(BattleView, torch_flame)
    // 투사체 출발점 (K-035, 7번): 병사 그림의 활·총 높이(키의 0.6)와 바깥쪽으로 조금, 대포는 포구 연기 자리(BattlePropView)와 같게
    public sealed partial class BattleSim
    {
        public const float BurnChance = 0.6f;
        public const float BurnSeconds = 3f;
        const float BurnDamageRatio = 0.25f;
        const float MuzzleHeightRatio = 0.6f;         // 병사 그림 높이(SoldierDrawSize)에 곱함
        const float MuzzleForwardDistance = 0.15f;    // 성벽 바깥쪽으로 (m)
        const float CannonMuzzleDistance = 0.7f;      // BattlePropView: 대포 자리 0.25 + 포구 연기 0.45
        const float CannonMuzzleLift = 0.12f;         // BattlePropView SmokeLift
        const float FlameSizeRatio = 0.6f;            // 불꽃 크기 = 적 그림 크기 × 이 값

        NativeArray<float> eBurn;                     // 화상 남은 시간 (0 = 없음). 그리기 작업이 읽는다
        float[] eBurnDps;
        float[] eBurnCarry;                           // 정수 피해로 나가고 남은 소수 부분
        readonly List<int> burning = new List<int>();
        Unity.Mathematics.Random burnRandom;

        public int BurningCount => burning.Count;

        void InitBurn(Allocator p)
        {
            int n = math.max(1, enemyCount);
            eBurn = new NativeArray<float>(n, p);
            eBurnDps = new float[n];
            eBurnCarry = new float[n];
            burnRandom = new Unity.Mathematics.Random((seed ^ 0x5bd1e995u) | 1u);
        }

        // 불바다 틱에 맞은 적 하나 (DamageArea에서). fieldDps = 그 불바다 초당 피해(배율 전)
        void TryIgnite(int e, float fieldDps)
        {
            float multiplier = FireDamageMultiplier;
            if (burnRandom.NextFloat() >= BurnChance * multiplier) return;
            float dps = fieldDps * BurnDamageRatio * multiplier;
            if (eBurn[e] <= 0f) burning.Add(e);
            eBurn[e] = BurnSeconds;
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
