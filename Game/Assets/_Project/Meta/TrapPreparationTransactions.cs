using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Traps;
using UnityEngine;

namespace HumanLastRampart.Meta
{
    // D-050/C-008 r6: 한 정비 방문의 구매·재설치 견적. 회수·환불은 제공하지 않는다.
    public sealed class TrapPreparationTransactions
    {
        public sealed class Quote
        {
            public int Cost { get; internal set; }
            public string Description { get; internal set; }
            internal string Stamp, DefinitionId, CellId;
            internal bool FromStorage, Marked;
        }
        readonly CampaignState campaign;
        readonly IDefinitionLookup definitions;
        readonly CampaignStartDef start;
        readonly TerrainDef terrain;
        Quote active;
        bool closed;
        public TerrainDef Terrain => terrain;
        public bool HasNextBattle => terrain != null;

        public TrapPreparationTransactions(CampaignState campaign, IDefinitionLookup definitions)
        {
            this.campaign = campaign; this.definitions = definitions;
            if (campaign == null || definitions == null) throw new ArgumentException("함정 진행 데이터 또는 정의가 없습니다");
            var starts = definitions.All<CampaignStartDef>();
            if (starts == null || starts.Count != 1 || starts[0] == null) throw new ArgumentException("시작 정의가 정확히 하나여야 합니다");
            start = starts[0];
            if (campaign.BattleNumber > 0 && !string.IsNullOrEmpty(start.WaveFor(campaign.BattleNumber)))
            {
                terrain = definitions.Get<TerrainDef>(start.TerrainFor(campaign.BattleNumber));
                if (terrain == null) throw new ArgumentException("다음 전투 지형 정의가 없습니다");
            }
            if (!Validate(out var reason)) throw new ArgumentException(reason);
            // 첫 정비(전투2 앞)는 App 초기값6을 유지한다. 이후 전투 번호당 한번 보충.
            if (HasNextBattle && campaign.TrapShopRestockedForBattle != campaign.BattleNumber)
            {
                var next = new List<TrapStockEntry>();
                foreach (var entry in campaign.TrapShop)
                {
                    var def = definitions.Get<TrapDef>(entry.TrapDefId);
                    next.Add(new TrapStockEntry { TrapDefId = entry.TrapDefId,
                        Count = (int)Math.Min(def.ShopStockMax, (long)entry.Count + def.ShopStockPerPrep) });
                }
                campaign.TrapShop = next;
                campaign.TrapShopRestockedForBattle = campaign.BattleNumber;
            }
        }
        // 무작위 지형 + 지형 손질 (D-077, K-049). 손질 목록이 바뀌면(새 목록으로 갈아 끼운다) 다시 만든다
        TerrainLayout layout;
        List<TerrainWork> layoutSource;
        int layoutCount = -1;
        public TerrainLayout Layout
        {
            get
            {
                if (terrain == null) return null;
                if (campaign.TerrainWorks == null) campaign.TerrainWorks = new List<TerrainWork>();
                if (layout == null || !ReferenceEquals(layoutSource, campaign.TerrainWorks) || layoutCount != campaign.TerrainWorks.Count)
                {
                    layout = TerrainGenerator.Generate(terrain, campaign.RngSeed, campaign.TerrainWorks);
                    layoutSource = campaign.TerrainWorks;
                    layoutCount = campaign.TerrainWorks.Count;
                }

                return layout;
            }
        }

        // 함정을 놓을 수 있는 칸 (격자 범위·정의 언덕 + 무작위 지형·지형 손질)
        public bool IsPlaceable(string cellId) => TrapGrid.TryPosition(terrain, cellId, out Vector2 point)
            && TrapGrid.IsPlaceablePoint(terrain, Layout, point);
        public CampaignState Campaign => campaign;
        public bool IsClosed => closed;

        public void Close() { closed = true; active = null; }
        public void CancelQuote() { active = null; }
        public int ShopCount(string id) => campaign.TrapShop.Find(e => e.TrapDefId == id)?.Count ?? 0;
        public int StoredCount(string id) => campaign.TrapStock.Find(e => e.TrapDefId == id)?.Count ?? 0;
        // 지형 손질의 칸 점유 판정도 실제 위치를 본다 (폭발로 밀린 함정 포함).
        public bool Occupied(string cellId)
        {
            if (!TrapGrid.TryPosition(terrain, cellId, out Vector2 point)) return false;
            int cell = Layout.CellOf(point.x, point.y);
            return campaign.Traps.Exists(t => t != null && t.TerrainId == terrain.Id && Layout.CellOf(t.ActualX, t.ActualZ) == cell);
        }

        public float Radius(string definitionId) => definitions.Get<TrapDef>(definitionId)?.DamageRadius ?? 0f;

        public bool CanPick(string definitionId, string id, IReadOnlyList<string> picked)
        {
            var def = definitions.Get<TrapDef>(definitionId);
            if (closed || def == null || !IsPlaceable(id) || !TrapGrid.TryPosition(terrain, id, out Vector2 point)) return false;
            foreach (var trap in campaign.Traps)
            {
                if (trap == null || trap.TerrainId != terrain.Id) continue;
                float radius = Mathf.Max(def.DamageRadius, Radius(trap.TrapDefId));
                if ((point - new Vector2(trap.ActualX, trap.ActualZ)).sqrMagnitude <= radius * radius) return false;
            }
            if (picked != null)
                foreach (string other in picked)
                    if (TrapGrid.TryPosition(terrain, other, out Vector2 at)
                        && (point - at).sqrMagnitude <= def.DamageRadius * def.DamageRadius) return false;
            return true;
        }

        public bool TryQuote(string definitionId, string cellId, bool fromStorage, bool marked,
            out Quote quote, out string reason, out bool dataError)
        {
            active = quote = null; reason = null; dataError = false;
            if (closed) { reason = "종료된 화면입니다"; return false; }
            if (!Validate(out reason)) { dataError = true; return false; }
            if (!HasNextBattle) { reason = "다음 전투가 없습니다"; return false; }
            var def = definitions.Get<TrapDef>(definitionId);
            if (def == null) { reason = "함정 정의가 없습니다"; dataError = true; return false; }
            if (!IsPlaceable(cellId)) { reason = "설치할 수 없는 칸입니다"; return false; }
            if (!CanPick(definitionId, cellId, null)) { reason = "다른 함정의 폭발 반경 안에는 설치할 수 없습니다"; return false; }
            if ((fromStorage ? StoredCount(def.Id) : ShopCount(def.Id)) < 1)
            { reason = fromStorage ? "보관 중인 함정이 없습니다" : "상점 재고가 없습니다"; return false; }
            int cost = fromStorage ? 0 : def.Price;
            if (campaign.Gold < cost) { reason = "골드가 부족합니다"; return false; }
            if (campaign.NextInstanceNumber == int.MaxValue || campaign.Traps.Exists(t => t.TrapId == "trap_" + ((long)campaign.NextInstanceNumber + 1)))
            { reason = "함정 ID를 안전하게 발급할 수 없습니다"; dataError = true; return false; }
            quote = new Quote { DefinitionId = def.Id, CellId = cellId, FromStorage = fromStorage, Marked = marked,
                Cost = cost, Stamp = Stamp(), Description = $"{def.DisplayName} · {cellId}\n{(fromStorage ? "보관품 재설치" : "상점 구매·설치")} · {cost:N0}골드\n지도 표시 {(marked ? "켜짐" : "없음 — 지도 점화 불가")}\n잔액 {campaign.Gold-cost:N0} · 환불 불가" };
            active = quote; return true;
        }
        public bool TryCommit(Quote quote, out string reason, out bool dataError)
        {
            reason = null; dataError = false;
            if (closed || quote == null || !ReferenceEquals(active, quote)) { reason = "만료되거나 사용한 견적입니다"; return false; }
            active = null;
            if (!Validate(out reason)) { dataError = true; return false; }
            if (quote.Stamp != Stamp()) { reason = "상태가 변경됐습니다. 새 견적을 확인해 주세요"; return false; }
            if (!CanPick(quote.DefinitionId, quote.CellId, null) || !TrapGrid.TryPosition(terrain, quote.CellId, out Vector2 position))
            { reason = "설치 위치가 변경됐습니다. 새 위치를 골라 주세요"; return false; }
            var traps = campaign.Traps.ConvertAll(t => t.Clone());
            var stock = (quote.FromStorage ? campaign.TrapStock : campaign.TrapShop).ConvertAll(e => e.Clone());
            stock.Find(e => e.TrapDefId == quote.DefinitionId).Count--;
            traps.Add(new TrapPlacement { TrapId = "trap_" + ((long)campaign.NextInstanceNumber + 1), TrapDefId = quote.DefinitionId,
                TerrainId = terrain.Id, SlotId = quote.CellId, ActualX = position.x, ActualZ = position.y,
                Marked = quote.Marked, MarkX = position.x, MarkZ = position.y });
            campaign.NewInstanceId("trap");
            campaign.Gold -= quote.Cost;
            campaign.Traps = traps;
            if (quote.FromStorage) campaign.TrapStock = stock; else campaign.TrapShop = stock;
            return true;
        }
        // 여러 칸 한 번에 구매·설치 (K-042 사용자 요청: 1개씩 사지 않게). 먼저 전부 되는지(칸·재고·골드·ID) 확인한 뒤
        // 칸마다 기존 견적(TryQuote)→확정(TryCommit)을 그대로 거친다. 사전 확인을 통과하면 중간에 실패하지 않는다
        public int Price(string definitionId) => definitions.Get<TrapDef>(definitionId)?.Price ?? 0;

        public bool CanBuyMany(string definitionId, IReadOnlyList<string> cellIds, bool fromStorage, out int cost, out string reason)
        {
            cost = 0; reason = null;
            if (closed) { reason = "종료된 화면입니다"; return false; }
            if (!HasNextBattle) { reason = "다음 전투가 없습니다"; return false; }
            var def = definitions.Get<TrapDef>(definitionId);
            if (def == null) { reason = "함정 정의가 없습니다"; return false; }
            if (cellIds == null || cellIds.Count == 0) { reason = "설치할 칸을 지도에서 고르세요"; return false; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var prior = new List<string>();
            foreach (string id in cellIds)
            {
                if (!seen.Add(id)) { reason = "같은 칸을 두 번 골랐습니다"; return false; }
                if (!IsPlaceable(id)) { reason = "설치할 수 없는 칸이 섞여 있습니다"; return false; }
                if (!CanPick(definitionId, id, prior)) { reason = "폭발 반경 안에 다른 함정이 있는 위치가 섞여 있습니다"; return false; }
                prior.Add(id);
            }
            int have = fromStorage ? StoredCount(def.Id) : ShopCount(def.Id);
            if (have < cellIds.Count) { reason = $"{(fromStorage ? "보관 중인 함정" : "상점 재고")}이 {have}개뿐입니다"; return false; }
            long total = fromStorage ? 0L : (long)def.Price * cellIds.Count;
            if (total > int.MaxValue) { reason = "함정 구매 합계가 너무 큽니다"; return false; }
            cost = (int)total;
            if (campaign.Gold < cost) { reason = $"골드가 {cost - campaign.Gold:N0} 모자랍니다"; return false; }
            if ((long)campaign.NextInstanceNumber + cellIds.Count >= int.MaxValue) { reason = "함정 ID를 안전하게 발급할 수 없습니다"; return false; }
            for (int k = 1; k <= cellIds.Count; k++)
                if (campaign.Traps.Exists(t => t.TrapId == "trap_" + ((long)campaign.NextInstanceNumber + k)))
                { reason = "함정 ID를 안전하게 발급할 수 없습니다"; return false; }
            return true;
        }

        public bool TryBuyMany(string definitionId, IReadOnlyList<string> cellIds, bool fromStorage, bool marked,
            out int placed, out int spent, out string reason, out bool dataError)
        {
            placed = 0; spent = 0; dataError = false;
            if (!Validate(out reason)) { dataError = true; return false; }
            if (!CanBuyMany(definitionId, cellIds, fromStorage, out _, out reason)) return false;
            foreach (string id in cellIds)
            {
                if (!TryQuote(definitionId, id, fromStorage, marked, out Quote quote, out reason, out dataError)
                    || !TryCommit(quote, out reason, out dataError))
                    return false;   // 사전 확인 뒤라 오지 않아야 한다 — 그때까지 설치한 칸은 남는다 (placed 개)
                placed++;
                spent += quote.Cost;
            }

            return true;
        }

        bool Validate(out string reason)
        {
            reason = null;
            if (campaign.Gold < 0 || campaign.NextInstanceNumber < 0 || campaign.BattleNumber < 1 ||
                campaign.TrapShopRestockedForBattle < 0 || campaign.Traps == null || campaign.TrapShop == null || campaign.TrapStock == null)
            { reason = "함정 진행 데이터가 잘못됐습니다"; return false; }
            if (HasNextBattle && (!ReferenceEquals(definitions.Get<TerrainDef>(terrain.Id), terrain) || start.TerrainFor(campaign.BattleNumber) != terrain.Id || string.IsNullOrEmpty(start.WaveFor(campaign.BattleNumber))))
            { reason = "다음 전투가 변경됐습니다"; return false; }
            var defs = definitions.All<TrapDef>();
            if (defs == null || defs.Count == 0) { reason = "함정 정의가 없습니다"; return false; }
            var defIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var def in defs)
                if (def == null || string.IsNullOrWhiteSpace(def.Id) || !defIds.Add(def.Id) || def.Price < 0 ||
                    !Finite(def.DamageRadius) || def.DamageRadius <= 0f || def.ShopStockStart < 0 || def.ShopStockPerPrep < 0 || def.ShopStockMax < def.ShopStockStart)
                { reason = "함정 가격·재고 정의가 잘못됐습니다"; return false; }
            if (!CheckStock(campaign.TrapShop, true, defIds, out reason) || !CheckStock(campaign.TrapStock, false, defIds, out reason)) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal); var slots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var trap in campaign.Traps)
            {
                if (trap == null || string.IsNullOrWhiteSpace(trap.TrapId) || !ids.Add(trap.TrapId) || !defIds.Contains(trap.TrapDefId))
                { reason = "설치 함정 ID·정의가 잘못됐습니다"; return false; }
                var ground = definitions.Get<TerrainDef>(trap.TerrainId);
                if (!ValidTerrain(ground) || !TrapGrid.TryPosition(ground, trap.SlotId, out Vector2 original) ||
                    (TrapGrid.TryParseFree(trap.SlotId, out _) && !TrapGrid.InBand(ground, original)) || !slots.Add(trap.TerrainId + ":" + trap.SlotId) ||
                    !Finite(trap.ActualX) || !Finite(trap.ActualZ) || (trap.Marked && (!Finite(trap.MarkX) || !Finite(trap.MarkZ))))
                { reason = "설치 함정 지형·칸·좌표가 잘못됐습니다"; return false; }
            }
            if (HasNextBattle && !ValidTerrain(terrain)) { reason = "격자 지형 수치가 잘못됐습니다"; return false; }
            return true;
        }
        bool CheckStock(List<TrapStockEntry> stock, bool shop, HashSet<string> defs, out string reason)
        {
            reason = null; var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in stock)
                if (entry == null || string.IsNullOrWhiteSpace(entry.TrapDefId) || !defs.Contains(entry.TrapDefId) || !ids.Add(entry.TrapDefId) ||
                    entry.Count < 0 || (shop && entry.Count > definitions.Get<TrapDef>(entry.TrapDefId).ShopStockMax))
                { reason = "상점 또는 보관 재고가 잘못됐습니다"; return false; }
            if (shop && ids.Count != defs.Count) { reason = "함정 상점 재고가 누락됐습니다"; return false; }
            return true;
        }
        static bool ValidTerrain(TerrainDef value)
        {
            if (value == null || !Finite(value.CastleHalfSize) || value.CastleHalfSize <= 0 || !Finite(value.TrapCellSize) || value.TrapCellSize <= 0 ||
                !Finite(value.TrapNear) || value.TrapNear < 0 || !Finite(value.TrapFar) || value.TrapFar <= value.TrapNear ||
                !Finite(value.FieldSize) || value.TrapFar > value.FieldSize - value.CastleHalfSize || value.Hills == null) return false;
            foreach (var h in value.Hills) if (!Finite(h.x) || !Finite(h.y) || !Finite(h.z) || h.y <= 0) return false;
            return true;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        string Stamp()
        {
            var b = new StringBuilder();
            Action<object> add = v => { string s = v is float f ? f.ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""; b.Append(s.Length).Append(':').Append(s); };
            add(campaign.Gold); add(campaign.NextInstanceNumber); add(campaign.BattleNumber); add(campaign.TrapShopRestockedForBattle);
            foreach (var list in new[] { campaign.TrapShop, campaign.TrapStock }) { add(list.Count); foreach (var e in list) { add(e.TrapDefId); add(e.Count); } }
            add(campaign.Traps.Count);
            foreach (var e in campaign.Traps) { add(e.TrapId); add(e.TrapDefId); add(e.TerrainId); add(e.SlotId); add(e.ActualX); add(e.ActualZ); add(e.Marked); add(e.MarkX); add(e.MarkZ); }
            if (campaign.TerrainWorks != null) { add(campaign.TerrainWorks.Count); foreach (var w in campaign.TerrainWorks) if (w != null) { add(w.TerrainId); add(w.CellId); add((int)w.Kind); } }   // 지형 손질 (K-049)
            foreach (var def in definitions.All<TrapDef>()) { add(def.Id); add(def.Price); add(def.DamageRadius); add(def.ShopStockStart); add(def.ShopStockPerPrep); add(def.ShopStockMax); }
            if (terrain != null) { add(terrain.Id); add(terrain.CastleHalfSize); add(terrain.TrapCellSize); add(terrain.TrapNear); add(terrain.TrapFar); add(terrain.FieldSize); foreach (var h in terrain.Hills) { add(h.x); add(h.y); add(h.z); } }
            return b.ToString();
        }
    }
}
