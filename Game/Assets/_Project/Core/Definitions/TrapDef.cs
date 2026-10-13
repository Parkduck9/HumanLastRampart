using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 함정 (C-008, C-010 v1.1 P1-C14). 1회차는 일회용 폭발 1종 (D-018).
    [CreateAssetMenu(menuName = "HLR/정의/함정", fileName = "TrapDef")]
    public sealed class TrapDef : DefinitionBase
    {
        [SerializeField] int price = 10;
        [SerializeField] float triggerRadius = 2f;     // 적이 이 안에 들어오면 발동
        [SerializeField] float damageRadius = 4f;
        [SerializeField] int damage = 50;
        [SerializeField] bool consumeOnTrigger = true; // 1회차는 true만 허용 (C-008)
        [SerializeField] float detectRadius = 6f;      // 회수 병력이 알아채는 거리
        [SerializeField] float recoverRisk = 1.5f;     // 확인 못 한 채 이 거리 안을 지나면 폭발 위험

        public int Price => price;
        public float TriggerRadius => triggerRadius;
        public float DamageRadius => damageRadius;
        public int Damage => damage;
        public bool ConsumeOnTrigger => consumeOnTrigger;
        public float DetectRadius => detectRadius;
        public float RecoverRisk => recoverRisk;
    }
}