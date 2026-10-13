using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;

namespace HumanLastRampart.App.Flow
{
    public enum FlowAction
    {
        OpenScreen,
        Quit,
        Stay,   // 받아들이지 않음 (화면에 남음)
    }

    // 다음 화면 결정 결과
    public struct FlowStep
    {
        public FlowAction Action;
        public ScreenId Screen;
        public bool CreateCampaign;     // 새 게임 만들기 (MainMenu 시작)
        public bool DiscardCampaign;    // 진행 데이터 버리기 (메인 복귀)
        public bool PrepareBattle;      // 전투 준비 정보 만들기
        public bool AdvanceBattle;      // 승리: 전투 번호 +1 (C-006)
        public bool QueueDefeatEnding;  // 패배: 패배 엔딩 스토리 요청 (D-014)

        public static FlowStep Open(ScreenId screen) => new FlowStep { Action = FlowAction.OpenScreen, Screen = screen };
        public static FlowStep ToMainMenu() => new FlowStep { Action = FlowAction.OpenScreen, Screen = ScreenId.MainMenu, DiscardCampaign = true };
        public static FlowStep Stay() => new FlowStep { Action = FlowAction.Stay };
    }

    // 지금 상황 요약 (진행 데이터에서 App이 채운다)
    public struct FlowState
    {
        public bool StoryIsDefeatEnding;
        public BattleOutcome? LastBattleOutcome;   // 검증을 통과한 결과만
        public bool HasNextBattle;                  // 다음 전투 번호에 웨이브가 있는가
    }

    // 다음 화면 순서표 — App 한 곳에서만 정한다 (C-003 4, D-014, D-017 E3, D-022 I5)
    // 상태가 없는 계산이라 자동 시험으로 확인한다.
    public static class FlowRouter
    {
        public static FlowStep Next(ScreenId current, ScreenOutcome outcome, FlowState state)
        {
            if (outcome == ScreenOutcome.BackToMainMenu)
            {
                return FlowStep.ToMainMenu();
            }

            switch (current)
            {
                case ScreenId.MainMenu:
                    if (outcome == ScreenOutcome.StartNewGame)
                    {
                        FlowStep step = FlowStep.Open(ScreenId.Story);
                        step.CreateCampaign = true;
                        return step;
                    }

                    return outcome == ScreenOutcome.QuitGame ? new FlowStep { Action = FlowAction.Quit } : FlowStep.Stay();

                case ScreenId.Story:
                    if (outcome != ScreenOutcome.Done) return FlowStep.Stay();
                    if (state.StoryIsDefeatEnding) return FlowStep.ToMainMenu();   // 패배 엔딩 → 처음부터
                    return PrepareBattle();

                case ScreenId.Battle:
                    if (outcome != ScreenOutcome.Done || state.LastBattleOutcome == null) return FlowStep.Stay();
                    if (state.LastBattleOutcome == BattleOutcome.Victory)
                    {
                        FlowStep win = FlowStep.Open(ScreenId.Result);
                        win.AdvanceBattle = true;
                        return win;
                    }

                    FlowStep lose = FlowStep.Open(ScreenId.Story);   // 결과 화면 건너뜀
                    lose.QueueDefeatEnding = true;
                    return lose;

                case ScreenId.Result:
                    return outcome == ScreenOutcome.Done ? FlowStep.Open(ScreenId.Prep) : FlowStep.Stay();

                case ScreenId.Prep:
                    if (outcome != ScreenOutcome.Done) return FlowStep.Stay();
                    return state.HasNextBattle ? PrepareBattle() : FlowStep.ToMainMenu();   // 4번째 뒤 메인

                default:
                    return FlowStep.Stay();
            }
        }

        // 화면별로 받을 수 있는 신호 (C-003 3-1, v1.2)
        public static bool IsAllowed(ScreenId screen, ScreenOutcome outcome, bool viaNewGame)
        {
            if (screen == ScreenId.MainMenu)
            {
                return viaNewGame ? outcome == ScreenOutcome.StartNewGame : outcome == ScreenOutcome.QuitGame;
            }

            return !viaNewGame && (outcome == ScreenOutcome.Done || outcome == ScreenOutcome.BackToMainMenu);
        }

        static FlowStep PrepareBattle()
        {
            FlowStep step = FlowStep.Open(ScreenId.Battle);
            step.PrepareBattle = true;
            return step;
        }
    }
}