using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using UnityEngine;

// 이름공간은 함정 칸과 같은 Core.Traps (Core.Terrain으로 하면 UnityEngine.Terrain 형식과 헷갈린다)
namespace HumanLastRampart.Core.Traps
{
    // 칸 종류 (전투 작업이 byte로 읽는다 — 값을 바꾸면 Combat TerrainField도 같이)
    public static class TerrainCell
    {
        public const byte Free = 0;
        public const byte Hill = 1;        // 언덕(무작위·쌓은 것) — 막힘: 돌아감·넘음(느려짐)
        public const byte EarthWall = 2;   // 토벽 — 막힘: 돌아감·넘음·허묾, 뒤는 엄폐
        public const byte Trees = 3;       // 나무 덩이 — 돌아감 (모든 길이 막히면 넘음)
        public const byte Rocks = 4;       // 바위 덩이 — 돌아감 (모든 길이 막히면 넘음)
        public const byte Breach = 5;      // 허물어진 토벽 — 지나갈 수 있는 틈

        public static bool Blocks(byte kind) => kind >= Hill && kind <= Rocks;
    }

    // 지형 규칙 수치 (D-077 확정값 + 제안값). 제안값은 사용자 확인 뒤 정의 데이터로 옮긴다
    public static class TerrainRules
    {
        // D-077 ①: 막힘을 만난 적 — 50% 돌아감, 30% 넘음(넘는 동안 속도 −30%), 20% 토벽 허묾
        public const float DetourChance = 0.5f;
        public const float CrossChance = 0.3f;
        public const float CrossSpeed = 0.7f;
        // D-077 ⑥: 모든 길이 막히면 100% 넘거나 허문다 — 둘의 비율은 ①의 30:20을 따른다 (제안)
        public const float BlockedCrossShare = 0.6f;
        // 허물기 (사용자 2026-10-16: 닿는 순간이 아니라 몇 초 때린 뒤 무너짐) — 제안: 한 마리 8초, 여럿이면 빨리
        public const float EarthWallHp = 80f;
        public const float BreakDamagePerSecond = 10f;
        public const float FreeZone = 10f;          // 성벽에서 이 거리 안은 지금처럼 곧장 (설계 4장)
        // 지형 손질 비용 (D-077 ③ 골드만 — 설계 3장 제안값)
        public const int EarthWallGold = 20;
        public const int HillGold = 10;
        // 무작위 지형 (설계 2장 제안값)
        public const int HillsMin = 4, HillsMax = 7;
        public const float HillRadiusMin = 6f, HillRadiusMax = 14f;
        public const int TreeClustersMin = 6, TreeClustersMax = 10, TreeClusterCellsMax = 3;
        public const int RockClustersMin = 4, RockClustersMax = 8, RockClusterCellsMax = 2;
        public const float WallClearance = 10f;       // 성벽에서 이 거리 안에는 놓지 않는다
        public const float FrontCorridorHalf = 10f;   // 면마다 정면 가운데 너비 20m에는 놓지 않는다
    }

    // 한 지형의 들판 격자 (무작위 + 지형 손질). 성 가운데 원점, 북 +Z, 동 +X — 전투와 같다.
    // 칸 크기 = 함정 칸 크기, 칸 경계는 원점 기준 정렬이라 함정 칸과 겹친다 (성 반폭·함정 시작 거리가 칸 크기의 배수일 때)
    public sealed class TerrainLayout
    {
        public float CellSize { get; }
        public float Extent { get; }          // 원점에서 격자 끝까지(m)
        public int Size { get; }              // 한 줄 칸 수
        public byte[] Cells { get; }
        public readonly List<Vector3> Hills = new List<Vector3>();       // 언덕 (x, z, 반경) — 그리기·함정 막기 (정의 언덕 + 무작위 + 쌓은 언덕)
        public readonly List<int> TreeCells = new List<int>();
        public readonly List<int> RockCells = new List<int>();
        public readonly List<int> EarthWallCells = new List<int>();
        public readonly List<int> RaisedHillCells = new List<int>();

        public TerrainLayout(float cellSize, float fieldSize)
        {
            CellSize = Mathf.Max(1f, cellSize);
            Extent = Mathf.Ceil((fieldSize + 20f) / CellSize) * CellSize;
            Size = Mathf.Max(1, Mathf.RoundToInt(Extent * 2f / CellSize));
            Cells = new byte[Size * Size];
        }

        public static TerrainLayout Empty(TerrainDef terrain) =>
            new TerrainLayout(terrain != null ? terrain.TrapCellSize : 5f, terrain != null ? terrain.FieldSize : 160f);

        public int CellOf(float x, float z)
        {
            int i = Mathf.FloorToInt((x + Extent) / CellSize), j = Mathf.FloorToInt((z + Extent) / CellSize);
            return i < 0 || j < 0 || i >= Size || j >= Size ? -1 : j * Size + i;
        }

        public Vector2 Center(int cell) => new Vector2(-Extent + (cell % Size + 0.5f) * CellSize, -Extent + (cell / Size + 0.5f) * CellSize);
        public byte KindAt(float x, float z) { int c = CellOf(x, z); return c < 0 ? TerrainCell.Free : Cells[c]; }

        // 함정 칸에 지형이 있어 함정을 놓을 수 없는가 (언덕·토벽·나무·바위)
        public bool BlocksTrap(TerrainDef terrain, string cellId)
        {
            if (!TrapGrid.TryParse(cellId, out int side, out int column, out int row)) return true;
            Vector2 c = TrapGrid.Center(terrain, side, column, row);
            return KindAt(c.x, c.y) != TerrainCell.Free;
        }

        public int CellOfTrapCell(TerrainDef terrain, string cellId)
        {
            if (!TrapGrid.TryParse(cellId, out int side, out int column, out int row)) return -1;
            Vector2 c = TrapGrid.Center(terrain, side, column, row);
            return CellOf(c.x, c.y);
        }

        // 성벽 바깥면에서 거리 (네모 성 — 가장 가까운 면)
        public static float WallDistance(float castleHalf, Vector2 p) => Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) - castleHalf;

        // 어느 면이든 정면 가운데 통로(성벽 앞 좌우 FrontCorridorHalf) 안인가 — 이 통로는 늘 비워 성벽까지 길이 아예 막히지 않게
        public static bool InFrontCorridor(float castleHalf, Vector2 p, float margin)
        {
            bool northSouth = Mathf.Abs(p.y) > castleHalf - 1f && Mathf.Abs(p.x) < TerrainRules.FrontCorridorHalf + margin;
            bool eastWest = Mathf.Abs(p.x) > castleHalf - 1f && Mathf.Abs(p.y) < TerrainRules.FrontCorridorHalf + margin;
            return northSouth || eastWest;
        }
    }

    // 무작위 지형 만들기 (D-076 ④·D-077, 설계 2장). 같은 씨앗·지형·손질이면 늘 같은 결과 — 함정 지도·전투·결과가 같은 지형을 본다
    public static class TerrainGenerator
    {
        public static TerrainLayout Generate(TerrainDef terrain, int seed, IReadOnlyList<TerrainWork> works)
        {
            var layout = TerrainLayout.Empty(terrain);
            if (terrain == null) return layout;
            float half = terrain.CastleHalfSize, cell = layout.CellSize;
            float far = terrain.FieldSize - half - 10f;   // 들판 끝 10m 안쪽까지
            var random = DeterministicRandom.From(seed, terrain.Id ?? "", "terrain");

            // 정의에 적힌 언덕(예전부터 있던 것)은 그대로
            foreach (Vector3 hill in terrain.Hills) AddHill(layout, hill);

            // 1. 언덕
            int hills = HillsCount(random);
            for (int k = 0, tries = 0; k < hills && tries < hills * 40; tries++)
            {
                float radius = random.Range(TerrainRules.HillRadiusMin, TerrainRules.HillRadiusMax);
                Vector2 p = RandomPoint(random, half + far);
                float wall = TerrainLayout.WallDistance(half, p);
                if (wall - radius < TerrainRules.WallClearance || wall + radius > far + 10f) continue;
                if (TerrainLayout.InFrontCorridor(half, p, radius)) continue;
                if (Overlaps(layout, p, radius + cell)) continue;
                AddHill(layout, new Vector3(p.x, radius, p.y));
                k++;
            }

            // 2. 나무 덩이·바위 덩이 (칸 1~3개 / 1~2개, 이웃 칸으로 자란다)
            AddClusters(layout, random, half, far, TerrainCell.Trees, random.NextInt(TerrainRules.TreeClustersMax - TerrainRules.TreeClustersMin + 1) + TerrainRules.TreeClustersMin, TerrainRules.TreeClusterCellsMax, layout.TreeCells);
            AddClusters(layout, random, half, far, TerrainCell.Rocks, random.NextInt(TerrainRules.RockClustersMax - TerrainRules.RockClustersMin + 1) + TerrainRules.RockClustersMin, TerrainRules.RockClusterCellsMax, layout.RockCells);

            // 3. 지형 손질 (플레이어가 지은 것) — 무작위 지형 위에 덮는다
            if (works != null)
            {
                foreach (TerrainWork work in works)
                {
                    if (work == null || work.TerrainId != terrain.Id) continue;
                    int c = layout.CellOfTrapCell(terrain, work.CellId);
                    if (c < 0) continue;
                    if (work.Kind == TerrainWorkKind.EarthWall)
                    {
                        layout.Cells[c] = TerrainCell.EarthWall;
                        layout.EarthWallCells.Add(c);
                    }
                    else
                    {
                        layout.Cells[c] = TerrainCell.Hill;
                        layout.RaisedHillCells.Add(c);
                        Vector2 center = layout.Center(c);
                        layout.Hills.Add(new Vector3(center.x, cell * 0.6f, center.y));   // 그리기용 작은 언덕 (칸 하나)
                    }
                }
            }

            return layout;
        }

        // 정의에 적힌 언덕만 (무작위·손질 없음) — 측정 비교용
        public static TerrainLayout FixedOnly(TerrainDef terrain)
        {
            var layout = TerrainLayout.Empty(terrain);
            if (terrain != null) foreach (Vector3 hill in terrain.Hills) AddHill(layout, hill);
            return layout;
        }

        static int HillsCount(DeterministicRandom random) => random.NextInt(TerrainRules.HillsMax - TerrainRules.HillsMin + 1) + TerrainRules.HillsMin;

        static Vector2 RandomPoint(DeterministicRandom random, float extent) => new Vector2(random.Range(-extent, extent), random.Range(-extent, extent));

        static bool Overlaps(TerrainLayout layout, Vector2 p, float radius)
        {
            foreach (Vector3 h in layout.Hills)
                if ((p - new Vector2(h.x, h.z)).sqrMagnitude < (radius + h.y) * (radius + h.y)) return true;
            return false;
        }

        // 언덕을 칸에 찍는다: 칸 가운데가 원 안이면 언덕 칸 (함정 칸 막기와 같은 규칙)
        static void AddHill(TerrainLayout layout, Vector3 hill)
        {
            layout.Hills.Add(hill);
            float r = hill.y;
            int c0 = layout.CellOf(hill.x - r, hill.z - r), c1 = layout.CellOf(hill.x + r, hill.z + r);
            int i0 = c0 < 0 ? 0 : c0 % layout.Size, j0 = c0 < 0 ? 0 : c0 / layout.Size;
            int i1 = c1 < 0 ? layout.Size - 1 : c1 % layout.Size, j1 = c1 < 0 ? layout.Size - 1 : c1 / layout.Size;
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    int c = j * layout.Size + i;
                    Vector2 center = layout.Center(c);
                    if ((center - new Vector2(hill.x, hill.z)).sqrMagnitude <= r * r && layout.Cells[c] == TerrainCell.Free)
                        layout.Cells[c] = TerrainCell.Hill;
                }
        }

        static void AddClusters(TerrainLayout layout, DeterministicRandom random, float half, float far, byte kind, int count, int maxCells, List<int> list)
        {
            float cell = layout.CellSize;
            for (int k = 0, tries = 0; k < count && tries < count * 40; tries++)
            {
                Vector2 p = RandomPoint(random, half + far);
                int c = layout.CellOf(p.x, p.y);
                if (!CanUse(layout, c, half, far)) continue;
                int size = random.NextInt(maxCells) + 1;
                Use(layout, c, kind, list);
                for (int n = 1; n < size; n++)
                {
                    // 이웃 네 칸 중 하나로 자란다
                    int dir = random.NextInt(4);
                    int next = dir == 0 ? c + 1 : dir == 1 ? c - 1 : dir == 2 ? c + layout.Size : c - layout.Size;
                    if (next < 0 || next >= layout.Cells.Length || Mathf.Abs(next % layout.Size - c % layout.Size) > 1) continue;
                    if (!CanUse(layout, next, half, far)) continue;
                    Use(layout, next, kind, list);
                    c = next;
                }

                k++;
            }
        }

        static bool CanUse(TerrainLayout layout, int c, float half, float far)
        {
            if (c < 0 || layout.Cells[c] != TerrainCell.Free) return false;
            Vector2 center = layout.Center(c);
            float wall = TerrainLayout.WallDistance(half, center);
            float halfCell = layout.CellSize * 0.5f;
            return wall - halfCell >= TerrainRules.WallClearance && wall <= far && !TerrainLayout.InFrontCorridor(half, center, halfCell);
        }

        static void Use(TerrainLayout layout, int c, byte kind, List<int> list)
        {
            layout.Cells[c] = kind;
            list.Add(c);
        }
    }
}
