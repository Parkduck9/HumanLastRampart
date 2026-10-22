using System.Linq;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Meta;
using NUnit.Framework;

namespace HumanLastRampart.Core.Tests
{
    // 진 밤의 결과 반영 (W11, 사용자 규칙): 병력 손실·성벽 피해는 남고, 보상 골드는 없다. 승리 경로는 그대로 승리 결과만 받는다
    public sealed class LostNightApplyTests
    {
        static CampaignState Prepared(BattleOutcome outcome, out BattleResult result)
        {
            var c = new CampaignState { BattleNumber = 3, Gold = 500, RngSeed = 1 };
            foreach (string id in WallIds.All) c.Wall.Add(new WallSegmentState { SegmentId = id, MaxHp = 1000, Hp = 1000 });
            c.Squads.Add(new SquadState { SquadId = "squad_1", UnitDefId = "unit_archer", Count = 100, PostId = "p1" });
            var setup = new BattleSetup { BattleRunId = "run_x", BattleNumber = 3 };
            foreach (var w in c.Wall) setup.Wall.Add(w.Clone());
            foreach (var s in c.Squads) setup.Squads.Add(s.Clone());
            result = new BattleResult
            {
                BattleRunId = "run_x", BattleNumber = 3, Outcome = outcome, EarnedGold = 700,
                DefeatReason = outcome == BattleOutcome.Defeat ? DefeatReason.WallDestroyed : DefeatReason.None,
                DefeatWallId = outcome == BattleOutcome.Defeat ? WallIds.North : null,
            };
            result.SquadLosses.Add(new SquadLoss { SquadId = "squad_1", LostCount = 30 });
            foreach (var w in c.Wall) result.WallOutcomes.Add(new WallOutcome { SegmentId = w.SegmentId, HpAfter = w.SegmentId == WallIds.North ? 0 : 400 });
            c.ActiveBattleSetup = setup;
            c.LastBattle = result;
            return c;
        }

        [Test]
        public void LostNight_KeepsLossesAndWallDamage_NoGold()
        {
            CampaignState c = Prepared(BattleOutcome.Defeat, out _);
            Assert.IsTrue(BattleResultApplier.TryApply(c, out string error, null, true), error);
            Assert.AreEqual(70, c.Squads.Single().Count);
            Assert.AreEqual(0, c.Wall.First(w => w.SegmentId == WallIds.North).Hp);
            Assert.AreEqual(400, c.Wall.First(w => w.SegmentId == WallIds.East).Hp);
            Assert.AreEqual(500, c.Gold, "진 밤은 보상 골드가 없다");
            Assert.AreEqual(3, c.BattleNumber, "번호는 그대로");
            Assert.IsTrue(BattleResultApplier.TryApply(c, out _, null, true), "같은 결과를 다시 반영해도 무변경");
            Assert.AreEqual(70, c.Squads.Single().Count);
        }

        [Test]
        public void WallFloor_RaisesOnlyLowWalls_ToPercentOfMax()
        {
            CampaignState c = Prepared(BattleOutcome.Defeat, out _);
            Assert.IsTrue(BattleResultApplier.TryApply(c, out string error, null, true), error);   // 북 0, 나머지 400/1000
            Assert.AreEqual(4, BeaconRules.RaiseWallFloor(c, 50), "50% 미만인 네 성벽 모두 500으로");
            Assert.IsTrue(c.Wall.All(w => w.Hp == 500));
            Assert.AreEqual(0, BeaconRules.RaiseWallFloor(c, 30), "이미 그 이상이면 그대로");
            c.Wall[0].Hp = 0;
            Assert.AreEqual(1, BeaconRules.RaiseWallFloor(c, 30));
            Assert.AreEqual(300, c.Wall[0].Hp, "최대 체력의 30%");
            Assert.AreEqual(0, BeaconRules.RaiseWallFloor(c, 0), "0이면 끔");
        }

        // 승리한 점화의 밤: 잃은 병사 + 수비대로 남긴 인원이 모두 빠져야 한다 (PlayProbe EasyFullRun 병사 수 확인의 회귀 시험)
        [Test]
        public void IgnitionNightWin_SoldierDropEqualsLossesPlusGarrison()
        {
            var rng = new System.Random(7);
            for (int trial = 0; trial < 200; trial++)
            {
                CampaignState c = Prepared(BattleOutcome.Victory, out BattleResult result);
                c.Squads.Clear();
                c.ActiveBattleSetup.Squads.Clear();
                int squadCount = rng.Next(1, 8);
                for (int i = 0; i < squadCount; i++)
                {
                    var s = new SquadState { SquadId = "squad_" + i, UnitDefId = "unit_archer", Count = rng.Next(1, 200), PostId = "p" + i };
                    c.Squads.Add(s);
                    c.ActiveBattleSetup.Squads.Add(s.Clone());
                }

                result.SquadLosses.Clear();
                foreach (SquadState s in c.Squads)
                    if (rng.NextDouble() < 0.6) result.SquadLosses.Add(new SquadLoss { SquadId = s.SquadId, LostCount = rng.Next(0, s.Count + 1) });
                c.ActiveBattleSetup.IsIgnitionNight = true;
                c.FortressNumber = 1;
                c.NightNumber = 2;
                c.BattleNumber = 4;   // 결과 번호 3 + 1
                int before = c.Squads.Sum(s => s.Count);
                int lost = result.SquadLosses.Sum(l => l.LostCount);
                Assert.IsTrue(BattleResultApplier.TryApply(c, out string error), error);
                BeaconRules.ApplyWin(c, c.ActiveBattleSetup, result);
                Assert.IsTrue(c.LastNightRelit);
                Assert.AreEqual(before - lost - c.LastNightGarrison, c.Squads.Sum(s => s.Count), $"시도 {trial}: 잃은 {lost} + 수비대 {c.LastNightGarrison}");
                Assert.AreEqual(c.LastNightGarrison, BeaconRules.GarrisonSoldiers(c), "수비대 목록과 같은 수");
                Assert.IsTrue(c.Squads.All(s => s.Count > 0), "0명 분대 금지");
            }
        }

        // 군의관 천막이 있어도 점화의 밤 병사 수가 맞아야 한다 (잃은 병사 일부가 돌아온 뒤 수비대 20%)
        [Test]
        public void IgnitionNightWin_WithSurgeonTent_CountsRecoveredSoldiers()
        {
            CampaignState c = Prepared(BattleOutcome.Victory, out BattleResult result);
            c.ActiveBattleSetup.Boosts.RecoverLostRatio = 0.5f;
            c.ActiveBattleSetup.IsIgnitionNight = true;
            c.FortressNumber = 1;
            c.NightNumber = 2;
            c.BattleNumber = 4;
            int before = c.Squads.Sum(s => s.Count);   // 100명, 30명 잃음 → 15명 돌아옴
            Assert.IsTrue(BattleResultApplier.TryApply(c, out string error), error);
            Assert.AreEqual(before - 30 + 15, c.Squads.Sum(s => s.Count));
            BeaconRules.ApplyWin(c, c.ActiveBattleSetup, result);
            Assert.AreEqual(before - 30 + 15 - c.LastNightGarrison, c.Squads.Sum(s => s.Count));
            Assert.Greater(c.LastNightGarrison, 0);
        }

        [Test]
        public void BeaconRepair_LimitedPerDay_ResetsWhenDayBegins()        {
            CampaignState c = Prepared(BattleOutcome.Defeat, out _);
            c.Gold = 1000;
            Assert.IsTrue(BeaconRules.TryBuyRepair(c, out _, 1));
            Assert.IsFalse(BeaconRules.TryBuyRepair(c, out string why, 1), "하루 한 번");
            StringAssert.Contains("오늘", why);
            Assert.AreEqual(800, c.Gold);
            Assert.IsTrue(BeaconRules.TryBuyRepair(c, out _, 0), "0이면 제한 없음");
            BeaconRules.BeginDay(c);
            Assert.AreEqual(0, c.BeaconRepairBoughtToday);
            Assert.IsTrue(BeaconRules.TryBuyRepair(c, out _, 1), "새 낮에는 다시");
        }

        [Test]
        public void WallReinforce_RaisesMaxHpAndPriceGrows_UpToMax()
        {
            var rules = UnityEngine.ScriptableObject.CreateInstance<HumanLastRampart.Core.Definitions.BattleRulesDef>();
            try
            {
                CampaignState c = Prepared(BattleOutcome.Defeat, out _);
                c.Gold = 100000;
                int first = rules.WallReinforcePrice(0), second = rules.WallReinforcePrice(1);
                Assert.Greater(second, first, "살 때마다 값이 오른다");
                WallSegmentState north = c.FindWall(WallIds.North);
                Assert.IsTrue(BeaconRules.TryReinforceWall(c, WallIds.North, rules, out string why), why);
                Assert.AreEqual(1000 + rules.WallReinforceHp, north.MaxHp);
                Assert.AreEqual(1000 + rules.WallReinforceHp, north.Hp, "늘어난 만큼 체력도 찬다");
                Assert.AreEqual(100000 - first, c.Gold);
                Assert.AreEqual(1000, c.FindWall(WallIds.East).MaxHp, "다른 성벽은 그대로");
                while (BeaconRules.TryReinforceWall(c, WallIds.North, rules, out _)) { }
                Assert.AreEqual(rules.WallReinforceMaxPerWall, north.Reinforced);
                c.Gold = 0;
                Assert.IsFalse(BeaconRules.TryReinforceWall(c, WallIds.East, rules, out _), "골드 부족");
            }
            finally { UnityEngine.Object.DestroyImmediate(rules); }
        }

        [Test]
        public void LostNight_RejectsVictoryResult_AndNormalRejectsDefeat()
        {
            CampaignState win = Prepared(BattleOutcome.Victory, out _);
            Assert.IsFalse(BattleResultApplier.TryApply(win, out _, null, true));
            CampaignState loss = Prepared(BattleOutcome.Defeat, out _);
            loss.BattleNumber = 4;
            Assert.IsFalse(BattleResultApplier.TryApply(loss, out _));
        }
    }
}
