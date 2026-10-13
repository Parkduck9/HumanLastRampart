using HumanLastRampart.Core.Flow;
using HumanLastRampart.UI;

namespace HumanLastRampart.Meta
{
    public sealed class ResultEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.Result;

        protected override ScreenView BuildView(IScreenContext context)
        {
            var campaign = context.Campaign;
            bool valid = BattleResultApplier.TryApply(campaign, out string error);
            var result = campaign?.LastBattle;
            string description = valid ?
                $"전투 {result.BattleNumber} 종료\n처치한 적: {result.DefeatedEnemyCount:N0}\n획득 골드: {result.EarnedGold:N0}" :
                "전투 정보를 처리하지 못했습니다.";
            var view = new ScreenView(transform, "전투 결과", description);
            var next = view.AddButton("다음", () => Complete(ScreenOutcome.Done));
            next.interactable = valid;
            if (!valid) context.ReportDataError(error);
            return view;
        }
    }
}
