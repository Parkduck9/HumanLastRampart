using HumanLastRampart.Core.Flow;
using HumanLastRampart.UI;

namespace HumanLastRampart.Meta
{
    public sealed class PrepEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.Prep;

        protected override ScreenView BuildView(IScreenContext context)
        {
            var campaign = context.Campaign;
            string description = campaign == null ? "진행 정보가 없습니다." :
                $"다음 전투: {campaign.BattleNumber}\n보유 골드: {campaign.Gold:N0}";
            var view = new ScreenView(transform, "정비", description);
            view.AddButton("전투 준비 완료", () => Complete(ScreenOutcome.Done));
            return view;
        }
    }
}
