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
    // 마법사 자리와 이동 (D-058): 정면 가운데에서 시작, 다른 성벽 쪽 마법은 그 성벽 가운데로 가서 도착하면 나간다
    public sealed class BattleWizardTests
    {
        static DefinitionLookup LoadAll()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            return DefinitionLookup.Build(catalogs);
        }

        static BattleSim NewSim(DefinitionLookup lookup)
        {
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_wizard_test", out string error);
            Assert.IsNull(error);
            return new BattleSim(setup, lookup);
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
        public void Wizard_StartsAtNorthCenter_WithBackSheet()
        {
            DefinitionLookup lookup = LoadAll();
            using (BattleSim sim = NewSim(lookup))
            {
                Assert.IsTrue(sim.WizardPresent);
                Assert.AreEqual(0, sim.WizardSide, "정면(북)");
                Vector3 feet = sim.WizardFeet;
                Assert.Less(Mathf.Abs(feet.x), 1.5f, "가운데 근처");
                Assert.Greater(feet.z, sim.CastleHalf - 2f, "북쪽 성벽 위");
                Assert.AreEqual(BattleSim.WallHeight, feet.y, 0.001f);
                Texture sheet = sim.WizardUnit.Sheet;
                Assert.IsNotNull(sheet, "마법사 그림 연결");
                Assert.AreEqual(256, sheet.width);
                Assert.AreEqual(384, sheet.height);
            }
        }

        [Test]
        public void SpellOnFront_FiresAtOnce()
        {
            DefinitionLookup lookup = LoadAll();
            using (BattleSim sim = NewSim(lookup))
            {
                float time = 0f;
                Step(sim, ref time);
                Assert.IsTrue(sim.CastSpell(0, new Vector3(0f, 0f, sim.CastleHalf + 40f)));
                Assert.IsFalse(sim.WizardMoving);
                Assert.AreEqual(1, sim.Effects.Count, "바로 나간다");
            }
        }

        [Test]
        public void SpellOnEast_WalksThere_ThenFires_AndStays()
        {
            DefinitionLookup lookup = LoadAll();
            using (BattleSim sim = NewSim(lookup))
            {
                float time = 0f;
                Step(sim, ref time);
                Assert.IsTrue(sim.CastSpell(0, new Vector3(sim.CastleHalf + 40f, 0f, 0f)));
                Assert.IsTrue(sim.WizardMoving);
                Assert.AreEqual(1, sim.WizardSide, "동쪽으로 간다");
                Assert.AreEqual(0, sim.Effects.Count, "도착 전에는 안 나간다");
                Assert.IsFalse(sim.CastSpell(1, new Vector3(-sim.CastleHalf - 40f, 0f, 0f)), "이동 중에는 다른 마법 불가");

                // 북 가운데 → 동 가운데 = 120m, 10m/초 = 12초
                float start = time;
                for (int i = 0; i < 400 && sim.WizardMoving && !sim.Ended; i++) Step(sim, ref time);
                Assert.IsFalse(sim.WizardMoving, "도착");
                Assert.AreEqual(12f, time - start, 0.3f, "약 12초");
                Assert.AreEqual(1, sim.Effects.Count, "도착하면 나간다");
                Vector3 feet = sim.WizardFeet;
                Assert.Greater(feet.x, sim.CastleHalf - 2f, "동쪽 성벽 위");
                Assert.Less(Mathf.Abs(feet.z), 1.5f, "가운데 근처");

                for (int i = 0; i < 40; i++) Step(sim, ref time);
                Assert.AreEqual(1, sim.WizardSide, "쓴 뒤에도 동쪽에 머문다");
            }
        }
    }
}
