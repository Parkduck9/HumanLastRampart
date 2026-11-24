using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using UnityEngine;

namespace HumanLastRampart.BeaconMap
{
    public enum FortressMapState
    {
        Fogged,   // 아직 모르는 요새: 어둡고 안개가 덮여 있다
        Dead,     // 찾았지만 불이 꺼진 요새: 차갑고 불빛이 없다 (지금 지키는 곳·다음 목적지)
        Lit,      // 점화한 요새: 따뜻한 불빛
    }

    public struct FortressNode
    {
        public int Number;              // 요새 번호 (1부터)
        public Vector2 Site;            // 모형 위 자리 (x, z 미터)
        public FortressMapState State;
        public int Garrison;            // 남은 수비대 (점화한 요새만)
        public int Night, Nights;       // 지금 지키는 요새의 밤 진행 (Night/Nights). 지키는 요새가 아니면 0
    }

    // 봉화선 지도에 보여 줄 것 (W10, Concept_Beacon_v1 9.3·9.7). 진행 데이터 → 요새별 상태. 순수 계산이라 자동 시험으로 확인한다.
    public sealed class BeaconMapModel
    {
        // 요새 자리 (1회차 1·2번 + 앞으로 안개 속 3개). 길은 왼쪽 아래에서 오른쪽 위로 굽이친다
        public static readonly Vector2[] Sites =
        {
            new Vector2(-8.3f, -1.8f),
            new Vector2(-2.2f, 3.8f),
            new Vector2(3.8f, -1.6f),
            new Vector2(9.6f, 4.4f),
            new Vector2(14.2f, -1.0f),
        };

        public List<FortressNode> Nodes = new List<FortressNode>();
        public int LitCount;            // 점화한 요새 수 (1번부터 차례로)
        public bool RunComplete;        // 1회차 마지막 요새까지 점화
        public bool Preview;            // 정비에서 열어 본 보기 전용
        public string Title;            // "빛의 봉화선"
        public string Subtitle;         // "요새 1 · 다음: 요새 2"

        public static BeaconMapModel Build(CampaignState campaign, bool preview)
        {
            var model = new BeaconMapModel { Preview = preview, Title = "빛의 봉화선" };
            int fortressNow = campaign != null ? Mathf.Max(1, campaign.FortressNumber) : 1;
            bool complete = campaign?.NextStory != null && campaign.NextStory.Kind == StoryKind.RunComplete;
            model.RunComplete = complete;

            // 점화한 요새: 지금 요새 앞 번호들. 마지막 요새를 점화한 뒤에는 번호가 그대로라 그 요새까지
            int lit = complete ? fortressNow : fortressNow - 1;
            lit = Mathf.Clamp(lit, 0, BeaconRules.FortressCount);
            model.LitCount = lit;

            for (int i = 0; i < Sites.Length; i++)
            {
                int number = i + 1;
                FortressMapState state = number <= lit ? FortressMapState.Lit
                    : number == lit + 1 && number <= BeaconRules.FortressCount ? FortressMapState.Dead
                    : FortressMapState.Fogged;
                bool defended = state == FortressMapState.Dead && number == fortressNow && !complete;
                int nights = BeaconRules.NightsIn(number);
                model.Nodes.Add(new FortressNode
                {
                    Number = number,
                    Site = Sites[i],
                    State = state,
                    Garrison = state == FortressMapState.Lit ? GarrisonOf(campaign, number) : 0,
                    Night = defended ? Mathf.Clamp(campaign != null ? campaign.NightNumber : 1, 1, nights) : 0,
                    Nights = defended ? nights : 0,
                });
            }

            // 머리글: 방금 불을 켠 요새(보기 전용이면 지금 지키는 요새)와 다음 요새
            int focus = complete ? fortressNow : preview ? fortressNow : Mathf.Max(1, fortressNow - 1);
            if (complete) model.Subtitle = $"요새 {focus} · 1회차 완료";
            else if (focus >= BeaconRules.FortressCount) model.Subtitle = $"요새 {focus} · 마지막 요새";   // 다음 요새가 없다
            else model.Subtitle = $"요새 {focus} · 다음: 요새 {focus + 1}";

            return model;
        }

        public static int GarrisonOf(CampaignState campaign, int fortress)
        {
            if (campaign?.Garrisons == null) return 0;
            int sum = 0;
            foreach (GarrisonState g in campaign.Garrisons)
                if (g != null && g.FortressNumber == fortress) sum += Mathf.Max(0, g.Count);
            return sum;
        }
    }
}
