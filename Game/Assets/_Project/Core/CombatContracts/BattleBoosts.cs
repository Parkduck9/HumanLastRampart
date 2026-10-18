using System;
using System.Collections.Generic;

namespace HumanLastRampart.Core.CombatContracts
{
    // 전투에 넘기는 강화 수치 (C-005 v1.7 초안, K-058). App이 진행 데이터(장착 아이템·훈련·소모품·성벽 아이템)에서 계산해 넣고,
    // Combat은 정의를 다시 찾지 않고 이 숫자만 곱하거나 더한다. 기본값 = 강화 없음
    [Serializable]
    public sealed class BattleBoosts
    {
        public float TelescopeRadiusMul = 1f;
        public float MessengerSpeedMul = 1f;
        public float HeroWallIntervalMul = 1f;   // 주인공이 서 있는 성벽 병사 공격 간격
        public float ShoutMisheardMul = 1f;
        public float ExplosionDamageMul = 1f;
        public float ExplosionRadiusAdd;
        public float FireDurationAdd;
        public float FireBurnChanceAdd;
        public float SpellCooldownMul = 1f;
        public float RecoverLostRatio;           // 결과 반영 때 쓴다 (군의관 천막)
        public List<UnitBoost> Units = new List<UnitBoost>();   // 병종마다 (일반·용병 따로)
        public List<WallBoost> Walls = new List<WallBoost>();   // 성벽마다 (방패 난간)

        public UnitBoost UnitFor(string unitDefId, bool mercenary)
        {
            foreach (UnitBoost boost in Units)
                if (boost.UnitDefId == unitDefId && boost.Mercenary == mercenary) return boost;
            return null;
        }

        public float WallGuardMul(string wallId)
        {
            foreach (WallBoost boost in Walls)
                if (boost.WallId == wallId) return boost.OnWallDamageMul;
            return 1f;
        }
    }

    [Serializable]
    public sealed class UnitBoost
    {
        public string UnitDefId;
        public bool Mercenary;          // 용병 무리용 (정예 단계)
        public int Level;               // 적용된 훈련 단계 (표시용)
        public float DamageMul = 1f;
        public float IntervalMul = 1f;
        public float RangeAdd;
        public float AreaRadiusAdd;
        public float AccuracyNearAdd;
        public float AccuracyFarAdd;
        public float BurnChance;        // 화살·총알이 맞힌 적에게 화상 (불화살)
    }

    [Serializable]
    public sealed class WallBoost
    {
        public string WallId;
        public float OnWallDamageMul = 1f;
    }
}
