using System.Collections.Generic;
using System.Linq;
using HumanLastRampart.App.Data;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HumanLastRampart.App.Tests
{
    public sealed class FlowRouterTests
    {
        [Test]
        public void MainMenuStart_CreatesCampaignAndOpensStory()
        {
            FlowStep step = FlowRouter.Next(ScreenId.MainMenu, ScreenOutcome.StartNewGame, default);
            Assert.AreEqual(ScreenId.Story, step.Screen);
            Assert.IsTrue(step.CreateCampaign);
        }

        [Test]
        public void StoryDone_PreparesBattle_ButDefeatEndingGoesToMain()   // D-014
        {
            FlowStep normal = FlowRouter.Next(ScreenId.Story, ScreenOutcome.Done, default);
            Assert.AreEqual(ScreenId.Battle, normal.Screen);
            Assert.IsTrue(normal.PrepareBattle);

            FlowStep ending = FlowRouter.Next(ScreenId.Story, ScreenOutcome.Done, new FlowState { StoryIsDefeatEnding = true });
            Assert.AreEqual(ScreenId.MainMenu, ending.Screen);
            Assert.IsTrue(ending.DiscardCampaign);
        }

        [Test]
        public void BattleVictory_GoesToResult_DefeatSkipsResult()   // D-014 C4
        {
            FlowStep win = FlowRouter.Next(ScreenId.Battle, ScreenOutcome.Done, new FlowState { LastBattleOutcome = BattleOutcome.Victory });
            Assert.AreEqual(ScreenId.Result, win.Screen);
            Assert.IsTrue(win.AdvanceBattle);

            FlowStep lose = FlowRouter.Next(ScreenId.Battle, ScreenOutcome.Done, new FlowState { LastBattleOutcome = BattleOutcome.Defeat });
            Assert.AreEqual(ScreenId.Story, lose.Screen);
            Assert.IsTrue(lose.QueueDefeatEnding);
            Assert.IsFalse(lose.AdvanceBattle);
        }

        [Test]
        public void PrepDone_NextBattleOrMainAfterLast()   // D-022 I5
        {
            Assert.AreEqual(ScreenId.Battle, FlowRouter.Next(ScreenId.Prep, ScreenOutcome.Done, new FlowState { HasNextBattle = true }).Screen);
            FlowStep last = FlowRouter.Next(ScreenId.Prep, ScreenOutcome.Done, new FlowState { HasNextBattle = false });
            Assert.AreEqual(ScreenId.MainMenu, last.Screen);
            Assert.IsTrue(last.DiscardCampaign);
        }

        [Test]
        public void AllowedOutcomes_FollowC003()   // C-003 3-1, v1.2
        {
            Assert.IsTrue(FlowRouter.IsAllowed(ScreenId.MainMenu, ScreenOutcome.StartNewGame, true));
            Assert.IsFalse(FlowRouter.IsAllowed(ScreenId.MainMenu, ScreenOutcome.StartNewGame, false), "난이도 없이 시작 금지");
            Assert.IsTrue(FlowRouter.IsAllowed(ScreenId.MainMenu, ScreenOutcome.QuitGame, false));
            Assert.IsFalse(FlowRouter.IsAllowed(ScreenId.MainMenu, ScreenOutcome.Done, false));
            Assert.IsTrue(FlowRouter.IsAllowed(ScreenId.Result, ScreenOutcome.Done, false));
            Assert.IsFalse(FlowRouter.IsAllowed(ScreenId.Result, ScreenOutcome.QuitGame, false));
        }
    }

    public sealed class AppDataTests
    {
        [Test]
        public void ValidSet_PassesValidation()
        {
            DefinitionLookup lookup = DefinitionLookup.Build(new[] { AppTestData.Catalog(AppTestData.ValidSet()) });
            Assert.IsTrue(lookup.IsValid, string.Join("\n", lookup.Errors));
        }

        [Test]
        public void DuplicateId_IsRejectedNotOverwritten()   // C-010 4
        {
            List<DefinitionBase> set = AppTestData.ValidSet();
            set.Add(AppTestData.Make<WeaponDef>("weapon_bow"));
            DefinitionLookup lookup = DefinitionLookup.Build(new[] { AppTestData.Catalog(set) });
            Assert.IsFalse(lookup.IsValid);
            Assert.IsTrue(lookup.Errors.Any(e => e.Contains("중복")));
        }

        [Test]
        public void PreparationEconomy_MustBeExactlyOne()   // C-017, D-048
        {
            List<DefinitionBase> none = AppTestData.ValidSet().Where(d => !(d is PreparationEconomyDef)).ToList();
            DefinitionLookup missing = DefinitionLookup.Build(new[] { AppTestData.Catalog(none) });
            Assert.IsTrue(missing.Errors.Any(e => e.Contains("PreparationEconomyDef")), "없으면 시작을 막는다");

            List<DefinitionBase> wrongId = AppTestData.ValidSet().Where(d => !(d is PreparationEconomyDef)).ToList();
            wrongId.Add(AppTestData.Make<PreparationEconomyDef>("economy_other"));
            DefinitionLookup wrong = DefinitionLookup.Build(new[] { AppTestData.Catalog(wrongId) });
            Assert.IsTrue(wrong.Errors.Any(e => e.Contains("economy_preparation_1")), "Id가 다르면 오류");
        }

        [Test]
        public void MissingReference_IsReported()
        {
            List<DefinitionBase> set = AppTestData.ValidSet();
            set.Add(AppTestData.Make<UnitDef>("unit_gunner", ("kind", UnitKind.Gunner), ("defaultWeaponId", "weapon_gun")));
            DefinitionLookup lookup = DefinitionLookup.Build(new[] { AppTestData.Catalog(set) });
            Assert.IsTrue(lookup.Errors.Any(e => e.Contains("weapon_gun")));
        }

        [Test]
        public void NewGame_HasFourWallsAndDifficulty()   // C-004 v1.3
        {
            DefinitionLookup lookup = DefinitionLookup.Build(new[] { AppTestData.Catalog(AppTestData.ValidSet()) });
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 42);
            Assert.AreEqual(4, campaign.Wall.Count);
            CollectionAssert.AreEquivalent(WallIds.All, campaign.Wall.Select(w => w.SegmentId));
            Assert.AreEqual(Difficulty.Easy, campaign.Difficulty);
            Assert.AreEqual(2, campaign.Squads.Count);
            Assert.AreNotEqual(campaign.Squads[0].SquadId, campaign.Squads[1].SquadId);
            Assert.AreEqual(1, campaign.BattleNumber);
        }

        [Test]
        public void BattleSetup_IsDeepCopy_WithDifficultyAndForecast()   // C-005
        {
            DefinitionLookup lookup = DefinitionLookup.Build(new[] { AppTestData.Catalog(AppTestData.ValidSet()) });
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 42);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_test", out string error);
            Assert.IsNull(error);
            Assert.AreEqual("wave_1", setup.WaveDefId);
            Assert.AreEqual(0.5f, setup.EnemyCountMultiplier);
            Assert.AreEqual(25000, setup.MaxConcurrentEnemies);

            setup.Squads[0].Count = 0;
            setup.Wall[0].Hp = 0;
            Assert.AreEqual(20, campaign.Squads[0].Count, "준비 정보를 바꿔도 진행 데이터는 그대로");
            Assert.AreEqual(1000, campaign.Wall[0].Hp);

            WeatherForecast again = BattleForecast.Get(campaign.RngSeed, campaign.BattleNumber, lookup.All<BattleRulesDef>()[0]);
            Assert.AreEqual(again, setup.Forecast, "스토리 힌트와 같은 예보");
        }

        [Test]
        public void ResultCheck_RejectsWrongRunAndMissingReason()   // C-006
        {
            var campaign = new CampaignState
            {
                ActiveBattleSetup = new BattleSetup { BattleRunId = "run_a", BattleNumber = 2 },
                LastBattle = new BattleResult { BattleRunId = "run_a", BattleNumber = 2, Outcome = BattleOutcome.Victory },
            };
            Assert.IsNull(BattleResultCheck.Validate(campaign));

            campaign.LastBattle.BattleRunId = "run_old";
            Assert.IsNotNull(BattleResultCheck.Validate(campaign), "이전 실행 결과 거부");

            campaign.LastBattle.BattleRunId = "run_a";
            campaign.LastBattle.Outcome = BattleOutcome.Defeat;
            Assert.IsNotNull(BattleResultCheck.Validate(campaign), "패배 사유 없음 거부");
        }

        [Test]
        public void ResultCheck_RequiresOneOutcomePerParticipatingHero()   // C-006, M-CX-022
        {
            var setup = new BattleSetup { BattleRunId = "run_a", BattleNumber = 1 };
            setup.Heroes.Add(new HeroState { InstanceId = "hero_1", UnitDefId = "unit_hero", Hp = 300 });
            setup.ParticipatingHeroIds.Add("hero_1");
            var campaign = new CampaignState { ActiveBattleSetup = setup };

            campaign.LastBattle = new BattleResult { BattleRunId = "run_a", BattleNumber = 1, Outcome = BattleOutcome.Victory };
            Assert.IsNotNull(BattleResultCheck.Validate(campaign), "영웅 결과 누락 거부");

            campaign.LastBattle.HeroOutcomes.Add(new HeroOutcome { InstanceId = "hero_1", HpAfter = 300, IsAliveAfter = true });
            Assert.IsNull(BattleResultCheck.Validate(campaign));

            campaign.LastBattle.HeroOutcomes.Add(new HeroOutcome { InstanceId = "hero_1", HpAfter = 300, IsAliveAfter = true });
            Assert.IsNotNull(BattleResultCheck.Validate(campaign), "같은 영웅 두 항목 거부");

            // 시험용 결과(개발용 임시 화면)도 이 검사를 통과해야 한다
            campaign.LastBattle = Dev.DevPlaceholderScreen.MakeTestResult(setup, BattleOutcome.Victory, DefeatReason.None);
            Assert.IsNull(BattleResultCheck.Validate(campaign));
            Assert.AreEqual(300, campaign.LastBattle.HeroOutcomes[0].HpAfter);
        }
    }
}