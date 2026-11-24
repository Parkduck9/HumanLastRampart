using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;

namespace HumanLastRampart.App.Data
{
    // App이 승패 분기 전에 결과를 검사한다 (C-006 r2). 반영(C-007)은 결과 화면(Codex)이 한다.
    public static class BattleResultCheck
    {
        // 문제가 없으면 null
        public static string Validate(CampaignState campaign)
        {
            BattleResult result = campaign?.LastBattle;
            BattleSetup setup = campaign?.ActiveBattleSetup;
            if (result == null) return "전투 결과(LastBattle)가 없습니다";
            if (setup == null) return "전투 준비 정보(ActiveBattleSetup)가 없습니다";
            if (result.BattleRunId != setup.BattleRunId) return $"결과의 실행 ID({result.BattleRunId})가 이번 전투({setup.BattleRunId})와 다릅니다";
            if (result.BattleNumber != setup.BattleNumber) return $"결과의 전투 번호({result.BattleNumber})가 이번 전투({setup.BattleNumber})와 다릅니다";
            if (result.DefeatedEnemyCount < 0 || result.EarnedGold < 0 || result.DefeatedSpecialCount < 0) return "결과의 수치가 음수입니다";
            if (result.Outcome == BattleOutcome.Defeat && result.DefeatReason == DefeatReason.None) return "패배인데 패배 사유가 없습니다";

            // 참가 영웅마다 정확히 한 항목 (C-006). 나머지 반영 검증은 결과 화면(C-007)
            if (result.HeroOutcomes.Count != setup.ParticipatingHeroIds.Count) return $"영웅 결과 수({result.HeroOutcomes.Count})가 참가 영웅 수({setup.ParticipatingHeroIds.Count})와 다릅니다";
            foreach (string heroId in setup.ParticipatingHeroIds)
            {
                if (result.HeroOutcomes.FindAll(h => h.InstanceId == heroId).Count != 1) return $"참가 영웅 {heroId}의 결과가 한 항목이 아닙니다";
            }

            return null;
        }
    }
}