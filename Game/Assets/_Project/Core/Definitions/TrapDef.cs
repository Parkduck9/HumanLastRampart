using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 함정 (C-008, C-010 v1.1 P1-C14). 1회차는 일회용 폭발 1종 (D-018).
    [CreateAssetMenu(menuName = "HLR/정의/함정", fileName = "TrapDef")]
    public sealed class TrapDef : DefinitionBase
    {
        [SerializeField] int price = 25;               // D-050
        [SerializeField] float triggerRadius = 2f;     // 저절로 터짐 판정 거리 (D-050: 점화식, 저절로는 1%)
        [SerializeField] float damageRadius = 6f;      // D-050 (T2)
        [SerializeField] int damage = 300;             // 고블린 30 한 방, 오우거 600 절반
        [SerializeField] bool consumeOnTrigger = true; // 1회차는 true만 허용 (C-008)
        [SerializeField] float detectRadius = 6f;      // 회수 병력이 알아채는 거리
        [SerializeField] float recoverRisk = 1.5f;     // 확인 못 한 채 이 거리 안을 지나면 폭발 위험

        [Header("점화식 (D-050)")]
        [SerializeField] float fuseSeconds = 0.5f;     // 누른 뒤 터지기까지
        [SerializeField] float dudChance = 0.01f;      // 눌러도 안 터짐 (묻힌 채 남음)
        [SerializeField] float autoChance = 0.01f;     // 적이 처음 발동 거리에 닿을 때 저절로 터짐 (한 번만 판정)
        [SerializeField] float craterRadius = 3f;      // 터진 자리 구덩이
        [SerializeField] float craterSpeedMultiplier = 0.7f;   // 구덩이를 지나는 적 이동 속도

        [Header("상점 재고 (D-050, 대장장이가 적음)")]
        [SerializeField] int shopStockStart = 6;
        [SerializeField] int shopStockPerPrep = 3;     // 정비마다 (전투 번호당 한 번)
        [SerializeField] int shopStockMax = 12;

        public float FuseSeconds => fuseSeconds;
        public float DudChance => dudChance;
        public float AutoChance => autoChance;
        public float CraterRadius => craterRadius;
        public float CraterSpeedMultiplier => craterSpeedMultiplier;
        public int ShopStockStart => shopStockStart;
        public int ShopStockPerPrep => shopStockPerPrep;
        public int ShopStockMax => shopStockMax;

        public int Price => price;
        public float TriggerRadius => triggerRadius;
        public float DamageRadius => damageRadius;
        public int Damage => damage;
        public bool ConsumeOnTrigger => consumeOnTrigger;
        public float DetectRadius => detectRadius;
        public float RecoverRisk => recoverRisk;
    }
}