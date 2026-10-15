using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.UI;

namespace HumanLastRampart.Meta
{
    public sealed class ResultEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.Result;

        protected override ScreenView BuildView(IScreenContext context)
        {
            var campaign = context.Campaign;
            bool valid = BattleResultApplier.TryApply(campaign, out string error,
                context.Definitions.Get<TerrainDef>(campaign?.ActiveBattleSetup?.TerrainId));
            var result = campaign?.LastBattle;
            ResultReportFormatter.Report report = null;
            if (valid)
            {
                try
                {
                    report = ResultReportFormatter.Build(campaign.ActiveBattleSetup, result,
                        id => context.Definitions.Get<UnitDef>(id)?.DisplayName ?? id,
                        id => context.Definitions.Get<PostDef>(id)?.WallId,
                        id => context.Definitions.Get<UnitDef>(id)?.MaxHp ?? 0);
                }
                catch (System.ArgumentException exception)
                {
                    valid = false;
                    error = "결과 상세 데이터를 표시하지 못했습니다: " + exception.Message;
                }
            }
            string description = valid ? report.Summary : "전투 정보를 처리하지 못했습니다.";
            var view = new ScreenView(transform, "전투 결과", description);
            if (valid)
            {
                AddDetails(view, "성벽 피해", report.Walls);
                AddDetails(view, "병력 손실", report.Squads);
                AddDetails(view, "영웅 상태", report.Heroes);
            }
            var next = view.AddButton("다음", () => Complete(ScreenOutcome.Done));
            next.interactable = valid;
            view.MarkPrimary(next);   // 주된 다음 행동 (D-066)
            if (!valid) context.ReportDataError(error);
            return view;
        }

        static void AddDetails(ScreenView view, string title, string content)
        {
            UnityEngine.UI.Button button = null;
            button = view.AddButton(title, () => ResultDetailPopup.Show(button, title, content));
        }
    }
}
