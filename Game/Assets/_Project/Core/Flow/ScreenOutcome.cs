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
    }
}