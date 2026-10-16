using System;

namespace HumanLastRampart.Core.Campaign
{
    // 지형 손질 (D-077, K-049 1단계): 함정 공방 '지형' 탭에서 골드로 지은 것. 칸은 함정 칸과 같은 격자(TrapGrid 칸 ID)
    public enum TerrainWorkKind
    {
        EarthWall,   // 토벽 — 적의 길을 막고(돌아감·넘음·허묾), 뒤에 숨은 적은 총·화살을 맞지 않는다
        Hill,        // 쌓은 언덕 — 막힘(넘으면 느려짐), 함정 불가
    }

    [Serializable]
    public sealed class TerrainWork
    {
        public string TerrainId;          // 이 지형의 전투에만 쓰인다
        public string CellId;             // "cell_{성벽}_{열}_{행}" (TrapGrid)
        public TerrainWorkKind Kind;

        public TerrainWork Clone() => (TerrainWork)MemberwiseClone();
    }
}
