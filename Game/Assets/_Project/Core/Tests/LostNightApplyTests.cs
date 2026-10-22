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
