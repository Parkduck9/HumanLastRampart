using HumanLastRampart.Core.Flow;
using HumanLastRampart.UI;

namespace HumanLastRampart.Frontend
{
    public sealed class MainMenuEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.MainMenu;

        protected override ScreenView BuildView(IScreenContext context)
        {
            // 메인은 Campaign이 null일 수 있다. 새 게임 데이터 생성은 App 담당.
            var view = new ScreenView(transform, "Human Last Rampart", "마지막 성벽을 지켜라");
            view.AddButton("게임 시작", ShowDifficulty);
            view.AddButton("종료", () => Complete(ScreenOutcome.QuitGame));
            return view;
        }
    }
}
