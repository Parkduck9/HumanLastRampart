using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Upgrades;
using NUnit.Framework;

namespace HumanLastRampart.Core.Tests
{
    // 강화·상점·용병 규칙 (K-058, D-079): 구매·장착·훈련 효과 수치·용병 급료·정예 단계
    public sealed class UpgradeRulesTests
    {
        static FakeDefinitions Defs()
        {
            var defs = new FakeDefinitions()
                .Add(TestDefs.Make<UnitDef>("unit_hero", ("kind", UnitKind.Hero)))
                .Add(TestDefs.Make<UnitDef>("unit_wizard", ("kind", UnitKind.Wizard)))
                .Add(TestDefs.Make<UnitDef>("unit_archer", ("kind", UnitKind.Archer)))
                .Add(Item("item_lens", ItemSlot.HeroTool, 400, false, false, 1, new UpgradeEffect(UpgradeStat.TelescopeRadius, 0.4f)))
                .Add(Item("item_horn", ItemSlot.HeroTool, 300, false, false, 1, new UpgradeEffect(UpgradeStat.MessengerSpeed, 0.3f)))
                .Add(Item("item_staff", ItemSlot.WizardStaff, 600, false, false, 1, new UpgradeEffect(UpgradeStat.ExplosionDamage, 0.3333f), new UpgradeEffect(UpgradeStat.ExplosionRadius, 2f)))
                .Add(Item("item_arrows", ItemSlot.Group, 200, true, false, 5, new UpgradeEffect(UpgradeStat.UnitBurnChance, 0.2f, "unit_archer")))
                .Add(Item("item_tent", ItemSlot.Group, 800, false, false, 1, new UpgradeEffect(UpgradeStat.RecoverLost, 0.1f)))
                .Add(Item("item_rail", ItemSlot.Group, 500, false, true, 1, new UpgradeEffect(UpgradeStat.WallGuard, -0.3f)))
                .Add(Training(1, 300, new UpgradeEffect(UpgradeStat.UnitAccuracyFar, 0.1f, "unit_archer")))
                .Add(Training(2, 900, new UpgradeEffect(UpgradeStat.UnitInterval, -0.1f, "unit_archer")))
                .Add(Training(3, 2500, new UpgradeEffect(UpgradeStat.UnitDamage, 0.25f, "unit_archer")))
                .Add(TestDefs.Make<MercenaryDef>("merc_archer", ("unitDefId", "unit_archer"), ("contractCost", 15), ("wage", 3), ("perWallCap", 50), ("eliteLevel", 2)));
            return defs;
        }

        static ItemDef Item(string id, ItemSlot slot, int price, bool consumable, bool perWall, int maxOwned, params UpgradeEffect[] effects) =>
            TestDefs.Make<ItemDef>(id, ("slot", slot), ("price", price), ("consumable", consumable), ("perWall", perWall), ("maxOwned", maxOwned),
                ("effects", new List<UpgradeEffect>(effects)));

        static TrainingDef Training(int level, int price, UpgradeEffect effect) =>
            TestDefs.Make<TrainingDef>("training_archer_" + level, ("unitDefId", "unit_archer"), ("level", level), ("price", price), ("effects", new List<UpgradeEffect> { effect }));

        static CampaignState Campaign(int gold)
        {
            var c = new CampaignState { Gold = gold };
            c.Heroes.Add(new HeroState { InstanceId = "hero_1", UnitDefId = "unit_hero", Hp = 100 });
            c.Heroes.Add(new HeroState { InstanceId = "hero_2", UnitDefId = "unit_wizard", Hp = 60 });
            c.Squads.Add(new SquadState { SquadId = "squad_3", UnitDefId = "unit_archer", Count = 20, PostId = "post_north_squad_1" });
            return c;
        }

        [Test]
        public void HeroItem_BuyEquipsIntoEmptySlot_SwapMovesOldToBag()
        {
            FakeDefinitions defs = Defs();
            CampaignState c = Campaign(1000);
            Assert.IsTrue(UpgradeRules.TryBuy(c, defs, "item_lens", null, out string reason), reason);
            Assert.AreEqual(600, c.Gold, "값만큼 골드");
            Assert.AreEqual("hero_1", c.Items.Find(i => i.ItemId == "item_lens").EquippedBy, "빈 소지품 칸에 바로 끼움");
            Assert.IsFalse(UpgradeRules.TryBuy(c, defs, "item_lens", null, out _), "같은 영웅 아이템은 하나만");

            Assert.IsTrue(UpgradeRules.TryBuy(c, defs, "item_horn", null, out reason), reason);
            Assert.IsNull(c.Items.Find(i => i.ItemId == "item_horn").EquippedBy, "칸이 차 있으면 가방으로");
            Assert.IsTrue(UpgradeRules.TryEquip(c, defs, "item_horn", out reason), reason);
            Assert.AreEqual("hero_1", c.Items.Find(i => i.ItemId == "item_horn").EquippedBy);
            Assert.IsNull(c.Items.Find(i => i.ItemId == "item_lens").EquippedBy, "같은 칸에 있던 것은 가방으로");

            BattleBoosts b = UpgradeRules.ComputeBoosts(c, defs, new List<string>());
            Assert.AreEqual(1.3f, b.MessengerSpeedMul, 1e-4f, "끼운 나팔만 효과");
            Assert.AreEqual(1f, b.TelescopeRadiusMul, 1e-4f, "가방 속 렌즈는 효과 없음");
        }

        [Test]
        public void Buy_FailsWithoutGold_AndDoesNotChangeCampaign()
        {
            CampaignState c = Campaign(100);
            Assert.IsFalse(UpgradeRules.TryBuy(c, Defs(), "item_staff", null, out string reason));
            StringAssert.Contains("모자랍니다", reason);
            Assert.AreEqual(100, c.Gold);
            Assert.AreEqual(0, c.Items.Count);
        }

        [Test]
        public void Consumable_StacksAndIsListedForBattle_UnitBurnChance()
        {
            FakeDefinitions defs = Defs();
            CampaignState c = Campaign(5000);
            Assert.IsTrue(UpgradeRules.TryBuy(c, defs, "item_arrows", null, out _));
            Assert.IsTrue(UpgradeRules.TryBuy(c, defs, "item_arrows", null, out _));
            Assert.AreEqual(2, UpgradeRules.OwnedCount(c, "item_arrows"), "소모품은 쌓인다 (한 줄)");
            Assert.AreEqual(1, c.Items.FindAll(i => i.ItemId == "item_arrows").Count);
            var consumed = new List<string>();
            BattleBoosts b = UpgradeRules.ComputeBoosts(c, defs, consumed);
            CollectionAssert.AreEqual(new[] { "item_arrows" }, consumed, "이번 전투에 한 개 쓴다");
            Assert.AreEqual(0.2f, b.UnitFor("unit_archer", false).BurnChance, 1e-4f);
        }

        [Test]
        public void PerWallItem_OnePerWall_GuardsThatWallOnly()
        {
            FakeDefinitions defs = Defs();
            CampaignState c = Campaign(5000);
            Assert.IsFalse(UpgradeRules.TryBuy(c, defs, "item_rail", null, out _), "성벽을 골라야 한다");
            Assert.IsTrue(UpgradeRules.TryBuy(c, defs, "item_rail", WallIds.North, out string reason), reason);
            Assert.IsFalse(UpgradeRules.TryBuy(c, defs, "item_rail", WallIds.North, out _), "그 성벽에는 하나");
            Assert.IsTrue(UpgradeRules.TryBuy(c, defs, "item_rail", WallIds.East, out reason), reason);
            BattleBoosts b = UpgradeRules.ComputeBoosts(c, defs, null);
            Assert.AreEqual(0.7f, b.WallGuardMul(WallIds.North), 1e-4f);
            Assert.AreEqual(1f, b.WallGuardMul(WallIds.South), 1e-4f);
        }

        [Test]
        public void Training_StepsInOrder_EffectsStack()
        {
            FakeDefinitions defs = Defs();
            CampaignState c = Campaign(5000);
            Assert.IsTrue(UpgradeRules.TryTrain(c, defs, "unit_archer", out string reason), reason);
            Assert.IsTrue(UpgradeRules.TryTrain(c, defs, "unit_archer", out reason), reason);
            Assert.AreEqual(2, UpgradeRules.TrainingLevel(c, "unit_archer"));
            Assert.AreEqual(5000 - 300 - 900, c.Gold);
            UnitBoost archer = UpgradeRules.ComputeBoosts(c, defs, null).UnitFor("unit_archer", false);
            Assert.AreEqual(0.1f, archer.AccuracyFarAdd, 1e-4f, "1단계 효과");
            Assert.AreEqual(0.9f, archer.IntervalMul, 1e-4f, "2단계 효과");
            Assert.AreEqual(1f, archer.DamageMul, 1e-4f, "3단계는 아직");
            Assert.IsTrue(UpgradeRules.TryTrain(c, defs, "unit_archer", out reason), reason);
            Assert.IsFalse(UpgradeRules.TryTrain(c, defs, "unit_archer", out _), "최고 단계 다음은 없음");
        }

        [Test]
        public void Mercenary_CapCostWages_LeaveWhenUnpaid_EliteLevel()
        {
            FakeDefinitions defs = Defs();
            CampaignState c = Campaign(1000);
            Assert.IsTrue(UpgradeRules.TryHire(c, defs, "merc_archer", WallIds.North, 40, out string reason), reason);
            Assert.AreEqual(1000 - 40 * 15, c.Gold, "계약금");
            Assert.IsFalse(UpgradeRules.TryHire(c, defs, "merc_archer", WallIds.North, 20, out _), "성벽마다 50명까지");
            Assert.IsTrue(UpgradeRules.TryHire(c, defs, "merc_archer", WallIds.North, 10, out reason), reason);
            Assert.AreEqual(1, c.Mercenaries.Count, "같은 성벽·같은 용병은 한 계약으로");
            Assert.AreEqual(150, UpgradeRules.WagesDue(c, defs));

            UnitBoost merc = UpgradeRules.ComputeBoosts(c, defs, null).UnitFor("unit_archer", true);
            Assert.AreEqual(2, merc.Level, "정예 = 2단계");
            Assert.AreEqual(0.9f, merc.IntervalMul, 1e-4f, "2단계 효과까지");
            Assert.AreEqual(0, UpgradeRules.ComputeBoosts(c, defs, null).UnitFor("unit_archer", false).Level, "일반 궁수는 훈련 안 함");

            c.Gold = 200;   // 급료 150
            Assert.AreEqual(0, UpgradeRules.PayWages(c, defs).Count);
            Assert.AreEqual(50, c.Gold);
            Assert.AreEqual(1, c.Mercenaries.Count);
            Assert.AreEqual(1, UpgradeRules.PayWages(c, defs).Count, "모자라면 떠난다");
            Assert.AreEqual(50, c.Gold, "떠난 계약은 급료를 내지 않는다");
            Assert.AreEqual(0, c.Mercenaries.Count);
        }

        [Test]
        public void HeroItems_OnFallenHero_HaveNoEffect()
        {
            FakeDefinitions defs = Defs();
            CampaignState c = Campaign(1000);
            Assert.IsTrue(UpgradeRules.TryBuy(c, defs, "item_staff", null, out string reason), reason);
            Assert.AreEqual(1.3333f, UpgradeRules.ComputeBoosts(c, defs, null).ExplosionDamageMul, 1e-3f);
            Assert.AreEqual(2f, UpgradeRules.ComputeBoosts(c, defs, null).ExplosionRadiusAdd, 1e-4f);
            c.Heroes[1].IsAlive = false;
            Assert.AreEqual(1f, UpgradeRules.ComputeBoosts(c, defs, null).ExplosionDamageMul, 1e-4f, "쓰러진 마법사의 지팡이");
        }
    }
}
