using System.Collections.Generic;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 높은 등대 탑 (W20): 키가 성벽의 3배, 빛·불빛이 꼭대기 불방에서 나감, 주인공은 문 앞(오르지 않음), 전령은 몸체 밖
    public sealed class BattleTowerViewTests
    {
        [Test]
        public void Height_IsAboutThreeTimesWall()
        {
            Assert.AreEqual(3f, BattleTowerView.Height / BattleSim.WallHeight, 0.01f, "탑 키 = 성벽 높이의 3배");
            Assert.Greater(BattleTowerView.LampHeight, BattleTowerView.Height * 0.75f, "불방은 탑 위쪽");
            Assert.Less(BattleTowerView.LampHeight, BattleTowerView.Height, "불방은 꼭대기 지붕 아래");
        }

        [Test]
        public void BeamOrigin_IsAtTopLampRoom()
        {
            Assert.AreEqual(BattleSim.WallHeight + BattleTowerView.LampHeight, BattleBeamView.OriginHeight, 1e-4f);
            Assert.Greater(BattleBeamView.OriginHeight, BattleSim.WallHeight * 3f, "빛은 성벽 높이 3배보다 위에서 나간다");
        }

        [Test]
        public void Mesh_ReachesTowerTop_FewDrawCalls()
        {
            var parent = new GameObject("Tower View Test");
            try
            {
                using (var view = new BattleTowerView(parent.transform))
                {
                    Assert.LessOrEqual(view.RendererCount, 10, "모바일: 합친 메시 열 개 이하 (석벽·띠·지붕·소품 둘·번짐·창·문)");
                    float maxY = float.MinValue;
                    foreach (var f in parent.GetComponentsInChildren<MeshFilter>())
                        maxY = Mathf.Max(maxY, f.transform.TransformPoint(f.sharedMesh.bounds.max).y);
                    Assert.AreEqual(BattleSim.WallHeight + BattleTowerView.Height, maxY, 0.05f, "꼭대기 = 마당 윗면 + 탑 키");
                    Assert.AreEqual(view.LampWorld.y, BattleBeamView.OriginHeight, 1e-4f);
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void HeroPath_StairsToDoor_NeverEntersTowerBody()
        {
            var camera = new GameObject("Tower Path Camera", typeof(Camera));
            try
            {
                var rig = new BattleCameraRig(camera.GetComponent<Camera>(), 60f, BattleSim.WallHeight);
                float limit = BattleTowerView.BaseHalf + 0.25f;   // 몸체 + 받침(0.15m) 바깥
                foreach (float x in new[] { -3f, -1f, -0.01f, 0f, 1f, 3f })
                    for (int i = 0; i <= 200; i++)
                    {
                        Vector3 p = rig.TowerPath(new Vector3(x, BattleSim.WallHeight, 58f), i / 200f);
                        Assert.IsTrue(Mathf.Abs(p.x) > limit || Mathf.Abs(p.z) > limit, $"시작 x={x}, t={i / 200f:0.000}: 발 ({p.x:0.00}, {p.z:0.00})이 탑 몸체 안");
                        Assert.AreEqual(BattleSim.WallHeight, p.y, 1e-4f);
                    }

                Vector3 end = rig.TowerPath(new Vector3(0f, BattleSim.WallHeight, 58f), 1f);
                Assert.Less((end - rig.TowerFeet).magnitude, 1e-4f, "끝은 문 앞");
            }
            finally { Object.DestroyImmediate(camera); }
        }

        [Test]
        public void Mesh_HasDownFaces_SoOverhangsAreNotHollow()
        {
            var parent = new GameObject("Tower Down Faces");
            try
            {
                using (new BattleTowerView(parent.transform))
                {
                    int down = 0;
                    foreach (var f in parent.GetComponentsInChildren<MeshFilter>())
                        foreach (Vector3 n in f.sharedMesh.normals) if (n.y < -0.99f) down++;
                    Assert.Greater(down, 20, "받침·띠·둘레길·지붕 테에 아랫면이 있다");
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        // ── W27: Codex K-115 그림 연결 ──
        [Test]
        public void Art_IsAssigned_WithPointFilterAndRightWrap()
        {
            var parent = new GameObject("Tower Art Test");
            try
            {
                using (var view = new BattleTowerView(parent.transform))
                {
                    Assert.IsTrue(view.HasTowerArt, "석벽·띠·지붕·창틀·문·번짐 그림이 모두 붙었다");
                    var wall = (Texture2D)view.WallMaterial.mainTexture;
                    var trim = (Texture2D)view.TrimMaterial.mainTexture;
                    var roof = (Texture2D)view.RoofMaterial.mainTexture;
                    Assert.AreEqual("tower_wall_albedo", wall.name);
                    Assert.AreEqual("tower_trim_albedo", trim.name);
                    Assert.AreEqual("tower_roof_albedo", roof.name);
                    foreach (var t in new[] { wall, trim, roof }) Assert.AreEqual(FilterMode.Point, t.filterMode, t.name + " 점 필터");
                    Assert.AreEqual(TextureWrapMode.Repeat, wall.wrapModeU); Assert.AreEqual(TextureWrapMode.Repeat, wall.wrapModeV);
                    Assert.AreEqual(TextureWrapMode.Repeat, roof.wrapModeU); Assert.AreEqual(TextureWrapMode.Repeat, roof.wrapModeV);
                    Assert.AreEqual(TextureWrapMode.Repeat, trim.wrapModeU, "띠는 가로만 반복");
                    Assert.AreEqual(TextureWrapMode.Clamp, trim.wrapModeV);
                    Assert.IsNotNull(view.WindowTexture);
                    Assert.IsNotNull(view.DoorTexture);
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void WallUv_IsMeterBased_SoTextureIsNeverStretched()
        {
            var parent = new GameObject("Tower Uv Test");
            try
            {
                using (var view = new BattleTowerView(parent.transform))
                {
                    // 석벽 면 하나(세로로 선 면)의 UV 범위 / 면 크기 = 1 / (그림 한 장이 덮는 미터 8m)
                    var stone = parent.transform.Find("Beacon Tower (W27)/Tower Stone").GetComponent<MeshFilter>().sharedMesh;
                    Vector3[] v = stone.vertices; Vector2[] uv = stone.uv;
                    int checkedFaces = 0;
                    for (int i = 0; i + 3 < v.Length; i += 4)
                    {
                        if (Mathf.Abs(stone.normals[i].y) > 0.5f) continue;   // 윗·아랫면 제외
                        float h = (v[i + 2] - v[i]).y;
                        float du = Mathf.Abs(uv[i + 1].x - uv[i].x);
                        float dv = Mathf.Abs(uv[i + 3].y - uv[i].y);
                        Assert.AreEqual(Mathf.Abs(h) / 8f, dv, Mathf.Abs(h) / 8f * 0.05f + 1e-3f, $"면 {i / 4} 세로 UV는 높이/8m");
                        float w = Vector3.Distance(v[i], v[i + 1]);
                        Assert.AreEqual(w / 8f, du, w / 8f * 0.2f + 1e-3f, $"면 {i / 4} 가로 UV는 폭/8m (위로 좁아지는 면은 평균 폭)");
                        checkedFaces++;
                    }

                    Assert.Greater(checkedFaces, 20);
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void Door_IsOpen_LeafIsCutOut_ButArchRemains()
        {
            var parent = new GameObject("Tower Door Test");
            try
            {
                using (var view = new BattleTowerView(parent.transform))
                {
                    var door = (Texture2D)view.DoorTexture;
                    var source = Resources.Load<Texture2D>("BattleArt/Tower/door_lit");
                    Assert.IsTrue(source.isReadable, "문 그림은 읽을 수 있게 가져와야 문짝을 뚫을 수 있다");
                    Assert.AreNotSame(source, door, "뚫은 사본을 쓴다");
                    Assert.AreEqual(0f, door.GetPixel(43, 127 - 80).a, "문짝 자리는 투명 (열린 문)");
                    Assert.AreEqual(0f, door.GetPixel(43, 127 - 112).a, "아래쪽 문짝도 투명");
                    Assert.Greater(source.GetPixel(43, 127 - 80).a, 0.9f, "원본 문짝은 불투명");
                    Assert.Greater(door.GetPixel(14, 127 - 60).a, 0.9f, "돌 아치는 남는다");
                    Assert.Greater(door.GetPixel(43, 127 - 12).a, 0.9f, "아치 윗돌은 남는다");
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void DeadAndLit_StatesSwitchTextures_AndFlameKindsFollowStrength()
        {
            Assert.IsTrue(BattleTowerView.IsDead(0.35f), "복구 0 = 꺼진 탑");
            Assert.IsFalse(BattleTowerView.IsDead(0.4f), "조금이라도 복구하면 점등 그림");
            Assert.AreEqual(-1, BattleTowerView.FlameKind(0.35f));
            Assert.AreEqual(0, BattleTowerView.FlameKind(0.45f));
            Assert.AreEqual(1, BattleTowerView.FlameKind(0.7f));
            Assert.AreEqual(2, BattleTowerView.FlameKind(1f));
            Assert.AreEqual(0.35f, BeaconRules.TowerFlameStrength(0f, false), 1e-4f, "복구 0의 불꽃 세기 = 꺼짐 문턱 아래");
            Assert.AreEqual(2, BattleTowerView.FlameKind(BeaconRules.TowerFlameStrength(0f, true)), "점화의 밤은 강한 불");

            var parent = new GameObject("Tower State Test");
            try
            {
                using (var view = new BattleTowerView(parent.transform))
                {
                    view.Tick(false, 1f, 0.35f);
                    Assert.IsTrue(view.Dead);
                    Assert.AreEqual("lamp_window_dead", view.WindowTexture.name);
                    var deadDoor = view.DoorTexture;
                    StringAssert.Contains("Dead", deadDoor.name);

                    view.Tick(true, 1f, 0.85f);
                    Assert.IsFalse(view.Dead);
                    Assert.AreEqual("lamp_window_lit", view.WindowTexture.name);
                    StringAssert.Contains("Lit", view.DoorTexture.name);
                    Assert.AreNotSame(deadDoor, view.DoorTexture);

                    view.Tick(false, 1f, 0.35f);   // 다시 꺼짐
                    Assert.IsTrue(view.Dead);
                    Assert.AreEqual("lamp_window_dead", view.WindowTexture.name);
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void Interior_StairAndClimberPath_StayInsideTower()
        {
            Assert.Greater(BattleTowerInterior.OpeningHalf * 2f, 0.7f, "문 구멍은 일꾼이 돌림대 둘레를 도는 모습이 보일 만큼 넓다");
            Assert.LessOrEqual(BattleTowerInterior.WorkerOrbit + 0.12f, BattleTowerInterior.InnerHalf - 0.36f + 0.01f, "일꾼 몸 반폭까지 쳐도 원은 계단 안쪽 모서리 안");
            Assert.LessOrEqual(BattleTowerInterior.CapstanBarHalf, BattleTowerInterior.InnerHalf - 0.36f, "돌림대 막대는 계단에 닿지 않는다");
            for (int d = 0; d < 360; d += 15)
                for (int i = 0; i < BattleTowerInterior.WorkerCount; i++)
                {
                    Vector3 p = BattleTowerInterior.WorkerStation(i, d);
                    Assert.AreEqual(BattleTowerInterior.WorkerOrbit, new Vector2(p.x, p.z).magnitude, 1e-4f, "돌림대 둘레 원 위");
                    Assert.AreEqual(BattleTowerInterior.FloorY, p.y, 1e-4f);
                }

            // 계단: 단마다 오르고 맨 위는 천장 밑, 길은 모두 방 안
            Assert.AreEqual(BattleTowerInterior.FloorY + BattleTowerInterior.StairRise, BattleTowerInterior.StairPoint(0f).y, 0.001f, "발치 = 첫 단 윗면");
            Assert.AreEqual(BattleTowerInterior.StairTopY, BattleTowerInterior.StairPoint(1f).y, 0.001f);
            Assert.Less(BattleTowerInterior.StairTopY, BattleTowerView.GroundHeight, "맨 위는 천장 밑");
            float lastY = -1f;
            for (int i = 0; i <= 100; i++)
            {
                Vector3 p = BattleTowerInterior.StairPoint(i / 100f);
                Assert.LessOrEqual(Mathf.Abs(p.x), BattleTowerInterior.InnerHalf, "계단 x");
                Assert.LessOrEqual(Mathf.Abs(p.z), BattleTowerInterior.InnerHalf, "계단 z");
                Assert.GreaterOrEqual(p.y, lastY - 1e-4f, "내려가는 구간이 없다");
                lastY = p.y;
            }

            // 한 바퀴 (32초): 보일 때는 아래층 방 안이거나 불방 안, 벽 속에 있을 때는 Hidden
            bool sawLamp = false, sawHidden = false, sawStair = false;
            Vector3 station = BattleTowerInterior.WorkerStation(BattleTowerInterior.ClimberIndex, 0f);
            for (float t = 0f; t < BattleTowerInterior.ClimbCycleSeconds; t += 0.05f)
            {
                var phase = BattleTowerInterior.Climber(t, station, out Vector3 feet);
                if (phase == BattleTowerInterior.ClimbPhase.Hidden) { sawHidden = true; continue; }
                Assert.LessOrEqual(Mathf.Abs(feet.x), BattleTowerView.BaseHalf, $"t={t:0.00} x");
                Assert.LessOrEqual(Mathf.Abs(feet.z), BattleTowerView.BaseHalf, $"t={t:0.00} z");
                if (feet.y > BattleTowerView.LampBase - 0.01f) { sawLamp = true; Assert.LessOrEqual(Mathf.Abs(feet.x), BattleTowerView.LampOuterHalf, "불방 안"); }
                else
                {
                    Assert.LessOrEqual(Mathf.Abs(feet.x), BattleTowerInterior.InnerHalf + 0.01f, $"t={t:0.00} 방 안 x");
                    Assert.LessOrEqual(Mathf.Abs(feet.z), BattleTowerInterior.InnerHalf + 0.01f, $"t={t:0.00} 방 안 z");
                    Assert.LessOrEqual(feet.y, BattleTowerView.GroundHeight, "아래층 안");
                    if (feet.y > BattleTowerInterior.FloorY + 0.5f) sawStair = true;
                }
            }

            Assert.IsTrue(sawStair && sawHidden && sawLamp, "계단을 오르고 → 벽 속 → 불방에 닿는다");
            // 기다림 때는 제자리, 한 바퀴가 끝나면 다시 제자리 (되풀이)
            BattleTowerInterior.Climber(0f, station, out Vector3 start);
            Assert.AreEqual(0f, (start - station).magnitude, 1e-4f);
            BattleTowerInterior.Climber(BattleTowerInterior.ClimbCycleSeconds - 0.001f, station, out Vector3 end);
            Assert.Less((end - station).magnitude, 0.01f, "내려와 제자리로 돌아온다");
        }

        [Test]
        public void Workers_StandInsideTower_CapstanTurnsWhileBeamTurns_NotOutside()
        {
            using (var sim = MakeSim())
            {
                var quadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var mat = new Material(Shader.Find("Sprites/Default"));
                try
                {
                    var view = new BattleBeaconView(sim, mat, quadObj.GetComponent<MeshFilter>().sharedMesh, new Bounds(Vector3.zero, Vector3.one * 2000f));
                    try
                    {
                        Assert.IsTrue(view.HasCapstan, "돌림대가 있다 (K-116 나무 칸 그림)");
                        view.Draw();
                        for (int i = 0; i < BattleBeaconView.WorkerTotal; i++)
                            AssertInsideRoom(view.WorkerFeet(i), $"쉴 때 일꾼 {i}");

                        float before = view.CapstanDegrees;
                        sim.RotateBeam(1);
                        Assert.IsTrue(sim.BeamRotating);
                        view.Draw();
                        Assert.IsTrue(view.WorkersPushing, "빛줄기가 도는 동안 일꾼이 민다");
                        for (int i = 0; i < BattleBeaconView.WorkerTotal; i++)
                        {
                            AssertInsideRoom(view.WorkerFeet(i), $"밀 때 일꾼 {i}");
                            Assert.IsTrue(view.WorkerVisible(i), "미는 동안 모두 보인다 (계단 오르는 일꾼도 돌아와 민다)");
                        }

                        Assert.AreEqual(before, view.CapstanDegrees, 0.01f, "시간이 안 흐르면(dt 0) 돌림대는 그대로");

                        // 전투 시간이 흐르면 돈다: 초당 PushDegreesPerSecond, 도는 방향은 빛줄기 방향(faceLeft)과 맞는다
                        float start = view.CapstanDegrees;
                        for (int step = 1; step <= 10; step++)
                        {
                            sim.BeginFrame(0.05f); sim.Schedule(0.05f, step * 0.05f); sim.Complete();
                            view.Draw();
                        }

                        float turned = Mathf.DeltaAngle(start, view.CapstanDegrees);
                        Assert.AreEqual((view.FacingLeft ? 1f : -1f) * BattleTowerInterior.PushDegreesPerSecond * 0.5f, turned, 0.5f, "0.5초 동안 28도/초로 돈다");
                        // 한 번에 큰 시간이 흘러도(멈춤 뒤 등) 0.25초어치까지만 센다
                        float before2 = view.CapstanDegrees;
                        sim.BeginFrame(5f); sim.Schedule(5f, 5.5f); sim.Complete();
                        view.Draw();
                        Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(before2, view.CapstanDegrees)), BattleTowerInterior.PushDegreesPerSecond * 0.25f + 0.01f, "한 프레임 최대 0.25초");
                    }
                    finally { view.Dispose(); }
                }
                finally { Object.DestroyImmediate(quadObj); Object.DestroyImmediate(mat); }
            }
        }

        [Test]
        public void Climber_WalksBackToCapstan_WithoutTeleporting_WhenBeamStartsTurning()
        {
            using (var sim = MakeSim())
            {
                var quadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var mat = new Material(Shader.Find("Sprites/Default"));
                try
                {
                    var view = new BattleBeaconView(sim, mat, quadObj.GetComponent<MeshFilter>().sharedMesh, new Bounds(Vector3.zero, Vector3.one * 2000f));
                    try
                    {
                        float t = 0f;
                        int guard = 0;
                        view.Draw();
                        // 쉬는 동안 계단 중간까지 오른다 (기다림 5초 + 걸음 2초 + 계단 2초)
                        while ((view.WorkerFeet(2).y - BattleSim.WallHeight) < 1.0f && guard++ < 400)
                        {
                            t += 0.05f; sim.BeginFrame(0.05f); sim.Schedule(0.05f, t); sim.Complete(); view.Draw();
                        }

                        Assert.Less(guard, 400, "일꾼이 계단을 오른다");
                        sim.RotateBeam(1);
                        Vector3 last = view.WorkerFeet(2);
                        float maxJump = 0f;
                        for (int i = 0; i < 120; i++)
                        {
                            t += 0.05f; sim.BeginFrame(0.05f); sim.Schedule(0.05f, t); sim.Complete(); view.Draw();
                            maxJump = Mathf.Max(maxJump, (view.WorkerFeet(2) - last).magnitude);
                            last = view.WorkerFeet(2);
                        }

                        Assert.Less(maxJump, 0.3f, "한 프레임(0.05초)에 0.3m 넘게 튀지 않는다");
                        Assert.IsTrue(view.WorkerVisible(2));
                        AssertInsideRoom(last, "돌아온 일꾼");
                    }
                    finally { view.Dispose(); }
                }
                finally { Object.DestroyImmediate(quadObj); Object.DestroyImmediate(mat); }
            }
        }

        static void AssertInsideRoom(Vector3 world, string label)
        {
            Vector3 local = world - new Vector3(0f, BattleSim.WallHeight, 0f);
            Assert.LessOrEqual(Mathf.Abs(local.x), BattleTowerInterior.InnerHalf, label + " x");
            Assert.LessOrEqual(Mathf.Abs(local.z), BattleTowerInterior.InnerHalf, label + " z");
            Assert.GreaterOrEqual(local.y, BattleTowerInterior.FloorY - 1e-3f, label + " y");
            Assert.LessOrEqual(local.y, BattleTowerView.GroundHeight, label + " 아래층 안");
            Assert.Less(Mathf.Abs(local.z), BattleTowerView.BaseHalf - 0.2f, label + ": 문 앞 마당(z < -1.1)에 서지 않는다");
        }

        [Test]
        public void Capstan_MeshExists_AndSitsInsideTheRoom()
        {
            Mesh mesh = BattleTowerInterior.BuildCapstanMesh();
            try
            {
                Bounds b = mesh.bounds;
                Assert.Greater(mesh.vertexCount, 40);
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), BattleTowerInterior.CapstanBarHalf + 0.05f);
                Assert.LessOrEqual(b.max.y + BattleTowerInterior.FloorY, BattleTowerView.GroundHeight, "축이 천장을 넘지 않는다");
                Assert.GreaterOrEqual(b.min.y, -1e-4f);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        static BattleSim MakeSim()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_tower_view_w27", out string error);
            Assert.IsNull(error);
            return new BattleSim(setup, lookup);
        }

        [Test]
        public void HeroDoor_AndCouriers_AreOutsideTheBody()
        {
            Assert.Greater(BattleCameraRig.TowerHeroBack, BattleTowerView.BaseHalf + 0.2f, "주인공은 문 앞 마당에 선다 (탑 안이 아니다)");
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_tower_view", out string error);
            Assert.IsNull(error);
            using (var sim = new BattleSim(setup, lookup))
            {
                for (int i = 0; i < BattleSim.TowerCourierCount; i++)
                {
                    Vector3 p = sim.GetTowerCourier(i).Position;
                    Assert.AreEqual(BattleSim.WallHeight, p.y, 1e-4f);
                    Assert.IsTrue(Mathf.Abs(p.x) > BattleTowerView.BaseHalf + 0.15f || Mathf.Abs(p.z) > BattleTowerView.BaseHalf + 0.15f, $"전령 {i}는 탑 몸체 밖");
                }
            }
        }
    }
}
