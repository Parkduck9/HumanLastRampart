using System.Collections.Generic;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;

namespace HumanLastRampart.Combat.Tests
{
    // 등불 밝기와 등대 빛줄기 (Concept_Beacon_v1 9.3·9.4): 적 수 배율, 부채꼴 안 느려짐·피해 증가, 3초 회전, 밝기별 켠 시간
    public sealed class BattleLampTests
    {
        static DefinitionLookup Lookup()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            return DefinitionLookup.Build(catalogs);
        }

        static BattleSim NewSim(LampBrightness lamp, int beamSide = 0, int tier = 0)
        {
            DefinitionLookup lookup = Lookup();
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_lamp_test", out string error);
            Assert.IsNull(error);
            setup.LampBrightness = lamp;
            setup.LampCountMultiplier = lookup.All<BattleRulesDef>()[0].LampCountMultiplierFor((int)lamp);   // 팩토리는 계획된 밝기로 채우므로 시험이 바꾼 밝기에 맞춘다 (W11 데이터 값)
            setup.BeamSide = beamSide;
            setup.EnemyTier = tier;
            return new BattleSim(setup, lookup);
        }

        // 강 밝기 빛 안의 변형 적은 변형 효과가 꺼지고, 빛을 벗어나면 다시 켜진다. 보통 밝기에서는 꺼지지 않는다 (9.4·9.5)
        [Test]
        public void HighBeam_SuppressesTierBonusInsideOnly()
        {
            using (var sim = NewSim(LampBrightness.High, 0, 2))
            {
                float time = 0f;
                for (int i = 0; i < 400 && AliveList(sim, 2).Count < 2; i++) Step(sim, ref time);
                List<int> alive = AliveList(sim, 2);
                Assert.AreEqual(2, alive.Count);
                int a = alive[0], b = alive[1];
                sim.DebugPlaceEnemy(a, new float3(0f, 0f, 100f));     // 빛 안 (정북)
                sim.DebugPlaceEnemy(b, new float3(0f, 0f, -100f));    // 빛 밖 (정남)
                sim.Complete();
                Assert.IsFalse(sim.TierBonusActive(a), "강 빛 안: 변형 꺼짐");
                Assert.IsFalse(sim.TierHpActive(a) || sim.TierSpeedActive(a), "강 빛 안: 체력·속도 보너스 모두 없어짐");
                Assert.IsTrue(sim.TierBonusActive(b), "빛 밖: 변형 그대로");

                sim.DebugPlaceEnemy(a, new float3(0f, 0f, -100f));
                sim.Complete();
                Assert.IsTrue(sim.TierBonusActive(a), "빛을 벗어나면 다시 켜짐");

                sim.SetLamp(LampBrightness.Normal);
                sim.DebugPlaceEnemy(a, new float3(0f, 0f, 100f));
                sim.Complete();
                Assert.IsTrue(sim.TierBonusActive(a), "보통 빛은 변형 전체를 끄지 않음 (속도·크기·색은 그대로)");
                Assert.IsFalse(sim.TierHpActive(a), "W11: 보통 빛 안: 변형 체력 보너스는 없어짐");
                Assert.IsTrue(sim.TierSpeedActive(a), "W11: 보통 빛 안: 속도 보너스는 그대로");
                Assert.IsTrue(sim.TierHpActive(b), "빛 밖은 체력 보너스 그대로");

                sim.SetLamp(LampBrightness.Low);
                sim.Complete();
                Assert.IsTrue(sim.TierHpActive(a) && sim.TierSpeedActive(a), "약한 빛은 아무것도 없애지 않음 (체력 보너스 다시 켜짐)");
            }
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

        [Test]
        public void EnemyCount_ScalesWithBrightness()
        {
            int normal, low, high;
            using (var sim = NewSim(LampBrightness.Normal)) normal = sim.TotalEnemies;
            using (var sim = NewSim(LampBrightness.Low)) low = sim.TotalEnemies;
            using (var sim = NewSim(LampBrightness.High)) high = sim.TotalEnemies;
            Assert.Greater(normal, 50);
            Assert.AreEqual(normal * 0.8f, low, normal * 0.02f + 1f, "약 ×0.8");
            Assert.AreEqual(normal * 1.15f, high, normal * 0.02f + 1f, "강 ×1.15");
        }

        [Test]
        public void EnemyCount_DoesNotChangeWhenLampChangesInBattle()
        {
            using (var sim = NewSim(LampBrightness.Normal))
            {
                int before = sim.TotalEnemies;
                sim.SetLamp(LampBrightness.High);
                Assert.AreEqual(before, sim.TotalEnemies);
                Assert.AreEqual(LampBrightness.High, sim.Lamp);
            }
        }

        [Test]
        public void BeamWidthAndMultipliers_FollowTable()
        {
            using (var sim = NewSim(LampBrightness.Normal))
            {
                Assert.AreEqual(60f, sim.BeamWidth);
                sim.SetLamp(LampBrightness.Low); Assert.AreEqual(40f, sim.BeamWidth);
                sim.SetLamp(LampBrightness.High); Assert.AreEqual(80f, sim.BeamWidth);
                Assert.IsTrue(sim.BeamSuppressesTiers, "강은 변형 효과를 끄는 훅");
            }

            Assert.AreEqual(0.15f, BattleSim.BeamSlowAmount[(int)LampBrightness.Low]);
            Assert.AreEqual(0.25f, BattleSim.BeamSlowAmount[(int)LampBrightness.Normal]);
            Assert.AreEqual(0.35f, BattleSim.BeamSlowAmount[(int)LampBrightness.High]);
            Assert.AreEqual(0.10f, BattleSim.BeamDamageBonus[(int)LampBrightness.Low]);
            Assert.AreEqual(0.20f, BattleSim.BeamDamageBonus[(int)LampBrightness.Normal]);
            Assert.AreEqual(0.30f, BattleSim.BeamDamageBonus[(int)LampBrightness.High]);
        }

        [Test]
        public void ConeMembership_UsesHalfWidthAroundBeamDirection()
        {
            // 북쪽(+z) 보통 60°: 반폭 30°
            using (var sim = NewSim(LampBrightness.Normal, 0))
            {
                float time = 0f;
                for (int i = 0; i < 400 && AliveList(sim, 3).Count < 3; i++) Step(sim, ref time);
                List<int> alive = AliveList(sim, 4);
                Assert.GreaterOrEqual(alive.Count, 3, "살아 있는 적이 충분히 나와야 시험할 수 있다");
                sim.DebugPlaceEnemy(alive[0], new float3(0f, 0f, 80f));
                Assert.IsTrue(sim.InBeam(alive[0]), "정북");
                sim.DebugPlaceEnemy(alive[0], new float3(40f * math.tan(math.radians(29f)), 0f, 40f));
                Assert.IsTrue(sim.InBeam(alive[0]), "29°는 안");
                sim.DebugPlaceEnemy(alive[0], new float3(40f * math.tan(math.radians(31f)), 0f, 40f));
                Assert.IsFalse(sim.InBeam(alive[0]), "31°는 밖");
                sim.DebugPlaceEnemy(alive[0], new float3(0f, 0f, -80f));
                Assert.IsFalse(sim.InBeam(alive[0]), "반대편은 밖");
                // 강 밝기는 80°: 35°가 안으로 들어온다
                sim.SetLamp(LampBrightness.High);
                sim.DebugPlaceEnemy(alive[0], new float3(40f * math.tan(math.radians(35f)), 0f, 40f));
                Assert.IsTrue(sim.InBeam(alive[0]), "강 35°는 안");
            }
        }

        [Test]
        public void DamageTaken_IsRaisedInsideBeamOnly()
        {
            using (var sim = NewSim(LampBrightness.Normal, 0))
            {
                float time = 0f;
                List<int> north = null;
                for (int k = 0; k < 600; k++)
                {
                    north = new List<int>();
                    for (int i = 0; i < sim.TotalEnemies && north.Count < 2; i++) if (sim.DebugAlive(i) && sim.DebugSide(i) == 0) north.Add(i);
                    if (north.Count >= 2) break;
                    Step(sim, ref time);
                }

                Assert.AreEqual(2, north.Count, "북쪽에서 걷는 적 둘");
                int a = north[0], b = north[1];
                sim.DebugPlaceEnemy(a, new float3(0f, 0f, 100f));      // 빛 안 (정북)
                sim.DebugPlaceEnemy(b, new float3(100f, 0f, 100f));    // 빛 밖 (45°)
                int hpA = sim.DebugEnemyHp(a), hpB = sim.DebugEnemyHp(b);
                sim.DebugHit(a, 10);
                sim.DebugHit(b, 10);
                Step(sim, ref time);
                Assert.AreEqual(10, hpB - sim.DebugEnemyHp(b), "빛 밖 10 그대로");
                Assert.AreEqual(12, hpA - sim.DebugEnemyHp(a), "빛 안 보통 +20%");
            }
        }
        [Test]
        public void RotateBeam_TakesSixSeconds()
        {
            Assert.AreEqual(6f, BattleSim.BeamRotateSeconds, "W9: 일꾼이 돌리는 속도 6초 (조정 가능한 상수)");
            using (var sim = NewSim(LampBrightness.Normal, 0))
            {
                Assert.AreEqual(90f, sim.BeamAngleDegrees, 1e-3f);
                float time = 0f;
                sim.RotateBeam(1);   // 북 → 동 (90° → 0°)
                Assert.IsTrue(sim.BeamRotating);
                Assert.AreEqual(0f, sim.BeamTargetDegrees, 1e-3f, "도착할 각도");
                for (int i = 0; i < 60; i++) Step(sim, ref time);   // 3초
                Assert.IsTrue(sim.BeamRotating);
                Assert.AreEqual(45f, sim.BeamAngleDegrees, 1.5f, "도는 중에는 중간 각도");
                Assert.AreEqual(0.5f, sim.BeamRotateProgress, 0.03f);
                for (int i = 0; i < 59; i++) Step(sim, ref time);   // 5.95초
                Assert.IsTrue(sim.BeamRotating, "6초 전에는 아직");
                for (int i = 0; i < 2; i++) Step(sim, ref time);
                Assert.IsFalse(sim.BeamRotating);
                Assert.AreEqual(0f, sim.BeamAngleDegrees, 1e-3f);
                Assert.AreEqual(1, sim.BeamSide);
                Assert.AreEqual(sim.BeamAngleDegrees, sim.BeamTargetDegrees, 1e-3f, "서 있으면 목표 = 지금");
            }
        }

        [Test]
        public void LampSeconds_SumToElapsedAndLandInResult()
        {
            using (var sim = NewSim(LampBrightness.Low))
            {
                float time = 0f;
                for (int i = 0; i < 20; i++) Step(sim, ref time);   // 약 1초
                sim.SetLamp(LampBrightness.High);
                for (int i = 0; i < 40; i++) Step(sim, ref time);   // 강 2초
                sim.ForceEnd(BattleOutcome.Victory, DefeatReason.None, -1);
                BattleResult result = sim.BuildResult();
                Assert.AreEqual(3, result.LampSeconds.Length);
                Assert.AreEqual(1f, result.LampSeconds[(int)LampBrightness.Low], 0.01f);
                Assert.AreEqual(2f, result.LampSeconds[(int)LampBrightness.High], 0.01f);
                Assert.AreEqual(0f, result.LampSeconds[(int)LampBrightness.Normal], 1e-4f);
                Assert.AreEqual(sim.Elapsed, result.LampSeconds[0] + result.LampSeconds[1] + result.LampSeconds[2], 0.01f);
            }
        }

        static List<int> AliveList(BattleSim sim, int max)
        {
            var list = new List<int>();
            for (int i = 0; i < sim.TotalEnemies && list.Count < max; i++) if (sim.DebugAlive(i)) list.Add(i);
            return list;
        }
    }
}