using HumanLastRampart.Core.Campaign;
using UnityEngine;

namespace HumanLastRampart.Tutorial
{
    // 정비 화면 쪽 튜토리얼 읽기: 함정은 진행 데이터의 설치 목록 수가 늘어나면 놓은 것 (거래가 목록을 바꿔 끼우므로 매번 새로 읽는다)
    public sealed class PrepTutorialSource : ITutorialSource
    {
        readonly CampaignState campaign;
        readonly Transform owner;

        PrepTutorialSource(CampaignState campaign, Transform owner) { this.campaign = campaign; this.owner = owner; }

        // 정비 입구(PrepEntry)에서 한 줄로 부른다. 튜토리얼 대상이 아니면 아무것도 하지 않는다
        public static TutorialDirector TryBegin(Transform owner, CampaignState campaign)
        {
            if (campaign == null || !TutorialState.ShouldStartPrep(campaign.BattleNumber)) return null;
            return TutorialDirector.Begin(owner, new PrepTutorialSource(campaign, owner), TutorialStep.Trap, TutorialStep.Lens);
        }

        public bool Alive => owner != null && campaign != null;
        public void Tick(float dt) { }
        public float WalkedMeters => 0f;
        public bool MapOpen => false;
        public int SquadSends => 0;
        public int SpellCasts => 0;
        public int TrapCount => campaign.Traps != null ? campaign.Traps.Count : 0;
        public bool IsAvailable(TutorialStep step) => true;
        public bool TryGetTargetScreen(TutorialStep step, out Vector2 screen) { screen = default; return false; }
        public void SetTimeScale(float scale) { }
    }
}
