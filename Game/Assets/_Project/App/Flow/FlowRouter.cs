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
        public bool LoadSave;           // 이어하기: 저장한 진행을 불러온 뒤 연다 (K-062)
        public bool DeleteSave;         // 판이 끝남(패배 엔딩·마지막 전투 뒤): 저장 지움 (K-062)
        public bool ApplyNightLoss;     // 밤에 졌지만 엔딩 아님: 등대 복구 절반·연속 패배 +1 (W8)
        public bool BeginDay;           // 낮이 시작됨: 수비대 지원금 (W8). 이어하기로 들어올 때는 켜지 않는다
        public bool MapPreview;         // 봉화선 지도를 정비에서 '보기만' 연다 (W10) — 닫으면 정비로 돌아오고 낮 시작은 다시 하지 않는다

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
        public bool LastBattleWasPractice;          // 연습 전투가 끝남 (D-063, C-005 v1.5 초안)
        public bool LossCanRetry;                   // W8: 진 밤이 연속 한도에 닿지 않아 같은 요새 다음 낮으로 간다 (기본 false = 패배 엔딩)
        public bool ResultEndsRun;                  // W11: 그 진 밤이 연속 두 번째 패배다 → 결과 화면 뒤 패배 엔딩
        public bool ResultWasLostNight;             // W11: 방금 결과 화면은 진 밤의 보고였다 → 뒤에 복구 절반·지원금과 함께 같은 요새의 다음 낮
        public bool StoryIsRunComplete;            // W8: 1회차 완료 이야기 (끝나면 메인)
        public bool RunComplete;                    // W8: 결과 화면 뒤 1회차 완료 이야기로 간다
        public bool ResultStory;                    // W12: 봉화선 지도 뒤 점화 이야기로 간다 (요새 점화, 마지막 요새 제외)
        public bool StoryChainNext;                 // W12: 이 이야기 뒤에 이어 볼 이야기가 있다 (이야기 화면을 다시 연다)
        public bool StoryToDay;                     // W12: 이 이야기가 끝나면 전투 대신 낮(정비)으로 간다
        public bool ResumeStory;                    // W12: 이어하기: 저장 때 못 본 이야기(점화·1회차 완료)가 남아 있다 → 정비 대신 그 이야기부터
        public bool RelitFortress;                  // W10: 방금 밤에 요새를 점화했다 → 결과 뒤 봉화선 지도를 먼저 보여 준다
        public bool MapIsPreview;                   // W10: 봉화선 지도가 정비에서 열린 보기 전용이다
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

                    if (outcome == ScreenOutcome.ContinueGame)
                    {
                        // 이어하기 (K-062): 저장은 정비 화면에 들어올 때만 하므로 정비부터
                        FlowStep resume = FlowStep.Open(state.ResumeStory ? ScreenId.Story : ScreenId.Prep);   // W12: 못 본 이야기가 있으면 먼저 다시 본다
                        resume.LoadSave = true;
                        return resume;
                    }

                    return outcome == ScreenOutcome.QuitGame ? new FlowStep { Action = FlowAction.Quit } : FlowStep.Stay();

                case ScreenId.Story:
                    if (outcome != ScreenOutcome.Done) return FlowStep.Stay();
                    if (state.StoryIsDefeatEnding || state.StoryIsRunComplete) return EndOfRun();   // 패배 엔딩·1회차 완료 → 처음부터 (저장 지움)
                    if (state.StoryChainNext) return FlowStep.Open(ScreenId.Story);   // W12: 다음 이야기 (App이 NextStory를 바꿔 둔다)
                    if (state.StoryToDay)
                    {
                        FlowStep toDay = FlowStep.Open(ScreenId.Prep);   // W12: 새 요새 도착 이야기 뒤 첫 낮
                        toDay.BeginDay = true;
                        return toDay;
                    }

                    return PrepareBattle();

                case ScreenId.Battle:
                    // 연습: 결과 화면(결과 반영 C-007)·전투 번호 증가·패배 엔딩 없이 같은 번호의 실전으로 (D-063).
                    // 실전 준비 정보는 새로 만들어 연습 표시가 남지 않는다. 연습 뒤 갈 곳은 튜토리얼 흐름 확정 전 임시
                    if (outcome == ScreenOutcome.Done && state.LastBattleWasPractice) return PrepareBattle();
                    if (outcome != ScreenOutcome.Done || state.LastBattleOutcome == null) return FlowStep.Stay();
                    if (state.LastBattleOutcome == BattleOutcome.Victory)
                    {
                        FlowStep win = FlowStep.Open(ScreenId.Result);
                        win.AdvanceBattle = true;
                        return win;
                    }

                    // W8: 첫 패배는 등대 복구가 절반으로 줄고 같은 요새의 다음 낮으로, 연속 두 번째는 패배 엔딩.
                    // W11: 어느 쪽이든 결과 화면(4번)에서 그 밤에 잃은 병력을 먼저 보여 준다 (번호는 올리지 않는다). 갈 곳은 결과 화면 뒤 (아래 Result)
                    return FlowStep.Open(ScreenId.Result);

                case ScreenId.Result:
                    if (outcome != ScreenOutcome.Done) return FlowStep.Stay();
                    if (state.ResultWasLostNight && state.ResultEndsRun)
                    {
                        FlowStep lose = FlowStep.Open(ScreenId.Story);   // W11: 보고를 본 뒤 패배 엔딩 이야기
                        lose.QueueDefeatEnding = true;
                        return lose;
                    }

                    if (state.ResultWasLostNight)
                    {
                        FlowStep retry = FlowStep.Open(ScreenId.Prep);
                        retry.ApplyNightLoss = true;
                        retry.BeginDay = true;
                        return retry;
                    }

                    // W10: 요새를 점화한 밤 뒤(마지막 요새 포함)에는 봉화선 지도를 먼저 본다. 그 뒤는 지도에서 정한다
                    if (state.RunComplete || state.RelitFortress) return FlowStep.Open(ScreenId.BeaconMap);
                    return StartDay();

                case ScreenId.BeaconMap:
                    if (outcome != ScreenOutcome.Done) return FlowStep.Stay();
                    if (state.MapIsPreview) return FlowStep.Open(ScreenId.Prep);   // 정비에서 열어 본 것 → 정비로 (낮 지원금은 이미 받았다)
                    if (state.RunComplete || state.ResultStory) return FlowStep.Open(ScreenId.Story);   // W8: 마지막 요새 점화 → 1회차 완료 이야기 / W12: 요새 점화 이야기 (지도 다음)
                    return StartDay();                                              // 다음 요새의 첫 낮

                case ScreenId.Prep:
                    if (outcome == ScreenOutcome.OpenBeaconMap)
                    {
                        FlowStep view = FlowStep.Open(ScreenId.BeaconMap);
                        view.MapPreview = true;
                        return view;
                    }

                    if (outcome != ScreenOutcome.Done) return FlowStep.Stay();
                    return state.HasNextBattle ? PrepareBattle() : EndOfRun();   // 4번째 뒤 메인 (저장 지움)

                default:
                    return FlowStep.Stay();
            }
        }

        // 화면별로 받을 수 있는 신호 (C-003 3-1, v1.2)
        public static bool IsAllowed(ScreenId screen, ScreenOutcome outcome, bool viaNewGame)
        {
            if (screen == ScreenId.MainMenu)
            {
                return viaNewGame ? outcome == ScreenOutcome.StartNewGame : outcome == ScreenOutcome.QuitGame || outcome == ScreenOutcome.ContinueGame;
            }

            if (screen == ScreenId.Prep && outcome == ScreenOutcome.OpenBeaconMap) return !viaNewGame;   // W10: 정비에서만 지도 보기
            return !viaNewGame && (outcome == ScreenOutcome.Done || outcome == ScreenOutcome.BackToMainMenu);
        }

        // 낮이 시작되는 정비 (수비대 지원금)
        static FlowStep StartDay()
        {
            FlowStep day = FlowStep.Open(ScreenId.Prep);
            day.BeginDay = true;
            return day;
        }

        // 한 판이 끝남: 메인으로, 진행 버리고 저장도 지운다 (K-062)
        static FlowStep EndOfRun()
        {
            FlowStep step = FlowStep.ToMainMenu();
            step.DeleteSave = true;
            return step;
        }

        static FlowStep PrepareBattle()
        {
            FlowStep step = FlowStep.Open(ScreenId.Battle);
            step.PrepareBattle = true;
            return step;
        }
    }
}