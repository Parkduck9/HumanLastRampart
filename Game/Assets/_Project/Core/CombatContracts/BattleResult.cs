using System;
using System.Collections.Generic;

namespace HumanLastRampart.Core.CombatContracts
{
    public enum BattleOutcome
    {
        Victory,
        Defeat,
    }

    // 패배 사유 (C-006 v1.2, D-026 B1·B10)
    public enum DefeatReason
    {
        None,
        WallDestroyed,   // 성벽 체력 0
        GarrisonLost,    // 그 성벽 병력 70% 이상 사망 (뚫림)
    }

    // 전투 결과 (C-006 r2·v1.1·v1.2). Combat이 전투 종료 때 한 번 완성하고 이후 바꾸지 않는다.
    [Serializable]
    public sealed class BattleResult
    {
        public string BattleRunId;                   // 시작 BattleSetup과 같은 실행 ID
        public int BattleNumber;                     // 끝난 전투 번호
        public BattleOutcome Outcome;
        public int DefeatedEnemyCount;               // 처치 수 (특수 적 포함)
        public int DefeatedSpecialCount;             // v1.2
        public int EarnedGold;

        public DefeatReason DefeatReason;            // v1.2: 패배일 때만
        public string DefeatWallId;                  // v1.2: 패배를 일으킨 성벽

        public List<HeroOutcome> HeroOutcomes = new List<HeroOutcome>();
        public List<SquadLoss> SquadLosses = new List<SquadLoss>();
        public List<WallOutcome> WallOutcomes = new List<WallOutcome>();
        public List<string> ConsumedTrapIds = new List<string>();
        public List<TrapMove> TrapMoves = new List<TrapMove>();             // v1.1 (P1-C14)
    }

    [Serializable]
    public sealed class HeroOutcome
    {
        public string InstanceId;
        public int HpAfter;
        public bool IsAliveAfter;
    }

    [Serializable]
    public sealed class SquadLoss
    {
        public string SquadId;
        public int LostCount;
    }

    [Serializable]
    public sealed class WallOutcome
    {
        public string SegmentId;
        public int HpAfter;
    }

    [Serializable]
    public sealed class TrapMove
    {
        public string TrapId;
        public float NewX;
        public float NewZ;
    }
}