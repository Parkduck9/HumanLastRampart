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
            // D-076: 글자 덩어리 대신 그림판 한 장 — 성벽 도식·병종별 손실 막대·영웅 초상을 한 번에 보고 확인
            string headline = valid
                ? $"{(result.Outcome == BattleOutcome.Victory ? "승리" : "패배")} · 처치 {result.DefeatedEnemyCount:N0} · 골드 +{result.EarnedGold:N0}"
                : "전투 정보를 처리하지 못했습니다.";
            var view = new ScreenView(transform, $"전투 {result?.BattleNumber} 결과", headline);
            // K-027: 승리면 버틴 성벽, 패배면 무너진 성벽 배경. 큰 창이 거의 다 덮으므로 창을 조금 비치게
            if (valid && view.SetBackdrop(result.Outcome == BattleOutcome.Victory ? "victory" : "defeat"))
                view.SetPanelAlpha(0.88f);
            if (valid)
            {
                view.SetPanelSize(1600f, 900f);
                RectTransform board = view.AddArea("Result Board", 440f);
                board.sizeDelta = new Vector2(1488f, 440f);
                ResultBoard.Build(board, campaign.ActiveBattleSetup, result, id => context.Definitions.Get<UnitDef>(id));
            }
            var next = view.AddButton("다음", () => Complete(ScreenOutcome.Done));
            next.interactable = valid;
            view.MarkPrimary(next);   // 주된 다음 행동 (D-066)
            if (!valid) context.ReportDataError(error);
            return view;
        }

    }
}
