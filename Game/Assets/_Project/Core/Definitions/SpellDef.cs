using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 마법사 특수 공격 (C-010 v1.2). 1회차: 폭발, 불바다 (D-027 Q3)
    [CreateAssetMenu(menuName = "HLR/정의/마법", fileName = "SpellDef")]
    public sealed class SpellDef : DefinitionBase
    {
        [SerializeField] float radius = 10f;
        [SerializeField] int damage = 100;           // 폭발: 한 번 / 불바다: 초당
        [SerializeField] float duration;             // 0이면 즉시(폭발), 0보다 크면 지속(불바다)
        [SerializeField] float cooldown = 30f;
        [SerializeField] bool isFire;                // 비 오면 피해 감소 대상 (D-027 Q5)
        // 화상 (K-040): 불바다에서 처음 맞을 때 한 번 burnChance 확률로 몸에 불이 붙어 burnSeconds 동안 초당 burnDamage.
        // 불바다를 벗어나도 계속 탄다. 0이면 화상 없음
        [SerializeField] float burnChance;
        [SerializeField] float burnDamage;
        [SerializeField] float burnSeconds;

        public float Radius => radius;
        public int Damage => damage;
        public float Duration => duration;
        public float Cooldown => cooldown;
        public bool IsFire => isFire;
        public float BurnChance => burnChance;
        public float BurnDamage => burnDamage;
        public float BurnSeconds => burnSeconds;
    }
}