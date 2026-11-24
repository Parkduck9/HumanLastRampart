namespace HumanLastRampart.Core.Flow
{
    // 각 화면 씬의 루트에 정확히 하나 두는 진입 컴포넌트가 구현한다 (C-003 3).
    public interface IScreenEntry
    {
        ScreenId Id { get; }

        // 씬이 열린 뒤 App이 한 번 부른다. context는 이 화면이 열려 있는 동안만 유효하다.
        void OnEnter(IScreenContext context);

        // 씬을 닫기 직전 App이 한 번 부른다. 입력 처리·코루틴·이벤트 구독을 여기서 정리한다.
        void OnExit();
    }
}