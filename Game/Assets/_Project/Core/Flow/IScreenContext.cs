using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;

namespace HumanLastRampart.Core.Flow
{
    // App이 화면마다 새로 만들어 넘긴다 (C-003 v1·v1.1·v1.2).
    // 화면의 OnExit가 끝나면 무효가 된다: 그 뒤 Campaign·Definitions 접근은 InvalidOperationException,
    // Complete·CompleteNewGame은 무시되고 경고 로그가 남는다 (C-003 3-1).
    public interface IScreenContext
    {
        // 진행 데이터 (C-004). 메인 화면에서는 아직 판이 없으므로 null일 수 있다.
        CampaignState Campaign { get; }

        // 정의 데이터 찾기 (C-003 v1.1, C-010)
        IDefinitionLookup Definitions { get; }

        // "끝났다" 신호. 한 컨텍스트에서 처음 받아들인 신호만 처리한다.
        // 화면별로 받을 수 있는 신호가 정해져 있다 — MainMenu: QuitGame(시작은 CompleteNewGame) / 나머지: Done, BackToMainMenu
        void Complete(ScreenOutcome outcome);

        // MainMenu 전용: 난이도를 고른 뒤 새 게임 시작 (C-003 v1.2)
        void CompleteNewGame(NewGameOptions options);

        // 데이터 검증 실패를 Boot의 오류 안내로 알린다 (C-003 v1.3, D-017 E2).
        // 알린 뒤 이 컨텍스트의 Complete·CompleteNewGame은 무시된다. 여러 번 알려도 안내는 하나.
        void ReportDataError(string reason);
    }
}