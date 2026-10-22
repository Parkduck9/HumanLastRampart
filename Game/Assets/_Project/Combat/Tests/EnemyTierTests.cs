using System.Collections.Generic;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEditor;

namespace HumanLastRampart.Combat.Tests
{
    // 적 변형 3단계 배율 (등대 설계 9.5)
    public sealed class EnemyTierTests
    {
        [Test]
        public void Rules_MultipliersPerTier()
        {
            Assert.AreEqual(1f, EnemyTierRules.HpMultiplier(0));
            Assert.AreEqual(1.25f, EnemyTierRules.HpMultiplier(1));
            Assert.AreEqual(1.5f, EnemyTierRules.HpMultiplier(2));
            Assert.AreEqual(1f, EnemyTierRules.SpeedMultiplier(0));
            Assert.AreEqual(1f, EnemyTierRules.SpeedMultiplier(1));
            Assert.AreEqual(1.15f, EnemyTierRules.SpeedMultiplier(2));
            Assert.AreEqual(1f, EnemyTierRules.ScaleMultiplier(1));
            Assert.AreEqual(1.15f, EnemyTierRules.ScaleMultiplier(2));
            Assert.AreEqual(1f, EnemyTierRules.TintMultiplier(0).x);
            Assert.Less(EnemyTierRules.TintMultiplier(1).x, 1f, "물듦은 어둡게");
            Assert.AreEqual(EnemyTierRules.TintMultiplier(1), EnemyTierRules.TintMultiplier(2));
            Assert.AreEqual(3, EnemyTierRules.Clamp(3) + 1, "범위 밖은 2로");
            Assert.AreEqual(0, EnemyTierRules.Clamp(-4));
        }

        static DefinitionLookup LoadAll()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            return DefinitionLookup.Build(catalogs);
        }

        static BattleSetup Setup(DefinitionLookup lookup, int tier)
        {
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 5);
            campaign.BattleNumber = 2;
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_tier", out string error);
            Assert.IsNull(error);
            setup.EnemyTier = tier;
            return setup;
        }

        [Test]
        public void Sim_AppliesTierToHpAndSpeed_AndBeamCanDisable()
        {
            DefinitionLookup lookup = LoadAll();
            using (var plain = new BattleSim(Setup(lookup, 0), lookup))
            using (var tiered = new BattleSim(Setup(lookup, 2), lookup))
            {
                Assert.AreEqual(2, tiered.EnemyTier);
                Assert.AreEqual(0, plain.TierOf(0));
                Assert.AreEqual(2, tiered.TierOf(0));
                Assert.IsFalse(plain.TierBonusActive(0));
                Assert.IsTrue(tiered.TierBonusActive(0));

                // 같은 전투·같은 실행 ID: 슬롯마다 체력 1.5배(반올림), 속도 1.15배
                int hp0 = plain.EnemyMaxHpOf(0), hp2 = tiered.EnemyMaxHpOf(0);
                Assert.AreEqual(UnityEngine.Mathf.RoundToInt(hp0 * 1.5f), hp2);
                Assert.AreEqual(plain.EnemySpeedOf(0) * 1.15f, tiered.EnemySpeedOf(0), 1e-4f);

                // 빛줄기: 보너스 끄면 홀림 수준, 다시 켜면 원래대로
                tiered.SetTierBonusActive(0, false);
                Assert.IsFalse(tiered.TierBonusActive(0));
                Assert.AreEqual(hp0, tiered.EnemyMaxHpOf(0));
                Assert.AreEqual(plain.EnemySpeedOf(0), tiered.EnemySpeedOf(0), 1e-4f);
                tiered.SetTierBonusActive(0, true);
                Assert.AreEqual(hp2, tiered.EnemyMaxHpOf(0));
                Assert.AreEqual(plain.EnemySpeedOf(0) * 1.15f, tiered.EnemySpeedOf(0), 1e-4f);
            }
        }
    }
}
