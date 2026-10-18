using System.Collections.Generic;
using System.Linq;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Upgrades;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 강화·용병이 실제 정의 데이터로 전투 계산에 들어가는지 (K-058, D-079)
    public sealed class BattleUpgradeTests
    {
        static DefinitionLookup LoadAll()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            return DefinitionLookup.Build(catalogs);
        }

        [Test]
        public void Definitions_HaveShopTrainingMercenaries()
        {
            DefinitionLookup lookup = LoadAll();
            Assert.AreEqual(11, lookup.All<ItemDef>().Count, "상점 품목 11개 (Upgrade_Shop.md 3·4절)");
            Assert.AreEqual(9, lookup.All<TrainingDef>().Count, "훈련 3병종 × 3단계");
            Assert.AreEqual(2, lookup.All<MercenaryDef>().Count, "궁수·총병 용병");
            Assert.AreEqual(3, UpgradeRules.MaxTrainingLevel(lookup, "unit_archer"));
        }

        [Test]
        public void NoUpgrades_WeaponsMatchDefinitions()
        {
            DefinitionLookup lookup = LoadAll();
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 5);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_upgrade_none", out string error);
            Assert.IsNull(error);
            WeaponDef bow = lookup.Get<WeaponDef>(lookup.Get<UnitDef>("unit_archer").DefaultWeaponId);
            using (var sim = new BattleSim(setup, lookup))
            {
                WeaponKindData w = sim.WeaponOfSoldier(sim.FirstSoldierOf("unit_archer", false));
                Assert.AreEqual(bow.Damage, w.Damage);
                Assert.AreEqual(bow.Interval, w.Interval, 1e-4f);
                Assert.AreEqual(0f, w.BurnChance);
                Assert.AreEqual(BattleSim.TelescopeRadius, sim.TelescopeRange, 1e-4f);
            }
        }

        [Test]
        public void Upgrades_ChangeWeaponsSpellsTelescope_MercenariesOutsideGarrison()
        {
            DefinitionLookup lookup = LoadAll();
            CampaignState c = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 5);
            c.Gold = 100000;
            for (int i = 0; i < 3; i++) Assert.IsTrue(UpgradeRules.TryTrain(c, lookup, "unit_archer", out string reason), reason);
            foreach (string item in new[] { "item_telescope_lens", "item_gem_staff", "item_fire_arrows" })
                Assert.IsTrue(UpgradeRules.TryBuy(c, lookup, item, null, out string reason), item + ": " + reason);
            Assert.IsTrue(UpgradeRules.TryHire(c, lookup, "merc_archer", WallIds.North, 20, out string hire), hire);

            BattleSetup setup = BattleSetupFactory.Create(c, lookup, "run_upgrade_all", out string error);
            Assert.IsNull(error);
            Assert.AreEqual(1.4f, setup.Boosts.TelescopeRadiusMul, 1e-4f);
            CollectionAssert.Contains(setup.ConsumedItemIds, "item_fire_arrows");
            Assert.AreEqual(1, setup.Mercenaries.Count);
            Assert.AreEqual(20, setup.Mercenaries[0].Count);

            WeaponDef bow = lookup.Get<WeaponDef>(lookup.Get<UnitDef>("unit_archer").DefaultWeaponId);
            using (var sim = new BattleSim(setup, lookup))
            {
                WeaponKindData w = sim.WeaponOfSoldier(sim.FirstSoldierOf("unit_archer", false));
                Assert.AreEqual(Mathf.Max(1, Mathf.RoundToInt(bow.Damage * 1.25f)), w.Damage, "3단계 피해 +25%");
                Assert.AreEqual(bow.Interval * 0.9f, w.Interval, 1e-4f, "2단계 간격 −10%");
                Assert.AreEqual(Mathf.Clamp01(bow.AccuracyFar + 0.1f), w.AccuracyFar, 1e-4f, "1단계 먼 거리 명중");
                Assert.AreEqual(0.2f, w.BurnChance, 1e-4f, "불화살");

                Assert.AreEqual(BattleSim.TelescopeRadius * 1.4f, sim.TelescopeRange, 1e-3f, "망원경 렌즈");
                SpellDef explosion = lookup.All<SpellDef>().First(s => s.Duration <= 0f);
                Assert.AreEqual(explosion.Radius + 2f, sim.GetSpell(0).Radius, 1e-4f, "보석 지팡이 반경");

                // 용병: 북쪽에 서지만 성벽 병력(70% 패배 계산)에는 없다
                Assert.AreEqual(20, sim.MercenaryAlive(0));
                int northRegular = setup.Squads.Where(s => lookup.Get<PostDef>(s.PostId)?.Wall == WallSide.North).Sum(s => s.Count);
                Assert.AreEqual(northRegular, sim.Sides[0].GarrisonStart, "용병은 성벽 병력에 안 든다");
                Assert.GreaterOrEqual(sim.FirstSoldierOf("unit_archer", true), 0, "용병 병사");

                BattleResult result = sim.BuildResult();
                Assert.IsFalse(result.SquadLosses.Any(l => l.SquadId == setup.Mercenaries[0].SquadId), "용병 손실은 분대 손실에 섞이지 않는다");
            }
        }
    }
}
