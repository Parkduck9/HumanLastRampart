using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;

namespace HumanLastRampart.Meta
{
    // C-007/D-035 최소 범위. 검증·복사 준비 중 Campaign을 변경하지 않는다.
    public static class BattleResultApplier
    {
        // 성공(이미 반영된 결과 포함)은 true. 실패 시 Campaign은 그대로다.
        // lostNight (W11, 사용자 규칙): 진 밤의 결과도 반영한다 — 병력 손실·성벽 피해·영웅·함정·소모품은 남고, 보상 골드는 없다. 번호는 올리지 않는 밤이라 번호 검사만 다르다
        public static bool TryApply(CampaignState campaign, out string error, TerrainDef terrain = null, bool lostNight = false)
        {
            error = null;
            try
            {
                Require(campaign != null, "진행 데이터가 없습니다");
                BattleResult result = campaign.LastBattle;
                BattleSetup setup = campaign.ActiveBattleSetup;
                Require(result != null && setup != null, "전투 준비 또는 결과가 없습니다");
                Require(!string.IsNullOrWhiteSpace(result.BattleRunId) && result.BattleRunId == setup.BattleRunId,
                    "이번 전투의 실행 ID와 결과가 다릅니다");
                Require(result.BattleNumber > 0 && result.BattleNumber == setup.BattleNumber &&
                    (long)campaign.BattleNumber >= (long)result.BattleNumber + (lostNight ? 0 : 1), "끝난 전투 번호가 맞지 않습니다");   // W8: 결과 번호는 전투 자리(1~4), 진행 번호는 밤 번호라 5번째 밤부터 더 클 수 있다
                Require(result.Outcome == (lostNight ? BattleOutcome.Defeat : BattleOutcome.Victory), lostNight ? "패배 결과가 아닙니다" : "승리 결과가 아닙니다");

                // 이미 적용한 뒤에는 시작 상태와 다를 수 있다. 상태 검사보다 먼저 반환한다.
                if (campaign.LastAppliedBattleRunId == result.BattleRunId) return true;

                Require(campaign.Gold >= 0 && result.EarnedGold >= 0, "골드가 음수입니다");
                Require(result.DefeatedEnemyCount >= 0 && result.DefeatedSpecialCount >= 0 &&
                    result.DefeatedSpecialCount <= result.DefeatedEnemyCount, "처치 수가 올바르지 않습니다");
                Require(lostNight || (result.DefeatReason == DefeatReason.None && string.IsNullOrEmpty(result.DefeatWallId)),
                    "승리 결과에 패배 정보가 있습니다");
                long gold = (long)campaign.Gold + (lostNight ? 0 : result.EarnedGold);
                Require(gold <= int.MaxValue, "골드가 저장 범위를 넘습니다");

                var startHeroes = Index(setup.Heroes, h => h.InstanceId, "시작 영웅");
                var currentHeroes = Index(campaign.Heroes, h => h.InstanceId, "진행 영웅");
                var startSquads = Index(setup.Squads, s => s.SquadId, "시작 분대");
                var currentSquads = Index(campaign.Squads, s => s.SquadId, "진행 분대");
                var startWalls = Index(setup.Wall, w => w.SegmentId, "시작 성벽");
                var currentWalls = Index(campaign.Wall, w => w.SegmentId, "진행 성벽");
                Require(startHeroes.Count == currentHeroes.Count && startSquads.Count == currentSquads.Count &&
                    startWalls.Count == currentWalls.Count, "전투 시작 후 병력 또는 성벽 목록이 변경됐습니다");

                foreach (var pair in startHeroes)
                {
                    HeroState start = pair.Value;
                    Require(start.Hp >= 0 && start.IsAlive == (start.Hp > 0) &&
                        !string.IsNullOrWhiteSpace(start.UnitDefId), "시작 영웅 상태가 올바르지 않습니다");
                    Require(currentHeroes.TryGetValue(pair.Key, out HeroState current) &&
                        current.UnitDefId == start.UnitDefId && current.PostId == start.PostId &&
                        current.Hp == start.Hp && current.IsAlive == start.IsAlive,
                        "전투 시작 후 영웅 상태가 변경됐습니다: " + pair.Key);
                }
                foreach (var pair in startSquads)
                {
                    SquadState start = pair.Value;
                    Require(start.Count >= 0 && !string.IsNullOrWhiteSpace(start.UnitDefId),
                        "시작 분대 상태가 올바르지 않습니다");
                    Require(currentSquads.TryGetValue(pair.Key, out SquadState current) &&
                        current.UnitDefId == start.UnitDefId && current.PostId == start.PostId && current.Count == start.Count,
                        "전투 시작 후 분대 상태가 변경됐습니다: " + pair.Key);
                }
                Require(startWalls.Count == WallIds.All.Length, "성벽은 정확히 4개여야 합니다");
                foreach (var pair in startWalls)
                {
                    WallSegmentState start = pair.Value;
                    Require(WallIds.TryParse(pair.Key, out _) && start.MaxHp > 0 && start.Hp >= 0 && start.Hp <= start.MaxHp,
                        "시작 성벽 상태가 올바르지 않습니다");
                    Require(currentWalls.TryGetValue(pair.Key, out WallSegmentState current) &&
                        current.Hp == start.Hp && current.MaxHp == start.MaxHp,
                        "전투 시작 후 성벽 상태가 변경됐습니다: " + pair.Key);
                }

                Require(setup.ParticipatingHeroIds != null, "참가 영웅 목록이 없습니다");
                var participants = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in setup.ParticipatingHeroIds)
                {
                    Require(!string.IsNullOrWhiteSpace(id) && participants.Add(id), "참가 영웅 ID가 비었거나 중복됐습니다");
                    // C-005 v1.3: 참가 선정은 App이 맡는다. 주인공·마법사는 배치 지점 없이도 참가한다.
                    Require(startHeroes.TryGetValue(id, out HeroState hero) && hero.IsAlive,
                        "참가 영웅이 시작 생존 상태와 맞지 않습니다: " + id);
                }
                var heroes = Index(result.HeroOutcomes, h => h.InstanceId, "영웅 결과");
                Require(heroes.Count == participants.Count, "참가 영웅별 결과가 누락되거나 추가됐습니다");
                foreach (var pair in heroes)
                {
                    HeroOutcome outcome = pair.Value;
                    Require(participants.Contains(pair.Key), "참가하지 않은 영웅의 결과입니다: " + pair.Key);
                    Require(outcome.HpAfter >= 0 && outcome.HpAfter <= startHeroes[pair.Key].Hp &&
                        outcome.IsAliveAfter == (outcome.HpAfter > 0), "영웅 체력 또는 생존 결과가 올바르지 않습니다: " + pair.Key);
                }
                var losses = Index(result.SquadLosses, s => s.SquadId, "분대 손실");
                foreach (var pair in losses)
                {
                    Require(startSquads.TryGetValue(pair.Key, out SquadState squad), "알 수 없는 분대 결과입니다: " + pair.Key);
                    Require(pair.Value.LostCount >= 0 && pair.Value.LostCount <= squad.Count,
                        "분대 손실이 시작 인원 범위를 넘습니다: " + pair.Key);
                }
                var walls = Index(result.WallOutcomes, w => w.SegmentId, "성벽 결과");
                foreach (var pair in walls)
                {
                    Require(startWalls.TryGetValue(pair.Key, out WallSegmentState wall), "알 수 없는 성벽 결과입니다: " + pair.Key);
                    Require(pair.Value.HpAfter >= 0 && pair.Value.HpAfter <= wall.Hp,
                        "성벽 체력이 시작 범위를 넘습니다: " + pair.Key);
                }

                // C-007 v1.1: 소비/이동을 다른 결과와 함께 검증·복사 후 반영한다.
                List<TrapPlacement> nextTraps = PrepareTraps(campaign, setup, result, terrain);

                var nextHeroes = new List<HeroState>(campaign.Heroes.Count);
                foreach (HeroState current in campaign.Heroes)
                {
                    HeroState copy = current.Clone();
                    if (heroes.TryGetValue(copy.InstanceId, out HeroOutcome outcome))
                    {
                        copy.Hp = outcome.HpAfter;
                        copy.IsAlive = outcome.IsAliveAfter;
                    }
                    nextHeroes.Add(copy); // 사망한 영웅도 표시를 위해 유지한다.
                }
                // 군의관 천막 (K-058): 잃은 병사 중 일부가 돌아온다 (분대마다 반올림)
                float recover = setup.Boosts != null ? Math.Max(0f, Math.Min(1f, setup.Boosts.RecoverLostRatio)) : 0f;
                var nextSquads = new List<SquadState>(campaign.Squads.Count);
                foreach (SquadState current in campaign.Squads)
                {
                    SquadState copy = current.Clone();
                    if (losses.TryGetValue(copy.SquadId, out SquadLoss loss))
                        copy.Count -= loss.LostCount - (int)Math.Floor(loss.LostCount * recover + 0.5f);
                    if (copy.Count > 0) nextSquads.Add(copy);
                }

                // 용병 손실 (K-058, C-006 v1.3 초안): 계약 ID로 맞춘다
                var startMercs = Index(setup.Mercenaries ?? new List<SquadState>(), s => s.SquadId, "시작 용병");
                var mercLosses = Index(result.MercenaryLosses ?? new List<SquadLoss>(), s => s.SquadId, "용병 손실");
                foreach (var pair in mercLosses)
                {
                    Require(startMercs.TryGetValue(pair.Key, out SquadState merc), "알 수 없는 용병 결과입니다: " + pair.Key);
                    Require(pair.Value.LostCount >= 0 && pair.Value.LostCount <= merc.Count, "용병 손실이 시작 인원 범위를 넘습니다: " + pair.Key);
                }
                var nextMercs = new List<MercenaryContract>();
                foreach (MercenaryContract current in campaign.Mercenaries ?? new List<MercenaryContract>())
                {
                    MercenaryContract copy = current.Clone();
                    if (mercLosses.TryGetValue(copy.ContractId, out SquadLoss loss)) copy.Count -= loss.LostCount;
                    if (copy.Count > 0) nextMercs.Add(copy);
                }

                // 이번 전투에 쓴 소모품 하나씩 (불화살 꾸러미·화약 통)
                var nextItems = new List<OwnedItem>();
                foreach (OwnedItem current in campaign.Items ?? new List<OwnedItem>()) nextItems.Add(current.Clone());
                foreach (string used in setup.ConsumedItemIds ?? new List<string>())
                {
                    OwnedItem line = nextItems.Find(i => i.ItemId == used && string.IsNullOrEmpty(i.WallId) && i.Count > 0);
                    if (line != null) line.Count--;
                }
                nextItems.RemoveAll(i => i.Count <= 0);
                var nextWalls = new List<WallSegmentState>(campaign.Wall.Count);
                foreach (WallSegmentState current in campaign.Wall)
                {
                    WallSegmentState copy = current.Clone();
                    if (walls.TryGetValue(copy.SegmentId, out WallOutcome outcome)) copy.Hp = outcome.HpAfter;
                    nextWalls.Add(copy);
                }

                // 이 구간에는 기다림·UI·로그·외부 콜백이 없다. 성공 표시를 마지막에 기록한다.
                campaign.Gold = (int)gold;
                campaign.Heroes = nextHeroes;
                campaign.Squads = nextSquads;
                campaign.Wall = nextWalls;
                campaign.Traps = nextTraps;
                campaign.Mercenaries = nextMercs;
                campaign.Items = nextItems;
                campaign.LastAppliedBattleRunId = result.BattleRunId;
                return true;
            }
            catch (InvalidResultException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        static List<TrapPlacement> PrepareTraps(CampaignState campaign, BattleSetup setup,
            BattleResult result, TerrainDef terrain)
        {
            Require(result.ConsumedTrapIds != null && result.TrapMoves != null, "함정 결과 목록이 없습니다");
            var starts = Index(setup.Traps, t => t.TrapId, "시작 함정");
            var current = Index(campaign.Traps, t => t.TrapId, "진행 함정");
            bool hasResults = result.ConsumedTrapIds.Count > 0 || result.TrapMoves.Count > 0;
            Require((starts.Count == 0 && !hasResults) || !string.IsNullOrWhiteSpace(setup.TerrainId),
                "함정 결과의 전투 지형이 없습니다");
            foreach (var pair in starts)
            {
                TrapPlacement start = pair.Value;
                Require(start.TerrainId == setup.TerrainId && !string.IsNullOrWhiteSpace(start.TrapDefId) &&
                    !string.IsNullOrWhiteSpace(start.SlotId) && Finite(start.ActualX) && Finite(start.ActualZ) &&
                    (!start.Marked || (Finite(start.MarkX) && Finite(start.MarkZ))), "시작 함정 상태가 올바르지 않습니다");
                Require(current.TryGetValue(pair.Key, out TrapPlacement value) &&
                    value.TerrainId == start.TerrainId && value.TrapDefId == start.TrapDefId && value.SlotId == start.SlotId &&
                    value.ActualX == start.ActualX && value.ActualZ == start.ActualZ && value.Marked == start.Marked &&
                    (!start.Marked || (value.MarkX == start.MarkX && value.MarkZ == start.MarkZ)),
                    "전투 시작 후 함정 상태가 변경됐습니다: " + pair.Key);
            }
            if (!string.IsNullOrWhiteSpace(setup.TerrainId))
                foreach (var pair in current)
                    Require(pair.Value.TerrainId != setup.TerrainId || starts.ContainsKey(pair.Key),
                        "전투 시작 후 이 지형에 함정이 추가됐습니다: " + pair.Key);

            var consumed = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in result.ConsumedTrapIds)
                Require(!string.IsNullOrWhiteSpace(id) && consumed.Add(id) && starts.ContainsKey(id),
                    "소비 함정 ID가 비었거나 중복되거나 이번 전투에 없습니다");
            var moves = Index(result.TrapMoves, m => m.TrapId, "이동 함정");
            if (moves.Count > 0)
                Require(terrain != null && terrain.Id == setup.TerrainId && Finite(terrain.CastleHalfSize) &&
                    terrain.CastleHalfSize > 0f && Finite(terrain.TrapNear) && terrain.TrapNear >= 0f &&
                    Finite(terrain.FieldSize) && terrain.FieldSize > 0f, "이동 함정의 지형 정의가 올바르지 않습니다");
            foreach (var pair in moves)
            {
                Require(starts.ContainsKey(pair.Key) && !consumed.Contains(pair.Key),
                    "이동 함정이 이번 전투에 없거나 이미 소비됐습니다: " + pair.Key);
                TrapMove move = pair.Value;
                Require(Finite(move.NewX) && Finite(move.NewZ), "함정 이동 좌표가 유한 값이 아닙니다");
                // Combat.ValidGround와 같은 사각 성벽 거리. Mark/Slot은 이동시키지 않는다.
                double fromWall = Math.Max(Math.Abs((double)move.NewX), Math.Abs((double)move.NewZ)) - terrain.CastleHalfSize;
                Require(fromWall >= terrain.TrapNear * 0.5d && fromWall <= terrain.FieldSize,
                    "함정 이동 좌표가 성 안 또는 들판 밖입니다");
            }
            if (!hasResults) return campaign.Traps;
            var next = new List<TrapPlacement>(campaign.Traps.Count);
            foreach (TrapPlacement value in campaign.Traps)
            {
                if (consumed.Contains(value.TrapId)) continue;
                TrapPlacement copy = value.Clone();
                if (moves.TryGetValue(value.TrapId, out TrapMove move))
                {
                    copy.ActualX = move.NewX;
                    copy.ActualZ = move.NewZ;
                }
                next.Add(copy);
            }
            return next;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static Dictionary<string, T> Index<T>(List<T> list, Func<T, string> getId, string name) where T : class
        {
            Require(list != null, name + " 목록이 없습니다");
            var map = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (T item in list)
            {
                Require(item != null, name + " 항목이 비었습니다");
                string id = getId(item);
                Require(!string.IsNullOrWhiteSpace(id) && !map.ContainsKey(id), name + " ID가 비었거나 중복됐습니다");
                map.Add(id, item);
            }
            return map;
        }

        static void Require(bool valid, string reason)
        {
            if (!valid) throw new InvalidResultException(reason);
        }

        sealed class InvalidResultException : Exception
        {
            public InvalidResultException(string message) : base(message) { }
        }
    }
}
