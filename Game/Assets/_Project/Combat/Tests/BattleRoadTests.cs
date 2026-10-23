using System.Linq;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Combat.Render;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 계단 → 탑 의식 돌길 (W26): 등불 기둥 배치(탑 몸체 밖·길 양쪽·병사/전령 길 밖), 주인공 길이 돌길을 따름, 낮은 높이, 땅 번짐 상한, 밤 밝기 바닥
    public sealed class BattleRoadTests
    {
        const float Half = 60f;

        [TearDown]
        public void Restore()
        {
            HlrLights.ForceLow = false;
            HlrLights.Begin();
            HlrLights.Clear();
            GroundGlow.RefreshCap();
            GroundGlow.Flush(true, true);
        }

        static void Frame(int road, out Vector3 n, out Vector3 r) => BattleCourtyard.StairFrame(road, out n, out r);

        [Test]
        public void Lamps_AreSpacedAlongEachOfTheFourRoads_OnBothSides()
        {
            int count = BattleRoad.LampCount(Half);
            int perRoad = count / BattleRoad.RoadCount;
            Assert.AreEqual(count, perRoad * BattleRoad.RoadCount, "네 길이 같은 수");
            Assert.GreaterOrEqual(perRoad, 6, "길 하나에 세 쌍 이상 (반폭 60m)");
            Assert.LessOrEqual(count, 32, "W35: 네 길 합쳐도 예전(길 하나 26개)과 비슷하게 — 그리는 수·땅 번짐 상한 안");
            var perRoadCount = new int[BattleRoad.RoadCount];
            for (int i = 0; i < count; i += 2)
            {
                int road = BattleRoad.LampRoad(i);
                perRoadCount[road] += 2;
                Frame(road, out Vector3 n, out Vector3 r);
                Vector3 a = BattleRoad.LampBase(Half, i), b = BattleRoad.LampBase(Half, i + 1);
                Assert.AreEqual(road, BattleRoad.LampRoad(i + 1), "한 쌍은 같은 길");
                float la = Vector3.Dot(a, r), lb = Vector3.Dot(b, r);
                Assert.AreEqual(BattleRoad.LampOffset, Mathf.Abs(la), 1e-3f);
                Assert.AreEqual(-la, lb, 1e-3f, "길 가운데를 사이에 두고 마주 본다");
                Assert.Greater(Mathf.Abs(la), BattleRoad.HalfWidth + BattleRoad.KerbWidth, "연석 바깥");
                Assert.AreEqual(Vector3.Dot(a, n), Vector3.Dot(b, n), 1e-3f);
                Assert.Greater(Vector3.Dot(a, n), 0f, "기둥은 그 길(성벽) 쪽에 선다");
                int outer = i - 2 * BattleRoad.RoadCount;   // 같은 길 바로 바깥 짝
                if (outer >= 0) Assert.AreEqual(BattleRoad.LampSpacing, Vector3.Dot(BattleRoad.LampBase(Half, outer), n) - Vector3.Dot(a, n), 1e-3f, "길 따라 간격");
            }

            foreach (int c in perRoadCount) Assert.AreEqual(perRoad, c);
        }

        [Test]
        public void Lamps_StayBetweenStairFootAndPlaza_AndOutsideTowerBody()
        {
            for (int i = 0; i < BattleRoad.LampCount(Half); i++)
            {
                Frame(BattleRoad.LampRoad(i), out Vector3 n, out _);
                Vector3 p = BattleRoad.LampBase(Half, i);
                float dist = Vector3.Dot(p, n);
                Assert.Less(dist, BattleRoad.RoadStart(Half), "계단 아래쪽");
                Assert.Greater(dist, BattleRoad.PlazaRimRadius, "광장 가장자리 앞");
                Assert.IsTrue(Mathf.Abs(p.x) > BattleTowerView.BaseHalf + 0.15f || Mathf.Abs(p.z) > BattleTowerView.BaseHalf + 0.15f, "탑 몸체 밖");
                Assert.AreEqual(BattleSim.WallHeight, p.y, 1e-4f);
            }
        }

        [Test]
        public void Lamps_AreOffHeroPathAndCourierPaths()
        {
            var camera = new GameObject("Road Path Camera", typeof(Camera));
            try
            {
                var rig = new BattleCameraRig(camera.GetComponent<Camera>(), Half, BattleSim.WallHeight);
                for (int i = 0; i < BattleRoad.LampCount(Half); i++)
                {
                    Vector3 lamp = BattleRoad.LampBase(Half, i);
                    for (int side = 0; side < 4; side++)
                        foreach (float x in new[] { -3f, 0f, 3f })
                            for (int k = 0; k <= 400; k++)
                            {
                                Vector3 p = rig.TowerPath(side, rig.WallFeet(side, x, 2f), k / 400f);
                                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(lamp.x, lamp.z));
                                Assert.Greater(d, 1.2f, $"기둥 {i}가 성벽 {side} 주인공 길 위 (시작 along={x}, t={k / 400f:0.00})");
                            }

                    // 탑 전령은 탑에서 그 성벽 방향으로 곧장 달린다 (길 가운데에서 옆으로 최대 0.9m 비껴 선다)
                    Frame(BattleRoad.LampRoad(i), out _, out Vector3 r);
                    Assert.Greater(Mathf.Abs(Vector3.Dot(lamp, r)), 0.9f + 0.4f, $"기둥 {i}가 전령 길 위");
                }
            }
            finally { Object.DestroyImmediate(camera); }
        }

        [Test]
        public void HeroPath_FollowsRoadCenter_ThenRingsTheTower()
        {
            var camera = new GameObject("Road Hero Camera", typeof(Camera));
            try
            {
                var rig = new BattleCameraRig(camera.GetComponent<Camera>(), Half, BattleSim.WallHeight);
                for (int side = 0; side < 4; side++)
                {
                    Frame(side, out Vector3 n, out Vector3 r);
                    float maxLateral = 0f;
                    for (int k = 0; k <= 400; k++)
                    {
                        Vector3 p = rig.TowerPath(side, rig.WallFeet(side, 2f, 2f), k / 400f);
                        float dist = Vector3.Dot(p, n), lateral = Mathf.Abs(Vector3.Dot(p, r));
                        if (dist > BattleCameraRig.PlazaEntryZ && dist < BattleRoad.RoadStart(Half)) maxLateral = Mathf.Max(maxLateral, lateral);
                        Assert.AreEqual(BattleSim.WallHeight, p.y, 1e-4f, "발 높이는 늘 마당 윗면");
                        Assert.Less(lateral, BattleRoad.HalfWidth + 3f, $"성벽 {side}: 길·광장 곁을 벗어나지 않는다");
                    }

                    Assert.Less(maxLateral, BattleRoad.HalfWidth - 0.3f, $"성벽 {side}: 계단~돌길 구간은 길 안쪽");
                }

                Assert.Less(BattleCameraRig.PlazaEntryZ, BattleRoad.PlazaRadius, "광장 안에서 탑 옆으로 돈다");
            }
            finally { Object.DestroyImmediate(camera); }
        }

        [Test]
        public void Meshes_AreLow_FewDrawCalls_AndKeepPathClear()
        {
            var parent = new GameObject("Road View Test");
            try
            {
                using (var road = new BattleRoad(Half, parent.transform))
                {
                    // K-122: 돌길·연석 2 + 기둥·깃발은 사진기를 보는 세운 그림이라 하나씩 (메시·재질은 나눠 쓴다)
                    int lamps = BattleRoad.LampCount(Half);
                    int banners = BattleRoad.BannerSpots.Length + 2 * BattleRoad.RoadCount;   // 문 곁 둘 + 길마다 한 쌍 (W35)
                    Assert.LessOrEqual(road.RendererCount, 2 + lamps + banners, "돌길(네 갈래 합침) 둘 + 기둥·깃발 하나씩");
                    Assert.AreEqual(lamps, parent.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Road Lamp ")), "등불 기둥 그림 수 = 기둥 수");
                    Assert.AreEqual(banners, parent.GetComponentsInChildren<Transform>().Count(t => t.name == "Road Banner"), "깃발 그림 수");
                    Assert.AreEqual(2, parent.GetComponentsInChildren<MeshFilter>().Count(f => f.name == "Road Paving" || f.name == "Road Kerb And Plaza"), "돌길·연석은 네 갈래를 합친 메시 둘");
                    float low = BattleSim.WallHeight + 0.2f;   // 돌길·연석·광장은 이보다 낮다 (병사 키 0.35m의 절반 안쪽)
                    float maxAll = float.MinValue;
                    foreach (var f in parent.GetComponentsInChildren<MeshFilter>())
                    {
                        Bounds b = f.sharedMesh.bounds;
                        float max = f.transform.TransformPoint(b.max).y;
                        maxAll = Mathf.Max(maxAll, max);
                        if (f.name == "Road Paving" || f.name == "Road Kerb And Plaza")
                            Assert.Less(max, low, f.name + " 높이");
                    }

                    Assert.Less(maxAll, BattleSim.WallHeight + 3.5f, "기둥·깃발 판은 투명한 윗 여백을 포함해도 이 안 (보이는 그림은 0.9m·1.5m, 사진기·탑을 가리지 않는다)");
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        // K-122: 낮엔 꺼진 기둥 그림, 밤엔 불꽃 칸 그림으로 바뀌고 칸이 돈다. 깃발은 낮밤 없이 칸이 돈다
        [Test]
        public void LampSprites_SwapToNightFlameFrames_AndBannersFlap()
        {
            var parent = new GameObject("Road Sprite Test");
            int lightsBefore = HlrLights.Count;
            try
            {
                using (var road = new BattleRoad(Half, parent.transform))
                {
                    var lamp = parent.transform.Find("Beacon Road (W26)/Road Lamp 0").GetComponent<MeshRenderer>();
                    var banner = parent.transform.Find("Beacon Road (W26)/Road Banner").GetComponent<MeshRenderer>();
                    Assert.IsTrue(lamp.sharedMaterial.name.Contains("Lamp Day"), "처음엔 낮 기둥");
                    road.Tick(true, 1f);
                    Assert.IsTrue(lamp.sharedMaterial.name.Contains("Lamp Night"), "밤엔 불꽃 기둥");
                    Assert.AreEqual(1f / 6f, lamp.sharedMaterial.mainTextureScale.x, 1e-4f, "불꽃은 여섯 칸 판에서 한 칸");
                    Assert.AreEqual(1f / 4f, banner.sharedMaterial.mainTextureScale.x, 1e-4f, "깃발은 네 칸 판에서 한 칸");
                    road.Tick(false, 1f);
                    Assert.IsTrue(lamp.sharedMaterial.name.Contains("Lamp Day"), "낮에는 다시 꺼진 기둥");
                    Assert.AreEqual(lightsBefore, HlrLights.Count, "그림 기둥은 셰이더 빛 칸을 등록하지 않는다");
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void Pools_RespectCaps_AndUseNoShaderLightSlots()
        {
            Vector3 view = new Vector3(0f, BattleSim.WallHeight + 1.7f, Half - 2f);   // 성벽 위에서 길을 본다
            HlrLights.ForceLow = false;
            HlrLights.Begin();
            GroundGlow.RefreshCap();
            int InReach(float reach)
            {
                int n = 0;
                for (int i = 0; i < BattleRoad.LampCount(Half); i++)
                {
                    Vector3 p = BattleRoad.LampBase(Half, i);
                    if ((p.x - view.x) * (p.x - view.x) + (p.z - view.z) * (p.z - view.z) <= reach * reach) n++;
                }

                return n;
            }

            Assert.AreEqual(Mathf.Min(BattleLights.RoadPoolsPc, InReach(30f)), BattleLights.EmitRoad(false, 1f, view, Half), "PC: 닿는 범위 안 기둥, 상한까지");
            Assert.AreEqual(0, HlrLights.Count, "셰이더 빛 칸은 쓰지 않는다");
            Assert.LessOrEqual(GroundGlow.Count, GroundGlow.Max);
            GroundGlow.Flush(true, true);

            HlrLights.ForceLow = true;
            HlrLights.Begin();
            GroundGlow.RefreshCap();
            BattleLights.EmitCourtyard(true, 1f, false, Vector3.zero, 0f);   // 마당 웅덩이 2개 먼저
            int low = BattleLights.EmitRoad(true, 1f, view, Half);
            Assert.AreEqual(Mathf.Min(BattleLights.RoadPoolsLow, InReach(10f)), low, "모바일: 닿는 범위(10m) 안 기둥, 상한 4개까지");
            Assert.AreEqual(0, HlrLights.Count);
            Assert.LessOrEqual(GroundGlow.Count + 6, GroundGlow.Max, "마당 + 돌길 웅덩이 뒤에도 횃불 웅덩이 6개 자리가 남는다");
        }

        [Test]
        public void Plaza_TopsDiffer_StayBelowGlowQuad_AndBannersAvoidWorkers()
        {
            Assert.AreNotEqual(BattleRoad.PavingLift, BattleRoad.PlazaRimLift, "돌길과 광장 아랫단 윗면이 같은 높이면 겹쳐 깜박인다");
            Assert.AreNotEqual(BattleRoad.PavingLift, BattleRoad.PlazaLift);
            Assert.Less(BattleRoad.PlazaLift, 0.03f, "GroundGlow 판(바닥 +3cm)보다 낮아야 탑·문 앞 웅덩이가 보인다");
            // W27: 일꾼은 이제 탑 안이다. 깃발은 열린 문 그림(아치 + 오른쪽 랜턴, 가운데에서 -0.93~+1.16m)을 가리지 않게 옆으로
            foreach (Vector2 spot in BattleRoad.BannerSpots)
                Assert.Greater(Mathf.Abs(spot.x), 1.4f, $"깃발 x={spot.x}: 문 그림 밖");
        }

        [Test]
        public void NightFloor_KeepsSilhouettesReadable_BlueNotGrey()
        {
            float before = NightTuning.Level;
            NightTuning.Level = NightTuning.DefaultLevel;   // 기본 단계 기준으로 확인 (다른 시험이 바꿨을 수 있다)
            try { CheckNightFloor(); }
            finally { NightTuning.Level = before; }
        }

        static void CheckNightFloor()
        {
            Color floor = NightTuning.AmbientFloor;
            Assert.GreaterOrEqual(floor.b, 0.22f, "달빛 바닥 파란 성분 (W26)");
            Assert.Greater(floor.b, floor.r * 1.5f, "회색이 아니라 푸른 쪽");
            Assert.GreaterOrEqual(NightTuning.SunScale, 0.5f, "달빛 세기");
            Assert.GreaterOrEqual(NightTuning.SpriteBrightness, 0.75f, "빛 안 받는 그림 밝기");
            Assert.LessOrEqual(NightTuning.Vignette, 0.33f, "가장자리가 새까맣지 않게");
        }
    }
}

