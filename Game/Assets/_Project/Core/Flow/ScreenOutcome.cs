namespace HumanLastRampart.Core.Flow
{
    // 화면이 끝날 때 App에 알리는 이유. 어디로 갈지는 App이 정한다 (C-003, Q-A3).
    // 값을 지우거나 이름을 바꾸지 않는다 (C-003 변경 규칙).
    public enum ScreenOutcome
    {
        Done,           // 이 화면의 일을 마침
        StartNewGame,   // 메인: 시작 — 반드시 IScreenContext.CompleteNewGame으로 보낸다 (C-003 v1.2)
        QuitGame,       // 메인: 종료
        BackToMainMenu, // 어느 화면이든: 메인으로
        ContinueGame,   // 메인: 이어하기 — 저장한 진행을 불러와 정비 화면부터 (C-003 v1.4 초안, K-062). 맨 뒤에 붙여 기존 값은 그대로
        OpenBeaconMap,  // 정비: 봉화선 지도를 본다 (W10) — 보기만 하고 정비로 돌아온다. 맨 뒤에 붙여 기존 값은 그대로
    }
}
