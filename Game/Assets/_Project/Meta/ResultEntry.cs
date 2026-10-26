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

        // 진 밤 줄: 복구 절반(둘 다 내림), 성벽 안내는 최소 비율 밑으로 떨어진 성벽이 있을 때만
        static string LostNightLine(CampaignState campaign, IScreenContext context)
        {
            if (BeaconRules.LossEndsRun(campaign)) return "연속 두 번째 패배 — 등대를 지키지 못했습니다";
            int now = Mathf.FloorToInt(campaign.BeaconRepair), half = Mathf.FloorToInt(campaign.BeaconRepair / 2f);
            string line = now > 0 ? $"등대 복구가 절반으로: {now}% → {half}%" : "등대 복구 0%";
            int floor = FloorPercent(context);
            bool low = false;
            foreach (WallSegmentState wall in campaign.Wall) if (wall != null && wall.MaxHp > 0 && wall.Hp * 100L < (long)wall.MaxHp * floor) low = true;
            return low ? line + $"  · 무너진 성벽은 낮에 최소 {floor}%까지 되돌려집니다" : line;
        }

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
                float boardHeight = result.BossPresent ? 420f : 456f;   // 우두머리 줄(36)이 더해지는 만큼 판을 낮춰 쪽이 넘치지 않게
                RectTransform board = view.AddArea("Result Board", boardHeight);
                board.sizeDelta = new Vector2(1504f, boardHeight);
                ResultBoard.Build(board, campaign.ActiveBattleSetup, result, id => context.Definitions.Get<UnitDef>(id));

                // W8: 등대 복구 / 점화 (자리만 잡은 한 줄)
                RectTransform beacon = view.AddArea("Beacon Line", 40f);
                string line = lost
                    ? LostNightLine(campaign, context)
                    : campaign.LastNightRelit
                    ? $"요새 점화! 수비대 {campaign.LastNightGarrison:N0}명 남음"
                    : $"등대 복구 +{Mathf.RoundToInt(campaign.LastNightGain)}  (현재 {Mathf.RoundToInt(campaign.BeaconRepair)}%)";
                Text beaconText = Ui.Label(beacon, "Beacon Text", line, UiTokens.Text, !lost && campaign.LastNightRelit ? UiTokens.GoodInk : UiTokens.Ink, TextAnchor.MiddleCenter, true);
                Ui.Stretch(beaconText.rectTransform);

                // 우두머리 성벽손 오우거 (점화의 밤): 처치 여부와 나온 뒤 걸린 시간
                if (result.BossPresent)
                {
                    RectTransform boss = view.AddArea("Boss Line", 36f);
                    int seconds = Mathf.RoundToInt(result.BossSeconds);
                    string bossLine = result.BossDefeated
                        ? $"우두머리 성벽손 오우거 처치 · {seconds / 60}:{seconds % 60:00}" + (result.BossFalls > 0 ? $" · 떨어뜨림 {result.BossFalls}번" : "")
                        : "우두머리 성벽손 오우거는 쓰러뜨리지 못했습니다";
                    Text bossText = Ui.Label(boss, "Boss Text", bossLine, UiTokens.Text, result.BossDefeated ? UiTokens.GoodInk : UiTokens.DangerInk, TextAnchor.MiddleCenter, true);
                    Ui.Stretch(bossText.rectTransform);
                }
            }

            var next = view.AddButton("다음", "보고 마치기", () => Complete(ScreenOutcome.Done), true);
            next.interactable = valid;
            if (!valid) context.ReportDataError(error);
            return view;
        }
    }
}
