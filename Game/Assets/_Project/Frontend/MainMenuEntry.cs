using HumanLastRampart.Core.Flow;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Frontend
{
    public sealed class MainMenuEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.MainMenu;

        protected override ScreenView BuildView(IScreenContext context)
        {
            // 메인은 Campaign이 null일 수 있다. 새 게임 데이터 생성은 App 담당.
            // K-047: 해 질 녘 성벽 그림(K-027) 위 왼쪽에 양피지 임명장 — 한글 제목(세계 글꼴) + 작은 영문 이름, 단추는 [출정] 하나가 주인공
            var view = new ScreenView(transform, "인류의 마지막 성벽", "");
            if (view.SetBackdrop("main")) view.DockLeft(624f, 400f);
            else view.SetPanelSize(624f, 400f);
            RectTransform sub = view.AddArea("Subtitle", 32f);
            Text english = Ui.Label(sub, "English", "HUMAN LAST RAMPART", UiTokens.Small, UiTokens.InkSoft, TextAnchor.MiddleLeft, true);
            Ui.Stretch(english.rectTransform);
            view.AddArea("Gap", UiTokens.S32);
            view.AddButton("종료", "종료", () => Complete(ScreenOutcome.QuitGame), false);
            view.AddButton("게임 시작", "출정", ShowDifficulty, true);
            return view;
        }
    }
}
