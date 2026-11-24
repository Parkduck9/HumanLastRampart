using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Traps;

namespace HumanLastRampart.Meta
{
    // 지형 손질에서 할 수 있는 일 (함정 공방 '지형' 탭)
    public enum TerrainAction
    {
        EarthWall,   // 토벽 쌓기 — 칸당 20골드
        Hill,        // 언덕 쌓기 — 칸당 10골드
        Path,        // 길 내기 — 칸당 5골드. 빈 땅·언덕 칸을 길로 (적이 더 좋아함, 함정 가능)
        Move,        // 나무·바위 옮기기 — 한 칸 5골드. 고른 순서: 옮길 덩이 칸 → 놓을 빈 칸 (짝마다)
        Demolish,    // 철거 — 내가 지은 토벽·언덕·길을 없애고 낸 골드의 절반을 돌려받는다
    }

    // 지형 손질 (D-077 ③④, K-049 1단계 · K-051 2단계): 함정 칸 격자에서 칸을 골라 골드로 땅을 바꾼다.
    // - 동원의 대가는 골드만 (피로 없음). 철거하면 낸 골드의 RefundRatio만큼 돌려받는다
    // - 진행 데이터에는 새 목록으로 갈아 끼운다 (함정 거래와 같은 방식 — 실패하면 아무것도 안 바뀐다)
    public sealed class TerrainWorkTransactions
    {
        readonly TrapPreparationTransactions traps;

        public TerrainWorkTransactions(TrapPreparationTransactions traps)
        {
            this.traps = traps ?? throw new ArgumentNullException(nameof(traps));
        }

        // 하위 호환 (K-049): 짓기 값
        public static int Price(TerrainWorkKind kind) => kind == TerrainWorkKind.EarthWall ? TerrainRules.EarthWallGold : TerrainRules.HillGold;
        public static string NameOf(TerrainWorkKind kind) => kind == TerrainWorkKind.EarthWall ? "토벽" : "언덕";

        public static int Price(TerrainAction action)
        {
            switch (action)
            {
                case TerrainAction.EarthWall: return TerrainRules.EarthWallGold;
                case TerrainAction.Hill: return TerrainRules.HillGold;
                case TerrainAction.Path: return TerrainRules.PathGold;
                case TerrainAction.Move: return TerrainRules.MoveGold;
                default: return 0;
            }
        }

        public static string NameOf(TerrainAction action)
        {
            switch (action)
            {
                case TerrainAction.EarthWall: return "토벽 쌓기";
                case TerrainAction.Hill: return "언덕 쌓기";
                case TerrainAction.Path: return "길 내기";
                case TerrainAction.Move: return "나무·바위 옮기기";
                default: return "철거";
            }
        }

        public static int Refund(TerrainWork work) => work == null ? 0 : (int)Math.Floor(work.Paid * TerrainRules.RefundRatio);

        // 이 칸에 지어 둔 손질 중 마지막 것 (없으면 null)
        public TerrainWork WorkAt(string cellId)
        {
            var works = traps.Campaign.TerrainWorks;
            if (works == null || traps.Terrain == null) return null;
            return works.FindLast(w => w != null && w.TerrainId == traps.Terrain.Id && w.CellId == cellId);
        }

        // 하위 호환 (K-049): 토벽·언덕을 지을 수 있는 칸
        public bool CanBuildOn(string cellId) => CanTarget(TerrainAction.EarthWall, cellId, null);

        // 이 칸을 고를 수 있는가 (지도 칸 색칠). picked = 지금까지 고른 칸 (옮기기는 순서가 뜻을 가진다)
        public bool CanTarget(TerrainAction action, string cellId, IReadOnlyList<string> picked)
        {
            if (!traps.HasNextBattle || traps.IsClosed) return false;
            if (TerrainRules.TryParseFree(cellId, out float fx, out float fz, out float frot))
            {
                if (action == TerrainAction.Demolish) return WorkAt(cellId) != null;
                return (action == TerrainAction.EarthWall || action == TerrainAction.Hill)
                    && CanPlaceFree(action == TerrainAction.EarthWall ? TerrainWorkKind.EarthWall : TerrainWorkKind.Hill, fx, fz, frot, picked);
            }

            if (!InGrid(cellId)) return false;
            byte kind = KindOf(cellId);
            TerrainWork work = WorkAt(cellId);
            switch (action)
            {
                case TerrainAction.EarthWall:
                case TerrainAction.Hill:
                    return TrapGrid.IsPlaceable(traps.Terrain, cellId) && kind == TerrainCell.Free && !traps.Occupied(cellId);
                case TerrainAction.Path:
                    // 빈 땅·무작위 언덕(내가 쌓은 언덕은 철거로)만. 이미 길이면 안 됨
                    return kind == TerrainCell.Free || (kind == TerrainCell.Hill && (work == null || work.Kind != TerrainWorkKind.Hill));
                case TerrainAction.Move:
                {
                    if (picked != null && Contains(picked, cellId)) return true;   // 고른 칸은 다시 눌러 뺄 수 있게
                    bool source = (picked?.Count ?? 0) % 2 == 0;
                    return source ? kind == TerrainCell.Trees || kind == TerrainCell.Rocks
                                  : kind == TerrainCell.Free && !traps.Occupied(cellId);
                }
                default:
                    return work != null && (work.Kind == TerrainWorkKind.EarthWall || work.Kind == TerrainWorkKind.Hill || work.Kind == TerrainWorkKind.Path);
            }
        }

        // 합계: 양수 = 내는 골드, 음수 = 돌려받는 골드
        public bool CanApply(TerrainAction action, IReadOnlyList<string> cellIds, out int cost, out string reason)
        {
            cost = 0; reason = null;
            if (traps.IsClosed) { reason = "종료된 화면입니다"; return false; }
            if (!traps.HasNextBattle) { reason = "다음 전투가 없습니다"; return false; }
            if (cellIds == null || cellIds.Count == 0) { reason = action == TerrainAction.Move ? "옮길 덩이 칸, 놓을 빈 칸 순서로 고르세요" : "칸을 지도에서 고르세요"; return false; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int k = 0; k < cellIds.Count; k++)
            {
                string id = cellIds[k];
                if (!seen.Add(id)) { reason = "같은 칸을 두 번 골랐습니다"; return false; }
                if (!CanTarget(action, id, Prefix(cellIds, k))) { reason = WhyNot(action); return false; }
            }

            if (action == TerrainAction.Move && cellIds.Count % 2 != 0) { reason = "놓을 빈 칸을 하나 더 고르세요 (덩이 → 빈 칸 짝)"; return false; }
            if (action == TerrainAction.Demolish)
            {
                foreach (string id in cellIds) cost -= Refund(WorkAt(id));
                return true;
            }

            int units = action == TerrainAction.Move ? cellIds.Count / 2 : cellIds.Count;
            cost = Price(action) * units;
            if (traps.Campaign.Gold < cost) { reason = $"골드가 {cost - traps.Campaign.Gold:N0} 모자랍니다"; return false; }
            return true;
        }

        // 적용. done = 바뀐 칸 (작업 동작 연출용 — 옮기기는 놓은 칸)
        public bool TryApply(TerrainAction action, IReadOnlyList<string> cellIds, out List<string> done, out int goldDelta, out string reason)
        {
            done = new List<string>(); goldDelta = 0;
            if (!CanApply(action, cellIds, out int cost, out reason)) return false;
            CampaignState campaign = traps.Campaign;
            string terrainId = traps.Terrain.Id;
            var works = campaign.TerrainWorks != null ? campaign.TerrainWorks.ConvertAll(w => w.Clone()) : new List<TerrainWork>();
            switch (action)
            {
                case TerrainAction.Demolish:
                    foreach (string id in cellIds)
                    {
                        int index = works.FindLastIndex(w => w != null && w.TerrainId == terrainId && w.CellId == id);
                        if (index >= 0) works.RemoveAt(index);
                        done.Add(id);
                    }

                    break;
                case TerrainAction.Move:
                    for (int k = 0; k + 1 < cellIds.Count; k += 2)
                    {
                        bool trees = KindOf(cellIds[k]) == TerrainCell.Trees;
                        works.Add(new TerrainWork { TerrainId = terrainId, CellId = cellIds[k], Kind = TerrainWorkKind.Cleared });
                        works.Add(new TerrainWork { TerrainId = terrainId, CellId = cellIds[k + 1], Kind = trees ? TerrainWorkKind.PlacedTrees : TerrainWorkKind.PlacedRocks, Paid = TerrainRules.MoveGold });
                        done.Add(cellIds[k + 1]);
                    }

                    break;
                default:
                    TerrainWorkKind kind = action == TerrainAction.EarthWall ? TerrainWorkKind.EarthWall : action == TerrainAction.Hill ? TerrainWorkKind.Hill : TerrainWorkKind.Path;
                    foreach (string id in cellIds)
                    {
                        var work = new TerrainWork { TerrainId = terrainId, CellId = id, Kind = kind, Paid = Price(action) };
                        if (TerrainRules.TryParseFree(id, out float fx, out float fz, out float frot))
                        {
                            work.Free = true; work.X = fx; work.Z = fz; work.Rotation = frot;
                        }

                        works.Add(work);
                        done.Add(id);
                    }

                    break;
            }

            campaign.Gold -= cost;
            goldDelta = -cost;
            campaign.TerrainWorks = works;   // 새 목록 → 지형(Layout)을 다시 만든다
            traps.CancelQuote();             // 함정 견적은 지형이 바뀌었으니 다시
            return true;
        }

        // 하위 호환 (K-049)
        public bool CanBuild(TerrainWorkKind kind, IReadOnlyList<string> cellIds, out int cost, out string reason) =>
            CanApply(kind == TerrainWorkKind.EarthWall ? TerrainAction.EarthWall : TerrainAction.Hill, cellIds, out cost, out reason);

        public bool TryBuild(TerrainWorkKind kind, IReadOnlyList<string> cellIds, out int built, out int spent, out string reason)
        {
            bool ok = TryApply(kind == TerrainWorkKind.EarthWall ? TerrainAction.EarthWall : TerrainAction.Hill, cellIds, out List<string> done, out int delta, out reason);
            built = ok ? done.Count : 0;
            spent = -delta;
            return ok;
        }

        static string WhyNot(TerrainAction action)
        {
            switch (action)
            {
                case TerrainAction.Path: return "길을 낼 수 없는 칸이 섞여 있습니다 (빈 땅·무작위 언덕만)";
                case TerrainAction.Move: return "옮길 수 없는 칸입니다 — 나무·바위 덩이 칸, 함정 없는 빈 칸 순서로";
                case TerrainAction.Demolish: return "내가 지은 토벽·언덕·길만 철거할 수 있습니다";
                default: return "지을 수 없는 칸이 섞여 있습니다 (언덕·나무·바위·함정·이미 지은 칸)";
            }
        }

        // 자유 배치 (사용자 2026-10-20): 덮는 땅 칸이 모두 빈 땅이고, 성벽에서 함정 구역(TrapNear~TrapFar) 안이며,
        // 함정이 놓인 칸·먼저 고른 조각과 겹치지 않아야 한다
        public bool CanPlaceFree(TerrainWorkKind kind, float x, float z, float rotation, IReadOnlyList<string> picked)
        {
            var terrain = traps.Terrain;
            TerrainLayout layout = traps.Layout;
            if (terrain == null || layout == null) return false;
            var center = new UnityEngine.Vector2(x, z);
            float wall = TerrainLayout.WallDistance(terrain.CastleHalfSize, center);
            if (wall < terrain.TrapNear || wall > terrain.TrapFar) return false;
            var cells = new List<int>();
            TerrainRules.FreeCover(layout, kind, center, rotation, cells);
            if (cells.Count == 0) return false;
            foreach (int c in cells) if (layout.Cells[c] != TerrainCell.Free) return false;

            // 함정이 놓인 칸
            foreach (var trap in traps.Campaign.Traps)
            {
                if (trap == null || trap.TerrainId != terrain.Id) continue;
                if (cells.Contains(layout.CellOf(trap.ActualX, trap.ActualZ))) return false;
            }

            // 먼저 고른 조각 (아직 짓지 않은 것)
            if (picked != null)
            {
                var other = new List<int>();
                foreach (string id in picked)
                {
                    if (!TerrainRules.TryParseFree(id, out float px, out float pz, out float prot)) continue;
                    if (px == x && pz == z && prot == rotation) continue;
                    // 다른 종류를 섞어 고르지는 않으므로 같은 종류로 덮는다
                    TerrainRules.FreeCover(layout, kind, new UnityEngine.Vector2(px, pz), prot, other);
                    foreach (int c in other) if (cells.Contains(c)) return false;
                }
            }

            return true;
        }

        public bool CanPlaceFree(TerrainWorkKind kind, string freeId, IReadOnlyList<string> picked) =>
            TerrainRules.TryParseFree(freeId, out float x, out float z, out float rotation) && CanPlaceFree(kind, x, z, rotation, picked);

        bool InGrid(string cellId) =>
            traps.Terrain != null && TrapGrid.TryParse(cellId, out int side, out int column, out int row)
            && column < TrapGrid.Columns(traps.Terrain) && row < TrapGrid.Rows(traps.Terrain);

        byte KindOf(string cellId)
        {
            TerrainLayout layout = traps.Layout;
            int c = layout.CellOfTrapCell(traps.Terrain, cellId);
            return c < 0 ? TerrainCell.Hill : layout.Cells[c];
        }

        static bool Contains(IReadOnlyList<string> list, string id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == id) return true;
            return false;
        }

        static List<string> Prefix(IReadOnlyList<string> list, int count)
        {
            var prefix = new List<string>(count);
            for (int i = 0; i < count; i++) prefix.Add(list[i]);
            return prefix;
        }
    }
}
