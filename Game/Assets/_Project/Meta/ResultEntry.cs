using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.UI;
using UnityEngine;

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
            if (valid)
            {
                try
                {
                    ResultReportFormatter.Build(campaign.ActiveBattleSetup, result,
                        id => context.Definitions.Get<UnitDef>(id)?.DisplayName ?? id,
                        id => context.Definitions.Get<PostDef>(id)?.WallId,
                        id => context.Definitions.Get<UnitDef>(id)?.MaxHp ?? 0);
                }
                catch (System.ArgumentException exception)
                {
                    valid = false;
                    error = "결과 상세 데이터를 표시하지 못했습니다: " + exception.Message;   // 개발 기록용 (ReportDataError)
                }
            }

            // K-047 4번 화면: 승패 도장이 찍힌 전투 보고서 한 장 — 1순위는 승패, 그다음 성벽·병력·영웅 (D-076 그림판)
            var view = new ScreenView(transform, valid ? $"전투 {result.BattleNumber} 보고" : "보고서가 비었습니다",
                valid ? "" : "전령이 길을 잃었습니다. 다음으로 넘어갈 수 없습니다.");
            if (valid)
            {
                view.SetBackdrop(result.Outcome == BattleOutcome.Victory ? "victory" : "defeat", 0.45f);
                view.SetPanelSize(1600f, 900f);
                RectTransform head = view.AddArea("Result Head", 96f);
                ResultBoard.BuildHead(head, result);
                RectTransform board = view.AddArea("Result Board", 456f);
                board.sizeDelta = new Vector2(1504f, 456f);
                ResultBoard.Build(board, campaign.ActiveBattleSetup, result, id => context.Definitions.Get<UnitDef>(id));
            }

            var next = view.AddButton("다음", "보고 마치기", () => Complete(ScreenOutcome.Done), true);
            next.interactable = valid;
            if (!valid) context.ReportDataError(error);
            return view;
        }
    }
}
