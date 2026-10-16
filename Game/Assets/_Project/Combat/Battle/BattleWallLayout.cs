using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 성벽 위 난간 배치 (K-045): 돌출부(막힌 돌)·총안(돌출부 사이 빈칸)·횃불 자리를 한 곳에서 계산한다.
    // 성벽 3D(BattleWorldView)가 돌을 놓고, 병사 자리(BattleSim.Slots)·횃불(BattleLights·BattleEffectsView)이 같은 값을 쓴다 — 서로 어긋나지 않게
    // 위치는 모두 '성벽 따라 m'(WallGeometry.Right 방향, 성벽 가운데 0)
    public static class BattleWallLayout
    {
        // 시안이 꺼져 있어도(돌을 안 그려도) 병사 자리는 확정 시안 1 비율로 잡는다
        public static BattleWorldView.Proportion Proportion => BattleWorldView.Active ? BattleWorldView.Current : BattleWorldView.Drafts[1];

        // 모서리 발판이 차지하는 끝은 비운다
        public static float From(float castleHalf) => -castleHalf + Proportion.TowerPlatform * 0.5f;
        public static float To(float castleHalf) => castleHalf - Proportion.TowerPlatform * 0.5f;

        // 대포 사격구 안의 돌출부인가 (사격구에는 돌출부가 없다)
        public static bool InBay(float merlon, IReadOnlyList<float> bays)
        {
            BattleWorldView.Proportion p = Proportion;
            foreach (float bay in bays) if (Mathf.Abs(merlon - bay) < p.BayWidth * 0.5f + p.MerlonWidth * 0.5f) return true;
            return false;
        }

        // 돌출부 가운데 자리 전부 (사격구 안 것 포함, inBay로 표시)
        public static void MerlonSlots(float castleHalf, IReadOnlyList<float> bays, List<(float center, bool inBay)> output)
        {
            output.Clear();
            BattleWorldView.Proportion p = Proportion;
            float from = From(castleHalf), to = To(castleHalf);
            float period = p.MerlonWidth + p.MerlonGap;
            for (float m = from + p.MerlonGap + p.MerlonWidth * 0.5f; m < to - p.MerlonWidth * 0.5f; m += period)
                output.Add((m, InBay(m, bays)));
        }

        // 돌로 서 있는 돌출부 가운데 (사격구 밖)
        public static void Merlons(float castleHalf, IReadOnlyList<float> bays, List<float> output)
        {
            var all = new List<(float, bool)>();
            MerlonSlots(castleHalf, bays, all);
            output.Clear();
            foreach (var (m, inBay) in all) if (!inBay) output.Add(m);
        }

        // 총안 가운데: 양옆이 모두 서 있는 돌출부(또는 모서리 발판 쪽 끝)인 빈칸만. 사격구와 이어진 빈칸은 대포 자리라 뺀다
        public static void Embrasures(float castleHalf, IReadOnlyList<float> bays, List<float> output)
        {
            var all = new List<(float center, bool inBay)>();
            MerlonSlots(castleHalf, bays, all);
            output.Clear();
            BattleWorldView.Proportion p = Proportion;
            float half = (p.MerlonWidth + p.MerlonGap) * 0.5f;
            for (int k = 0; k < all.Count; k++)
            {
                bool leftStone = k == 0 || !all[k - 1].inBay;   // 첫 빈칸 왼쪽은 모서리 발판 쪽 난간
                if (all[k].inBay || !leftStone) continue;
                output.Add(all[k].center - half);
            }
        }

        // 횃불 자리: 예전처럼 성벽 따라 spacing(m) 간격·가운데 맞춤으로 잡되, 가장 가까운 돌출부 가운데로 옮긴다
        // (돌 뒤라 총안에 선 병사와 겹치지 않는다). 가까운 돌이 1m 안에 없으면(사격구) 그 횃불은 뺀다
        public static void Torches(float castleHalf, float inset, float spacing, IReadOnlyList<float> bays, List<float> output)
        {
            output.Clear();
            var merlons = new List<float>();
            Merlons(castleHalf, bays, merlons);
            float inner = castleHalf - inset;
            int count = Mathf.Max(1, Mathf.FloorToInt(inner * 2f / spacing));
            float start = -(count - 1) * spacing * 0.5f;
            for (int k = 0; k < count; k++)
            {
                float want = start + k * spacing;
                float best = float.NaN, bestDistance = 1f;
                foreach (float m in merlons)
                {
                    float d = Mathf.Abs(m - want);
                    if (d < bestDistance) { bestDistance = d; best = m; }
                }

                if (!float.IsNaN(best) && !output.Contains(best)) output.Add(best);
            }
        }
    }
}
