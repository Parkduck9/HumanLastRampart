using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 소유자별 정의 목록 (C-010 3): CombatCatalog(Claude), MetaCatalog·StoryCatalog(Codex), TrapCatalog.
    // Boot에서 App이 모든 카탈로그를 합쳐 검사한다 (C-010 4).
    [CreateAssetMenu(menuName = "HLR/정의/카탈로그", fileName = "Catalog")]
    public sealed class DefinitionCatalog : ScriptableObject
    {
        [SerializeField] List<DefinitionBase> entries = new List<DefinitionBase>();

        public IReadOnlyList<DefinitionBase> Entries => entries;
    }
}