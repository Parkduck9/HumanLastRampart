using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 아이템이 들어가는 자리 (K-058, D-079 U1: 주인공·마법사 각 2칸)
    public enum ItemSlot
    {
        Group,        // 병사 단체 아이템 (장착하지 않음)
        HeroTool,     // 주인공 소지품
        HeroBadge,    // 주인공 휘장
        WizardStaff,  // 마법사 지팡이
        WizardCharm,  // 마법사 부적
    }

    // 상점 품목 (C-010, K-058 확장). 영웅 아이템은 한 개씩 사서 칸에 끼우고, 단체 아이템은 영구 또는 한 전투용(소모품)
    [CreateAssetMenu(menuName = "HLR/정의/상점 품목", fileName = "ItemDef")]
    public sealed class ItemDef : DefinitionBase
    {
        [SerializeField] int price = 10;
        [SerializeField, TextArea] string description;
        [SerializeField] ItemSlot slot = ItemSlot.Group;
        [SerializeField] bool consumable;         // 한 전투 쓰고 사라짐 (불화살 꾸러미·화약 통)
        [SerializeField] bool perWall;            // 성벽 하나에 붙음 (방패 난간) — 성벽마다 하나
        [SerializeField] int maxOwned = 1;        // 가질 수 있는 최대 수 (소모품은 쌓아 둘 수 있는 수, 0 = 제한 없음)
        [SerializeField] int shopOrder;           // 상점 장부 순서 (작을수록 위, D-079 U7 1차 4개가 앞)
        [SerializeField] List<UpgradeEffect> effects = new List<UpgradeEffect>();

        public int Price => price;
        public string Description => description;
        public ItemSlot Slot => slot;
        public bool IsHeroItem => slot != ItemSlot.Group;
        public bool Consumable => consumable;
        public bool PerWall => perWall;
        public int MaxOwned => maxOwned;
        public int ShopOrder => shopOrder;
        public IReadOnlyList<UpgradeEffect> Effects => effects;

        // 이 칸에 끼우는 영웅 종류 (단체 아이템이면 null)
        public UnitKind? HeroKind => slot == ItemSlot.HeroTool || slot == ItemSlot.HeroBadge ? UnitKind.Hero
            : slot == ItemSlot.WizardStaff || slot == ItemSlot.WizardCharm ? UnitKind.Wizard : (UnitKind?)null;
    }
}
