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
                return new ScreenView(transform, "정비 오류", "진행 데이터와 정비 가격을 확인해 주세요.");
            }
            var view = new ScreenView(transform, "정비", $"다음 전투: {campaign.BattleNumber}\n보유 골드: {campaign.Gold:N0} · 병력 {session.TotalCount():N0}/{session.Rules.GlobalCapacity:N0}\n성벽을 선택해 모집·수리하세요. 함정은 빈 격자 칸에 설치할 수 있습니다.");
            Button repair = null, recruit = null, trapButton = null;
            repair = view.AddButton("성벽 수리", () => PreparationPanel.Show(repair, context, session, false));
            recruit = view.AddButton("병력 모집·보충", () => PreparationPanel.Show(recruit, context, session, true));
            trapButton = view.AddButton("함정 구매·설치", () => TrapPreparationPanel.Show(trapButton, context, traps, session));
            // K-058 (D-079): 상점(영웅 장비 칸·단체 아이템), 병영 안 [병종 훈련]·[용병 고용]
            Button shop = null, training = null, mercenary = null;
            shop = view.AddButton("상점", () => UpgradePanel.Show(shop, context, session, UpgradePanel.Mode.Shop));
            training = view.AddButton("병종 훈련", () => UpgradePanel.Show(training, context, session, UpgradePanel.Mode.Training));
            mercenary = view.AddButton("용병 고용", () => UpgradePanel.Show(mercenary, context, session, UpgradePanel.Mode.Mercenary));
            PreparationPanel.OpenTraining = () => training.onClick.Invoke();
            PreparationPanel.OpenMercenary = () => mercenary.onClick.Invoke();
            var ready = view.AddButton("전투 준비 완료", () => { session.Close(); traps.Close(); Complete(ScreenOutcome.Done); });
            view.MarkPrimary(ready);   // 주된 다음 행동 (D-066)

            // D-076 정비 마당: 창은 위쪽 요약 띠로 올리고, 버튼은 숨겨 둔 채(같은 길로 열림) 마당 장소에서 E로 연다
            view.DockTop(1100f, 330f);
            foreach (Button b in new[] { repair, recruit, trapButton, shop, training, mercenary, ready }) ScreenView.HideButton(b);
            bool backdrop = view.SetBackdrop("prep");   // K-027 성 안 마당 배경 (있으면 마당의 하늘·성벽 띠 대신)
            PrepYard yard = PrepYard.Build(view, HeroUnit(context), backdrop);
            yard.AddStation("성벽 수리", 500f, new Color(0.45f, 0.42f, 0.38f), () => repair.onClick.Invoke());
            yard.AddStation("병영 (모집·보충)", 1300f, new Color(0.42f, 0.30f, 0.24f), () => recruit.onClick.Invoke());
            yard.AddStation("함정 공방", 2100f, new Color(0.30f, 0.30f, 0.36f), () => trapButton.onClick.Invoke());
            yard.AddStation("상점", 2550f, new Color(0.46f, 0.36f, 0.22f), () => shop.onClick.Invoke());   // K-058 보급 상인 (그림은 Codex 요청)
            yard.AddStation("성벽 계단 (다음 전투)", 3000f, new Color(0.40f, 0.38f, 0.30f), () => yard.AskNextBattle(() => ready.onClick.Invoke()));
            var lifetime = repair.GetComponentInParent<UnityEngine.Canvas>().gameObject.AddComponent<PreparationSessionLifetime>();
            lifetime.Session = session; lifetime.Traps = traps;
            return view;
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
