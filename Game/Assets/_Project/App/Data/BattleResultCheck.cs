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
            return null;
        }
    }
}