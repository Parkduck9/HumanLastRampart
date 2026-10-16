using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;

namespace HumanLastRampart.App.Data
{
    // 전투 준비 정보 만들기 (C-005 r2·v1.1·v1.2). 진행 데이터의 복사본만 담는다.
    public static class BattleSetupFactory
    {
        // 실패하면 null과 이유
        public static BattleSetup Create(CampaignState campaign, IDefinitionLookup defs, string runId, out string error)
        {
            error = null;
            IReadOnlyList<CampaignStartDef> starts = defs.All<CampaignStartDef>();
            IReadOnlyList<BattleRulesDef> rulesList = defs.All<BattleRulesDef>();
            if (starts.Count == 0 || rulesList.Count == 0)
            {
                error = "시작 설정 또는 전투 규칙이 없습니다";
                return null;
            }

            CampaignStartDef start = starts[0];
            BattleRulesDef rules = rulesList[0];
            string waveId = start.WaveFor(campaign.BattleNumber);
            if (waveId == null)
            {
                error = $"전투 {campaign.BattleNumber}의 웨이브가 없습니다";
                return null;
            }

            bool easy = campaign.Difficulty == Difficulty.Easy;
            var setup = new BattleSetup
            {
                BattleRunId = runId,
                BattleNumber = campaign.BattleNumber,
                WaveDefId = waveId,
                TerrainId = start.TerrainFor(campaign.BattleNumber),
                Difficulty = campaign.Difficulty,
                EnemyCountMultiplier = easy ? rules.EasyEnemyMultiplierFor(campaign.BattleNumber) : rules.RealisticEnemyMultiplierFor(campaign.BattleNumber),
                MaxConcurrentEnemies = easy ? rules.MaxConcurrentEnemiesEasy : rules.MaxConcurrentEnemiesRealistic,
                Forecast = BattleForecast.Get(campaign.RngSeed, campaign.BattleNumber, rules),
                InitialTargeting = (campaign.Targeting ?? TargetingSettings.CreateDefault()).Clone(),
            };

            // 참가 영웅 (C-005 v1.3, D-044 M7): 살아 있고 배치됐거나, 주인공·마법사는 배치 지점 없이도 참가
            foreach (HeroState hero in campaign.Heroes)
            {
                setup.Heroes.Add(hero.Clone());
                UnitDef unit = defs.Get<UnitDef>(hero.UnitDefId);
                bool alwaysJoins = unit != null && (unit.Kind == UnitKind.Hero || unit.Kind == UnitKind.Wizard);
                if (hero.IsAlive && (hero.PostId != null || alwaysJoins))
                {
                    setup.ParticipatingHeroIds.Add(hero.InstanceId);
                }
            }

            foreach (SquadState squad in campaign.Squads)
            {
                if (squad.Count < 0)
                {
                    error = $"분대 {squad.SquadId} 인원이 음수입니다";
                    return null;
                }

                setup.Squads.Add(squad.Clone());
            }

            foreach (WallSegmentState wall in campaign.Wall)
            {
                setup.Wall.Add(wall.Clone());
            }

            foreach (TrapPlacement trap in campaign.Traps)
            {
                if (trap.TerrainId == setup.TerrainId)
                {
                    setup.Traps.Add(trap.Clone());
                }
            }

            // 무작위 지형·지형 손질 (D-077, K-049): 씨앗과 이 지형의 손질만 넘긴다 — 전투가 같은 규칙으로 지형을 다시 만든다
            setup.TerrainSeed = campaign.RngSeed;
            if (campaign.TerrainWorks != null)
            {
                foreach (TerrainWork work in campaign.TerrainWorks)
                {
                    if (work != null && work.TerrainId == setup.TerrainId) setup.TerrainWorks.Add(work.Clone());
                }
            }

            return setup;
        }

        public static string NewRunId() => "run_" + Guid.NewGuid().ToString("N");
    }
}