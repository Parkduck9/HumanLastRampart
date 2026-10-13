using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 상점 품목 (C-010). 효과 종류는 Codex 정비 화면 설계(P1-X06)에서 정한다 — 필드 추가는 메시지로 요청.
    [CreateAssetMenu(menuName = "HLR/정의/상점 품목", fileName = "ItemDef")]
    public sealed class ItemDef : DefinitionBase
    {
        [SerializeField] int price = 10;
        [SerializeField, TextArea] string description;

        public int Price => price;
        public string Description => description;
    }
}