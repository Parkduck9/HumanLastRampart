using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    // 지형 (C-010 v1.1 P1-C14). 1회차 4개, 전투 번호에 따라 순서대로 (D-018, I4)
    [CreateAssetMenu(menuName = "HLR/정의/지형", fileName = "TerrainDef")]
    public sealed class TerrainDef : DefinitionBase
    {
        [SerializeField] float fieldSize = 320f;          // 성 중심에서 들판 끝까지 거리
        [SerializeField] float castleHalfSize = 60f;      // 성 중심에서 성벽까지 거리
        [SerializeField] Vector2 recoveryGate;            // 회수 병력 출발 위치 (X, Z)

        public float FieldSize => fieldSize;
        public float CastleHalfSize => castleHalfSize;
        public Vector2 RecoveryGate => recoveryGate;
    }
}