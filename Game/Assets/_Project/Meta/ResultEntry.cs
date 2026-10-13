using HumanLastRampart.Core.Flow;
using HumanLastRampart.UI;

namespace HumanLastRampart.Meta
{
    public sealed class ResultEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.Result;

        protected override ScreenView BuildView(IScreenContext context)
        {
            // 현재는 표시/완료 뼈대. C-007 결과 반영은 Meta의 후속 작업이며 App에 위임하지 않는다.
            var result = context.Campaign?.LastBattle;
            string description = result == null ? "전투 결과가 없습니다." :
                $"전투 {result.BattleNumber} 종료\n처치한 적: {result.DefeatedEnemyCount:N0}\n획득 골드: {result.EarnedGold:N0}";
            var view = new ScreenView(transform, "전투 결과", description);
            view.AddButton("다음", () => Complete(ScreenOutcome.Done));
            return view;
        }
    }
}
