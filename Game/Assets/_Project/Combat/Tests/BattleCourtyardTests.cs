using HumanLastRampart.Combat.Battle;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 마당 계단 높이 보정 (W19): 순수 계산 HeroLift — 층계참·단마다 높이, 다른 성벽·마당·층계참 밖에서는 0, 좌우 대칭
    public sealed class BattleCourtyardTests
    {
        const float Half = 20f;

        // 계단 쪽 성벽 안쪽 가장자리 가운데에서 (lz: 통로 쪽 +, lx: 성벽 따라) 떨어진 발 위치
        static Vector3 At(float lz, float lx, int side = BattleCameraRig.StairsSide)
        {
            var n = WallGeometry.Outward(side);
            var r = WallGeometry.Right(side);
            var nv = new Vector3(n.x, 0f, n.z);
            var rv = new Vector3(r.x, 0f, r.z);
            return nv * (Half - 2f + lz) + rv * lx + Vector3.up * BattleSim.WallHeight;
        }

        // W35: 계단은 네 성벽 모두에 같은 모양 — 같은 자리는 어느 성벽이든 같은 높이
        [Test]
        public void AllFourWalls_HaveTheSameStairHeights()
        {
            for (int side = 0; side < 4; side++)
            {
                Assert.AreEqual(BattleCourtyard.LandingHeight, BattleCourtyard.HeroLift(At(0.1f, 0f, side), Half), 1e-4f, $"성벽 {side} 층계참");
                for (int k = 0; k < BattleCourtyard.StepCount - 1; k++)
                {
                    float expected = BattleCourtyard.LandingHeight * (1f - (k + 1f) / BattleCourtyard.StepCount);
                    Assert.AreEqual(expected, BattleCourtyard.HeroLift(At(-(k + 0.5f) * BattleCourtyard.TreadDepth, 0f, side), Half), 1e-4f, $"성벽 {side} {k}번째 단");
                }

                Assert.AreEqual(0f, BattleCourtyard.HeroLift(At(BattleCourtyard.LandingDepth + 0.3f, 0f, side), Half), 1e-4f, $"성벽 {side}: 층계참 바깥");
            }
        }

        [Test]
        public void Stairs_AreOneMergedMeshPerKind_AcrossFourWalls()
        {
            var parent = new GameObject("Courtyard Four Stairs Test");
            try
            {
                using (new BattleCourtyard(Half, parent.transform))
                {
                    var mesh = parent.transform.Find("Courtyard (W27)/Courtyard Stairs").GetComponent<MeshFilter>().sharedMesh;
                    var seen = new bool[4];
                    foreach (Vector3 v in mesh.vertices)
                    {
                        Vector3 c = new Vector3(v.x, 0f, v.z);
                        for (int side = 0; side < 4; side++)
                        {
                            BattleCourtyard.StairFrame(side, out Vector3 n, out Vector3 r);
                            if (Vector3.Dot(c, n) > Half - 3.5f && Mathf.Abs(Vector3.Dot(c, r)) < 1.6f) seen[side] = true;
                        }
                    }

                    for (int side = 0; side < 4; side++) Assert.IsTrue(seen[side], $"성벽 {side} 계단 모양이 합친 메시에 있다");
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void Landing_HasFullHeight()
        {
            Assert.AreEqual(BattleCourtyard.LandingHeight, BattleCourtyard.HeroLift(At(0.1f, 0f), Half), 1e-4f);
        }

        [Test]
        public void Treads_StepDownOneByOne()
        {
            float previous = BattleCourtyard.LandingHeight;
            for (int k = 0; k < BattleCourtyard.StepCount - 1; k++)
            {
                float expected = BattleCourtyard.LandingHeight * (1f - (k + 1f) / BattleCourtyard.StepCount);
                float lift = BattleCourtyard.HeroLift(At(-(k + 0.5f) * BattleCourtyard.TreadDepth, 0f), Half);
                Assert.AreEqual(expected, lift, 1e-4f, $"{k}번째 단");
                Assert.Less(lift, previous, "단마다 낮아진다");
                previous = lift;
            }

            Assert.AreEqual(0f, BattleCourtyard.HeroLift(At(-(BattleCourtyard.StepCount - 0.5f) * BattleCourtyard.TreadDepth, 0f), Half), 1e-4f, "마당 바닥");
        }

        [Test]
        public void OutsideStairs_IsZero()
        {
            Assert.AreEqual(0f, BattleCourtyard.HeroLift(At(BattleCourtyard.LandingDepth + 0.3f, 0f), Half), 1e-4f, "층계참보다 바깥(처음 서는 자리·병사 길)");
            Assert.AreEqual(0f, BattleCourtyard.HeroLift(At(0.1f, BattleCourtyard.StairHalfWidth + 0.2f), Half), 1e-4f, "계단 폭 밖");
            Assert.AreEqual(0f, BattleCourtyard.HeroLift(new Vector3(0f, BattleSim.WallHeight, 0f), Half), 1e-4f, "탑 앞");
            for (int side = 0; side < 4; side++)   // W35: 모든 성벽에 계단이 있으니 같은 규칙이 네 성벽에 모두 맞다
            {
                Assert.AreEqual(0f, BattleCourtyard.HeroLift(At(BattleCourtyard.LandingDepth + 0.3f, 0f, side), Half), 1e-4f, $"성벽 {side}: 층계참보다 바깥(처음 서는 자리·병사 길)");
                Assert.AreEqual(0f, BattleCourtyard.HeroLift(At(0.1f, BattleCourtyard.StairHalfWidth + 0.2f, side), Half), 1e-4f, $"성벽 {side}: 계단 폭 밖");
                Assert.AreEqual(0f, BattleCourtyard.HeroLift(At(-(BattleCourtyard.StepCount + 3f) * BattleCourtyard.TreadDepth, 0f, side), Half), 1e-4f, $"성벽 {side}: 마당");
            }
        }

        // ── W27: Codex K-116 그림 연결 ──
        [Test]
        public void K116Art_IsApplied_FewDrawCalls()
        {
            var parent = new GameObject("Courtyard K116 Test");
            try
            {
                using (var yard = new BattleCourtyard(Half, parent.transform))
                {
                    Assert.IsTrue(yard.HasK116Floor);
                    var floor = (Texture2D)yard.FloorTexture;
                    Assert.AreEqual("courtyard_cobble_512", floor.name);
                    Assert.AreEqual(FilterMode.Point, floor.filterMode);
                    Assert.AreEqual(TextureWrapMode.Repeat, floor.wrapMode, "바닥은 양방향 반복");
                    Assert.IsTrue(yard.HasK116Stairs, "계단 윗면·앞면·옆돌 그림");
                    Assert.IsTrue(yard.HasK116Props, "상자·통 그림 판");
                    Assert.GreaterOrEqual(yard.DecalsPlaced, 10, "데칼이 듬성듬성");
                    Assert.AreEqual(3, yard.BillboardCount, "무기 걸이·수레·화살 묶음");
                    Assert.LessOrEqual(yard.RendererCount, 9, "모바일: 합친 메시 아홉 개 이하");
                    var mf = parent.transform.Find("Courtyard (W27)/Courtyard Floor").GetComponent<MeshRenderer>().sharedMaterial;
                    Assert.AreEqual(Half * 2f - 4f, mf.mainTextureScale.x * 2.6f, 1e-3f, "바닥 반복 = 폭 / 한 장이 덮는 미터");
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void StairFaces_UseTreadRiserFlankCells_NeverStretchedAcrossTheAtlas()
        {
            var parent = new GameObject("Courtyard Stair Uv Test");
            try
            {
                using (new BattleCourtyard(Half, parent.transform))
                {
                    var steps = parent.transform.Find("Courtyard (W27)/Courtyard Stairs").GetComponent<MeshFilter>().sharedMesh;
                    Vector3[] n = steps.normals; Vector2[] uv = steps.uv;
                    int up = 0, front = 0;
                    for (int i = 0; i < uv.Length; i++)
                    {
                        if (n[i].y > 0.9f) { Assert.GreaterOrEqual(uv[i].y, 6f / 16f - 1e-3f, "윗면은 윗 10줄 칸"); up++; }
                        else { Assert.LessOrEqual(uv[i].y, 6f / 16f + 1e-3f, "앞면은 아래 6줄 칸"); front++; }
                        Assert.That(uv[i].x, Is.InRange(0f, 1f), "칸 그림은 판 안에서만 (반복 없음)");
                    }

                    Assert.Greater(up, 20); Assert.Greater(front, 4);
                    var walls = parent.transform.Find("Courtyard (W27)/Courtyard Stair Walls").GetComponent<MeshFilter>().sharedMesh;
                    foreach (Vector2 p in walls.uv) Assert.That(p.y, Is.InRange(0f, 1f), "옆돌 세로는 한 장 안 (가로만 반복)");
                    var flank = parent.transform.Find("Courtyard (W27)/Courtyard Stair Walls").GetComponent<MeshRenderer>().sharedMaterial.mainTexture;
                    Assert.AreEqual(TextureWrapMode.Repeat, flank.wrapModeU);
                    Assert.AreEqual(TextureWrapMode.Clamp, flank.wrapModeV);
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void Decals_AvoidRoadPlazaAndStairs_AndSpritesKeepTheirAspect()
        {
            var parent = new GameObject("Courtyard Decal Test");
            try
            {
                using (new BattleCourtyard(Half, parent.transform))
                {
                    BattleCourtyard.StairFrame(out Vector3 n, out Vector3 r);
                    var mesh = parent.transform.Find("Courtyard (W27)/Courtyard Decals").GetComponent<MeshFilter>().sharedMesh;
                    Vector3[] v = mesh.vertices;
                    Assert.AreEqual(0, v.Length % 4);
                    for (int i = 0; i < v.Length; i += 4)
                    {
                        Vector3 c = (v[i] + v[i + 1] + v[i + 2] + v[i + 3]) * 0.25f;
                        c.y = 0f;
                        Assert.GreaterOrEqual(c.magnitude, BattleRoad.PlazaRimRadius + 1.2f - 1e-3f, "탑 광장 밖");
                        Assert.IsFalse(BattleCourtyard.OnAnyRoad(c, BattleRoad.HalfWidth + BattleRoad.KerbWidth + 2.2f - 1e-3f), "네 돌길·계단 줄 밖 (W35)");
                        // W41: 바닥 위 1cm. 겹친 데칼만 위로 1cm 층씩 (같은 높이로 겹치면 깜박인다)
                        float layer = (v[i].y - (BattleSim.WallHeight + 0.02f)) / 0.01f;
                        Assert.GreaterOrEqual(layer, -0.1f, "바닥 위 1cm (먼 곳에서 깜박이지 않게)");
                        Assert.Less(Mathf.Abs(layer - Mathf.Round(layer)), 0.1f, "겹침 층은 1cm 단위");
                        Assert.LessOrEqual(layer, 4.1f, "겹침 층은 몇 개 안 된다");
                    }

                    // 세운 그림: 원본 칸 비율 그대로 (무기 걸이 72×80)
                    var rack = parent.transform.Find("Courtyard (W27)/Courtyard Weapon Rack").GetComponent<MeshFilter>().sharedMesh;
                    Assert.AreEqual(72f / 80f, rack.bounds.size.x / rack.bounds.size.y, 1e-3f, "그림 비율 그대로 (늘이지 않는다)");
                    Assert.IsNotNull(parent.transform.Find("Courtyard (W27)/Courtyard Cart").GetComponent<CourtyardBillboard>(), "사진기를 보게 돌아선다");
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void RoadPaving_UsesK116Cobble_ThroughTheSwapHook()
        {
            var parent = new GameObject("Road K116 Test");
            try
            {
                using (var road = new BattleRoad(Half, parent.transform))
                {
                    Assert.IsTrue(road.HasPavingArt, "road_paving 또는 K-116 돌바닥이 돌길에 붙었다");
                    var paving = parent.transform.Find("Beacon Road (W26)/Road Paving").GetComponent<MeshRenderer>().sharedMaterial;
                    Assert.IsNotNull(paving.mainTexture);
                    Assert.AreEqual(FilterMode.Point, paving.mainTexture.filterMode);
                    Assert.AreEqual(TextureWrapMode.Repeat, paving.mainTexture.wrapMode);
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void IsSymmetric_AndNeverBuriesPastLandingEdge()
        {
            foreach (float lz in new[] { 0.1f, 0.4f, -0.1f, -0.6f })
                foreach (float lx in new[] { 0.3f, 0.9f })
                    Assert.AreEqual(BattleCourtyard.HeroLift(At(lz, lx), Half), BattleCourtyard.HeroLift(At(lz, -lx), Half), 1e-5f);

            // 층계참은 바깥 모서리에서 1.5m보다 안쪽에서 끝나고 0.1m 이하 (W19 검토)
            Assert.LessOrEqual(BattleCourtyard.LandingHeight, 0.1f);
            Assert.GreaterOrEqual(2f - BattleCourtyard.LandingDepth, 1.5f);
            Assert.AreEqual(0f, BattleCourtyard.HeroLift(At(BattleCourtyard.LandingDepth, 0f), Half), 1e-4f, "상자 모서리에서 보정 0");
        }
    }
}
