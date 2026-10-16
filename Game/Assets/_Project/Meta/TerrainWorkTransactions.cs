using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Traps;

namespace HumanLastRampart.Meta
{
    // 지형 손질 (D-077 ③④, K-049 1단계): 함정 공방 '지형' 탭에서 함정 칸 격자를 골라 골드로 토벽·언덕을 짓는다.
    // - 동원의 대가는 골드만 (피로 없음). 환불·철거는 1단계에 없다
    // - 지을 수 있는 칸: 함정 칸 범위 안, 비어 있는 땅(언덕·나무·바위·다른 손질 없음), 함정이 없는 칸
    // - 진행 데이터에는 새 목록으로 갈아 끼운다 (함정 거래와 같은 방식 — 실패하면 아무것도 안 바뀐다)
    public sealed class TerrainWorkTransactions
    {
        readonly TrapPreparationTransactions traps;

        public TerrainWorkTransactions(TrapPreparationTransactions traps)
        {
            this.traps = traps ?? throw new ArgumentNullException(nameof(traps));
        }

        public static int Price(TerrainWorkKind kind) => kind == TerrainWorkKind.EarthWall ? TerrainRules.EarthWallGold : TerrainRules.HillGold;
        public static string NameOf(TerrainWorkKind kind) => kind == TerrainWorkKind.EarthWall ? "토벽" : "언덕";

        // 이 칸에 지을 수 있는가 (이유 없이 참/거짓 — 지도 칸 색칠용)
        public bool CanBuildOn(string cellId)
        {
            if (!traps.HasNextBattle || traps.IsClosed) return false;
            if (!TrapGrid.IsPlaceable(traps.Terrain, cellId)) return false;   // 범위 밖·정의 언덕
            TerrainLayout layout = traps.Layout;
            int c = layout.CellOfTrapCell(traps.Terrain, cellId);
            return c >= 0 && layout.Cells[c] == TerrainCell.Free && !traps.Occupied(cellId);
        }

        // 이 칸에 지어 둔 손질 (없으면 null)
        public TerrainWork WorkAt(string cellId)
        {
            var works = traps.Campaign.TerrainWorks;
            if (works == null || traps.Terrain == null) return null;
            return works.Find(w => w != null && w.TerrainId == traps.Terrain.Id && w.CellId == cellId);
        }

        public bool CanBuild(TerrainWorkKind kind, IReadOnlyList<string> cellIds, out int cost, out string reason)
        {
            cost = 0; reason = null;
            if (traps.IsClosed) { reason = "종료된 화면입니다"; return false; }
            if (!traps.HasNextBattle) { reason = "다음 전투가 없습니다"; return false; }
            if (cellIds == null || cellIds.Count == 0) { reason = "지을 칸을 지도에서 고르세요"; return false; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in cellIds)
            {
                if (!seen.Add(id)) { reason = "같은 칸을 두 번 골랐습니다"; return false; }
                if (!CanBuildOn(id)) { reason = "지을 수 없는 칸이 섞여 있습니다 (언덕·나무·바위·함정·이미 지은 칸)"; return false; }
            }

            cost = Price(kind) * cellIds.Count;
            if (traps.Campaign.Gold < cost) { reason = $"골드가 {cost - traps.Campaign.Gold:N0} 모자랍니다"; return false; }
            return true;
        }

        public bool TryBuild(TerrainWorkKind kind, IReadOnlyList<string> cellIds, out int built, out int spent, out string reason)
        {
            built = 0; spent = 0;
            if (!CanBuild(kind, cellIds, out int cost, out reason)) return false;
            CampaignState campaign = traps.Campaign;
            var works = campaign.TerrainWorks != null ? campaign.TerrainWorks.ConvertAll(w => w.Clone()) : new List<TerrainWork>();
            foreach (string id in cellIds) works.Add(new TerrainWork { TerrainId = traps.Terrain.Id, CellId = id, Kind = kind });
            campaign.Gold -= cost;
            campaign.TerrainWorks = works;   // 새 목록 → 지형(Layout)을 다시 만든다
            traps.CancelQuote();             // 함정 견적은 지형이 바뀌었으니 다시
            built = cellIds.Count;
            spent = cost;
            return true;
        }
    }
}
