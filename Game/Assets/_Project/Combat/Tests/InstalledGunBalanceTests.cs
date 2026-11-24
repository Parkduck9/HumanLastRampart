using System.Collections.Generic;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Upgrades;
using NUnit.Framework;
using UnityEditor;

namespace HumanLastRampart.Combat.Tests
{
    public sealed class InstalledGunBalanceTests
    {
        static DefinitionLookup Load()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            return DefinitionLookup.Build(catalogs);
        }

        [TestCase(0, 30, 40f, 0.8f, 0.45f)]
        [TestCase(1, 30, 40f, 0.85f, 0.55f)]
        [TestCase(3, 36, 46f, 0.85f, 0.55f)]
        public void InstalledGunAndTrainingFlowIntoActualBattle(int level, int damage, float range, float mid, float far)
        {
            DefinitionLookup defs = Load();
            CampaignState campaign = CampaignFactory.Create(defs, new NewGameOptions(Difficulty.Easy), 11);
            campaign.Gold = 100000;
            for (int i = 0; i < level; i++)
                Assert.IsTrue(UpgradeRules.TryTrain(campaign, defs, "unit_gunner", out string reason), reason);
            BattleSetup setup = BattleSetupFactory.Create(campaign, defs, "installed_gun_balance_test", out string error);
            Assert.IsNull(error);
            using (var sim = new BattleSim(setup, defs))
            {
                var weapon = sim.WeaponOfSoldier(sim.FirstSoldierOf("unit_gunner", false));
                Assert.AreEqual(damage, weapon.Damage);
                Assert.That(weapon.Range, Is.EqualTo(range).Within(0.0001f));
                Assert.That(weapon.Interval, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(weapon.AccuracyNear, Is.EqualTo(0.98f).Within(0.0001f));
                Assert.That(weapon.AccuracyMid, Is.EqualTo(mid).Within(0.0001f));
                Assert.That(weapon.AccuracyFar, Is.EqualTo(far).Within(0.0001f));
                Assert.AreEqual(8, defs.Get<UnitDef>("unit_gunner").RecruitCost);
            }
        }
    }
}
