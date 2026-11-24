using HumanLastRampart.Core.Campaign;
using UnityEngine;

namespace HumanLastRampart.Core.Definitions
{
    public enum PostKind
    {
        Squad,   // 궁수·총병 분대, 영웅
        Cannon,  // 대포 운용병 분대만. 성벽마다 정확히 4개 (D-027)
    }

    // 성벽 위 배치 지점 (C-010 v1.2)
    [CreateAssetMenu(menuName = "HLR/정의/배치 지점", fileName = "PostDef")]
    public sealed class PostDef : DefinitionBase
    {
        [SerializeField] WallSide wall;
        [SerializeField] PostKind kind;
        [SerializeField, Range(0f, 1f)] float positionAlongWall = 0.5f;  // 성벽 왼쪽 끝 0 ~ 오른쪽 끝 1 (성 안에서 바깥을 볼 때)

        public WallSide Wall => wall;
        public string WallId => WallIds.Of(wall);
        public PostKind Kind => kind;
        public float PositionAlongWall => positionAlongWall;
    }
}