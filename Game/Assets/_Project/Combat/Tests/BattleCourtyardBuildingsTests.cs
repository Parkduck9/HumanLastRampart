using HumanLastRampart.Combat.Battle;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 마당 건물 블록아웃 (W28): 마당 안 · 탑 둘레 밖 · 북쪽 길 밖 · 성벽보다 낮게 · 서로 안 겹침
    public sealed class BattleCourtyardBuildingsTests
    {
        const float Half = 20f;

        [Test]
        public void Buildings_StayInsideCourtyard()
        {
            float inner = Half - 2f;
            foreach (var p in BattleCourtyardBuildings.Plots)
            {
                Rect r = BattleCourtyardBuildings.Footprint(p, Half);
                Assert.GreaterOrEqual(r.xMin, -inner, p.Name); Assert.LessOrEqual(r.xMax, inner, p.Name);
                Assert.GreaterOrEqual(r.yMin, -inner, p.Name); Assert.LessOrEqual(r.yMax, inner, p.Name);
            }
        }

        [Test]
        public void Buildings_KeepTowerRingAndRoadClear()
        {
            var o = WallGeometry.Outward(BattleCameraRig.StairsSide);
            var n = new Vector2(o.x, o.z);
            foreach (var p in BattleCourtyardBuildings.Plots)
            {
                Rect r = BattleCourtyardBuildings.Footprint(p, Half);
                // 탑 둘레: 사각형에서 원점까지 가장 가까운 거리
                float dx = Mathf.Max(r.xMin, 0f, -r.xMax), dz = Mathf.Max(r.yMin, 0f, -r.yMax);
                Assert.Greater(Mathf.Sqrt(dx * dx + dz * dz), BattleCourtyardBuildings.TowerRing, p.Name + " 탑 둘레");
                // 길: 계단 쪽 성벽 방향 띠 (너비 6m)
                var right = new Vector2(n.y, -n.x);
                Vector2[] corners = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax) };
                float lo = float.MaxValue, hi = float.MinValue, along = float.MinValue;
                foreach (var c in corners) { float s = Vector2.Dot(c, right); lo = Mathf.Min(lo, s); hi = Mathf.Max(hi, s); along = Mathf.Max(along, Vector2.Dot(c, n)); }
                bool crossesStrip = lo < BattleCourtyardBuildings.RoadHalfWidth && hi > -BattleCourtyardBuildings.RoadHalfWidth && along > 0f;
                Assert.IsFalse(crossesStrip, p.Name + " 북쪽 길");
            }
        }

        [Test]
        public void Buildings_BelowWallHeight_AndNotOverlapping()
        {
            var plots = BattleCourtyardBuildings.Plots;
            for (int i = 0; i < plots.Length; i++)
            {
                Assert.Less(plots[i].TotalHeight, BattleSim.WallHeight, plots[i].Name + " 높이");
                for (int j = i + 1; j < plots.Length; j++)
                    Assert.IsFalse(BattleCourtyardBuildings.Footprint(plots[i], Half).Overlaps(BattleCourtyardBuildings.Footprint(plots[j], Half)), plots[i].Name + "·" + plots[j].Name + " 겹침");
            }
        }
    }
}
