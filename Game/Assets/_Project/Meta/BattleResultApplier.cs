using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;

namespace HumanLastRampart.Meta
{
    // C-007/D-035 최소 범위. 검증·복사 준비 중 Campaign을 변경하지 않는다.
    public static class BattleResultApplier
    {
        // 성공(이미 반영된 결과 포함)은 true. 실패 시 Campaign은 그대로다.
        public static bool TryApply(CampaignState campaign, out string error)
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
                    (long)campaign.BattleNumber == (long)result.BattleNumber + 1, "끝난 전투 번호가 맞지 않습니다");
                Require(result.Outcome == BattleOutcome.Victory, "승리 결과가 아닙니다");

                // 이미 적용한 뒤에는 시작 상태와 다를 수 있다. 상태 검사보다 먼저 반환한다.
                if (campaign.LastAppliedBattleRunId == result.BattleRunId) return true;

                Require(campaign.Gold >= 0 && result.EarnedGold >= 0, "골드가 음수입니다");
                Require(result.DefeatedEnemyCount >= 0 && result.DefeatedSpecialCount >= 0 &&
                    result.DefeatedSpecialCount <= result.DefeatedEnemyCount, "처치 수가 올바르지 않습니다");
                Require(result.DefeatReason == DefeatReason.None && string.IsNullOrEmpty(result.DefeatWallId),
                    "승리 결과에 패배 정보가 있습니다");
                long gold = (long)campaign.Gold + result.EarnedGold;
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
                    Require(startHeroes.TryGetValue(id, out HeroState hero) && hero.IsAlive &&
                        !string.IsNullOrWhiteSpace(hero.PostId), "참가 영웅이 시작 배치와 맞지 않습니다: " + id);
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

                // 함정은 Phase 3에서 연결한다. 미지원 결과를 조용히 일부만 적용하지 않는다.
                Require(result.ConsumedTrapIds != null && result.TrapMoves != null,
                    "함정 결과 목록이 없습니다");
                Require(result.ConsumedTrapIds.Count == 0 && result.TrapMoves.Count == 0,
                    "함정 결과 반영은 아직 지원하지 않습니다");

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
                var nextSquads = new List<SquadState>(campaign.Squads.Count);
                foreach (SquadState current in campaign.Squads)
                {
                    SquadState copy = current.Clone();
                    if (losses.TryGetValue(copy.SquadId, out SquadLoss loss)) copy.Count -= loss.LostCount;
                    if (copy.Count > 0) nextSquads.Add(copy);
                }
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
                campaign.LastAppliedBattleRunId = result.BattleRunId;
                return true;
            }
            catch (InvalidResultException exception)
            {
                error = exception.Message;
                return false;
            }
        }

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
