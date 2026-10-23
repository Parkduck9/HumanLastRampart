using System.Collections.Generic;
using HumanLastRampart.Combat.Battle;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 마당 건물 블록아웃 (W28): 마당 안 · 탑 둘레 밖 · 북쪽 길 밖 · 낮게 · 서로 안 겹침 (장식 건물·구조물 포함)
    public sealed class BattleCourtyardBuildingsTests
    {
        const float Half = 20f;

        // 이름·땅·높이·지붕 위에 얹힌 것인가
        static List<(string name, Rect rect, float height, bool attached)> All()
        {
            var list = new List<(string, Rect, float, bool)>();
            foreach (var p in BattleCourtyardBuildings.Plots)
                list.Add((p.Name, BattleCourtyardBuildings.Footprint(p, Half), p.TotalHeight, false));
            foreach (var q in BattleCourtyardBuildings.Pieces)
                list.Add((q.Name, BattleCourtyardBuildings.Footprint(q, Half), q.Y0 + q.Height, q.Attached));
            return list;
        }

        [Test]
        public void Buildings_StayInsideCourtyard()
        {
            float inner = Half - 2f;
            foreach (var (name, r, _, _) in All())
            {
                Assert.GreaterOrEqual(r.xMin, -inner, name); Assert.LessOrEqual(r.xMax, inner, name);
                Assert.GreaterOrEqual(r.yMin, -inner, name); Assert.LessOrEqual(r.yMax, inner, name);
            }
        }

        [Test]
        public void Buildings_KeepTowerRingAndRoadClear()
        {
            var o = WallGeometry.Outward(BattleCameraRig.StairsSide);
            var n = new Vector2(o.x, o.z);
            var right = new Vector2(n.y, -n.x);
            foreach (var (name, r, _, _) in All())
            {
                // 탑 둘레: 사각형에서 원점까지 가장 가까운 거리
                float dx = Mathf.Max(r.xMin, 0f, -r.xMax), dz = Mathf.Max(r.yMin, 0f, -r.yMax);
                Assert.Greater(Mathf.Sqrt(dx * dx + dz * dz), BattleCourtyardBuildings.TowerRing, name + " 탑 둘레");
                // 길: 계단 쪽 성벽 방향 띠 (너비 6m)
                Vector2[] corners = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax) };
                float lo = float.MaxValue, hi = float.MinValue, along = float.MinValue;
                foreach (var c in corners) { float s = Vector2.Dot(c, right); lo = Mathf.Min(lo, s); hi = Mathf.Max(hi, s); along = Mathf.Max(along, Vector2.Dot(c, n)); }
                bool crossesStrip = lo < BattleCourtyardBuildings.RoadHalfWidth && hi > -BattleCourtyardBuildings.RoadHalfWidth && along > 0f;
                Assert.IsFalse(crossesStrip, name + " 북쪽 길");
            }
        }

        [Test]
        public void Buildings_AreLow()
        {
            foreach (var p in BattleCourtyardBuildings.Plots)
                Assert.LessOrEqual(p.TotalHeight, BattleCourtyardBuildings.MaxBuildingHeight, p.Name + " 높이");
            foreach (var q in BattleCourtyardBuildings.Pieces)
                Assert.LessOrEqual(q.Y0 + q.Height, BattleCourtyardBuildings.MaxPieceHeight, q.Name + " 높이");
            Assert.Less(BattleCourtyardBuildings.MaxPieceHeight, BattleSim.WallHeight);
        }

        [Test]
        public void Buildings_DoNotOverlap()
        {
            var all = All();
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                {
                    if (all[i].attached || all[j].attached) continue;   // 지붕 위 굴뚝·종탑·우물 지붕은 건물과 겹쳐도 된다
                    Assert.IsFalse(all[i].rect.Overlaps(all[j].rect), all[i].name + "·" + all[j].name + " 겹침");
                }
        }
    }
}
