using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;

namespace HumanLastRampart.App.Data
{
    // 새 게임의 진행 데이터 만들기 (C-004 v1~v1.3, D-015 D3·D5)
    public static class CampaignFactory
    {
        public static CampaignState Create(IDefinitionLookup defs, NewGameOptions options, int seed)
        {
            IReadOnlyList<CampaignStartDef> starts = defs.All<CampaignStartDef>();
            CampaignStartDef start = starts.Count > 0 ? starts[0] : null;
            if (start == null)
            {
                return null;
            }

            var campaign = new CampaignState
            {
                BattleNumber = 1,
                Gold = start.StartGold,
                Difficulty = options.Difficulty,
                RngSeed = seed,
                NextStory = new StoryRequest { Kind = StoryKind.Normal, StoryId = start.FirstStoryId },
            };

            foreach (WallStart wall in start.Walls)
            {
                campaign.Wall.Add(new WallSegmentState { SegmentId = WallIds.Of(wall.Side), MaxHp = wall.MaxHp, Hp = wall.MaxHp });
            }

            foreach (HeroStart hero in start.Heroes)
            {
                UnitDef unit = defs.Get<UnitDef>(hero.UnitDefId);
                campaign.Heroes.Add(new HeroState
                {
                    InstanceId = campaign.NewInstanceId("hero"),
                    UnitDefId = hero.UnitDefId,
                    Hp = unit != null ? unit.MaxHp : 1,
                    IsAlive = true,
                    PostId = string.IsNullOrEmpty(hero.PostId) ? null : hero.PostId,
                });
            }

            foreach (SquadStart squad in start.Squads)
            {
                campaign.Squads.Add(new SquadState
                {
                    SquadId = campaign.NewInstanceId("squad"),
                    UnitDefId = squad.UnitDefId,
                    Count = squad.Count,
                    PostId = string.IsNullOrEmpty(squad.PostId) ? null : squad.PostId,
                });
            }

            return campaign;
        }
    }
}