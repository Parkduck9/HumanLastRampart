using System;
using System.Collections.Generic;
using System.Text;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;

namespace HumanLastRampart.Meta
{
    // 결과 표시만 담당한다. 현재 Campaign의 반영 후 값 대신 시작 스냅샷과 결과를 맞춘다.
    public static class ResultReportFormatter
    {
        public sealed class Report
        {
            public string Summary;
            public string Walls;
            public string Squads;
            public string Heroes;
        }

        public static Report Build(BattleSetup setup, BattleResult result,
            Func<string, string> unitName = null, Func<string, string> postWall = null,
            Func<string, int> unitMaxHp = null)
        {
            if (setup == null || result == null) throw new ArgumentException("전투 시작 또는 결과 없음");
            var wallResults = Index(result.WallOutcomes, w => w.SegmentId);
            var losses = Index(result.SquadLosses, s => s.SquadId);
            var heroes = Index(result.HeroOutcomes, h => h.InstanceId);
            var walls = new StringBuilder();
            var squads = new StringBuilder();
            var heroText = new StringBuilder();
            long totalDamage = 0, totalLoss = 0;
            foreach (string id in WallIds.All)
            {
                var start = setup.Wall.Find(w => w.SegmentId == id);
                if (start == null) throw new ArgumentException("성벽 시작 정보 없음: " + id);
                int after = wallResults.TryGetValue(id, out var outcome) ? outcome.HpAfter : start.Hp;
                totalDamage += (long)start.Hp - after;
                walls.AppendLine($"{WallName(id)}: {start.Hp:N0} → {after:N0} / 최대 {start.MaxHp:N0}")
                    .AppendLine($"이번 피해 {(long)start.Hp - after:N0}").AppendLine();
            }
            foreach (var start in setup.Squads)
            {
                int lost = losses.TryGetValue(start.SquadId, out var loss) ? loss.LostCount : 0;
                totalLoss += lost;
                string wall = string.IsNullOrEmpty(start.PostId) ? "미배치" : WallName(postWall?.Invoke(start.PostId));
                squads.AppendLine($"{wall} · {Name(unitName, start.UnitDefId)} [{start.SquadId}]")
                    .AppendLine($"{start.Count:N0} → {start.Count - lost:N0}명 · 손실 {lost:N0}" +
                        (start.Count > 0 && lost == start.Count ? " · 전멸" : "")).AppendLine();
            }
            foreach (var start in setup.Heroes)
            {
                bool participated = heroes.TryGetValue(start.InstanceId, out var outcome);
                int after = participated ? outcome.HpAfter : start.Hp;
                bool alive = participated ? outcome.IsAliveAfter : start.IsAlive;
                int max = unitMaxHp?.Invoke(start.UnitDefId) ?? 0;
                heroText.AppendLine($"{Name(unitName, start.UnitDefId)} [{start.InstanceId}]")
                    .AppendLine($"{(alive ? "생존" : "사망")} · HP {start.Hp:N0} → {after:N0}" +
                        (max > 0 ? $" / 최대 {max:N0}" : "") + (participated ? "" : " · 미참전")).AppendLine();
            }
            return new Report
            {
                Summary = $"밤 종료 · 처치 {result.DefeatedEnemyCount:N0}\n" +
                    $"획득 골드 {result.EarnedGold:N0} · 병력 손실 {totalLoss:N0}명\n성벽 피해 합계 {totalDamage:N0}",
                Walls = walls.ToString(),
                Squads = squads.Length == 0 ? "참가 분대 없음" : squads.ToString(),
                Heroes = heroText.Length == 0 ? "영웅 없음" : heroText.ToString()
            };
        }

        static Dictionary<string, T> Index<T>(List<T> values, Func<T, string> id) where T : class
        {
            if (values == null) throw new ArgumentException("결과 목록 없음");
            var map = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (T value in values)
            {
                if (value == null || string.IsNullOrEmpty(id(value))) throw new ArgumentException("결과 ID 없음");
                map.Add(id(value), value);
            }
            return map;
        }

        static string Name(Func<string, string> lookup, string id) => lookup?.Invoke(id) ?? id;
        static string WallName(string id)
        {
            switch (id)
            {
                case WallIds.North: return "북쪽 성벽";
                case WallIds.East: return "동쪽 성벽";
                case WallIds.South: return "남쪽 성벽";
                case WallIds.West: return "서쪽 성벽";
                default: return "배치 정보 없음";
            }
        }
    }
}
