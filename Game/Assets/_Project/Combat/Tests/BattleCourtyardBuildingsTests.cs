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
            foreach (var (name, r, _, _) in All())
            {
                // 탑 둘레: 사각형에서 원점까지 가장 가까운 거리
                float dx = Mathf.Max(r.xMin, 0f, -r.xMax), dz = Mathf.Max(r.yMin, 0f, -r.yMax);
                Assert.Greater(Mathf.Sqrt(dx * dx + dz * dz), BattleCourtyardBuildings.TowerRing, name + " 탑 둘레");
                // 길: 네 성벽 방향 띠 (W35 십자, 너비 6m)
                for (int side = 0; side < 4; side++)
                {
                    var o = WallGeometry.Outward(side);
                    var n = new Vector2(o.x, o.z);
                    var right = new Vector2(n.y, -n.x);
                    Vector2[] corners = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax) };
                    float lo = float.MaxValue, hi = float.MinValue, along = float.MinValue;
                    foreach (var c in corners) { float s = Vector2.Dot(c, right); lo = Mathf.Min(lo, s); hi = Mathf.Max(hi, s); along = Mathf.Max(along, Vector2.Dot(c, n)); }
                    bool crossesStrip = lo < BattleCourtyardBuildings.RoadHalfWidth && hi > -BattleCourtyardBuildings.RoadHalfWidth && along > 0f;
                    Assert.IsFalse(crossesStrip, name + " 성벽 " + side + " 길");
                }
            }
        }

        // 기능 건물은 예전 성벽에 그대로 (병영 동 · 성벽 수리 서 · 함정 공방·상점 남) — 길만 비켜 서 있다
        [Test]
        public void FunctionalBuildings_KeepTheirWalls_AndStayClearOfAllFourStairs()
        {
            var expected = new Dictionary<string, int> { { "성벽 수리", 3 }, { "병영", 1 }, { "함정 공방", 2 }, { "상점", 2 } };
            int found = 0;
            foreach (var p in BattleCourtyardBuildings.Plots)
            {
                if (!expected.TryGetValue(p.Name, out int side)) continue;
                found++;
                Assert.AreEqual(side, p.Side, p.Name + " 성벽");
                Assert.AreNotEqual(BattleCourtyardBuildings.Facade.None, p.Front, p.Name + " 기능 건물 앞면");
                Rect fr = BattleCourtyardBuildings.Footprint(p, Half);
                for (int s = 0; s < 4; s++)   // 층계(폭 3.4m 안쪽 계단 + 돌담)와도 안 겹친다
                {
                    BattleCourtyard.StairFrame(s, out Vector3 n, out Vector3 r);
                    Vector3 c = n * (Half - BattleCourtyard.WallThickness - 0.6f);   // 계단 가운데 (층계참~맨 아래 단 사이)
                    float ex = Mathf.Abs(r.x) * 1.8f + Mathf.Abs(n.x) * 1.4f, ez = Mathf.Abs(r.z) * 1.8f + Mathf.Abs(n.z) * 1.4f;   // 폭 1.5 + 돌담 0.2 (+0.1), 깊이 1.7
                    Assert.IsFalse(fr.Overlaps(Rect.MinMaxRect(c.x - ex, c.z - ez, c.x + ex, c.z + ez)), p.Name + " 이 성벽 " + s + " 계단과 겹침");
                }
            }

            Assert.AreEqual(4, found);
        }

        // 영웅이 걷는 길(네 성벽 → 탑 문 앞)이 어떤 건물 땅도 지나지 않는다
        [Test]
        public void HeroPaths_NeverCrossABuildingFootprint()
        {
            var camera = new GameObject("Buildings Path Camera", typeof(Camera));
            try
            {
                var rig = new BattleCameraRig(camera.GetComponent<Camera>(), Half, BattleSim.WallHeight);
                var all = All();
                for (int side = 0; side < 4; side++)
                    foreach (float along in new[] { -3f, 0f, 3f })
                        for (int k = 0; k <= 400; k++)
                        {
                            Vector3 p = rig.TowerPath(side, rig.WallFeet(side, along, 2f), k / 400f);
                            foreach (var (name, rect, _, _) in all)
                                Assert.IsFalse(rect.Contains(new Vector2(p.x, p.z)), $"성벽 {side} along {along} t {k / 400f:0.00}: 발이 {name} 땅 안");
                        }
            }
            finally { Object.DestroyImmediate(camera); }
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
        public void Art_PropBillboards_FollowGivenCamera_NotCameraMain()
        {
            var b = Build(out GameObject parent);
            var camObject = new GameObject("Battle Camera (not MainCamera)", typeof(Camera));
            try
            {
                var board = parent.GetComponentInChildren<BattleCourtyardPropBillboards>();
                Mesh m = board.GetComponent<MeshFilter>().sharedMesh;
                camObject.transform.rotation = Quaternion.LookRotation(Vector3.right);   // 동쪽 성벽 쪽을 본다
                b.Tick(false, camObject.GetComponent<Camera>());
                Assert.Less(m.normals[board.FirstVertex].x, -0.99f, "사진기를 따라 판이 돈다");
                camObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);   // 북쪽 성벽으로 돌린다
                b.Tick(false, camObject.GetComponent<Camera>());
                Assert.Less(m.normals[board.FirstVertex].z, -0.99f, "다른 성벽으로 돌려도 따라간다");
            }
            finally { b.Dispose(); Object.DestroyImmediate(parent); Object.DestroyImmediate(camObject); }
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
