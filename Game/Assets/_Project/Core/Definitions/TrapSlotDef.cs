using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 함정 설치 지점 (C-008 r1, C-010 v1.1)
    [CreateAssetMenu(menuName = "HLR/정의/함정 설치 지점", fileName = "TrapSlotDef")]
    public sealed class TrapSlotDef : DefinitionBase
    {
        [SerializeField] string terrainId;
        [SerializeField] float x;
        [SerializeField] float z;

        public string TerrainId => terrainId;
        public float X => x;
        public float Z => z;
    }
}