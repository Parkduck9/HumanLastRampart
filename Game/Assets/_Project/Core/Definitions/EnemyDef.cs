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
        [SerializeField] Texture2D normalSheet;     // sheet 짝 노멀맵 (같은 칸 배치, 초록 = 위, D-062 G4). 없으면 노멀맵 없이
        // 동작 그림 (K-032 미리 연결, 사용자 검토 전). 모두 칸 256×384·발 (128, 352) 4칸, 없으면 sheet 그대로
        [SerializeField] Texture2D attackSheet;     // 성벽 공격·성벽 위 싸움 (sheet에 공격 행이 없을 때)
        [SerializeField] Texture2D deathSheet;      // 쓰러짐, 마지막 칸 유지 후 사라짐
        [SerializeField] Texture2D deploySheet;     // 사다리 세우기 (오르기 시작 직후)
        [SerializeField] Texture2D climbSheet;      // 사다리 오르기 (뒷모습)
        [SerializeField] Texture2D ladderSheet;     // 사다리 소품 1칸 256×1536, 아래 (128, 1472), 실체 1152px = 4.3m

        public bool IsSpecial => isSpecial;
        public int MaxHp => maxHp;
        public float MoveSpeed => moveSpeed;
        public int WallDamage => wallDamage;
        public int RewardGold => rewardGold;
        public bool CarriesLadder => carriesLadder;
        public float ClimbSpeed => climbSpeed;
        public int OnWallAttack => onWallAttack;
        public Texture2D Sheet => sheet;
        public Texture2D NormalSheet => normalSheet;
        public Texture2D AttackSheet => attackSheet;
        public Texture2D DeathSheet => deathSheet;
        public Texture2D DeploySheet => deploySheet;
        public Texture2D ClimbSheet => climbSheet;
        public Texture2D LadderSheet => ladderSheet;
    }
}