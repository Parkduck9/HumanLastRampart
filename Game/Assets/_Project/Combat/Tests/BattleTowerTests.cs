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
    // 등대 탑 계단 (W9, W35 네 성벽): 통로 가운데 안쪽 끝에서 내려가고 탑에서 W·A·S·D 성벽으로 올라오기, 거리 ÷ 속도 시간, 탑에 있는 동안 '성벽에 없음'
    public sealed class BattleTowerTests
    {
        static BattleCameraRig NewRig(out GameObject camera)
        {
            camera = new GameObject("Tower Test Camera", typeof(Camera));
            return new BattleCameraRig(camera.GetComponent<Camera>(), 60f, BattleSim.WallHeight);
        }

        static void Run(BattleCameraRig rig, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.05f) rig.Update(0.05f);
        }

        [Test]
        public void Stairs_GoDownAndUp_FollowsRulesAndLeavesWall()
        {
            var rig = NewRig(out GameObject camera);
            try
            {
                Assert.IsTrue(rig.OnWall, "처음엔 성벽 위");
                Assert.AreEqual(BattleCameraRig.StairsSide, rig.Side);
                Assert.IsFalse(rig.NearStairs, "통로 바깥쪽에서는 계단이 멀다");
                Assert.IsFalse(rig.GoDown());

                rig.DepthInput = -1f;   // S: 통로 안쪽으로
                Run(rig, 0.5f);
                rig.DepthInput = 0f;
                Assert.IsTrue(rig.NearStairs, "통로 안쪽 끝 = 계단");
                float along = rig.Along;
                Vector3 wallFeet = rig.HeroFeet;

                Assert.IsTrue(rig.GoDown());
                Assert.IsFalse(rig.OnWall, "내려가기 시작하면 성벽을 비운다");
                Assert.IsTrue(rig.InTransition);
                rig.WalkInput = 1f;   // 이동 입력은 탑 오가는 동안 무시된다
                Run(rig, rig.PathSeconds * 0.5f);
                Assert.AreEqual(BattleCameraRig.HeroPose.Walk, rig.Pose);
                Assert.IsTrue(rig.FacingFront, "내려올 때는 사진기 쪽으로 걷는 앞모습");
                Assert.IsFalse(rig.AtTower);
                Run(rig, rig.PathSeconds);
                rig.WalkInput = 0f;
                Assert.IsTrue(rig.AtTower);
                Assert.IsFalse(rig.OnWall);
                Assert.AreEqual(along, rig.Along, 1e-4f, "성벽 위 자리는 그대로 기억");
                Assert.Less((rig.HeroFeet - rig.TowerFeet).magnitude, 1e-3f, "탑 앞에 선다");
                Assert.AreEqual(BattleSim.WallHeight, rig.HeroFeet.y, 1e-3f, "마당 윗면은 성벽 윗면 높이");
                Assert.AreEqual(BattleCameraRig.HeroPose.Idle, rig.Pose);

                Assert.IsTrue(rig.GoUp());
                Assert.IsFalse(rig.OnWall, "오르는 중에도 아직 성벽에 없다");
                Run(rig, rig.PathSeconds * 0.5f);
                Assert.IsFalse(rig.FacingFront);
                Run(rig, rig.PathSeconds);
                Assert.IsTrue(rig.OnWall);
                Assert.Less((rig.HeroFeet - wallFeet).magnitude, 1e-3f, "올라와 처음 서 있던 자리로");
                Assert.IsFalse(rig.GoUp(), "성벽에서는 올라갈 곳이 없다");
            }
            finally { Object.DestroyImmediate(camera); }
        }

        [Test]
        public void Stairs_OnlyAtWallMiddle_OnEveryWall()
        {
            var rig = NewRig(out GameObject camera);
            try
            {
                for (int side = 0; side < 4; side++)
                {
                    rig.ShowSide(side);
                    Assert.IsFalse(rig.NearStairs, $"성벽 {side}: 통로 바깥쪽에서는 계단이 멀다");
                    GoToStairs(rig);
                    Assert.IsTrue(rig.NearStairs, $"성벽 {side}: 통로 안쪽 끝 = 계단");
                    rig.WalkInput = 1f;   // 오른쪽으로 달려 계단에서 벗어난다
                    Run(rig, 1.0f);   // 5m
                    rig.WalkInput = 0f;
                    Assert.IsFalse(rig.NearStairs, $"성벽 {side}: 가운데에서 3m 넘게 벗어나면 계단이 아니다");
                    Assert.IsFalse(rig.GoDown());
                    Assert.IsTrue(rig.OnWall);
                }
            }
            finally { Object.DestroyImmediate(camera); }
        }

        // 통로 안쪽 끝(계단)까지 걷는다
        static void GoToStairs(BattleCameraRig rig)
        {
            rig.DepthInput = -1f;
            Run(rig, 0.5f);
            rig.DepthInput = 0f;
        }

        // 탑 앞에 닿을 때까지 (시간 제한 안에)
        static void RunUntilArrived(BattleCameraRig rig, System.Func<bool> arrived)
        {
            for (float t = 0f; t < rig.PathSeconds + 2f && !arrived(); t += 0.05f) rig.Update(0.05f);
        }

        static float OutwardYaw(int side)
        {
            var n = WallGeometry.Outward(side);
            return Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;
        }

        [Test]
        public void Stairs_DescendFromEachOfTheFourWalls_ToTheDoor()
        {
            var rig = NewRig(out GameObject camera);
            try
            {
                for (int side = 0; side < 4; side++)
                {
                    rig.ShowSide(side);
                    GoToStairs(rig);
                    float along = rig.Along;
                    Assert.IsTrue(rig.GoDown(), $"성벽 {side}에서 E");
                    Assert.IsFalse(rig.OnWall);
                    Assert.AreEqual(side, rig.PathSide);
                    RunUntilArrived(rig, () => rig.AtTower);
                    Assert.IsTrue(rig.AtTower, $"성벽 {side}: 탑 앞 도착");
                    Assert.AreEqual(side, rig.Side, "내려온 성벽을 기억");
                    Assert.AreEqual(along, rig.Along, 1e-4f);
                    Assert.Less((rig.HeroFeet - rig.TowerFeet).magnitude, 1e-3f);
                    Run(rig, 1f);
                    Assert.AreEqual(0f, Mathf.DeltaAngle(rig.Yaw, 0f), 1f, "탑 앞에서는 사진기가 북쪽(문 쪽)을 본다");
                    Assert.IsTrue(rig.GoUp(), "왔던 성벽으로");
                    RunUntilArrived(rig, () => rig.OnWall);
                    Assert.IsTrue(rig.OnWall);
                    Assert.AreEqual(side, rig.Side);
                    Run(rig, 1f);
                    Assert.AreEqual(0f, Mathf.DeltaAngle(rig.Yaw, OutwardYaw(side)), 1f, "올라오면 그 성벽 바깥을 본다");
                }
            }
            finally { Object.DestroyImmediate(camera); }
        }

        [Test]
        public void Stairs_GoUpFromTower_ToEachOfTheFourWalls_AtTheirMiddle()
        {
            var rig = NewRig(out GameObject camera);
            try
            {
                rig.ShowSide(0);
                GoToStairs(rig);
                rig.WalkInput = 1f;
                Run(rig, 0.3f);   // 가운데에서 조금 옆 (1.5m)
                rig.WalkInput = 0f;
                Assert.IsTrue(rig.NearStairs);
                Assert.IsTrue(rig.GoDown());
                RunUntilArrived(rig, () => rig.AtTower);
                Assert.IsTrue(rig.AtTower);
                Assert.IsFalse(rig.GoUp(4), "없는 성벽");
                Assert.IsFalse(rig.GoUp(-1));
                foreach (int side in new[] { 1, 2, 3, 0 })   // 동 · 남 · 서 · 북 (D · S · A · W)
                {
                    Assert.IsTrue(rig.GoUp(side), $"탑에서 성벽 {side}로");
                    Assert.IsFalse(rig.OnWall, "올라가는 동안 성벽을 비운다");
                    Assert.IsFalse(rig.GoUp(side), "올라가는 중에는 다시 고를 수 없다");
                    RunUntilArrived(rig, () => rig.OnWall);
                    Assert.IsTrue(rig.OnWall);
                    Assert.AreEqual(side, rig.Side);
                    Assert.AreEqual(0f, rig.Along, 1e-4f, "그 성벽 가운데");
                    Assert.AreEqual(BattleCameraRig.HeroInset, rig.Inset, 1e-4f);
                    Assert.Less((rig.HeroFeet - rig.WallFeet(side, 0f, BattleCameraRig.HeroInset)).magnitude, 1e-3f);
                    Run(rig, 1f);
                    Assert.AreEqual(0f, Mathf.DeltaAngle(rig.Yaw, OutwardYaw(side)), 1f, "그 성벽 바깥을 본다");

                    GoToStairs(rig);   // 다시 계단에서 내려가 다음 성벽을 고른다
                    Assert.IsTrue(rig.GoDown());
                    RunUntilArrived(rig, () => rig.AtTower);
                    Assert.IsTrue(rig.AtTower);
                }

                // 내려가는 중에는 내려오던 성벽으로만 되돌아간다
                rig.ShowSide(1);
                GoToStairs(rig);
                Assert.IsTrue(rig.GoDown());
                Run(rig, 1.5f);
                Assert.IsFalse(rig.AtTower);
                Assert.IsFalse(rig.GoUp(3), "다른 성벽으로는 되돌아갈 수 없다");
                Assert.IsTrue(rig.GoUp(1), "내려오던 성벽으로 되돌아간다");
                RunUntilArrived(rig, () => rig.OnWall);
                Assert.IsTrue(rig.OnWall);
                Assert.AreEqual(1, rig.Side);
            }
            finally { Object.DestroyImmediate(camera); }
        }

        // 거리 ÷ 속도: 평지는 성벽 달리기 속도, 계단은 걷기 속도 — 예전처럼 어디서든 1.6초가 아니다
        [Test]
        public void TravelTime_IsDistanceOverSpeed_NotFixed()
        {
            var rig = NewRig(out GameObject camera);
            try
            {
                foreach (int side in new[] { 0, 1, 2, 3 })
                {
                    rig.ShowSide(side);
                    GoToStairs(rig);
                    Vector3 prev = rig.HeroFeet;
                    Assert.IsTrue(rig.GoDown());
                    float planned = rig.PathSeconds;
                    float length = 0f, maxSpeed = 0f, minSpeed = float.MaxValue, elapsed = 0f;
                    while (!rig.AtTower && elapsed < planned + 2f)
                    {
                        rig.Update(0.05f);
                        elapsed += 0.05f;
                        Vector3 now = rig.HeroFeet;
                        float step = Vector3.Distance(prev, now);
                        if (step > 1e-4f)
                        {
                            length += step;
                            maxSpeed = Mathf.Max(maxSpeed, step / 0.05f);
                            if (step > 0.1f) minSpeed = Mathf.Min(minSpeed, step / 0.05f);
                        }

                        prev = now;
                    }

                    Assert.AreEqual(planned, elapsed, 0.1f, $"성벽 {side}: 걸린 시간 = 계획한 시간");
                    Assert.Greater(elapsed, 5f, $"성벽 {side}: 고정 1.6초가 아니라 길이에 따라 (반폭 60m)");
                    Assert.LessOrEqual(maxSpeed, BattleSim.HeroSpeed + 0.01f, "평지는 성벽 달리기 속도를 넘지 않는다");
                    Assert.AreEqual(BattleSim.HeroWalkSpeed, minSpeed, 0.05f, "계단 구간은 걷기 속도");
                    float stair = (60f - rig.Inset) - (60f - BattleCourtyard.WallThickness - BattleCourtyard.StepCount * BattleCourtyard.TreadDepth);   // 통로 안쪽 끝에서 계단 맨 아래까지
                    float expected = (length - stair) / BattleSim.HeroSpeed + stair / BattleSim.HeroWalkSpeed;
                    Assert.AreEqual(expected, elapsed, 0.3f, $"성벽 {side}: 시간 ≈ 평지 거리 / {BattleSim.HeroSpeed} + 계단 거리 / {BattleSim.HeroWalkSpeed}");
                    rig.GoUp();
                    RunUntilArrived(rig, () => rig.OnWall);
                }
            }
            finally { Object.DestroyImmediate(camera); }
        }

        [Test]
        public void ShowSide_ResetsTower()
        {
            var rig = NewRig(out GameObject camera);
            try
            {
                rig.DepthInput = -1f;
                Run(rig, 0.5f);
                rig.DepthInput = 0f;
                rig.GoDown();
                RunUntilArrived(rig, () => rig.AtTower);
                Assert.IsTrue(rig.AtTower);
                rig.ShowSide(2);
                Assert.IsTrue(rig.OnWall, "패배 장면 등으로 성벽을 보여 줄 때는 탑 상태를 푼다");
                Assert.IsFalse(rig.TowerTarget);
            }
            finally { Object.DestroyImmediate(camera); }
        }

        [Test]
        public void Commander_AbsentFlagFollowsSetCommander()
        {
            DefinitionLookup lookup = Lookup();
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_tower_test", out string error);
            Assert.IsNull(error);
            using (var sim = new BattleSim(setup, lookup))
            {
                Assert.IsTrue(sim.CommanderOnWall, "처음엔 북쪽 성벽 지휘관");
                sim.SetCommander(0, 0f, false);
                Assert.IsFalse(sim.CommanderOnWall, "탑에 있으면 성벽에 없다");
                sim.SetCommander(0, 0f);
                Assert.IsTrue(sim.CommanderOnWall, "생략하면 성벽 위 (예전 호출과 같다)");
            }
        }

        [Test]
        public void Beacon_TowerSizeMatchesK110Art()
        {
            // K-110: 탑 그림 칸 크기와 불그릇 높이는 W9 2차에서 새 그림(탑 키 약 3m)에 맞춘다
            Assert.Greater(BattleBeaconView.TowerCellMeters, 1.2f);
            Assert.Greater(BattleBeaconView.BowlHeight, 0.8f);
            Assert.Less(BattleBeaconView.BowlHeight, BattleBeaconView.TowerCellMeters);
        }

        [Test]
        public void TowerFlameStrength_IgnitionNightFullElseFromRepair()
        {
            Assert.AreEqual(1f, BeaconRules.TowerFlameStrength(0f, true), 1e-4f);
            Assert.AreEqual(0.35f, BeaconRules.TowerFlameStrength(0f, false), 1e-4f);
            Assert.AreEqual(0.85f, BeaconRules.TowerFlameStrength(BeaconRules.RepairMax, false), 1e-4f);
            Assert.Greater(BeaconRules.TowerFlameStrength(50f, false), BeaconRules.TowerFlameStrength(10f, false));
        }

        static void Step(BattleSim sim, ref float time)
        {
            const float dt = 0.05f;
            sim.BeginFrame(dt);
            if (sim.Ended) return;
            time += dt;
            sim.Schedule(dt, time);
            sim.Complete();
        }

        static BattleSim NewSim()
        {
            DefinitionLookup lookup = Lookup();
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_tower_courier", out string error);
            Assert.IsNull(error);
            return new BattleSim(setup, lookup);
        }

        [Test]
        public void Rotate_SameSideIgnored_And180Takes12Seconds()
        {
            using (var sim = NewSim())
            {
                float time = 0f;
                sim.RotateBeam(sim.BeamSide);
                Assert.IsFalse(sim.BeamRotating, "같은 방향 명령은 무시");
                int far = (sim.BeamSide + 2) % 4;
                sim.RotateBeam(far);
                Assert.IsTrue(sim.BeamRotating);
                Assert.AreEqual(12f, sim.BeamSecondsLeft, 0.01f, "180도 = 12초");
                for (int i = 0; i < 230; i++) Step(sim, ref time);   // 11.5초
                Assert.IsTrue(sim.BeamRotating);
                for (int i = 0; i < 20; i++) Step(sim, ref time);
                Assert.IsFalse(sim.BeamRotating);
            }
        }

        [Test]
        public void TowerCouriers_ThreeStandby_RunToWall_ForthQueues()
        {
            using (var sim = NewSim())
            {
                float time = 0f;
                int started = 0;
                var notices = new List<string>();
                sim.SquadOrderStarted += () => started++;
                sim.Notice += n => notices.Add(n);
                Assert.AreEqual(3, sim.TowerCouriersFree);

                sim.IssueMessenger(BattleSim.TowerOrigin, 0f, 1, TargetPriority.HighestHpRatio);
                sim.IssueMessenger(BattleSim.TowerOrigin, 0f, 2, TargetPriority.HighestHpRatio);
                sim.IssueMessenger(BattleSim.TowerOrigin, 0f, 3, TargetPriority.HighestHpRatio);
                Step(sim, ref time);
                Assert.AreEqual(0, sim.TowerCouriersFree);
                Assert.AreEqual(0, started, "빛·우선순위 명령은 분대 명령 수에 세지 않는다");

                sim.IssueMessenger(BattleSim.TowerOrigin, 0f, 1, TargetPriority.Nearest);
                Step(sim, ref time);
                Assert.AreEqual(1, sim.TowerOrdersQueued, "네 번째 명령은 줄을 선다");
                Assert.IsTrue(notices.Exists(n => n.Contains("줄을 섭니다")), "줄 섰다고 알린다");
                Assert.IsTrue(sim.GetTowerCourier(0).Busy);

                float limit = 0f;
                for (int s = 0; s < 4; s++) limit = Mathf.Max(limit, sim.TowerRouteSeconds(s));
                for (int i = 0; i < (int)(limit * 2f / 0.05f) + 200; i++) Step(sim, ref time);
                Assert.AreEqual(0, sim.TowerOrdersQueued);
                Assert.AreEqual(TargetPriority.Nearest, sim.PriorityOf(1), "줄 선 명령도 결국 성벽에 적용 — 조용히 사라지지 않는다");

                for (int i = 0; i < (int)(limit * 3f / 0.05f); i++) Step(sim, ref time);
                Assert.AreEqual(3, sim.TowerCouriersFree, "전령은 탑으로 돌아와 다시 대기");
            }
        }
        static DefinitionLookup Lookup()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            return DefinitionLookup.Build(catalogs);
        }
    }
}
