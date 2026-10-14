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

        [Header("함정 칸 (D-050, C-008 r6)")]
        [SerializeField] float trapCellSize = 5f;         // 칸 한 변(m)
        [SerializeField] float trapNear = 10f;            // 성벽에서 이 거리부터
        [SerializeField] float trapFar = 80f;             // 이 거리까지
        [Tooltip("놓을 수 없는 곳 (언덕 등): x, z = 중심(전장 좌표), y = 반경(m). 칸 가운데가 안에 들면 막힘")]
        [SerializeField] Vector3[] hills = new Vector3[0];

        public float FieldSize => fieldSize;
        public float CastleHalfSize => castleHalfSize;
        public Vector2 RecoveryGate => recoveryGate;
        public float TrapCellSize => trapCellSize;
        public float TrapNear => trapNear;
        public float TrapFar => trapFar;
        public System.Collections.Generic.IReadOnlyList<Vector3> Hills => hills;
    }
}