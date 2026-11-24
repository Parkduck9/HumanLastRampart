using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 용병 (K-058, D-079 U8·U9): 정원 밖 · 정예(훈련 eliteLevel 단계와 같은 능력, 그 병종 훈련이 더 높으면 그쪽) ·
    // 고용할 때 계약금 + 전투마다 급료(전투 시작 때 자동으로 빠지고, 못 주면 그 전투 전에 떠남) · 70% 손실 패배 계산에 안 들어감
    [CreateAssetMenu(menuName = "HLR/정의/용병", fileName = "MercenaryDef")]
    public sealed class MercenaryDef : DefinitionBase
    {
        [SerializeField] string unitDefId;
        [SerializeField] int contractCost = 15;    // 1명 계약금
        [SerializeField] int wage = 3;             // 1명 전투마다 급료
        [SerializeField] int perWallCap = 50;      // 성벽마다 최대
        [SerializeField] int eliteLevel = 2;       // 훈련 단계 상당

        public string UnitDefId => unitDefId;
        public int ContractCost => contractCost;
        public int Wage => wage;
        public int PerWallCap => perWallCap;
        public int EliteLevel => eliteLevel;
    }
}
