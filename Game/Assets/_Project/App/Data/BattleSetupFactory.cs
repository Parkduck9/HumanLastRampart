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
            // 빛을 잇는 자들 (W8): 밤 번호(BattleNumber)를 기존 전투 1~4번 자리로 옮겨 웨이브·지형·적 수를 정한다 (BeaconRules.BattleSlot).
            // 전투가 읽는 BattleSetup.BattleNumber도 이 자리 번호다
            int slot = BeaconRules.BattleSlot(campaign.BattleNumber, start.BattleCount);
            string waveId = start.WaveForNight(campaign.BattleNumber);
            if (waveId == null)
            {
                error = $"전투 {campaign.BattleNumber}의 웨이브가 없습니다";
                return null;
            }

            bool easy = campaign.Difficulty == Difficulty.Easy;
            bool ignition = BeaconRules.IsIgnitionNight(campaign);
            var setup = new BattleSetup
            {
                BattleRunId = runId,
                BattleNumber = slot,
                WaveDefId = waveId,
                TerrainId = start.TerrainForNight(campaign.BattleNumber),
                Difficulty = campaign.Difficulty,
                EnemyCountMultiplier = easy ? rules.EasyEnemyMultiplierFor(slot) : rules.RealisticEnemyMultiplierFor(slot),
                MaxConcurrentEnemies = easy ? rules.MaxConcurrentEnemiesEasy : rules.MaxConcurrentEnemiesRealistic,
                Forecast = BattleForecast.Get(campaign.RngSeed, campaign.BattleNumber, rules),   // 예보 방식은 다른 작업이 바꾼다
                InitialTargeting = (campaign.Targeting ?? TargetingSettings.CreateDefault()).Clone(),
                LampBrightness = campaign.PlannedLamp,
                BeamSide = 0,
                EnemyTier = BeaconRules.EnemyTier(campaign.FortressNumber, ignition),
                IsIgnitionNight = ignition,
                FortressNumber = campaign.FortressNumber,
                NightNumber = campaign.NightNumber,
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

            // 강화·용병 (K-058, D-079): 진행 데이터에서 수치를 계산해 넘긴다. 용병은 그 성벽 가운데 분대 지점에 선다
            setup.Boosts = Core.Upgrades.UpgradeRules.ComputeBoosts(campaign, defs, setup.ConsumedItemIds);
            if (campaign.Mercenaries != null)
            {
                foreach (MercenaryContract contract in campaign.Mercenaries)
                {
                    MercenaryDef merc = defs.Get<MercenaryDef>(contract.MercenaryDefId);
                    string post = MiddleSquadPost(defs, contract.WallId);
                    if (merc == null || post == null || contract.Count <= 0) continue;
                    setup.Mercenaries.Add(new SquadState { SquadId = contract.ContractId, UnitDefId = merc.UnitDefId, Count = contract.Count, PostId = post });
                }
            }

            return setup;
        }

        // 성벽 가운데에 가장 가까운 분대 지점
        static string MiddleSquadPost(IDefinitionLookup defs, string wallId)
        {
            PostDef best = null;
            foreach (PostDef post in defs.All<PostDef>())
            {
                if (post.Kind != PostKind.Squad || post.WallId != wallId) continue;
                if (best == null || Math.Abs(post.PositionAlongWall - 0.5f) < Math.Abs(best.PositionAlongWall - 0.5f)) best = post;
            }

            return best?.Id;
        }

        public static string NewRunId() => "run_" + Guid.NewGuid().ToString("N");
    }
}