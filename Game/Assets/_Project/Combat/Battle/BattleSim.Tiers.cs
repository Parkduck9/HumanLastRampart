using HumanLastRampart.Combat.PerfLab;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 적 변형 단계 (등대 설계 9.5). 단계는 전투 전체에 하나(setup.EnemyTier)이고, 적마다 보너스를 끌 수 있다.
    // 등대 빛줄기 안의 적은 변형 보너스(체력·속도·크기·색)를 잃는다 — 빛줄기 쪽 작업이 SetTierBonusActive를 부른다.
    public sealed partial class BattleSim
    {
        // 전투 전체 단계 (0 홀림 · 1 물듦 · 2 변형)
        public int EnemyTier => EnemyTierRules.Clamp(setup.EnemyTier);

        // 적 한 마리의 원래 단계
        public int TierOf(int slot) => eTier[slot];

        // 적 한 마리의 지금 최대 체력·이동 속도 (시험·빛줄기 쪽 확인용)
        public int EnemyMaxHpOf(int slot) => eMaxHp[slot];
        public float EnemySpeedOf(int slot) => eSpeed[slot];

        // 지금 변형 보너스가 켜져 있는가 (단계 0이면 보너스가 없으니 false)
        public bool TierBonusActive(int slot) => eTier[slot] > 0 && eTierShown[slot] > 0;

        // 보너스 켜기·끄기. 끄면 체력(현재 비율 유지)·속도·크기·색이 홀림 수준으로, 다시 켜면 원래대로.
        // 그리기 작업이 끝난 뒤(Complete 후) 부른다.
        public void SetTierBonusActive(int slot, bool active)
        {
            int tier = eTier[slot];
            if (tier <= 0 || (eTierShown[slot] > 0) == active) return;

            float hpFactor = EnemyTierRules.HpMultiplier(tier);
            float speedFactor = EnemyTierRules.SpeedMultiplier(tier);
            if (!active)
            {
                hpFactor = 1f / hpFactor;
                speedFactor = 1f / speedFactor;
            }

            int oldMax = eMaxHp[slot];
            int newMax = Mathf.Max(1, Mathf.RoundToInt(oldMax * hpFactor));
            if (eHp[slot] > 0) eHp[slot] = Mathf.Clamp(Mathf.RoundToInt(eHp[slot] * hpFactor), 1, newMax);
            eMaxHp[slot] = newMax;
            eSpeed[slot] *= speedFactor;
            eTierShown[slot] = (byte)(active ? tier : 0);
        }
    }
}
