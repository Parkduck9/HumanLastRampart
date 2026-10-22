using System;
using System.Collections.Generic;
using HumanLastRampart.Core.CombatContracts;

namespace HumanLastRampart.Core.Campaign
{
    // 요새 × 밤 진행 규칙 (W8, Concept_Beacon_v1 9.1·9.3·9.7, 2.4). 순수 계산 + 진행 데이터 갱신. 화면·정의 에셋을 모른다.
    // 수치는 전부 시작값 — 조정(측정)은 조정 담당이 한다. 수치를 바꿀 때는 이 파일의 const만 고친다.
    public static class BeaconRules
    {
        // ── 구조 ─────────────────────────────────────────────
        public const int FortressCount = 2;                       // 1회차 요새 수 (9.7)
        static readonly int[] NightsByFortress = { 2, 3 };        // 요새 1 = 밤 2번, 요새 2 = 밤 3번. 마지막 밤이 점화의 밤
        public const int MaxConsecutiveLosses = 2;                // 연속 두 번 지면 패배 엔딩 (9.3)
        public const string RunCompleteStoryId = "story_run1_complete";   // 1회차 완료 이야기 (글은 조정 담당)

        // ── 등대 복구 ────────────────────────────────────────
        public const float RepairMax = 100f;
        public const float GainLow = 20f, GainNormal = 35f, GainHigh = 50f;   // 밝기별 밤당 기본 복구 (9.4)
        public const float WallReadyRatio = 0.9f;                 // 점화 조건: 4면 성벽 모두 체력 90% 이상
        public const float ReadyEpsilon = 0.01f;                  // 부동소수 비교 여유

        // 낮 '등대 수리' (정비 행동). 시작값 — 측정 뒤 조정
        public const int DayRepairGold = 200;                     // 이 값 골드로
        public const float DayRepairAmount = 10f;                 // 이만큼 복구

        // ── 수비대 (2.4 A안) ─────────────────────────────────
        public const int GarrisonPercent = 20;                    // 이긴 요새에 남기는 살아남은 병력 비율
        public const int SupportGoldPerSoldier = 2;               // 낮마다 수비대 1명당 지원금

        // 밤 번호(이긴 밤 수 + 1) → 기존 전투 1~4번 자리. 1·2·3·4·4 (5번째 이후는 4번 자리 유지).
        // 요새 1 = 자리 1·2, 요새 2 = 자리 3·4(·4). 요새 2는 적 변형 단계(EnemyTier)도 오른다. 측정은 조정 담당
        public static int BattleSlot(int battleNumber, int slotCount = 4)
        {
            if (slotCount < 1) return 1;
            return Math.Max(1, Math.Min(battleNumber, slotCount));
        }

        // 화면에 보이는 이름: "요새 1 · 밤 2" (총 밤 수를 넘어간 밤은 총 수를 붙이지 않는다)
        public static string NightLabel(int fortress, int night, bool withTotal = false) =>
            withTotal && night <= NightsIn(fortress) ? $"요새 {fortress} · 밤 {night}/{NightsIn(fortress)}" : $"요새 {fortress} · 밤 {night}";

        public static int NightsIn(int fortress)
        {
            int i = Math.Max(1, fortress) - 1;
            return NightsByFortress[Math.Min(i, NightsByFortress.Length - 1)];
        }

        public static int TotalNights
        {
            get { int sum = 0; foreach (int n in NightsByFortress) sum += n; return sum; }
        }

        // 요새별 복구 비례 상수: 보통 밝기로 이 요새의 밤 수만큼 지나면 정확히 100
        public static float FortressScale(int fortress) => RepairMax / (NightsIn(fortress) * GainNormal);

        // 밝기별 복구 기본값
        public static float BaseGain(LampBrightness lamp)
        {
            switch (lamp)
            {
                case LampBrightness.Low: return GainLow;
                case LampBrightness.High: return GainHigh;
                default: return GainNormal;
            }
        }

        // 이긴 밤의 등대 복구 증가 = Σ (밝기별 켠 시간 비율 × 기본값) × 요새 비례 상수.
        // LampSeconds가 비었거나 합이 0이면 (전투 쪽이 아직 안 채움) 밤 전체를 보통으로 본다.
        public static float RepairGain(float[] lampSeconds, int fortress)
        {
            float scale = FortressScale(fortress);
            float total = 0f;
            if (lampSeconds != null)
                for (int i = 0; i < 3 && i < lampSeconds.Length; i++) total += Math.Max(0f, lampSeconds[i]);
            if (total <= 0f) return GainNormal * scale;

            float gain = 0f;
            for (int i = 0; i < 3 && i < lampSeconds.Length; i++)
                gain += Math.Max(0f, lampSeconds[i]) / total * BaseGain((LampBrightness)i);
            return gain * scale;
        }

        // 점화의 밤에 들어갈 수 있는 복구 선: 보통 밝기로 마지막 밤 바로 앞까지 쌓이는 값 (= 100 − 보통 한 밤 몫).
        // 센 밝기·낮 수리로 이 선을 넘으면 점화의 밤이 하루 일찍 온다 (9.3, B5)
        public static float IgnitionRepairLine(int fortress) => RepairMax - RepairMax / NightsIn(fortress);

        public static bool RepairReady(CampaignState campaign) =>
            campaign != null && campaign.BeaconRepair + ReadyEpsilon >= IgnitionRepairLine(campaign.FortressNumber);

        // 4면 성벽이 모두 90% 이상 (정수 비교)
        public static bool WallsReady(CampaignState campaign)
        {
            if (campaign?.Wall == null || campaign.Wall.Count == 0) return false;
            foreach (WallSegmentState wall in campaign.Wall)
                if (wall == null || wall.MaxHp <= 0 || wall.Hp * 10L < wall.MaxHp * 9L) return false;
            return true;
        }

        // 다음 밤이 점화의 밤인가: 복구가 선을 넘고 성벽도 90% 이상일 때만
        public static bool IsIgnitionNight(CampaignState campaign) => RepairReady(campaign) && WallsReady(campaign);

        // 점화에 모자란 것을 알림으로 (정비 화면). 둘 다 되면 null
        public static string MissingForIgnition(CampaignState campaign)
        {
            bool repair = RepairReady(campaign), walls = WallsReady(campaign);
            if (repair && walls) return null;
            if (!repair && !walls) return "등대 복구와 성벽 90%가 모자랍니다";
            return repair ? "성벽 90%가 모자랍니다" : "등대 복구가 모자랍니다";
        }

        // 적 변형 단계: 요새 1 = 0 홀림, 요새 2 = 1 물듦, 요새 2 점화의 밤 = 2 변형 (9.5)
        public static int EnemyTier(int fortress, bool ignitionNight)
        {
            if (fortress <= 1) return 0;
            return ignitionNight ? 2 : 1;
        }

        // ── 낮 ───────────────────────────────────────────────

        // 낮이 시작될 때 수비대 지원금 (인원당 SupportGoldPerSoldier). 받은 값을 LastSupportGold에 남긴다
        public static int BeginDay(CampaignState campaign)
        {
            int support = GarrisonSoldiers(campaign) * SupportGoldPerSoldier;
            campaign.Gold += support;
            campaign.LastSupportGold = support;
            return support;
        }

        public static int GarrisonSoldiers(CampaignState campaign)
        {
            int sum = 0;
            if (campaign?.Garrisons != null) foreach (GarrisonState g in campaign.Garrisons) sum += Math.Max(0, g.Count);
            return sum;
        }

        // 낮 '등대 수리': 골드를 쓰고 복구를 더한다 (100에서 멈춤). 실패하면 이유
        public static bool TryBuyRepair(CampaignState campaign, out string reason)
        {
            reason = null;
            if (campaign.BeaconRepair >= RepairMax) { reason = "등대가 이미 모두 복구됐습니다"; return false; }
            if (campaign.Gold < DayRepairGold) { reason = $"골드 부족: 필요 {DayRepairGold:N0}, 보유 {campaign.Gold:N0}"; return false; }
            campaign.Gold -= DayRepairGold;
            campaign.BeaconRepair = Math.Min(RepairMax, campaign.BeaconRepair + DayRepairAmount);
            return true;
        }

        // ── 밤 결과 ──────────────────────────────────────────

        // 이긴 밤 반영 (결과 화면이 전투 결과 반영 뒤 한 번 부른다. 같은 실행 ID는 두 번 반영하지 않는다).
        // 복구 증가 · 연속 패배 0 · 밤 번호 증가. 점화의 밤이면 요새 점화(수비대 남김) 뒤 다음 요새로 (또는 1회차 완료)
        public static void ApplyWin(CampaignState campaign, BattleSetup setup, BattleResult result)
        {
            if (campaign == null || result == null || setup == null) return;
            if (!string.IsNullOrEmpty(result.BattleRunId) && campaign.LastNightRunId == result.BattleRunId) return;

            campaign.LastNightRunId = result.BattleRunId;
            campaign.LastNightFortress = campaign.FortressNumber;
            campaign.LastNightIndex = campaign.NightNumber;
            campaign.ConsecutiveNightLosses = 0;
            campaign.LastNightGain = 0f;
            campaign.LastNightRelit = false;
            campaign.LastNightGarrison = 0;

            if (setup.IsIgnitionNight)
            {
                Relight(campaign);
                return;
            }

            float gain = RepairGain(result.LampSeconds, campaign.FortressNumber);
            float before = campaign.BeaconRepair;
            campaign.BeaconRepair = Math.Min(RepairMax, before + gain);
            campaign.LastNightGain = campaign.BeaconRepair - before;
            campaign.NightNumber++;
        }

        // 진 밤 반영 (App이 한 번 부른다): 복구 절반(내림), 연속 패배 +1. 연속 한도에 닿으면 true (패배 엔딩)
        public static bool ApplyLoss(CampaignState campaign)
        {
            campaign.BeaconRepair = (float)Math.Floor(campaign.BeaconRepair / 2f);
            campaign.ConsecutiveNightLosses++;
            return campaign.ConsecutiveNightLosses >= MaxConsecutiveLosses;
        }

        // 진 밤 다음 낮 시작 (W11): 무너졌거나 심하게 상한 성벽을 최대 체력의 percent%까지 되돌린다 (이미 그 이상이면 그대로).
        // percent는 전투 규칙 데이터(BattleRulesDef.LostNightWallFloorPercent)에서 온다. 바뀐 성벽 수를 돌려준다
        public static int RaiseWallFloor(CampaignState campaign, int percent)
        {
            if (campaign?.Wall == null || percent <= 0) return 0;
            int changed = 0;
            foreach (WallSegmentState wall in campaign.Wall)
            {
                if (wall == null || wall.MaxHp <= 0) continue;
                int floor = (int)(((long)wall.MaxHp * Math.Min(100, percent) + 99) / 100);
                if (wall.Hp >= floor) continue;
                wall.Hp = floor;
                changed++;
            }

            return changed;
        }

        // 지금 진 밤이 연속 한도에 닿는가 (반영 전 판단용)
        public static bool LossEndsRun(CampaignState campaign) => campaign.ConsecutiveNightLosses + 1 >= MaxConsecutiveLosses;

        // 요새 점화 성공: 수비대 남김 → 다음 요새(밤 1·복구 0·성벽 가득) 또는 1회차 완료
        static void Relight(CampaignState campaign)
        {
            campaign.LastNightRelit = true;
            int left = TakeGarrison(campaign.Squads, GarrisonPercent);
            campaign.LastNightGarrison = left;
            if (left > 0)
            {
                GarrisonState entry = campaign.Garrisons.Find(g => g.FortressNumber == campaign.FortressNumber);
                if (entry == null) campaign.Garrisons.Add(new GarrisonState { FortressNumber = campaign.FortressNumber, Count = left });
                else entry.Count += left;
            }

            campaign.BeaconRepair = RepairMax;
            if (campaign.FortressNumber >= FortressCount)
            {
                campaign.NextStory = new StoryRequest { Kind = StoryKind.RunComplete, StoryId = RunCompleteStoryId };
                return;
            }

            // 새 요새: 성벽 가득, 복구 0, 밤 1. 새 지형 = 지형 씨앗을 바꿔 무작위 지형을 다시 만든다 (지형 손질·설치 함정은 옛 요새에 남음).
            // 회수해 둔 함정(TrapStock)·상점 재고·병력·영웅은 그대로 들고 간다
            campaign.FortressNumber++;
            campaign.NightNumber = 1;
            campaign.BeaconRepair = 0f;
            campaign.PlannedLamp = LampBrightness.Normal;
            foreach (WallSegmentState wall in campaign.Wall) wall.Hp = wall.MaxHp;
            campaign.Traps.Clear();
            campaign.TerrainWorks.Clear();
            campaign.RngSeed = unchecked(campaign.RngSeed * 1103515245 + 12345 + campaign.FortressNumber * 7919);
        }

        // 살아남은 일반 병력의 percent%(내림)를 분대에서 비율대로 떼어 낸다. 큰 몫 우선 배분, 한 분대를 0으로 만들지는 않는다.
        // 인원이 0인 분대는 목록에서 뺀다. 떼어 낸 인원 수를 돌려준다
        public static int TakeGarrison(List<SquadState> squads, int percent)
        {
            if (squads == null) return 0;
            squads.RemoveAll(s => s == null || s.Count <= 0);
            long total = 0;
            foreach (SquadState s in squads) total += s.Count;
            long target = total * percent / 100;
            if (target <= 0) return 0;

            int n = squads.Count;
            var take = new int[n];
            var frac = new long[n];   // 나머지 (분대 인원 × percent % 100)
            long given = 0;
            for (int i = 0; i < n; i++)
            {
                long scaled = (long)squads[i].Count * percent;
                take[i] = (int)Math.Min(scaled / 100, squads[i].Count - 1);
                frac[i] = scaled % 100;
                given += take[i];
            }

            // 모자란 인원은 나머지가 큰 분대부터 한 명씩
            while (given < target)
            {
                int best = -1;
                for (int i = 0; i < n; i++)
                {
                    if (take[i] >= squads[i].Count - 1) continue;
                    if (best < 0 || frac[i] > frac[best]) best = i;
                }
                if (best < 0) break;   // 더 뗄 수 있는 분대가 없음
                take[best]++;
                frac[best] = -1;
                given++;
            }

            for (int i = 0; i < n; i++) squads[i].Count -= take[i];
            squads.RemoveAll(s => s.Count <= 0);
            return (int)given;
        }
    }
}
