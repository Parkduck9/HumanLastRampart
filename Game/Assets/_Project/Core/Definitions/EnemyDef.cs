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
        [SerializeField] Texture2D carrySheet;      // 사다리를 들고 다가오는 동작. 없으면 기존 걷기 그림
        [SerializeField] Texture2D ladderSideSheet; // 사다리를 옆에서 볼 때
        [SerializeField] SheetNormalPair[] poseNormals;
        // 우두머리 (성벽손 오우거, Boss_WallhandOgre_v1). 점화의 밤에만 한 마리 — 일반 웨이브에는 넣지 않는다
        [SerializeField] bool isBoss;
        [SerializeField] float drawScale = 1f;                 // 그림 크기 배율 (임시 그림 = 오우거 ×1.6)
        [SerializeField] float unlitPointDamageMul = 0.25f;    // 빛줄기 밖·통로 아래에서 한 대씩 쏘는 무기(화살·총)가 주는 피해 비율
        [SerializeField] float litSpeedMul = 1.2f;             // 빛줄기 안 걷기 배율 (화가 남). 빛줄기 느려짐은 받지 않는다
        [SerializeField] float climbAreaDamageMul = 2f;        // 기어오르는 동안 범위 피해(대포·폭발·불바다) 배율
        [SerializeField] float knockOffRatio = 0.15f;          // 한 번 오르는 동안 최대 체력의 이만큼 잃으면 떨어진다
        [SerializeField] float knockdownSeconds = 6f;          // 떨어진 뒤 쓰러져 있는 시간
        [SerializeField] float trapKnockdownSeconds = 4f;      // 함정 폭발 범위 안일 때 쓰러지는 시간
        [SerializeField] int onWallWallDamage;                 // 통로 위에서 그 성벽에 주는 초당 피해 (병사 피해는 onWallAttack)
        [SerializeField] float easyHpMul = 0.7f;               // 쉬움 난이도의 체력 배율 (변형 단계 보너스는 우두머리에 안 붙는다)

        public bool IsSpecial => isSpecial;
        public bool IsBoss => isBoss;
        public float DrawScale => Mathf.Max(0.1f, drawScale);
        public float UnlitPointDamageMul => unlitPointDamageMul;
        public float LitSpeedMul => litSpeedMul;
        public float ClimbAreaDamageMul => climbAreaDamageMul;
        public float KnockOffRatio => knockOffRatio;
        public float KnockdownSeconds => knockdownSeconds;
        public float TrapKnockdownSeconds => trapKnockdownSeconds;
        public int OnWallWallDamage => onWallWallDamage;
        public float EasyHpMul => Mathf.Max(0.05f, easyHpMul);
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
        public Texture2D CarrySheet => carrySheet;
        public Texture2D LadderSideSheet => ladderSideSheet;
        public Texture2D MovingSheet => carriesLadder && carrySheet != null ? carrySheet : sheet;
        public Texture2D NormalFor(Texture2D color)
        {
            if (color == null) return null;
            if (poseNormals != null) foreach (var pair in poseNormals)
                if (pair.Sheet == color) return SheetNormalPair.Match(color, pair.Normal);
            return SheetNormalPair.Match(color, color == sheet ? normalSheet : null);
        }
    }
}
