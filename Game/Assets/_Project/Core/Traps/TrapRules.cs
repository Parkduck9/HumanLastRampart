using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using UnityEngine;

namespace HumanLastRampart.Core.Traps
{
    // 회수 명령 (C-015, 1회차 C-016)
    [Serializable]
    public sealed class RecoveryOrder
    {
        public string RecoveryId;                         // "recovery_" 고유 ID
        public string TerrainId;
        public string SquadId;                            // 투입 분대 하나
        public List<string> TargetTrapIds = new List<string>();   // 지도 표시를 보고 고른 함정
    }

    [Serializable]
    public sealed class RecoveryResult
    {
        public string RecoveryId;
        public List<string> RecoveredTrapIds = new List<string>();     // 회수 성공 → 보관함
        public List<string> ExplodedTrapIds = new List<string>();      // 경로에서 터짐 → 제거, 보관 안 함
        public int SquadLoss;                                          // 투입 분대 손실 (반영하는 쪽이 인원 수 이하로 자른다)
        public List<string> NewlySpottedTrapIds = new List<string>();  // 확인해 지도 표시를 갱신할 함정
    }

    // 함정 판정 공용 계산 (C-015, D-021 G4). 상태가 없고, 같은 입력이면 항상 같은 결과.
    public static class TrapRules
    {
        // 1회차 회수 (P1-C14 5): 출발점 → 고른 함정의 지도 표시 위치들 → 출발점, 직선 이동.
        // - 지도 표시가 실제 위치와 맞는 함정은 처음부터 "아는 함정"(피해 감)
        // - 모르는 함정이 탐지 거리 안에 들어오면 일정 확률로 알아챔
        // - 알아채지 못한 채 위험 거리 안을 지나가면 폭발 → 제거, 병력 손실
        // - 고른 함정은 알고 있고 경로가 닿으면 회수
        public static RecoveryResult ResolveRecovery(
            RecoveryOrder order,
            IReadOnlyList<TrapPlacement> trapsOnTerrain,
            TerrainDef terrain,
            IDefinitionLookup defs,
            int seed)
        {
            var result = new RecoveryResult { RecoveryId = order.RecoveryId };
            if (trapsOnTerrain == null || trapsOnTerrain.Count == 0)
            {
                return result;
            }

            BattleRulesDef rules = FirstRules(defs);
            float spotChance = rules != null ? rules.RecoverySpotChance : 0.6f;
            int lossPerExplosion = rules != null ? rules.RecoveryExplosionLoss : 3;
            var random = DeterministicRandom.From(seed, order.RecoveryId, "recovery");

            int count = trapsOnTerrain.Count;
            var known = new bool[count];
            var gone = new bool[count];
            var reached = new bool[count];
            var detect = new float[count];
            var risk = new float[count];
            for (int i = 0; i < count; i++)
            {
                TrapPlacement trap = trapsOnTerrain[i];
                TrapDef def = defs != null ? defs.Get<TrapDef>(trap.TrapDefId) : null;
                detect[i] = def != null ? def.DetectRadius : 6f;
                risk[i] = def != null ? def.RecoverRisk : 1.5f;
                known[i] = trap.Marked && Distance(trap.ActualX, trap.ActualZ, trap.MarkX, trap.MarkZ) <= detect[i];
            }

            // 경로 만들기: 출발점 → 고른 함정(지도에 표시된 것만)의 표시 위치 → 출발점
            Vector2 gate = terrain != null ? terrain.RecoveryGate : Vector2.zero;
            var path = new List<Vector2> { gate };
            foreach (string targetId in order.TargetTrapIds)
            {
                int index = IndexOf(trapsOnTerrain, targetId);
                if (index >= 0 && trapsOnTerrain[index].Marked)
                {
                    path.Add(new Vector2(trapsOnTerrain[index].MarkX, trapsOnTerrain[index].MarkZ));
                }
            }

            path.Add(gate);

            for (int segment = 0; segment + 1 < path.Count; segment++)
            {
                Vector2 a = path[segment];
                Vector2 b = path[segment + 1];
                for (int i = 0; i < count; i++)
                {
                    if (gone[i])
                    {
                        continue;
                    }

                    TrapPlacement trap = trapsOnTerrain[i];
                    float distance = DistanceToSegment(new Vector2(trap.ActualX, trap.ActualZ), a, b);
                    if (!known[i] && distance <= detect[i] && random.Chance(spotChance))
                    {
                        known[i] = true;
                        result.NewlySpottedTrapIds.Add(trap.TrapId);
                    }

                    if (!known[i] && distance <= risk[i])
                    {
                        gone[i] = true;
                        result.ExplodedTrapIds.Add(trap.TrapId);
                        result.SquadLoss += lossPerExplosion;
                        continue;
                    }

                    if (known[i] && distance <= detect[i])
                    {
                        reached[i] = true;
                    }
                }
            }

            foreach (string targetId in order.TargetTrapIds)
            {
                int index = IndexOf(trapsOnTerrain, targetId);
                if (index >= 0 && !gone[index] && known[index] && reached[index] && !result.RecoveredTrapIds.Contains(targetId))
                {
                    result.RecoveredTrapIds.Add(targetId);
                }
            }

            return result;
        }

        static BattleRulesDef FirstRules(IDefinitionLookup defs)
        {
            if (defs == null)
            {
                return null;
            }

            IReadOnlyList<BattleRulesDef> all = defs.All<BattleRulesDef>();
            return all != null && all.Count > 0 ? all[0] : null;
        }

        static int IndexOf(IReadOnlyList<TrapPlacement> traps, string trapId)
        {
            for (int i = 0; i < traps.Count; i++)
            {
                if (traps[i].TrapId == trapId)
                {
                    return i;
                }
            }

            return -1;
        }

        static float Distance(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx;
            float dz = az - bz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float t = lengthSq > 1e-6f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq) : 0f;
            return Vector2.Distance(point, a + ab * t);
        }
    }
}