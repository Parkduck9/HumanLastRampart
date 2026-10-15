using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Meta;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// 측정 도구 (밤 시험 Tools/NightTest가 씀, D-067): 1~4번 전투를 이어서 진행 — 결과 반영(Codex 반영기) → 수리·모집(시험용 정책) → 다음 전투
public sealed class SimProbe
{
    static readonly System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    [Test]
    public void CampaignBalance()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        var rules = lookup.All<BattleRulesDef>()[0];
        var perKillField = typeof(BattleRulesDef).GetField("corpseHeightPerKill", F);
        float perKill0 = (float)perKillField.GetValue(rules);
        var atkField = typeof(EnemyDef).GetField("onWallAttack", F);
        var enemies = lookup.All<EnemyDef>();
        int[] atk0 = enemies.Select(e => (int)atkField.GetValue(e)).ToArray();
        var log = new StringBuilder();

        var piles = new[] { ("더미 추천(0.1m·공격x5)", 0.1f, 5) };
        var economies = new[] { "4차 10HP=1골드 수리+남은 골드 전부 궁수" };
        try
        {
            foreach (var (pileName, perKill, atkMul) in piles)
            {
                perKillField.SetValue(rules, perKill);
                for (int i = 0; i < enemies.Count; i++) atkField.SetValue(enemies[i], atk0[i] * atkMul);
                foreach (string economy in economies)
                {
                    foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Realistic })
                    {
                        log.AppendLine($"== {pileName} / {economy} / {difficulty}");
                        RunCampaign(lookup, difficulty, economy, log);
                    }
                }
            }
        }
        finally
        {
            perKillField.SetValue(rules, perKill0);
            for (int i = 0; i < enemies.Count; i++) atkField.SetValue(enemies[i], atk0[i]);
        }

        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    static void RunCampaign(DefinitionLookup lookup, Difficulty difficulty, string economy, StringBuilder log)
    {
        CampaignState c = CampaignFactory.Create(lookup, new NewGameOptions(difficulty), 1234);
        for (int battle = 1; battle <= 4; battle++)
        {
            c.BattleNumber = battle;
            c.LastBattle = null;
            BattleSetup setup = BattleSetupFactory.Create(c, lookup, $"camp_{difficulty}_{economy.Substring(0, 2)}_{battle}", out string error);
            if (setup == null) { log.AppendLine($"  전투{battle}: 준비 오류 {error}"); return; }
            c.ActiveBattleSetup = setup;
            string wallsBefore = string.Join("/", c.Wall.Select(w => w.Hp));
            int soldiersBefore = c.Squads.Sum(s => s.Count);
            BattleResult r;
            float elapsed;
            using (var sim = new BattleSim(setup, lookup))
            {
                while (!sim.Ended && sim.Elapsed < 1500f)
                {
                    sim.BeginFrame(0.05f);
                    if (sim.Ended) break;
                    sim.Schedule(0.05f, sim.Elapsed);
                    sim.Complete();
                }
                if (!sim.Ended) { log.AppendLine($"  전투{battle}: 시간초과 처치 {sim.Kills}/{sim.TotalEnemies}"); return; }
                r = sim.BuildResult();
                elapsed = sim.Elapsed;
            }

            int lost = r.SquadLosses.Sum(l => l.LostCount);
            log.Append($"  전투{battle} [{r.Outcome}{(r.Outcome == BattleOutcome.Defeat ? " " + r.DefeatReason + " " + r.DefeatWallId : "")}] {elapsed:0}s 처치 {r.DefeatedEnemyCount} 보상 {r.EarnedGold} 잃은병사 {lost} | 시작 병사 {soldiersBefore} 성벽 {wallsBefore}");
            if (r.Outcome == BattleOutcome.Defeat) { log.AppendLine(); return; }

            // App과 같은 순서: 결과 기록 → 번호 +1 → 결과 화면 반영(Codex BattleResultApplier)
            c.LastBattle = r;
            c.BattleNumber = r.BattleNumber + 1;
            if (!BattleResultApplier.TryApply(c, out string applyError)) { log.AppendLine($" 반영 오류 {applyError}"); return; }
            string afterBattle = $" → 골드 {c.Gold} 병사 {c.Squads.Sum(s => s.Count)} 성벽 {string.Join("/", c.Wall.Select(w => w.Hp))}";

            // 정비 (시험용 정책 — 실제 게임에는 아직 없음)
            int repairUnit = economy.StartsWith("2") ? 1 : (economy.StartsWith("3") || economy.StartsWith("4")) ? 10 : 0;
            string prep = "";
            if (repairUnit > 0)
            {
                int cost = c.Wall.Sum(w => (int)Math.Ceiling((w.MaxHp - w.Hp) / (double)repairUnit));
                if (cost <= c.Gold) { c.Gold -= cost; foreach (var w in c.Wall) w.Hp = w.MaxHp; prep += $" 수리 {cost}골드"; }
                else prep += $" 수리 거부(견적 {cost} > 보유 {c.Gold})";
            }
            if (economy.StartsWith("3"))
            {
                int hired = 0;
                foreach (WallSide side in Enum.GetValues(typeof(WallSide)))
                {
                    SquadState archers = c.Squads.FirstOrDefault(s => s.UnitDefId == "unit_archer" && s.PostId != null && lookup.Get<PostDef>(s.PostId)?.Wall == side);
                    if (archers != null && c.Gold >= 40) { archers.Count += 8; c.Gold -= 40; hired += 8; }
                }
                prep += $" 모집 궁수 {hired}";
            }
            if (economy.StartsWith("4"))
            {
                int hired = 0;
                var perWall = Enum.GetValues(typeof(WallSide)).Cast<WallSide>().Select(side => c.Squads.FirstOrDefault(s => s.UnitDefId == "unit_archer" && s.PostId != null && lookup.Get<PostDef>(s.PostId)?.Wall == side)).Where(s => s != null).ToList();
                int each = c.Gold / 5 / perWall.Count;
                foreach (var sq in perWall) { sq.Count += each; c.Gold -= each * 5; hired += each; }
                prep += $" 모집 궁수 {hired}";
            }
            log.AppendLine(afterBattle + (prep.Length > 0 ? " | 정비" + prep + $" → 골드 {c.Gold} 병사 {c.Squads.Sum(s => s.Count)}" : ""));
        }
        log.AppendLine("  ★ 4번까지 모두 승리");
    }

    // 가격 2판 (Codex BALANCING_v2) 실제 측정 — 시험 전용 정책, 데이터 미변경
    [Test]
    public void PriceV2()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        var log = new StringBuilder();
        log.AppendLine($"소스 {Environment.GetEnvironmentVariable("HLR_HEAD")} · dt 0.05s · 시드 1234 · 각 조건 3회");
        var candidates = new[] { ("A", new[] { 5, 8, 12 }), ("B", new[] { 8, 12, 16 }) };
        foreach (var (name, prices) in candidates)
            foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Realistic })
                for (int rep = 1; rep <= 3; rep++)
                {
                    log.AppendLine($"== 후보 {name} (궁수 {prices[0]}/{prices[1]}/{prices[2]}) / {difficulty} / {rep}회");
                    RunPriced(lookup, difficulty, prices, log);
                }
        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    static int ArcherPrice(int[] prices, int total) => total < 600 ? prices[0] : total < 1200 ? prices[1] : prices[2];

    static void RunPriced(DefinitionLookup lookup, Difficulty difficulty, int[] prices, StringBuilder log) => RunPriced(lookup, difficulty, prices, log, false);

    // MAP-6: 사람이 하듯 자동으로 — 대포는 5초마다 사거리 안 가장 몰린 곳, 마법은 준비되면 성벽 150m 안 가장 몰린 곳, 안개·밤엔 망원경으로 각 면 무리 확인
    static void Assist(BattleSim sim)
    {
        for (int i = 0; i < sim.CannonCount; i++)
        {
            var cannon = sim.GetCannon(i);
            if (!cannon.Alive) continue;
            Vector3? p = sim.DensestPointNear(cannon.Position, cannon.Range - 4f);
            if (p.HasValue) sim.SetCannonAim(i, p.Value);
        }

        for (int s = 0; s < sim.SpellCount; s++)
        {
            if (!sim.GetSpell(s).Ready || !sim.WizardPresent) continue;
            Vector3? best = sim.DensestPointNear(Vector3.zero, sim.CastleHalf + BattleSim.SpellCastRange);
            if (best.HasValue) sim.CastSpell(s, best.Value);
        }

        if (sim.LowVisibility)
        {
            for (int side = 0; side < 4; side++)
            {
                var n = WallGeometry.Outward(side);
                Vector3? p = sim.DensestPointNear(new Vector3(n.x, 0f, n.z) * (sim.CastleHalf + 80f), 80f);
                if (p.HasValue) sim.LookThroughTelescope(p.Value);
            }
        }
    }

    [Test]
    public void AssistA()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        var log = new StringBuilder();
        log.AppendLine($"소스 {Environment.GetEnvironmentVariable("HLR_HEAD")} · dt 0.05s · 시드 1234 · 가격 A · 보조 정책(대포 5초마다 몰린 곳·마법 준비되면·망원경 안개/밤)");
        bool assist = true;
        var rulesDef = lookup.All<BattleRulesDef>().First();
        var byBattle = typeof(BattleRulesDef).GetField("easyEnemyMultiplierByBattle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (float m4 in new[] { 0.40f, 0.35f })
            foreach (Difficulty difficulty in new[] { Difficulty.Easy })
            {
                byBattle.SetValue(rulesDef, new[] { 0f, 0f, 0f, m4 });   // 메모리에서만 (저장 안 함)
                log.AppendLine($"== 보조 사용 · 1줄 배치 · 쉬움 4번 배율 {m4} / {difficulty}");
                RunPriced(lookup, difficulty, new[] { 5, 8, 12 }, log, assist);
            }
        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    // D-053(정면 공격) 뒤 균형: 정책 0 = 예전(궁수 네 면 고르게), 1 = 정면(북) 먼저, 2 = 정면 먼저 + 함정
    [Test]
    public void FrontTraps()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        var log = new StringBuilder();
        log.AppendLine($"소스 {Environment.GetEnvironmentVariable("HLR_HEAD")} · dt 0.05s · 시드 1234 · 가격 A · 보조(대포·마법·망원경) · D-053 정면 공격");
        string[] names = { "예전(궁수 네 면 고르게)", "정면 먼저", "정면 먼저 + 함정(산 만큼, 6마리 이상 몰리면 점화)" };
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Realistic })
            for (int policy = 0; policy < 3; policy++)
            {
                log.AppendLine($"== {difficulty} · {names[policy]}");
                RunPriced(lookup, difficulty, new[] { 5, 8, 12 }, log, true, policy);
            }
        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    // D-065: 성벽 40m + 적이 넓게 퍼져서 옴 — 적 수 배율별 승패 (값은 메모리에서만)
    [Test]
    public void Funnel40Sweep()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        var rulesDef = lookup.All<BattleRulesDef>().First();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var easyBase = typeof(BattleRulesDef).GetField("easyEnemyMultiplier", flags);
        var easyBy = typeof(BattleRulesDef).GetField("easyEnemyMultiplierByBattle", flags);
        var realBy = typeof(BattleRulesDef).GetField("realisticEnemyMultiplierByBattle", flags);
        object e0 = easyBase.GetValue(rulesDef), eb0 = easyBy.GetValue(rulesDef), rb0 = realBy.GetValue(rulesDef);
        var log = new StringBuilder();
        log.AppendLine($"시드 1234 · 가격 A · 보조 · 정면 먼저 + 함정 · 성벽 40m · 깔때기 들판 반폭 {BattleSim.FunnelFieldHalfWidth}m");
        try
        {
            foreach (float f in new[] { 1f, 1.5f, 2f, 3f })
            {
                easyBase.SetValue(rulesDef, 0.5f * f);
                easyBy.SetValue(rulesDef, new[] { 0f, 0f, 0f, 0.4f * f });
                realBy.SetValue(rulesDef, new[] { f, f, f, f });
                foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Realistic })
                {
                    log.AppendLine($"== 적 ×{f} / {difficulty}");
                    RunPriced(lookup, difficulty, new[] { 5, 8, 12 }, log, true, 2);
                }
            }
        }
        finally
        {
            easyBase.SetValue(rulesDef, e0); easyBy.SetValue(rulesDef, eb0); realBy.SetValue(rulesDef, rb0);
        }

        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    // 버그 찾기 10회: 시드·난이도를 바꿔 4번 전투까지, 매초 사람처럼 아무렇게나 조작(마법 아무 데나·함정·대포·명령·망원경)하며 규칙이 깨지는지 본다
    [Test]
    public void Fuzz10()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        var log = new StringBuilder();
        var problems = new List<string>();
        var priorities = (TargetPriority[])Enum.GetValues(typeof(TargetPriority));
        for (int run = 0; run < 10; run++)
        {
            int seed = 7001 + run * 37;
            Difficulty difficulty = run % 3 == 2 ? Difficulty.Realistic : Difficulty.Easy;
            var rng = new System.Random(seed);
            BattleSim current = null;
            float nextAct = 0f, movingSince = -1f, lastElapsed = -1f;
            int casts = 0, moves = 0, arrivals = 0, rejected = 0, fires = 0;
            int lastEffects = 0, battleIdx = 0;
            string Where(BattleSim s) => $"[{run + 1}회 시드 {seed} {difficulty} 전투 {battleIdx} {s.Elapsed:0.0}s]";
            void Problem(BattleSim s, string text) { if (problems.Count < 60) problems.Add($"{Where(s)} {text}"); }

            void Chaos(BattleSim sim)
            {
                if (!ReferenceEquals(sim, current)) { current = sim; battleIdx++; lastEffects = 0; nextAct = 0f; movingSince = -1f; lastElapsed = -1f; }
                // 규칙 검사 (작업이 끝난 뒤라 읽어도 안전)
                if (sim.Elapsed < lastElapsed) Problem(sim, "시간이 거꾸로 감");
                lastElapsed = sim.Elapsed;
                if (sim.Kills > sim.TotalEnemies) Problem(sim, $"처치 {sim.Kills} > 전체 {sim.TotalEnemies}");
                if (sim.WizardPresent)
                {
                    Vector3 f = sim.WizardFeet;
                    float edge = Mathf.Max(Mathf.Abs(f.x), Mathf.Abs(f.z));
                    if (Mathf.Abs(f.y - BattleSim.WallHeight) > 0.01f || edge < sim.CastleHalf - 1.6f || edge > sim.CastleHalf + 0.01f || float.IsNaN(f.x))
                        Problem(sim, $"마법사가 성벽 위가 아님 ({f.x:0.0},{f.y:0.0},{f.z:0.0})");
                    if (sim.WizardMoving)
                    {
                        if (movingSince < 0f) { movingSince = sim.Elapsed; moves++; }
                        else if (sim.Elapsed - movingSince > 26f) { Problem(sim, $"마법사 이동이 {sim.Elapsed - movingSince:0}초째 안 끝남"); movingSince = sim.Elapsed; }
                    }
                    else if (movingSince >= 0f)
                    {
                        arrivals++;
                        movingSince = -1f;
                        if (sim.Effects.Count <= lastEffects && !sim.Ended) Problem(sim, "마법사 도착했는데 마법 효과가 안 생김");
                    }
                }
                lastEffects = sim.Effects.Count;
                if (sim.Effects.Count > 20) Problem(sim, $"마법 효과가 {sim.Effects.Count}개 쌓임");
                for (int side = 0; side < 4; side++)
                {
                    var st = sim.Sides[side];
                    if (st.WallHp < 0 || st.WallHp > st.WallMaxHp) Problem(sim, $"{side}면 성벽 체력 {st.WallHp}/{st.WallMaxHp}");
                }

                if (sim.Elapsed < nextAct) return;
                nextAct = sim.Elapsed + 1f;
                double roll = rng.NextDouble();
                if (roll < 0.35 && sim.SpellCount > 0)
                {
                    // 아무 면, 성벽 밖 0~170m(일부는 사거리 밖), 가끔 성 안
                    int side = rng.Next(4);
                    var n = WallGeometry.Outward(side); var r = WallGeometry.Right(side);
                    float depth = rng.NextDouble() < 0.1 ? -rng.Next(0, 50) : sim.CastleHalf + (float)rng.NextDouble() * 170f;
                    float lateral = ((float)rng.NextDouble() * 2f - 1f) * (sim.CastleHalf + 100f);
                    var p = new Vector3(n.x * depth + r.x * lateral, 0f, n.z * depth + r.z * lateral);
                    int effects0 = sim.Effects.Count;
                    bool wasMoving = sim.WizardMoving;
                    if (sim.CastSpell(rng.Next(sim.SpellCount), p)) { casts++; if (wasMoving) Problem(sim, "이동 중인데 마법이 받아들여짐"); }
                    else rejected++;
                }
                else if (roll < 0.5)
                {
                    for (int t = 0; t < sim.TrapCount; t++)
                        if (sim.GetTrap(t).State == BattleSim.TrapState.Armed && rng.NextDouble() < 0.3 && sim.DetonateTrap(t)) fires++;
                }
                else if (roll < 0.65 && sim.CannonCount > 0)
                {
                    int i = rng.Next(sim.CannonCount);
                    var cannon = sim.GetCannon(i);
                    if (rng.NextDouble() < 0.2) sim.ClearCannonAim(i);
                    else sim.SetCannonAim(i, cannon.Position + new Vector3((float)rng.NextDouble() * 200f - 100f, 0f, (float)rng.NextDouble() * 200f - 100f));
                }
                else if (roll < 0.8)
                {
                    TargetPriority pr = priorities[rng.Next(priorities.Length)];
                    int kind = rng.Next(4), a = rng.Next(4), b = rng.Next(4);
                    if (kind == 0) sim.IssueDirect(a, pr);
                    else if (kind == 1) sim.IssueGlobal(pr);
                    else if (kind == 2) sim.IssueShout(a, b, pr);
                    else sim.IssueMessenger(a, ((float)rng.NextDouble() * 2f - 1f) * sim.CastleHalf, b, pr);
                }
                else if (roll < 0.9)
                {
                    sim.LookThroughTelescope(new Vector3((float)rng.NextDouble() * 400f - 200f, 0f, (float)rng.NextDouble() * 400f - 200f));
                }
                else
                {
                    sim.SetCommander(rng.Next(4), ((float)rng.NextDouble() * 2f - 1f) * sim.CastleHalf);
                }
            }

            log.AppendLine($"== {run + 1}회 · 시드 {seed} · {difficulty} · 정면 먼저 + 함정 + 무작위 조작");
            try
            {
                RunPriced(lookup, difficulty, new[] { 5, 8, 12 }, log, false, 2, seed, Chaos);
            }
            catch (Exception e)
            {
                problems.Add($"[{run + 1}회 시드 {seed}] 예외: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                log.AppendLine($"  !! 예외 {e.GetType().Name}: {e.Message}");
            }
            log.AppendLine($"  조작: 마법 성공 {casts}·거절 {rejected}, 마법사 이동 {moves}·도착 {arrivals}, 함정 점화 {fires}");
        }

        log.AppendLine();
        log.AppendLine(problems.Count == 0 ? "문제 0건" : $"문제 {problems.Count}건:");
        foreach (string p in problems) log.AppendLine("  - " + p);
        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    // 정면 줄이기 + 나오는 시간 늘리기 (사용자 제안): 정책 2(정면 먼저 + 함정)로 세 가지 값 비교 — 값은 메모리에서만 바꾼다
    [Test]
    public void FrontSpread()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        var rulesDef = lookup.All<BattleRulesDef>().First();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var rate = typeof(BattleRulesDef).GetField("spawnPerSecond", flags);
        var easyBy = typeof(BattleRulesDef).GetField("easyEnemyMultiplierByBattle", flags);
        var realBy = typeof(BattleRulesDef).GetField("realisticEnemyMultiplierByBattle", flags);
        var log = new StringBuilder();
        log.AppendLine($"소스 {Environment.GetEnvironmentVariable("HLR_HEAD")} · 시드 1234 · 가격 A · 보조 · D-053·054 · 정책: 정면 먼저 + 함정");
        var variants = new[] { ("A 초당 25마리", 25f, 1f), ("B 초당 15마리", 15f, 1f), ("C 초당 15마리 + 쉬움4·현실3 적 −15%", 15f, 0.85f) };
        foreach (var (name, perSecond, cut) in variants)
        {
            rate.SetValue(rulesDef, perSecond);
            easyBy.SetValue(rulesDef, new[] { 0f, 0f, 0f, 0.4f * cut });
            realBy.SetValue(rulesDef, new[] { 0f, 0f, cut, 0f });
            foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Realistic })
            {
                log.AppendLine($"== {name} / {difficulty}");
                RunPriced(lookup, difficulty, new[] { 5, 8, 12 }, log, true, 2);
            }
        }

        rate.SetValue(rulesDef, 0f); easyBy.SetValue(rulesDef, new[] { 0f, 0f, 0f, 0.4f }); realBy.SetValue(rulesDef, new float[0]);
        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    static void RunPriced(DefinitionLookup lookup, Difficulty difficulty, int[] prices, StringBuilder log, bool assist, int policy = 0, int seed = 1234, Action<BattleSim> chaos = null)
    {
        const int SlotCap = 160, WallCap = 500, TotalCap = 2000;
        CampaignState c = CampaignFactory.Create(lookup, new NewGameOptions(difficulty), seed);
        CampaignStartDef start = lookup.All<CampaignStartDef>()[0];
        TrapDef bomb = lookup.Get<TrapDef>("trap_bomb");
        WallSide WallOf(SquadState s) => lookup.Get<PostDef>(s.PostId).Wall;
        for (int battle = 1; battle <= 4; battle++)
        {
            c.BattleNumber = battle;
            c.LastBattle = null;
            BattleSetup setup = BattleSetupFactory.Create(c, lookup, $"price_{difficulty}_{seed}_{battle}", out string error);
            if (setup == null) { log.AppendLine($"  전투{battle}: 준비 오류 {error}"); return; }
            c.ActiveBattleSetup = setup;
            int enemyTotal = 0;
            enemyTotal = BattleSim.ScaledGroupCounts(lookup.Get<WaveDef>(setup.WaveDefId).Groups.ToList(), setup.EnemyCountMultiplier).Sum();
            int gold0 = c.Gold, troops0 = c.Squads.Sum(s => s.Count);
            string walls0 = string.Join("/", c.Wall.Select(w => w.Hp));
            BattleResult r; float elapsed;
            using (var sim = new BattleSim(setup, lookup))
            {
                float nextAssist = 0f;
                int fired = 0;
                while (!sim.Ended && sim.Elapsed < 1500f)
                {
                    if (assist && sim.Elapsed >= nextAssist) { Assist(sim); nextAssist = sim.Elapsed + 5f; }
                    // 함정: 6마리 이상 몰리면 점화 (지도에서 누르는 것과 같은 함수)
                    for (int t = 0; t < sim.TrapCount; t++)
                        if (sim.GetTrap(t).State == BattleSim.TrapState.Armed && sim.EnemiesInTrapRange(t) >= 6 && sim.DetonateTrap(t)) fired++;
                    chaos?.Invoke(sim);
                    sim.BeginFrame(0.05f); if (sim.Ended) break; sim.Schedule(0.05f, sim.Elapsed); sim.Complete();
                }
                if (!sim.Ended) { log.AppendLine($"  전투{battle}: 시간초과(실패와 구분) 처치 {sim.Kills}/{sim.TotalEnemies}"); return; }
                r = sim.BuildResult(); elapsed = sim.Elapsed;
                log.Append($"  [정면 {sim.FrontShare:P0}, 우회 무리 {sim.AttackPlan.Count(g => g.Flank)}, 보고 {sim.FlankReports}, 함정 {sim.TrapCount}개 중 점화 {fired}·터짐 {r.ConsumedTrapIds.Count}]");
            }

            // 함정 소비·이동은 Codex 결과 반영기(C-007 v1.1, T-CX-035)가 반영한다 — 측정이 곧 연동 확인
            int trapsBefore = c.Traps.Count, consumed = r.ConsumedTrapIds.Count, moved = r.TrapMoves.Count;
            TerrainDef battleTerrain = lookup.Get<TerrainDef>(setup.TerrainId);
            int lost = r.SquadLosses.Sum(l => l.LostCount);
            int wiped = r.SquadLosses.Count(l => c.Squads.First(s => s.SquadId == l.SquadId).Count == l.LostCount);
            log.Append($"  전투{battle} 적{enemyTotal} [{r.Outcome}{(r.Outcome == BattleOutcome.Defeat ? " " + r.DefeatReason + " " + r.DefeatWallId : "")}] {elapsed:0}s 처치 {r.DefeatedEnemyCount} 보상 {r.EarnedGold} | 시작 골드 {gold0} 병력 {troops0} 성벽 {walls0} | 손실 {lost}명(분대 {r.SquadLosses.Count}, 전멸 {wiped})");
            if (r.Outcome == BattleOutcome.Defeat) { log.AppendLine(); return; }
            c.LastBattle = r; c.BattleNumber = r.BattleNumber + 1;
            if (!BattleResultApplier.TryApply(c, out string applyError, battleTerrain)) { log.AppendLine($" 반영 오류 {applyError}"); return; }
            // 연동 확인: 터진 함정은 사라지고, 같은 결과를 다시 반영해도 바뀌지 않는다 (재진입)
            if (c.Traps.Count != trapsBefore - consumed) { log.AppendLine($" 함정 반영 불일치 {trapsBefore}-{consumed}≠{c.Traps.Count}"); return; }
            int goldAfter = c.Gold, trapsAfter = c.Traps.Count;
            BattleResultApplier.TryApply(c, out _, battleTerrain);
            if (c.Gold != goldAfter || c.Traps.Count != trapsAfter) { log.AppendLine(" 재반영으로 값이 바뀜!"); return; }
            log.Append($" → 끝 골드 {c.Gold} 병력 {c.Squads.Sum(s => s.Count)} 성벽 {string.Join("/", c.Wall.Select(w => w.Hp))} 함정 반영(터짐 {consumed}·밀림 {moved}) 재반영 무변경");

            // 정면 먼저 정책: 북쪽 빈 분대 지점(D-054로 2곳 추가)에 궁수 새 분대를 만든다 (Codex 정비 '빈 지점 새 분대'와 같은 일)
            if (policy >= 1)
            {
                foreach (PostDef post in lookup.All<PostDef>().Where(p => p.Wall == WallSide.North && p.Kind == PostKind.Squad))
                    if (!c.Squads.Any(s => s.PostId == post.Id))
                        c.Squads.Add(new SquadState { SquadId = c.NewInstanceId("squad"), UnitDefId = "unit_archer", Count = 0, PostId = post.Id });
            }

            // 정비: 전체 수리(10HP=1골드, 벽별 올림) → 남은 돈 전부 궁수(벽 라운드로빈·정원·단계 가격)
            int repair = c.Wall.Sum(w => (int)Math.Ceiling((w.MaxHp - w.Hp) / 10.0));
            string repairText;
            if (repair <= c.Gold) { c.Gold -= repair; foreach (var w in c.Wall) w.Hp = w.MaxHp; repairText = $"수리 {repair}"; }
            else repairText = $"수리 거부(견적 {repair}>{c.Gold})";

            // 함정 상점 보충(정비마다 +3, 최대 12) → 정책 2면 살 수 있는 만큼 사서 북쪽 앞 칸에 표시해 묻는다
            var shop = c.TrapShop.First(s => s.TrapDefId == "trap_bomb");
            if (c.TrapShopRestockedForBattle != c.BattleNumber) { shop.Count = Math.Min(bomb.ShopStockMax, shop.Count + bomb.ShopStockPerPrep); c.TrapShopRestockedForBattle = c.BattleNumber; }
            int trapsBought = 0;
            if (policy == 2)
            {
                string terrainId = start.TerrainFor(c.BattleNumber);
                TerrainDef terrain = lookup.Get<TerrainDef>(terrainId);
                var cells = new List<(int col, int row)>();
                foreach (int row in new[] { 8, 10, 12, 9, 11 })
                    foreach (int col in new[] { 4, 8, 12, 16, 20, 6, 10, 14, 18 })
                        cells.Add((col, row));
                foreach (var (col, row) in cells)
                {
                    if (shop.Count <= 0 || c.Gold < bomb.Price) break;
                    string id = HumanLastRampart.Core.Traps.TrapGrid.CellId(0, col, row);
                    if (!HumanLastRampart.Core.Traps.TrapGrid.IsPlaceable(terrain, 0, col, row) || c.Traps.Any(t => t.TerrainId == terrainId && t.SlotId == id)) continue;
                    var p = HumanLastRampart.Core.Traps.TrapGrid.Center(terrain, 0, col, row);
                    c.Traps.Add(new TrapPlacement { TrapId = c.NewInstanceId("trap"), TrapDefId = "trap_bomb", TerrainId = terrainId, SlotId = id, ActualX = p.x, ActualZ = p.y, Marked = true, MarkX = p.x, MarkZ = p.y });
                    shop.Count--; c.Gold -= bomb.Price; trapsBought++;
                }
            }

            int hired = 0, spent = 0, capRefusals = 0;
            while (true)
            {
                int total = c.Squads.Sum(s => s.Count);
                int price = ArcherPrice(prices, total);
                if (c.Gold < price) break;
                if (total >= TotalCap) { capRefusals++; break; }
                var candidatesWalls = Enum.GetValues(typeof(WallSide)).Cast<WallSide>()
                    .Select(side => (side, count: c.Squads.Where(s => s.PostId != null && WallOf(s) == side).Sum(s => s.Count)))
                    .Where(x => x.count < WallCap)
                    .OrderBy(x => policy >= 1 && x.side == WallSide.North ? -1 : x.count).ToList();   // 정책 1·2: 정면(북) 먼저
                SquadState target = null;
                foreach (var (side, _) in candidatesWalls)
                {
                    target = c.Squads.Where(s => s.UnitDefId == "unit_archer" && s.PostId != null && WallOf(s) == side && s.Count < SlotCap).OrderBy(s => s.Count).FirstOrDefault();
                    if (target != null) break;
                }
                if (target == null) { capRefusals++; break; }
                target.Count++; c.Gold -= price; spent += price; hired++;
            }
            c.Squads.RemoveAll(s => s.Count == 0);   // 새로 만든 빈 분대 중 못 채운 것은 없앤다 (0명 분대 금지)
            int freeSlots = c.Squads.Where(s => s.UnitDefId == "unit_archer").Sum(s => SlotCap - s.Count);
            log.AppendLine($" | 정비 {repairText}, 함정 {trapsBought}개(상점 남음 {shop.Count}, 묻힌 함정 {c.Traps.Count}), 궁수 {hired}명 {spent}골드{(capRefusals > 0 ? " (정원으로 멈춤)" : "")} → 골드 {c.Gold} 병력 {c.Squads.Sum(s => s.Count)} 남은 궁수 자리 {freeSlots}");
        }
        log.AppendLine("  ★ 4번까지 모두 승리");
    }
}