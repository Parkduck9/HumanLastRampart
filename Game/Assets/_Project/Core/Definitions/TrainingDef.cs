using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 병종 단체 훈련 한 단계 (K-058, D-079 U4 고정가·U5 병종 전체). 단계는 1부터 차례로만 올린다.
    // 효과는 쌓인다: 3단계 병종 = 1·2·3단계 효과 모두
    [CreateAssetMenu(menuName = "HLR/정의/병종 훈련", fileName = "TrainingDef")]
    public sealed class TrainingDef : DefinitionBase
    {
        [SerializeField] string unitDefId;
        [SerializeField] int level = 1;
        [SerializeField] int price = 300;
        [SerializeField, TextArea] string description;
        [SerializeField] List<UpgradeEffect> effects = new List<UpgradeEffect>();

        public string UnitDefId => unitDefId;
        public int Level => level;
        public int Price => price;
        public string Description => description;
        public IReadOnlyList<UpgradeEffect> Effects => effects;
    }
}
