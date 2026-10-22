using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Meta
{
    public sealed class ResultEntry : ScreenEntryBase
    {
        public override ScreenId Id => ScreenId.Result;

        static int FloorPercent(IScreenContext context)
        {
            var rules = context.Definitions.All<BattleRulesDef>();
            return rules.Count > 0 ? rules[0].LostNightWallFloorPercent : 0;
        }

        protected override ScreenView BuildView(IScreenContext context)
        {
            var campaign = context.Campaign;
            // W11: 진 밤(첫 패배)도 이 화면에서 결과를 반영해 잃은 병력·성벽 피해를 보여 준다 (보상 골드 없음). 실패하면 승리 때처럼 막는다
            bool lost = campaign?.LastBattle != null && campaign.LastBattle.Outcome == BattleOutcome.Defeat;
            bool valid = BattleResultApplier.TryApply(campaign, out string error,
                context.Definitions.Get<TerrainDef>(campaign?.ActiveBattleSetup?.TerrainId), lost);
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

            // W8: 이긴 밤의 등대 복구·요새 점화·수비대 남김을 진행 데이터에 반영한다 (같은 전투 실행 ID는 한 번만)
            if (valid && !lost) BeaconRules.ApplyWin(campaign, campaign.ActiveBattleSetup, result);

            // K-047 4번 화면: 승패 도장이 찍힌 전투 보고서 한 장 — 1순위는 승패, 그다음 성벽·병력·영웅 (D-076 그림판)
            var view = new ScreenView(transform, valid ? (lost ? BeaconRules.NightLabel(campaign.ActiveBattleSetup.FortressNumber, campaign.ActiveBattleSetup.NightNumber) + " 보고 · 패배" : BeaconRules.NightLabel(campaign.LastNightFortress, campaign.LastNightIndex) + " 보고") : "보고서가 비었습니다",
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

                // W8: 등대 복구 / 점화 (자리만 잡은 한 줄)
                RectTransform beacon = view.AddArea("Beacon Line", 40f);
                string line = lost
                    ? $"등대 복구가 절반으로: {Mathf.RoundToInt(campaign.BeaconRepair)}% → {Mathf.FloorToInt(campaign.BeaconRepair / 2f)}%  · 무너진 성벽은 낮에 최소 {FloorPercent(context)}%까지 되돌려집니다"
                    : campaign.LastNightRelit
                    ? $"요새 점화! 수비대 {campaign.LastNightGarrison:N0}명 남음"
                    : $"등대 복구 +{Mathf.RoundToInt(campaign.LastNightGain)}  (현재 {Mathf.RoundToInt(campaign.BeaconRepair)}%)";
                Text beaconText = Ui.Label(beacon, "Beacon Text", line, UiTokens.Text, !lost && campaign.LastNightRelit ? UiTokens.GoodInk : UiTokens.Ink, TextAnchor.MiddleCenter, true);
                Ui.Stretch(beaconText.rectTransform);
            }

            var next = view.AddButton("다음", "보고 마치기", () => Complete(ScreenOutcome.Done), true);
            next.interactable = valid;
            if (!valid) context.ReportDataError(error);
            return view;
        }
    }
}
