using System.Globalization;
using HumanLastRampart.Core.Definitions;
using UnityEngine;

namespace HumanLastRampart.Core.Traps
{
    // 함정 칸 (D-050, C-008 r6). 상태가 없는 계산 — Prep(칸 고르기·지도)과 Combat(위치)이 같은 규칙을 쓴다.
    // - 성벽마다 성벽 앞 구역을 칸으로 나눈다: 성벽 좌우 = 열(왼쪽 끝 0, 바깥을 볼 때 기준), 성벽에서 먼 쪽 = 행(가까운 줄 0)
    // - 칸 ID: "cell_{성벽 0~3}_{열}_{행}" (성벽 0 북·1 동·2 남·3 서, WallSide 순서). TrapPlacement.SlotId에 넣는다
    // - 전장 좌표: 성 가운데가 원점, 북쪽 +Z, 동쪽 +X (전투와 같음)
    public static class TrapGrid
    {
        public static int Columns(TerrainDef terrain) => Mathf.Max(1, Mathf.FloorToInt(terrain.CastleHalfSize * 2f / terrain.TrapCellSize));
        public static int Rows(TerrainDef terrain) => Mathf.Max(1, Mathf.FloorToInt((terrain.TrapFar - terrain.TrapNear) / terrain.TrapCellSize));

        public static string CellId(int side, int column, int row) => $"cell_{side}_{column}_{row}";

        // 새 함정은 격자에 붙이지 않는다. 왕복 가능한 소수 문자열로 실제 클릭 좌표를 보존한다.
        public static string FreeId(float x, float z) => "free:" + x.ToString("R", CultureInfo.InvariantCulture)
            + ":" + z.ToString("R", CultureInfo.InvariantCulture);

        public static bool TryParseFree(string id, out Vector2 point)
        {
            point = default;
            if (string.IsNullOrEmpty(id) || !id.StartsWith("free:")) return false;
            string[] parts = id.Split(':');
            if ((parts.Length != 3 && parts.Length != 4)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)
                || !Finite(x) || !Finite(z)) return false;
            // 기존 자유 지형 이름표와 같은 회전 0 표기도 읽는다. 함정에는 회전이 없다.
            if (parts.Length == 4 && (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotation) || rotation != 0f)) return false;
            point = new Vector2(x, z);
            return true;
        }

        public static bool TryPosition(TerrainDef terrain, string id, out Vector2 point)
        {
            if (TryParseFree(id, out point)) return terrain != null;
            if (terrain == null || !TryParse(id, out int side, out int column, out int row)
                || column >= Columns(terrain) || row >= Rows(terrain)) return false;
            point = Center(terrain, side, column, row);
            return true;
        }

        // 네모 성의 모서리도 포함한다. 함정 띠는 가장 가까운 성벽 면에서의 거리다.
        public static bool InBand(TerrainDef terrain, Vector2 point)
        {
            if (terrain == null || !Finite(point.x) || !Finite(point.y)) return false;
            float distance = TerrainLayout.WallDistance(terrain.CastleHalfSize, point);
            return distance >= terrain.TrapNear && distance <= terrain.TrapFar
                && Mathf.Abs(point.x) <= terrain.FieldSize && Mathf.Abs(point.y) <= terrain.FieldSize;
        }

        public static bool IsPlaceablePoint(TerrainDef terrain, TerrainLayout layout, Vector2 point)
        {
            if (!InBand(terrain, point)) return false;
            if (layout != null) return layout.CellOf(point.x, point.y) >= 0 && TerrainCell.TrapGround(layout.KindAt(point.x, point.y));
            foreach (Vector3 hill in terrain.Hills)
                if ((point - new Vector2(hill.x, hill.z)).sqrMagnitude <= hill.y * hill.y) return false;
            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool TryParse(string cellId, out int side, out int column, out int row)
        {
            side = column = row = -1;
            if (string.IsNullOrEmpty(cellId) || !cellId.StartsWith("cell_")) return false;
            string[] parts = cellId.Split('_');
            bool ok = parts.Length == 4
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out side)
                && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out column)
                && int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out row)
                && side >= 0 && side < 4;
            // 정규 모양만 (앞자리 0 같은 별칭 금지 — 같은 칸이 다른 문자열로 두 번 점유되지 않게, Codex V1)
            return ok && cellId == CellId(side, column, row);
        }

        // 칸 가운데 (X, Z)
        public static Vector2 Center(TerrainDef terrain, int side, int column, int row)
        {
            Vector2 outward = Outward(side);
            var right = new Vector2(outward.y, -outward.x);   // 바깥을 볼 때 오른쪽 (전투 WallGeometry.Right와 같음)
            float half = terrain.CastleHalfSize;
            float cell = terrain.TrapCellSize;
            float along = -half + (column + 0.5f) * cell;
            float away = half + terrain.TrapNear + (row + 0.5f) * cell;
            return outward * away + right * along;
        }

        // 놓을 수 있는 칸인가: 범위 안이고 언덕(놓을 수 없는 곳)에 들지 않는다
        public static bool IsPlaceable(TerrainDef terrain, int side, int column, int row)
        {
            if (terrain == null || side < 0 || side > 3 || column < 0 || row < 0 || column >= Columns(terrain) || row >= Rows(terrain)) return false;
            Vector2 c = Center(terrain, side, column, row);
            foreach (Vector3 hill in terrain.Hills)
            {
                if ((c - new Vector2(hill.x, hill.z)).sqrMagnitude <= hill.y * hill.y) return false;
            }

            return true;
        }

        public static bool IsPlaceable(TerrainDef terrain, string cellId) =>
            TryParse(cellId, out int side, out int column, out int row) && IsPlaceable(terrain, side, column, row);

        // 무작위 지형·지형 손질까지 보고 (D-077, K-049): 언덕·토벽·나무·바위 칸에는 함정을 놓을 수 없다
        public static bool IsPlaceable(TerrainDef terrain, TerrainLayout layout, string cellId) =>
            IsPlaceable(terrain, cellId) && (layout == null || !layout.BlocksTrap(terrain, cellId));

        static Vector2 Outward(int side)
        {
            switch (side)
            {
                case 0: return new Vector2(0f, 1f);
                case 1: return new Vector2(1f, 0f);
                case 2: return new Vector2(0f, -1f);
                default: return new Vector2(-1f, 0f);
            }
        }
    }
}
