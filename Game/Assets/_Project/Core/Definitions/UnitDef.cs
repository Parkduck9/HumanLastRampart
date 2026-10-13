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
        [SerializeField] int recruitCost;
        [SerializeField] Texture2D sheet;          // 그림 시트 (규격은 그림 확정 후)

        public UnitKind Kind => kind;
        public int MaxHp => maxHp;
        public string DefaultWeaponId => defaultWeaponId;
        public int RecruitCost => recruitCost;
        public Texture2D Sheet => sheet;

        // 한 명씩 관리하는 종류 (HeroState) / 분대로 관리하는 종류 (SquadState) — D-015 D1
        public bool IsIndividual => kind == UnitKind.Hero || kind == UnitKind.Officer || kind == UnitKind.Elite || kind == UnitKind.Wizard;
    }
}