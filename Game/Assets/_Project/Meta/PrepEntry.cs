using HumanLastRampart.Core.Flow;
using HumanLastRampart.UI;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Meta
{
    public sealed class PrepEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.Prep;

        protected override ScreenView BuildView(IScreenContext context)
        {
            var campaign = context.Campaign;
            PreparationTransactions session;
            TrapPreparationTransactions traps;
            try { session = new PreparationTransactions(campaign, context.Definitions);
                traps = new TrapPreparationTransactions(campaign, context.Definitions); }
            catch (ArgumentException ex)
            {
                context.ReportDataError(ex.Message);
                return new ScreenView(transform, "정비 마당을 열 수 없습니다", "장부가 맞지 않습니다. 개발 기록을 확인해 주세요.");
            }

            var view = new ScreenView(transform, "", "");
            // 장소 일은 숨긴 단추로 연다 (마당 E와 시험이 같은 길). 열쇠는 시험이 찾는 이름
            Button repair = null, recruit = null, trapButton = null;
            repair = view.AddButton("성벽 수리", () => PreparationPanel.Show(repair, context, session, false));
            recruit = view.AddButton("병력 모집·보충", () => PreparationPanel.Show(recruit, context, session, true));
            trapButton = view.AddButton("함정 구매·설치", () => TrapPreparationPanel.Show(trapButton, context, traps, session));
            var ready = view.AddButton("전투 준비 완료", () => { session.Close(); traps.Close(); Complete(ScreenOutcome.Done); });
            foreach (Button b in new[] { repair, recruit, trapButton, ready }) ScreenView.HideButton(b);

            // D-076 정비 마당 + K-047: 위쪽은 황동 계기 띠 한 줄 — 1순위 골드, 그다음 병력·다음 전투 (S-20)
            view.DockTop(1040f, 80f);
            BuildSummary(view.AddArea("Summary", 48f), campaign.BattleNumber);
            PreparationPanel.RefreshSummary(repair, context, session);

            bool backdrop = view.SetBackdrop("prep");   // K-027 성 안 마당 배경 (있으면 마당의 하늘·성벽 띠 대신)
            PrepYard yard = PrepYard.Build(view, HeroUnit(context), backdrop);
            yard.AddStation("성벽 수리", 500f, new Color(0.45f, 0.42f, 0.38f), () => repair.onClick.Invoke());
            yard.AddStation("병영", 1300f, new Color(0.42f, 0.30f, 0.24f), () => recruit.onClick.Invoke());
            yard.AddStation("함정 공방", 2100f, new Color(0.30f, 0.30f, 0.36f), () => trapButton.onClick.Invoke());
            yard.AddStation("성벽 계단", 3000f, new Color(0.40f, 0.38f, 0.30f), () => yard.AskNextBattle(() => ready.onClick.Invoke()));
            var lifetime = repair.GetComponentInParent<UnityEngine.Canvas>().gameObject.AddComponent<PreparationSessionLifetime>();
            lifetime.Session = session; lifetime.Traps = traps;
            return view;
        }

        // [정비 마당]  전투 2 앞            [골드] 1,240   [병력] 520/1,200
        static void BuildSummary(RectTransform area, int battle)
        {
            HorizontalLayoutGroup row = Ui.Row(area, "Summary Row", UiTokens.S16);
            Ui.Stretch((RectTransform)row.transform);
            Text title = Ui.Label(row.transform, "Title", "정비 마당", UiTokens.H2, UiTokens.Brass, TextAnchor.MiddleLeft, true);
            Ui.Size(title, title.preferredWidth + 8f, 48f);
            Text next = Ui.Label(row.transform, "Next Battle", $"전투 {battle} 앞", UiTokens.Text, UiTokens.BodyDim);
            Ui.Size(next, next.preferredWidth + 8f, 48f);
            RectTransform spacer = Ui.Rect(row.transform, "Spacer");
            Ui.Size(spacer, 0f, 48f, true);
            Image gold = Ui.HudIcon(row.transform, "Gold Icon", "Icons/stat_gold", 32f);
            if (gold == null) Ui.Size(Ui.Label(row.transform, "Gold Word", "골드", UiTokens.Small, UiTokens.BodyDim), 44f, 48f);
            Text goldValue = Ui.Label(row.transform, "Gold Value", "", UiTokens.H2, UiTokens.Body, TextAnchor.MiddleLeft, true);
            Ui.Size(goldValue, 136f, 48f);
            Image troops = Ui.HudIcon(row.transform, "Troops Icon", "Icons/stat_troops", 24f);
            if (troops == null) Ui.Size(Ui.Label(row.transform, "Troops Word", "병력", UiTokens.Small, UiTokens.BodyDim), 44f, 48f);
            Text troopsValue = Ui.Label(row.transform, "Troops Value", "", UiTokens.Text, UiTokens.Body);
            Ui.Size(troopsValue, 136f, 48f);
        }

        // 마당을 걷는 주인공 그림: 참가 영웅 중 주인공(UnitKind.Hero)
        static HumanLastRampart.Core.Definitions.UnitDef HeroUnit(IScreenContext context)
        {
            var campaign = context.Campaign;
            if (campaign?.Heroes == null) return null;
            foreach (var hero in campaign.Heroes)
            {
                var unit = context.Definitions.Get<HumanLastRampart.Core.Definitions.UnitDef>(hero.UnitDefId);
                if (unit != null && unit.Kind == HumanLastRampart.Core.Definitions.UnitKind.Hero) return unit;
            }

            return null;
        }
    }
}
