using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using UnityEngine;

namespace HumanLastRampart.Tutorial
{
    // 정비 화면 쪽 튜토리얼 읽기: 함정은 진행 데이터의 설치 목록 수가 늘어나면 놓은 것 (거래가 목록을 바꿔 끼우므로 매번 새로 읽는다)
    public sealed class PrepTutorialSource : ITutorialSource
    {
        readonly CampaignState campaign;
        readonly Transform owner;

        readonly IDefinitionLookup definitions;

        readonly System.Collections.Generic.Dictionary<string, int> trapPrices = new System.Collections.Generic.Dictionary<string, int>();   // 한 번만 만든다 (모르는 번호는 조용히 건너뛰기 위해 Get 대신 사용)
        bool canTrap;   // 매 프레임 Tick에서 한 번만 다시 계산한 값

        PrepTutorialSource(CampaignState campaign, Transform owner, IDefinitionLookup definitions)
        {
            this.campaign = campaign; this.owner = owner; this.definitions = definitions;
            if (definitions != null) foreach (TrapDef def in definitions.All<TrapDef>()) if (def != null) trapPrices[def.Id] = def.Price;
            canTrap = ComputeCanTrap();
        }

        // 정비 입구(PrepEntry)에서 한 줄로 부른다. 튜토리얼 대상이 아니면 아무것도 하지 않는다
        public static TutorialDirector TryBegin(Transform owner, CampaignState campaign, IDefinitionLookup definitions)
        {
            if (campaign == null || !TutorialState.ShouldStartPrep(campaign.BattleNumber)) return null;
            return TutorialDirector.Begin(owner, new PrepTutorialSource(campaign, owner, definitions), TutorialStep.Trap, TutorialStep.Lens);
        }

        // 함정 놓기는 하나라도 놓을 수 있을 때만 시킨다: 보관품이 있거나, 상점 재고를 지금 골드로 살 수 있을 때. 아니면 설명만 한다
        public bool CanPerform(TutorialStep step) => step != TutorialStep.Trap || canTrap;   // Tick이 갱신한 값을 돌려준다 (하루 몇 번씩 불려도 가볍게)

        bool ComputeCanTrap()
        {
            if (campaign.TrapStock != null)
                foreach (TrapStockEntry e in campaign.TrapStock) if (e != null && e.Count > 0) return true;
            if (campaign.TrapShop != null)
                foreach (TrapStockEntry e in campaign.TrapShop)
                {
                    if (e == null || e.Count <= 0 || e.TrapDefId == null) continue;
                    if (trapPrices.TryGetValue(e.TrapDefId, out int price) && price <= campaign.Gold) return true;   // 모르는 함정 번호는 조용히 건너뛴다
                }

            return false;
        }

        public bool Alive => owner != null && campaign != null;
        public void Tick(float dt) { if (campaign != null) canTrap = ComputeCanTrap(); }
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
