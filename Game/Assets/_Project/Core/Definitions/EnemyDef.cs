using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    [CreateAssetMenu(menuName = "HLR/정의/적", fileName = "EnemyDef")]
    public sealed class EnemyDef : DefinitionBase
    {
        [SerializeField] bool isSpecial;            // 특수 적 (오우거형, D-026 B7)
        [SerializeField] int maxHp = 30;
        [SerializeField] float moveSpeed = 4f;
        [SerializeField] int wallDamage = 1;        // 성벽에 닿았을 때 초당 피해
        [SerializeField] int rewardGold = 1;
        [SerializeField] bool carriesLadder;        // 사다리 든 변형 (D-026 B10)
        [SerializeField] float climbSpeed = 1f;
        [SerializeField] int onWallAttack = 1;      // 성벽 위에서 병사 공격력
        [SerializeField] Texture2D sheet;           // 4열×2행 (걷기 4·공격 4) 규격 — 고블린 시트 기준

        public bool IsSpecial => isSpecial;
        public int MaxHp => maxHp;
        public float MoveSpeed => moveSpeed;
        public int WallDamage => wallDamage;
        public int RewardGold => rewardGold;
        public bool CarriesLadder => carriesLadder;
        public float ClimbSpeed => climbSpeed;
        public int OnWallAttack => onWallAttack;
        public Texture2D Sheet => sheet;
    }
}