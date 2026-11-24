using HumanLastRampart.Core.Campaign;
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
            var view = new ScreenView(transform, "빛을 잇는 자", "");   // K-121: 게임 제목 변경 (영문 이름은 아래 작은 글씨)
            // K-047 고침: 단추를 판 폭 안에 세로로 — 주인공 단추(이어하기 또는 출정)를 맨 위에 판 폭 가득, 아래 줄에 설정·종료
            float height = canContinue ? 512f : 432f;
            // K-121: 네 겹 시차 배경 (그림이 없으면 K-027 한 장 배경으로)
            if (MenuParallax.Build(view.BackdropRoot) != null || view.SetBackdrop("main")) view.DockLeft(624f, height);
            else view.SetPanelSize(624f, height);
            RectTransform sub = view.AddArea("Subtitle", 32f);
            Text english = Ui.Label(sub, "English", "LIGHTBEARERS", UiTokens.Text, UiTokens.InkSoft, TextAnchor.MiddleLeft);
            Ui.Stretch(english.rectTransform);
            if (canContinue)
            {
                string difficulty = save.Difficulty == Difficulty.Easy ? "쉬움" : "현실";
                Button cont = Ui.Button(view.AddArea("Continue", 64f), "이어하기", $"이어하기 · {(save.FortressNumber > 0 ? BeaconRules.NightLabel(save.FortressNumber, save.NightNumber) : "전투 " + save.BattleNumber)} 앞 · {difficulty}", () => Complete(ScreenOutcome.ContinueGame), UiButtonKind.Primary);
                Ui.Stretch((RectTransform)cont.transform);
                view.SetFirst(cont);
            }

            Button start = Ui.Button(view.AddArea("Start", 64f), "게임 시작", canContinue ? "새로 출정" : "출정", ShowDifficulty, canContinue ? UiButtonKind.Normal : UiButtonKind.Primary);
            Ui.Stretch((RectTransform)start.transform);
            if (!canContinue) view.SetFirst(start);
            HorizontalLayoutGroup row = Ui.Row(view.AddArea("More", 64f), "Row", UiTokens.S16);
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            Ui.Stretch((RectTransform)row.transform);
            Ui.Button(row.transform, "설정", "설정", ShowSettings);
            Ui.Button(row.transform, "종료", "종료", () => Complete(ScreenOutcome.QuitGame), UiButtonKind.Quiet);
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
