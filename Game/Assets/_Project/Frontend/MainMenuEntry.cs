using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Save;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Frontend
{
    public sealed class MainMenuEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.MainMenu;

        SettingsPopup settings;

        protected override ScreenView BuildView(IScreenContext context)
        {
            // 메인은 Campaign이 null일 수 있다. 새 게임 데이터 생성은 App 담당.
            // K-047: 해 질 녘 성벽 그림(K-027) 위 왼쪽에 양피지 임명장 — 한글 제목(세계 글꼴) + 작은 영문 이름, 주인공 단추는 하나
            // K-062: 이어하기(저장이 있을 때만, 그때는 이어하기가 주인공)·설정 — 시작/설정/종료 (FirstRead 0번 화면)
            bool canContinue = CampaignSave.TryPeek(out CampaignSave.Summary save);
            var view = new ScreenView(transform, "인류의 마지막 성벽", "");
            float height = canContinue ? 520f : 460f;
            if (view.SetBackdrop("main")) view.DockLeft(624f, height);
            else view.SetPanelSize(624f, height);
            RectTransform sub = view.AddArea("Subtitle", 32f);
            Text english = Ui.Label(sub, "English", "HUMAN LAST RAMPART", UiTokens.Small, UiTokens.InkSoft, TextAnchor.MiddleLeft, true);
            Ui.Stretch(english.rectTransform);
            view.AddArea("Gap", UiTokens.S32);
            view.AddButton("종료", "종료", () => Complete(ScreenOutcome.QuitGame), false);
            view.AddButton("설정", "설정", ShowSettings, false);
            view.AddButton("게임 시작", "출정", ShowDifficulty, !canContinue);
            if (canContinue)
            {
                string difficulty = save.Difficulty == Difficulty.Easy ? "쉬움" : "현실";
                view.AddButton("이어하기", $"이어하기 — {save.BattleNumber}번째 전투 앞 정비 ({difficulty})", () => Complete(ScreenOutcome.ContinueGame), true);
            }
            return view;
        }

        void ShowSettings()
        {
            if (View == null || (settings != null && settings.IsOpen)) return;
            View.SetInput(false);   // 설정 창이 열린 동안 뒤 메뉴는 막는다
            settings = SettingsPopup.Show(transform, () =>
            {
                settings = null;
                if (View == null) return;
                View.SetInput(true);
                View.FocusFirst();
            });
        }

        protected override void OnDestroy()
        {
            settings?.Close();
            base.OnDestroy();
        }
    }
}
