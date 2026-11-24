using System;

namespace HumanLastRampart.Core.Campaign
{
    // 지형 손질 (D-077, K-049·K-051): 함정 공방 '지형' 탭에서 골드로 한 것. 칸은 함정 칸과 같은 격자(TrapGrid 칸 ID).
    // 목록 순서대로 무작위 지형 위에 덮는다 (옮기기 = 옮긴 자리 Cleared + 놓은 자리 PlacedTrees/PlacedRocks 두 줄)
    public enum TerrainWorkKind
    {
        EarthWall,     // 토벽 — 적의 길을 막고(돌아감·넘음·허묾), 뒤에 숨은 적은 총·화살을 맞지 않는다
        Hill,          // 쌓은 언덕 — 막힘(돌아감 70%·넘음 30%), 함정 불가
        Path,          // 길 내기 (K-051) — 언덕·빈 땅을 길로. 적이 더 좋아하고 함정을 놓을 수 있다
        Cleared,       // 나무·바위를 옮겨 간 자리 (K-051)
        PlacedTrees,   // 옮겨 놓은 나무 덩이 (K-051)
        PlacedRocks,   // 옮겨 놓은 바위 덩이 (K-051)
    }

    [Serializable]
    public sealed class TerrainWork
    {
        public string TerrainId;          // 이 지형의 전투에만 쓰인다
        public string CellId;             // "cell_{성벽}_{열}_{행}" (TrapGrid)
        public TerrainWorkKind Kind;
        public int Paid;                  // 낸 골드 (K-051 철거 환불 계산용). 옮기기는 놓은 줄에 적는다
        // 자유 배치 (사용자 2026-10-20: 토벽·언덕은 칸 없이 아무 곳에, 오른쪽 누르기로 돌려서). Free면 CellId는 "free:..." 이름표일 뿐
        // 이고, 자리(X·Z, 성 가운데 기준 m)와 돌림(도, 45도 단위)으로 덮는 땅 칸을 계산한다 (TerrainRules.FreeCover)
        public bool Free;
        public float X, Z;
        public float Rotation;

        public TerrainWork Clone() => (TerrainWork)MemberwiseClone();
    }
}
