using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Save;
using HumanLastRampart.UI;

namespace HumanLastRampart.Frontend
{
    public sealed class MainMenuEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.MainMenu;

        SettingsPopup settings;

        protected override ScreenView BuildView(IScreenContext context)
        {
            // 메인은 Campaign이 null일 수 있다. 새 게임 데이터 생성은 App 담당.
            // K-027: 해 질 녘 성벽 배경 + 부제 「인류의 마지막 성벽」(시안 A). 창은 왼쪽 제목 여백으로 옮기고 배경이 비치게
            // K-062: 이어하기(저장이 있을 때만)·설정 추가 — 시작/설정/종료 (FirstRead 0번 화면)
            bool canContinue = CampaignSave.TryPeek(out CampaignSave.Summary save);
            var view = new ScreenView(transform, "Human Last Rampart", "인류의 마지막 성벽");
            if (view.SetBackdrop("main"))
            {
                view.DockLeft(640f, canContinue ? 760f : 640f);
                view.SetPanelAlpha(0.82f);
            }

            if (canContinue)
            {
                string difficulty = save.Difficulty == Difficulty.Easy ? "쉬움" : "현실";
                var resume = view.AddButton($"이어하기 — {save.BattleNumber}번째 전투 앞 정비 ({difficulty})", () => Complete(ScreenOutcome.ContinueGame));
                view.MarkPrimary(resume);
            }

            view.AddButton("게임 시작", ShowDifficulty);
            view.AddButton("설정", ShowSettings);
            view.AddButton("종료", () => Complete(ScreenOutcome.QuitGame));
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
