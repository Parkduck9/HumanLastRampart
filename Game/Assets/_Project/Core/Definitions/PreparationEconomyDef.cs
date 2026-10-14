using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 정비 모집·수리 가격과 정원 (C-017, D-042 후보 A). 정확히 1개, Id = economy_preparation_1 (데이터 에셋은 Codex Data/Meta).
    // 정비 견적의 유일한 가격 원본이다 — UnitDef.RecruitCost로 되돌아가지 않는다.
    [CreateAssetMenu(menuName = "HLR/정의/정비 경제", fileName = "PreparationEconomyDef")]
    public sealed class PreparationEconomyDef : DefinitionBase
    {
        public const string ExpectedId = "economy_preparation_1";

        [Header("모집 단계 (전체 일반 병력 기준, 구매 직전 인원으로 1명씩)")]
        [SerializeField] int[] tierStartCounts = { 0, 600, 1200 };
        [SerializeField] int[] archerPrices = { 5, 8, 12 };
        [SerializeField] int[] gunnerPrices = { 8, 12, 18 };
        [SerializeField] int[] cannonCrewPrices = { 10, 15, 20 };

        [Header("정원")]
        [SerializeField] int globalCapacity = 2000;
        [SerializeField] int wallCapacity = 500;
        [SerializeField] int squadPostCapacity = 160;
        [SerializeField] int cannonCrewPostCapacity = 5;

        [Header("수리")]
        [SerializeField] int repairHpPerGold = 10;
        [SerializeField] int defaultRepairHp = 100;

        public IReadOnlyList<int> TierStartCounts => tierStartCounts;
        public IReadOnlyList<int> ArcherPrices => archerPrices;
        public IReadOnlyList<int> GunnerPrices => gunnerPrices;
        public IReadOnlyList<int> CannonCrewPrices => cannonCrewPrices;
        public int GlobalCapacity => globalCapacity;
        public int WallCapacity => wallCapacity;
        public int SquadPostCapacity => squadPostCapacity;
        public int CannonCrewPostCapacity => cannonCrewPostCapacity;
        public int RepairHpPerGold => repairHpPerGold;
        public int DefaultRepairHp => defaultRepairHp;

        // 병종별 단계 가격. 영웅·마법사 등 정비에서 사지 않는 병종은 null
        public IReadOnlyList<int> PricesFor(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Archer: return archerPrices;
                case UnitKind.Gunner: return gunnerPrices;
                case UnitKind.CannonCrew: return cannonCrewPrices;
                default: return null;
            }
        }
    }
}
