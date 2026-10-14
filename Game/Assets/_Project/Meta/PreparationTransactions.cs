using System;
using System.Collections.Generic;
using System.Text;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Economy;

namespace HumanLastRampart.Meta
{
    // C-017: one synchronous Prep visit owns its quotes. No persistent Campaign fields.
    public sealed class PreparationTransactions
    {
        public sealed class Quote
        {
            public int Cost { get; internal set; }
            public int Amount { get; internal set; }
            public string Description { get; internal set; }
            internal string Stamp, PostId, UnitId, WallId;
            internal bool Recruit, RepairAll;
            internal int Requested;
        }

        readonly CampaignState campaign;
        readonly IDefinitionLookup definitions;
        readonly PreparationEconomyDef rules;
        Quote active;
        bool closed;
        public PreparationEconomyDef Rules => rules;

        public PreparationTransactions(CampaignState campaign, IDefinitionLookup definitions)
        {
            this.campaign = campaign;
            this.definitions = definitions;
            if (campaign == null || definitions == null) throw new ArgumentException("진행 정보 또는 정의가 없습니다");
            var all = definitions.All<PreparationEconomyDef>();
            if (all == null || all.Count != 1 || all[0] == null || all[0].Id != PreparationEconomyDef.ExpectedId)
                throw new ArgumentException("정비 가격 정의가 정확히 하나여야 합니다");
            rules = all[0];
            if (!ValidateState(out var reason)) throw new ArgumentException(reason);
        }

        public void Close() { closed = true; active = null; }
        public void CancelQuote() { active = null; }

        public bool TryRecruit(string postId, string unitId, int count, out Quote quote, out string reason, out bool dataError)
        {
            quote = null; reason = null; dataError = false; active = null;
            if (closed) { reason = "종료된 정비 화면입니다"; return false; }
            if (!ValidateState(out reason)) { dataError = true; return false; }
            var post = definitions.Get<PostDef>(postId);
            var unit = definitions.Get<UnitDef>(unitId);
            if (post == null || unit == null) { dataError = true; reason = "모집 지점 또는 병종 정의가 없습니다"; return false; }
            if (count <= 0) { reason = "모집 인원은 1명 이상이어야 합니다"; return false; }
            bool compatible = post.Kind == PostKind.Cannon ? unit.Kind == UnitKind.CannonCrew :
                unit.Kind == UnitKind.Archer || unit.Kind == UnitKind.Gunner;
            if (!compatible) { reason = "이 지점에는 해당 병종을 배치할 수 없습니다"; return false; }
            var existing = AtPost(postId);
            if (existing != null && existing.UnitDefId != unitId) { reason = "기존 분대와 같은 병종만 보충할 수 있습니다"; return false; }
            int cap = post.Kind == PostKind.Cannon ? rules.CannonCrewPostCapacity : rules.SquadPostCapacity;
            if ((long)(existing?.Count ?? 0) + count > cap) { reason = $"지점 정원 {cap}명을 넘습니다"; return false; }
            if ((long)WallCount(post.WallId) + count > rules.WallCapacity) { reason = $"성벽 정원 {rules.WallCapacity}명을 넘습니다"; return false; }
            if (!PreparationEconomyRules.TryQuoteRecruit(rules, unit.Kind, TotalCount(), count, out int cost, out reason)) return false;
            if (campaign.Gold < cost) { reason = $"골드 부족: 필요 {cost:N0}, 보유 {campaign.Gold:N0}"; return false; }
            if (existing == null && (campaign.NextInstanceNumber == int.MaxValue ||
                campaign.Squads.Exists(s => s.SquadId == "squad_" + ((long)campaign.NextInstanceNumber + 1))))
            { reason = "새 분대 ID를 안전하게 발급할 수 없습니다"; dataError = true; return false; }
            quote = new Quote { Recruit = true, PostId = postId, UnitId = unitId, Requested = count,
                Cost = cost, Amount = count, Stamp = Stamp(),
                Description = $"{WallName(post.WallId)} / {post.DisplayName}\n{unit.DisplayName} {count:N0}명 · {cost:N0}골드\n전체 {TotalCount():N0} → {TotalCount()+count:N0}/{rules.GlobalCapacity:N0}\n지점 {(existing?.Count ?? 0)+count}/{cap} · 성벽 {WallCount(post.WallId)+count}/{rules.WallCapacity}\n잔액 {campaign.Gold-cost:N0}" };
            active = quote;
            return true;
        }

        public bool TryRepair(string wallId, int requested, bool all, out Quote quote, out string reason, out bool dataError)
        {
            quote = null; reason = null; dataError = false; active = null;
            if (closed) { reason = "종료된 정비 화면입니다"; return false; }
            if (!ValidateState(out reason)) { dataError = true; return false; }
            if (requested <= 0) { reason = "수리 요청은 1HP 이상이어야 합니다"; return false; }
            if (!all && campaign.FindWall(wallId) == null) { reason = "수리 성벽이 없습니다"; dataError = true; return false; }
            long amount = 0, cost = 0;
            var detail = new StringBuilder();
            foreach (var wall in campaign.Wall)
            {
                if (!all && wall.SegmentId != wallId) continue;
                if (!PreparationEconomyRules.TryQuoteRepair(rules, wall.Hp, wall.MaxHp, requested, out int hp, out int gold, out reason))
                { dataError = true; return false; }
                amount += hp; cost += gold;
                detail.AppendLine($"{WallName(wall.SegmentId)}: {wall.Hp:N0} → {wall.Hp+hp:N0}/{wall.MaxHp:N0} · {gold:N0}골드");
            }
            if (cost > int.MaxValue || amount > int.MaxValue) { reason = "합계가 저장 범위를 넘습니다"; dataError = true; return false; }
            if (amount == 0) { reason = "이미 완전히 수리돼 있습니다"; return false; }
            if (cost > campaign.Gold) { reason = $"골드 부족: 필요 {cost:N0}, 보유 {campaign.Gold:N0}"; return false; }
            quote = new Quote { Recruit = false, RepairAll = all, WallId = wallId, Requested = requested,
                Amount = (int)amount, Cost = (int)cost, Stamp = Stamp(),
                Description = detail + $"복구 합계 {amount:N0}HP · 비용 {cost:N0}골드\n잔액 {campaign.Gold-cost:N0}" };
            active = quote; return true;
        }

        public bool TryCommit(Quote quote, out string reason, out bool dataError)
        {
            reason = null; dataError = false;
            if (closed || quote == null || !ReferenceEquals(active, quote)) { reason = "만료되거나 이미 사용한 견적입니다"; return false; }
            active = null; // consume before any attempt; errors require a fresh confirmation
            if (!ValidateState(out reason)) { dataError = true; return false; }
            if (quote.Stamp != Stamp()) { reason = "상태가 변경됐습니다. 새 견적을 확인해 주세요"; return false; }
            if (campaign.Gold < quote.Cost) { reason = "골드가 부족합니다"; return false; }
            List<SquadState> squads = null;
            List<WallSegmentState> walls = null;
            bool newSquad = false;
            if (quote.Recruit)
            {
                squads = campaign.Squads.ConvertAll(s => s.Clone());
                var target = squads.Find(s => s.PostId == quote.PostId);
                if (target == null)
                {
                    newSquad = true;
                    target = new SquadState { SquadId = "squad_" + ((long)campaign.NextInstanceNumber + 1),
                        UnitDefId = quote.UnitId, PostId = quote.PostId };
                    squads.Add(target);
                }
                target.Count += quote.Amount;
            }
            else
            {
                walls = campaign.Wall.ConvertAll(w => w.Clone());
                foreach (var wall in walls)
                {
                    if (!quote.RepairAll && wall.SegmentId != quote.WallId) continue;
                    if (!PreparationEconomyRules.TryQuoteRepair(rules, wall.Hp, wall.MaxHp, quote.Requested, out int hp, out _, out reason))
                    { dataError = true; return false; }
                    wall.Hp += hp;
                }
            }
            // No validation/allocation after this point; publish prepared state once.
            if (newSquad) campaign.NewInstanceId("squad");
            campaign.Gold -= quote.Cost;
            if (squads != null) campaign.Squads = squads;
            if (walls != null) campaign.Wall = walls;
            return true;
        }

        public int TotalCount()
        {
            long count = 0;
            foreach (var s in campaign.Squads) count += s.Count;
            return checked((int)count);
        }
        public int WallCount(string wallId)
        {
            int count = 0;
            foreach (var s in campaign.Squads)
                if (!string.IsNullOrEmpty(s.PostId) && definitions.Get<PostDef>(s.PostId)?.WallId == wallId) count += s.Count;
            return count;
        }
        SquadState AtPost(string id) => campaign.Squads.Find(s => s.PostId == id);

        bool ValidateState(out string reason)
        {
            reason = null;
            var errors = PreparationEconomyRules.Validate(rules);
            if (errors.Count != 0) { reason = string.Join("; ", errors); return false; }
            var economy = definitions.All<PreparationEconomyDef>();
            if (economy == null || economy.Count != 1 || !ReferenceEquals(economy[0], rules) || rules.Id != PreparationEconomyDef.ExpectedId)
            { reason = "정비 가격 정의가 변경됐습니다"; return false; }
            if (campaign.Gold < 0 || campaign.NextInstanceNumber < 0 || campaign.Squads == null || campaign.Wall == null)
            { reason = "진행 데이터의 골드·인원·ID가 잘못됐습니다"; return false; }
            var postDefs = definitions.All<PostDef>();
            if (postDefs == null) { reason = "지점 정의 목록이 없습니다"; return false; }
            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in postDefs)
                if (p == null || string.IsNullOrWhiteSpace(p.Id) || !definitionIds.Add(p.Id) ||
                    (int)p.Wall < 0 || (int)p.Wall > 3 || (p.Kind != PostKind.Squad && p.Kind != PostKind.Cannon))
                { reason = "지점 정의 ID·종류·성벽이 잘못됐습니다"; return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var posts = new HashSet<string>(StringComparer.Ordinal);
            var wallCounts = new Dictionary<string, long>(StringComparer.Ordinal);
            long total = 0;
            foreach (var s in campaign.Squads)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.SquadId) || !ids.Add(s.SquadId) || s.Count <= 0)
                { reason = "분대 ID·인원이 잘못됐습니다"; return false; }
                var unit = definitions.Get<UnitDef>(s.UnitDefId);
                if (unit == null || unit.IsIndividual) { reason = "분대 병종이 잘못됐습니다"; return false; }
                total += s.Count;
                if (string.IsNullOrEmpty(s.PostId)) continue;
                var p = definitions.Get<PostDef>(s.PostId);
                if (p == null || !posts.Add(s.PostId)) { reason = "지점 정의가 없거나 분대가 중복 배치됐습니다"; return false; }
                bool cannon = p.Kind == PostKind.Cannon;
                if ((p.Kind != PostKind.Squad && !cannon) ||
                    (cannon ? unit.Kind != UnitKind.CannonCrew : unit.Kind != UnitKind.Archer && unit.Kind != UnitKind.Gunner) ||
                    s.Count > (cannon ? rules.CannonCrewPostCapacity : rules.SquadPostCapacity))
                { reason = "분대 병종·지점 정원이 맞지 않습니다"; return false; }
                wallCounts.TryGetValue(p.WallId, out long count); wallCounts[p.WallId] = count + s.Count;
            }
            if (total > rules.GlobalCapacity) { reason = "전체 병력 정원이 초과됐습니다"; return false; }
            ids.Clear();
            foreach (var wall in campaign.Wall)
            {
                if (wall == null || !WallIds.TryParse(wall.SegmentId, out _) || !ids.Add(wall.SegmentId) ||
                    wall.MaxHp <= 0 || wall.Hp < 0 || wall.Hp > wall.MaxHp)
                { reason = "성벽 체력·ID가 잘못됐습니다"; return false; }
                if (wallCounts.TryGetValue(wall.SegmentId, out long count) && count > rules.WallCapacity)
                { reason = "성벽 병력 정원이 초과됐습니다"; return false; }
            }
            if (ids.Count != 4) { reason = "성벽은 정확히 4개여야 합니다"; return false; }
            return true;
        }

        string Stamp()
        {
            var b = new StringBuilder();
            b.Append(campaign.Gold).Append('|').Append(campaign.NextInstanceNumber).Append('|').Append(campaign.BattleNumber)
                .Append('|').Append(campaign.LastAppliedBattleRunId).Append('|').Append(campaign.ActiveBattleSetup?.BattleRunId);
            foreach (var s in campaign.Squads) b.Append('|').Append(s.SquadId).Append(':').Append(s.UnitDefId).Append(':').Append(s.PostId).Append(':').Append(s.Count);
            foreach (var w in campaign.Wall) b.Append('|').Append(w.SegmentId).Append(':').Append(w.Hp).Append(':').Append(w.MaxHp);
            b.Append('|').Append(rules.Id).Append(':').Append(rules.GlobalCapacity).Append(':').Append(rules.WallCapacity)
                .Append(':').Append(rules.SquadPostCapacity).Append(':').Append(rules.CannonCrewPostCapacity)
                .Append(':').Append(rules.RepairHpPerGold).Append(':').Append(rules.DefaultRepairHp);
            foreach (int v in rules.TierStartCounts) b.Append(',').Append(v);
            foreach (var kind in new[] { UnitKind.Archer, UnitKind.Gunner, UnitKind.CannonCrew })
                foreach (int v in rules.PricesFor(kind)) b.Append(',').Append(v);
            foreach (var p in definitions.All<PostDef>()) b.Append('|').Append(p.Id).Append(':').Append(p.Kind).Append(':').Append(p.WallId);
            foreach (var u in definitions.All<UnitDef>()) b.Append('|').Append(u.Id).Append(':').Append(u.Kind);
            return b.ToString();
        }

        public static string WallName(string id) => id == WallIds.North ? "북쪽" : id == WallIds.East ? "동쪽" : id == WallIds.South ? "남쪽" : "서쪽";
    }
}
