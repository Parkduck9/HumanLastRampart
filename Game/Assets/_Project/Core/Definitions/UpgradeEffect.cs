using System;
using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 강화 효과 종류 (K-058, D-079, Upgrade_Shop.md). 값의 뜻은 종류마다 다르다:
    // 배율(×) = 1 + 값 (예: 0.4 → ×1.4, −0.1 → ×0.9) / 더하기(+) = 그대로 더함 (m·초·확률)
    public enum UpgradeStat
    {
        TelescopeRadius,    // × 망원경 반경 (주인공)
        MessengerSpeed,     // × 전령 달리기
        HeroWallInterval,   // × 주인공이 서 있는 성벽 병사 공격 간격 (−0.1 = 10% 빨라짐)
        ShoutMisheard,      // × 소리 지르기 잘못 들을 확률
        ExplosionDamage,    // × 폭발 마법 피해
        ExplosionRadius,    // + 폭발 마법 반경(m)
        FireDuration,       // + 불바다 지속(초)
        FireBurnChance,     // + 불바다 화상 확률
        SpellCooldown,      // × 모든 마법 재사용 대기
        UnitDamage,         // × 병종 무기 피해 (unitDefId)
        UnitInterval,       // × 병종 공격 간격
        UnitRange,          // + 병종 사거리(m)
        UnitAreaRadius,     // + 병종 범위(m, 대포)
        UnitAccuracyNear,   // + 병종 가까운 거리 명중
        UnitAccuracyFar,    // + 병종 먼 거리 명중
        UnitBurnChance,     // + 병종 화살·총알 화상 확률 (불화살)
        RecoverLost,        // + 승리한 전투에서 잃은 병사 중 돌아오는 비율 (군의관 천막)
        WallGuard,          // × 성벽 위 적이 병사에게 주는 피해 (방패 난간, 그 성벽만)
    }

    [Serializable]
    public struct UpgradeEffect
    {
        public UpgradeStat Stat;
        public float Value;
        public string UnitDefId;   // Unit* 종류만: 대상 병종

        public UpgradeEffect(UpgradeStat stat, float value, string unitDefId = null)
        {
            Stat = stat;
            Value = value;
            UnitDefId = unitDefId;
        }

        public static bool IsAdditive(UpgradeStat stat) =>
            stat == UpgradeStat.ExplosionRadius || stat == UpgradeStat.FireDuration || stat == UpgradeStat.FireBurnChance
            || stat == UpgradeStat.UnitRange || stat == UpgradeStat.UnitAreaRadius || stat == UpgradeStat.UnitAccuracyNear
            || stat == UpgradeStat.UnitAccuracyFar || stat == UpgradeStat.UnitBurnChance || stat == UpgradeStat.RecoverLost;

        public static bool IsUnitStat(UpgradeStat stat) =>
            stat == UpgradeStat.UnitDamage || stat == UpgradeStat.UnitInterval || stat == UpgradeStat.UnitRange || stat == UpgradeStat.UnitAreaRadius
            || stat == UpgradeStat.UnitAccuracyNear || stat == UpgradeStat.UnitAccuracyFar || stat == UpgradeStat.UnitBurnChance;
    }
}
