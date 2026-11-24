using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;

namespace HumanLastRampart.App.Tests
{
    // 이어하기·저장 지우기 순서표 (K-062)
    public sealed class SaveFlowTests
    {
        [Test]
        public void Continue_OpensPrep_WithLoad()
        {
            Assert.IsTrue(FlowRouter.IsAllowed(ScreenId.MainMenu, ScreenOutcome.ContinueGame, false), "메인은 이어하기를 보낼 수 있다");
            Assert.IsFalse(FlowRouter.IsAllowed(ScreenId.MainMenu, ScreenOutcome.ContinueGame, true), "새 게임 길로는 안 됨");
            Assert.IsFalse(FlowRouter.IsAllowed(ScreenId.Prep, ScreenOutcome.ContinueGame, false), "다른 화면은 안 됨");
            FlowStep step = FlowRouter.Next(ScreenId.MainMenu, ScreenOutcome.ContinueGame, new FlowState());
            Assert.AreEqual(FlowAction.OpenScreen, step.Action);
            Assert.AreEqual(ScreenId.Prep, step.Screen);
            Assert.IsTrue(step.LoadSave);
            Assert.IsFalse(step.CreateCampaign);
        }

        [Test]
        public void EndOfRun_DeletesSave_BackToMenu_KeepsSave()
        {
            FlowStep defeat = FlowRouter.Next(ScreenId.Story, ScreenOutcome.Done, new FlowState { StoryIsDefeatEnding = true });
            Assert.IsTrue(defeat.DeleteSave, "패배 엔딩 뒤 저장 지움");
            FlowStep last = FlowRouter.Next(ScreenId.Prep, ScreenOutcome.Done, new FlowState { HasNextBattle = false });
            Assert.IsTrue(last.DeleteSave, "마지막 전투 뒤 저장 지움");
            FlowStep back = FlowRouter.Next(ScreenId.Prep, ScreenOutcome.BackToMainMenu, new FlowState());
            Assert.IsFalse(back.DeleteSave, "도중에 메인으로 가면 저장은 남긴다 (이어하기)");
            FlowStep win = FlowRouter.Next(ScreenId.Battle, ScreenOutcome.Done, new FlowState { LastBattleOutcome = BattleOutcome.Victory });
            Assert.IsFalse(win.DeleteSave);
        }
    }
}
