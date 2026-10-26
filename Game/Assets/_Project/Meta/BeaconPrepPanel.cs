using System;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Meta
{
    // 정비(낮) 화면의 등대 줄 (W8, Concept_Beacon_v1 9.3·9.4·2.4). 자리만 잡은 임시 배치 — 그림·배치는 나중에 다듬는다.
    //  줄 1: 요새 N · 밤 M/총   [등대 복구 막대 %]   성벽 조건 / 점화 준비 상태
    //  줄 2: 수비대 지원금 +N        오늘 밤 등불 밝기 [약][보통][강]   [등대 수리]
    // 진행 데이터를 직접 바꾸는 것은 밝기 선택(PlannedLamp)과 등대 수리(골드→복구)뿐이다.
    public static class BeaconPrepPanel
    {
        // W36: 등대 세부 칸은 탑 기둥을 가리지 않게 왼쪽 좁은 판(네 줄)에 놓는다.
        //  줄 1: 밤 · 복구 막대 %    줄 2: 성벽 조건 / 점화 준비    lampLine: 밝기 약·보통·강    buyLine: 수비대 지원금 · 등대 수리
        public static void Build(RectTransform line1, RectTransform line2, RectTransform line3, IScreenContext context, Action refreshGold, Text brief, RectTransform lampLine, RectTransform buyLine)
        {
            CampaignState campaign = context.Campaign;
            var ruleList = context.Definitions.All<BattleRulesDef>();
            int repairsPerDay = ruleList.Count > 0 ? ruleList[0].BeaconRepairsPerDay : 0;   // W11: 하루 한도 (규칙 데이터)

            // 줄 3 (W12): 밤 사이 한마디 (Story_Beacon_v1 3장). 어젯밤 결과·점화 준비 상태에 맞는 부관/주인공의 한두 줄
            Text dayLine = null;   // line3가 null이면 한마디 줄이 없는 날
            if (line3 != null)
            {
                HorizontalLayoutGroup row3 = Ui.Row(line3, "Beacon Row 3", UiTokens.S16);
                Ui.Stretch((RectTransform)row3.transform);
                dayLine = Ui.Label(row3.transform, "Beacon Day Line", "", UiTokens.Small, UiTokens.BodyDim, TextAnchor.MiddleLeft);   // W16: 긴 글줄은 Black 대신 갈무리
                Ui.Size(dayLine, 1500f, 48f, true);
            }

            HorizontalLayoutGroup row1 = Ui.Row(line1, "Beacon Row 1", UiTokens.S16);
            Ui.Stretch((RectTransform)row1.transform);
            Text night = Ui.Label(row1.transform, "Beacon Night", "", UiTokens.Text, UiTokens.Brass, TextAnchor.MiddleLeft, true);
            Ui.Size(night, 220f, 48f);
            UiBar bar = Ui.Bar(row1.transform, "Beacon Repair Bar", 252f, 24f, false);
            bar.FixedColor = UiTokens.Brass;
            Text percent = Ui.Label(row1.transform, "Beacon Repair Value", "", UiTokens.Text, UiTokens.Body);
            Ui.Size(percent, 96f, 48f);

            HorizontalLayoutGroup rowWalls = Ui.Row(line2, "Beacon Row Walls", UiTokens.S16);
            Ui.Stretch((RectTransform)rowWalls.transform);
            Text walls = Ui.Label(rowWalls.transform, "Beacon Wall Condition", "", UiTokens.Small, UiTokens.Body);
            Ui.Size(walls, 640f, 48f, true);

            HorizontalLayoutGroup row2 = Ui.Row(lampLine, "Beacon Row 2", UiTokens.S16);
            Ui.Stretch((RectTransform)row2.transform);
            Text lampWord = Ui.Label(row2.transform, "Beacon Lamp Word", "오늘 밤 등불 밝기", UiTokens.Small, UiTokens.BodyDim);
            Ui.Size(lampWord, 220f, 48f);   // W16: 갈무리 폭에 맞춤
            lampWord.horizontalOverflow = HorizontalWrapMode.Overflow;
            Button buy = null;   // Refresh가 쓰므로 먼저 선언
            var lampButtons = new Button[3];
            LampBrightness[] order = { LampBrightness.Low, LampBrightness.Normal, LampBrightness.High };
            string[] names = { "약", "보통", "강" };
            for (int i = 0; i < 3; i++)
            {
                LampBrightness pick = order[i];
                lampButtons[i] = Ui.Button(row2.transform, "등불 " + names[i], names[i], () =>
                {
                    campaign.PlannedLamp = pick;
                    Refresh();
                }, UiButtonKind.Tab);
                Ui.Size(lampButtons[i], 96f, 48f);
            }

            HorizontalLayoutGroup rowBuy = Ui.Row(buyLine, "Beacon Row Buy", UiTokens.S16);
            Ui.Stretch((RectTransform)rowBuy.transform);
            Text support = Ui.Label(rowBuy.transform, "Beacon Support", "", UiTokens.Small, UiTokens.Body);
            Ui.Size(support, 220f, 48f);
            buy = Ui.Button(rowBuy.transform, "등대 수리", $"등대 수리 +{BeaconRules.DayRepairAmount:0} ({BeaconRules.DayRepairGold:N0}골드)", () =>
            {
                BeaconRules.TryBuyRepair(campaign, out _, repairsPerDay);
                refreshGold?.Invoke();
                Refresh();
            }, UiButtonKind.Normal);
            Ui.Size(buy, 400f, 48f);   // W16: 갈무리 폭에 맞춤


            void Refresh()
            {
                night.text = BeaconRules.NightLabel(campaign.FortressNumber, campaign.NightNumber, true);
                bar.Set(Mathf.Clamp01(campaign.BeaconRepair / BeaconRules.RepairMax));
                percent.text = Mathf.FloorToInt(campaign.BeaconRepair) + "%";

                float low = 1f;
                foreach (WallSegmentState wall in campaign.Wall) if (wall != null && wall.MaxHp > 0) low = Mathf.Min(low, (float)wall.Hp / wall.MaxHp);
                bool wallsOk = BeaconRules.WallsReady(campaign);
                string missing = BeaconRules.MissingForIgnition(campaign);
                walls.text = $"성벽 최저 {Mathf.FloorToInt(low * 100f)}% (90% 필요)  ·  " + (missing ?? "다음 밤은 점화의 밤");
                walls.color = missing == null ? UiTokens.Good : (wallsOk ? UiTokens.Body : UiTokens.DangerLight);

                if (brief != null)   // W36: 얇은 위쪽 줄에 보이는 한 줄 요약 (자세한 것은 [등대 세부] 칸)
                {
                    brief.text = $"등대 {Mathf.FloorToInt(campaign.BeaconRepair)}% · " + (missing ?? "다음 밤은 점화의 밤");
                    brief.color = missing == null ? UiTokens.Good : UiTokens.Body;
                }

                if (dayLine != null)
                    dayLine.text = string.Join("   ·   ", BeaconRules.DayLines(campaign).ConvertAll(l => $"{l.speaker}: \"{l.text}\""));

                support.text = $"수비대 지원금 +{campaign.LastSupportGold:N0}";
                for (int i = 0; i < 3; i++) Ui.SetOn(lampButtons[i], campaign.PlannedLamp == order[i]);
                bool usedToday = repairsPerDay > 0 && campaign.BeaconRepairBoughtToday >= repairsPerDay;
                Ui.LabelOf(buy).text = usedToday ? "오늘 수리함 (내일 다시)" : $"등대 수리 +{BeaconRules.DayRepairAmount:0} ({BeaconRules.DayRepairGold:N0}골드)";   // W11: 하루 한 번
                buy.interactable = !usedToday && campaign.BeaconRepair < BeaconRules.RepairMax && campaign.Gold >= BeaconRules.DayRepairGold;
            }


            Refresh();
            // 성벽 수리·골드 변화를 따라간다 (다른 패널이 진행 데이터를 바꾼다). 세부 칸이 접혀 있어도 한 줄 요약이 따라가도록 늘 켜진 요약 줄 글자에 붙인다
            (brief != null ? brief.gameObject : line1.gameObject).AddComponent<BeaconPrepWatcher>().Init(campaign, Refresh);
        }

        // 진행 데이터가 바뀌면 줄을 새로 그린다 (값이 같으면 아무것도 안 한다). 숫자만 견주어 매 프레임 할당이 없다
        sealed class BeaconPrepWatcher : MonoBehaviour
        {
            CampaignState campaign;
            Action refresh;
            int gold, lamp, boughtToday;
            float repair;
            long wallSum;

            public void Init(CampaignState campaignState, Action refreshAction)
            {
                campaign = campaignState;
                refresh = refreshAction;
                Snapshot();
            }

            void Update()
            {
                if (campaign == null) return;
                int g = campaign.Gold, l = (int)campaign.PlannedLamp, b = campaign.BeaconRepairBoughtToday;
                float r = campaign.BeaconRepair;
                long w = WallSum();
                if (g == gold && l == lamp && b == boughtToday && r == repair && w == wallSum) return;
                Snapshot();
                refresh?.Invoke();
            }

            void Snapshot() { gold = campaign.Gold; lamp = (int)campaign.PlannedLamp; boughtToday = campaign.BeaconRepairBoughtToday; repair = campaign.BeaconRepair; wallSum = WallSum(); }

            long WallSum()
            {
                long sum = 0; int i = 1;
                foreach (WallSegmentState wall in campaign.Wall) sum += (wall != null ? wall.Hp : -1) * (long)i++;   // 칸마다 가중해 자리가 바뀌어도 알아챈다
                return sum;
            }
        }
    }
}