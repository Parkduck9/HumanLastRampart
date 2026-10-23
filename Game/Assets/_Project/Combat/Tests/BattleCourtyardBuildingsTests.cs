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

        // ── W31 (Codex K-118): 그림 입히기 ──
        static BattleCourtyardBuildings Build(out GameObject parent)
        {
            parent = new GameObject("Courtyard Buildings Test");
            return new BattleCourtyardBuildings(Half, parent.transform);
        }

        [Test]
        public void Art_Textures_AreAssigned_AndTileable()
        {
            var b = Build(out GameObject parent);
            try
            {
                Assert.IsTrue(b.HasWallArt, "벽·지붕 반복 재질이 불러와졌다");
                Assert.IsTrue(b.HasSpriteArt, "앞면 데칼·소품 그림판이 만들어졌다");
                int tiled = 0;
                foreach (var r in b.Renderers)
                {
                    Texture tex = r.sharedMaterial.HasProperty("_BaseMap") ? r.sharedMaterial.GetTexture("_BaseMap") : r.sharedMaterial.mainTexture;
                    Assert.IsNotNull(tex, r.name + " 재질에 그림이 있다");
                    Assert.AreEqual(FilterMode.Point, tex.filterMode, r.name + " 점 필터");
                    if (r.name.StartsWith("Walls") || r.name.StartsWith("Roofs"))
                    {
                        Assert.AreEqual(TextureWrapMode.Repeat, tex.wrapMode, r.name + " 반복");
                        Assert.AreEqual(256, tex.width, r.name + " 256px = 8m");
                        tiled++;
                    }
                }

                Assert.AreEqual(5, tiled, "벽 셋(석조·회벽·판벽) + 지붕 둘(슬레이트·기와)");
                Assert.AreEqual(4, b.FacadeDecals, "기능 건물 네 채 앞면 데칼");
                Assert.Greater(b.PropQuads, 6, "굴뚝·종탑·울타리·건초·장작·우물·물통");
            }
            finally { b.Dispose(); Object.DestroyImmediate(parent); }
        }

        [Test]
        public void Art_Uvs_AreMeterBased_NotStretched()
        {
            var b = Build(out GameObject parent);
            try
            {
                float expected = 1f / BattleCourtyardBuildings.TextureMeters;   // 0.125 UV/m
                int checkedTriangles = 0;
                foreach (var r in b.Renderers)
                {
                    if (!r.name.StartsWith("Walls") && !r.name.StartsWith("Roofs")) continue;
                    Mesh m = r.GetComponent<MeshFilter>().sharedMesh;
                    Vector3[] v = m.vertices; Vector2[] uv = m.uv; int[] t = m.triangles;
                    Assert.AreEqual(v.Length, uv.Length, r.name + " UV가 있다");
                    for (int i = 0; i < t.Length; i += 3)
                        for (int e = 0; e < 3; e++)
                        {
                            int a = t[i + e], c = t[i + (e + 1) % 3];
                            float world = (v[a] - v[c]).magnitude, tex = (uv[a] - uv[c]).magnitude;
                            if (world < 0.05f) continue;
                            Assert.AreEqual(expected, tex / world, expected * 0.03f, r.name + " 변 길이당 UV (늘어나지 않음)");
                        }

                    checkedTriangles += t.Length / 3;
                }

                Assert.Greater(checkedTriangles, 100);
            }
            finally { b.Dispose(); Object.DestroyImmediate(parent); }
        }

        [Test]
        public void Art_SpriteTops_StayUnderPieceLimit_AndDrawCallsStayLow()
        {
            var b = Build(out GameObject parent);
            try
            {
                Assert.LessOrEqual(b.DrawCalls, 6, "낮 그리기: 벽 3 + 지붕 2 + 그림판 1");
                float floor = BattleSim.WallHeight;
                foreach (var r in b.Renderers)
                {
                    if (r.name != "Building Sprites") continue;
                    foreach (Vector3 p in r.GetComponent<MeshFilter>().sharedMesh.vertices)
                        Assert.LessOrEqual(p.y - floor, BattleCourtyardBuildings.MaxPieceHeight + 0.05f, "소품 꼭대기 3m 이내");
                }
            }
            finally { b.Dispose(); Object.DestroyImmediate(parent); }
        }

        [Test]
        public void Art_Night_SwapsAtlas_AndLightsWindowsOnly()
        {
            var b = Build(out GameObject parent);
            try
            {
                Assert.IsFalse(b.WindowsLit);
                Texture dayTex = b.AtlasInUse;
                b.Tick(true);
                Assert.IsTrue(b.Night);
                Assert.IsTrue(b.WindowsLit, "밤에는 창문이 켜진다");
                Assert.AreNotEqual(dayTex, b.AtlasInUse, "밤 앞면 그림으로 바뀐다");
                Assert.LessOrEqual(b.DrawCalls, 7, "밤: 켜진 창 1번만 늘어난다 (빛 칸은 안 쓴다)");
                b.Tick(false);
                Assert.IsFalse(b.WindowsLit);
                Assert.AreEqual(dayTex, b.AtlasInUse, "낮에는 낮 그림");
            }
            finally { b.Dispose(); Object.DestroyImmediate(parent); }
        }

        [Test]
        public void Art_PropBillboards_FaceTheCamera()
        {
            var b = Build(out GameObject parent);
            try
            {
                var board = parent.GetComponentInChildren<BattleCourtyardPropBillboards>();
                Assert.IsNotNull(board);
                Mesh m = board.GetComponent<MeshFilter>().sharedMesh;
                board.Face(Vector3.right);   // 사진기가 동쪽을 본다 → 판 앞면은 서쪽을 본다
                Assert.Less(m.normals[board.FirstVertex].x, -0.99f);
                board.Face(new Vector3(0f, -0.5f, 1f));   // 수평 성분만 쓴다
                Assert.Less(m.normals[board.FirstVertex].z, -0.99f);
            }
            finally { b.Dispose(); Object.DestroyImmediate(parent); }
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
