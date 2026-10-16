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
            // K-027: 해 질 녘 성벽 배경 + 부제 「인류의 마지막 성벽」(시안 A). 창은 왼쪽 제목 여백으로 옮기고 배경이 비치게
            var view = new ScreenView(transform, "Human Last Rampart", "인류의 마지막 성벽");
            if (view.SetBackdrop("main"))
            {
                view.DockLeft(640f, 520f);
                view.SetPanelAlpha(0.82f);
            }
            view.AddButton("게임 시작", ShowDifficulty);
            view.AddButton("종료", () => Complete(ScreenOutcome.QuitGame));
            return view;
        }
    }
}
