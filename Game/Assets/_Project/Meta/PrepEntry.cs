using HumanLastRampart.Core.Flow;
using HumanLastRampart.UI;
using System;
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
            try { session = new PreparationTransactions(campaign, context.Definitions); }
            catch (ArgumentException ex)
            {
                context.ReportDataError(ex.Message);
                return new ScreenView(transform, "정비 오류", "진행 데이터와 정비 가격을 확인해 주세요.");
            }
            var view = new ScreenView(transform, "정비", $"다음 전투: {campaign.BattleNumber}\n보유 골드: {campaign.Gold:N0} · 병력 {session.TotalCount():N0}/{session.Rules.GlobalCapacity:N0}\n성벽을 선택해 모집·수리하세요. 함정 기능은 후속 작업입니다.");
            Button repair = null, recruit = null;
            repair = view.AddButton("성벽 수리", () => PreparationPanel.Show(repair, context, session, false));
            recruit = view.AddButton("병력 모집·보충", () => PreparationPanel.Show(recruit, context, session, true));
            view.AddButton("전투 준비 완료", () => { session.Close(); Complete(ScreenOutcome.Done); });
            repair.GetComponentInParent<UnityEngine.Canvas>().gameObject.AddComponent<PreparationSessionLifetime>().Session = session;
            return view;
        }
    }
}
