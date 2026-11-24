namespace HumanLastRampart.Core.Campaign
{
    // 성벽은 정확히 4개다 (D-026 B8, D-027 Q0, C-004 v1.3).
    public enum WallSide
    {
        North,
        East,
        South,
        West,
    }

    public static class WallIds
    {
        public const string North = "wall_north";
        public const string East = "wall_east";
        public const string South = "wall_south";
        public const string West = "wall_west";

        public static readonly string[] All = { North, East, South, West };

        public static string Of(WallSide side)
        {
            switch (side)
            {
                case WallSide.North: return North;
                case WallSide.East: return East;
                case WallSide.South: return South;
                default: return West;
            }
        }

        public static bool TryParse(string wallId, out WallSide side)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i] == wallId)
                {
                    side = (WallSide)i;
                    return true;
                }
            }

            side = WallSide.North;
            return false;
        }

        // 마주 보는 성벽 (소리 지르기 확률 계산 등, D-028·D-030)
        public static WallSide Opposite(WallSide side) => (WallSide)(((int)side + 2) % 4);

        public static bool AreAdjacent(WallSide a, WallSide b) => a != b && Opposite(a) != b;
    }
}