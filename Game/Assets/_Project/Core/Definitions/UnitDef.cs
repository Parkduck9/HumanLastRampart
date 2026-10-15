using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 아군 유닛 종류 (C-010, D-026 B5, D-027 Q7)
    public enum UnitKind
    {
        Hero,        // 주인공 — 한 명씩
        Officer,     // 특수 장교 — 한 명씩
        Elite,       // 강력한 병사 — 한 명씩
        Wizard,      // 마법사 — 한 명씩 (특수 공격)
        Archer,      // 궁수 — 분대
        Gunner,      // 총병 — 분대
        CannonCrew,  // 대포 운용병 — 분대, 대포 지점에만
    }

    [CreateAssetMenu(menuName = "HLR/정의/아군 유닛", fileName = "UnitDef")]
    public sealed class UnitDef : DefinitionBase
    {
        [SerializeField] UnitKind kind;
        [SerializeField] int maxHp = 10;
        [SerializeField] string defaultWeaponId;   // WeaponDef
        [SerializeField] int recruitCost;          // 정비 견적에는 쓰지 않는다 — 가격 원본은 PreparationEconomyDef (C-017)
        [SerializeField] Texture2D sheet;          // 그림 시트 (규격은 그림 확정 후)
        [SerializeField] Texture2D idleSheet;      // 서 있을 때 1칸(주인공 뒷모습 양발 서 있기, D-066). 없으면 sheet의 0번 칸
        [SerializeField] Texture2D runSheet;      // 성벽 위를 달릴 때(전령·우회 보고) 옆모습 달리기 4칸, 왼쪽을 본다. 없으면 sheet 그대로 (미리 연결, 사용자 검토 전)
        // 노멀맵 (D-062 G4): 짝 시트와 같은 칸 배치, 초록 = 위. 없으면 노멀맵 없이 그린다
        [SerializeField] Texture2D normalSheet;
        [SerializeField] Texture2D idleNormalSheet;
        [SerializeField] Texture2D runNormalSheet;

        public UnitKind Kind => kind;
        public int MaxHp => maxHp;
        public string DefaultWeaponId => defaultWeaponId;
        public int RecruitCost => recruitCost;
        public Texture2D Sheet => sheet;
        public Texture2D RunSheet => runSheet;
        public Texture2D IdleSheet => idleSheet;
        public Texture2D NormalSheet => normalSheet;
        public Texture2D IdleNormalSheet => idleNormalSheet;
        public Texture2D RunNormalSheet => runNormalSheet;

        // 한 명씩 관리하는 종류 (HeroState) / 분대로 관리하는 종류 (SquadState) — D-015 D1
        public bool IsIndividual => kind == UnitKind.Hero || kind == UnitKind.Officer || kind == UnitKind.Elite || kind == UnitKind.Wizard;
    }
}