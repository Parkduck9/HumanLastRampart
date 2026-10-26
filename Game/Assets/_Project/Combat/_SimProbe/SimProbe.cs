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
public sealed partial class SimProbe
{
    static readonly System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    // 시험 상태 누수 방지: 여러 시험이 리플렉션으로 정의(BattleRulesDef·EnemyDef·가격 등) 값을 메모리에서 바꾸고
    // 원래대로 되돌리지 않아, 전체 시험을 돌리면 뒤 시험(UpgradeCompare) 숫자가 달라졌다. 시험마다 전체 정의 값을 저장해 두고 끝나면 복원한다.
    readonly List<(ScriptableObject asset, string json)> definitionSnapshot = new List<(ScriptableObject, string)>();

    [SetUp]
    public void SnapshotDefinitions()
    {
        definitionSnapshot.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) definitionSnapshot.Add((asset, EditorJsonUtility.ToJson(asset)));
        }
    }

    [TearDown]
    public void RestoreDefinitions()
    {
        foreach (var (asset, json) in definitionSnapshot)
        {
            if (asset != null && EditorJsonUtility.ToJson(asset) != json) EditorJsonUtility.FromJsonOverwrite(json, asset);
        }

        definitionSnapshot.Clear();
    }

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

    // D-073 뒤 난이도 다시 맞추기: 적 수 배율 후보마다 쉬움·현실 4전투 한 바퀴 (목표: 쉬움 1~4 승리, 현실 1~3 승리·4 패배)
    [Test]
    public void MultiplierSweep()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        var rules = lookup.All<BattleRulesDef>()[0];
        var easyField = typeof(BattleRulesDef).GetField("easyEnemyMultiplier", F);
        var easyByField = typeof(BattleRulesDef).GetField("easyEnemyMultiplierByBattle", F);
        var realByField = typeof(BattleRulesDef).GetField("realisticEnemyMultiplierByBattle", F);
        var perKillField = typeof(BattleRulesDef).GetField("corpseHeightPerKill", F);
        var atkField = typeof(EnemyDef).GetField("onWallAttack", F);
        var enemies = lookup.All<EnemyDef>();
        object easy0 = easyField.GetValue(rules), easyBy0 = easyByField.GetValue(rules), realBy0 = realByField.GetValue(rules), perKill0 = perKillField.GetValue(rules);
        int[] atk0 = enemies.Select(e => (int)atkField.GetValue(e)).ToArray();
        var log = new StringBuilder();
        const string economy = "4차 10HP=1골드 수리+남은 골드 전부 궁수";
        try
        {
            // CampaignBalance와 같은 조건(더미 0.1m·성벽 위 공격 5배)
            perKillField.SetValue(rules, 0.1f);
            for (int i = 0; i < enemies.Count; i++) atkField.SetValue(enemies[i], atk0[i] * 5);
            foreach (float easy in new[] { 0.75f, 0.6f, 0.5f })
            {
                easyField.SetValue(rules, easy);
                easyByField.SetValue(rules, new[] { 0f, 0f, 0f, easy * 0.8f });
                log.AppendLine($"== 쉬움 배율 {easy} (4번 {easy * 0.8f})");
                RunCampaign(lookup, Difficulty.Easy, economy, log);
            }

            foreach (float real in new[] { 1.5f, 1.2f, 1.0f })
            {
                realByField.SetValue(rules, new[] { real, real, real, real });
                log.AppendLine($"== 현실 배율 {real}");
                RunCampaign(lookup, Difficulty.Realistic, economy, log);
            }
        }
        finally
        {
            easyField.SetValue(rules, easy0);
            easyByField.SetValue(rules, easyBy0);
            realByField.SetValue(rules, realBy0);
            perKillField.SetValue(rules, perKill0);
            for (int i = 0; i < enemies.Count; i++) atkField.SetValue(enemies[i], atk0[i]);
        }

        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    static float ReinforceShare;   // 0 = 옮기지 않음
    static bool MeasureFrames;     // 전투마다 계산 시간 기록

    // 성능 (밤사이 계획 6): 병력 이동·각도 규칙 뒤 전투 계산 한 프레임 시간. 각도 규칙 켬/끔 비교, 정면으로 1/3 옮기는 플레이
    [Test]
    public void FrameCost()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        var rules = lookup.All<BattleRulesDef>()[0];
        var perKillField = typeof(BattleRulesDef).GetField("corpseHeightPerKill", F);
        var atkField = typeof(EnemyDef).GetField("onWallAttack", F);
        var enemies = lookup.All<EnemyDef>();
        object perKill0 = perKillField.GetValue(rules);
        int[] atk0 = enemies.Select(e => (int)atkField.GetValue(e)).ToArray();
        bool arc0 = HumanLastRampart.Combat.Battle.BattleSim.FrontArcOnly;
        var log = new StringBuilder();
        try
        {
            perKillField.SetValue(rules, 0.1f);
            for (int i = 0; i < enemies.Count; i++) atkField.SetValue(enemies[i], atk0[i] * 5);
            MeasureFrames = true;
            ReinforceShare = 0.34f;
            foreach (bool arc in new[] { true, false })
            {
                HumanLastRampart.Combat.Battle.BattleSim.FrontArcOnly = arc;
                log.AppendLine($"== 현실 · 앞만 쏘기 {(arc ? "켬" : "끔")} · 정면으로 1/3");
                RunCampaign(lookup, Difficulty.Realistic, "4차 10HP=1골드 수리+남은 골드 전부 궁수", log);
            }
        }
        finally
        {
            MeasureFrames = false;
            ReinforceShare = 0f;
            HumanLastRampart.Combat.Battle.BattleSim.FrontArcOnly = arc0;
            perKillField.SetValue(rules, perKill0);
            for (int i = 0; i < enemies.Count; i++) atkField.SetValue(enemies[i], atk0[i]);
        }

        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    static void Reinforce(HumanLastRampart.Combat.Battle.BattleSim sim, float share, StringBuilder log)
    {
        int moved = 0;
        for (int side = 1; side < 4; side++)
        {
            var groups = new List<int>();
            for (int g = 0; g < sim.GroupCount; g++)
            {
                var info = sim.GetGroup(g);
                if (info.Movable && info.Side == side && info.Alive > 0) groups.Add(g);
            }

            int take = Mathf.FloorToInt(groups.Count * share);
            for (int k = 0; k < take; k++)
                if (sim.MoveGroup(groups[k], HumanLastRampart.Combat.Battle.BattleSim.FrontSide, true)) moved += sim.GetGroup(groups[k]).Alive;
        }

        log.Append($"    (준비: 정면으로 {moved}명 옮김) ");
    }

    // 사용자 결정(2026-10-16): 난이도 기준 = 준비 단계에 옆·뒤 묶음 1/3을 정면으로 옮기는 플레이.
    // 목표 쉬움 1~4 승리, 현실 1~3 승리·4 패배 → 현실 4번 배율만 올려 본다
    [Test]
    public void Realistic4Sweep()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        var rules = lookup.All<BattleRulesDef>()[0];
        var realByField = typeof(BattleRulesDef).GetField("realisticEnemyMultiplierByBattle", F);
        var perKillField = typeof(BattleRulesDef).GetField("corpseHeightPerKill", F);
        var atkField = typeof(EnemyDef).GetField("onWallAttack", F);
        var enemies = lookup.All<EnemyDef>();
        object realBy0 = realByField.GetValue(rules), perKill0 = perKillField.GetValue(rules);
        int[] atk0 = enemies.Select(e => (int)atkField.GetValue(e)).ToArray();
        var log = new StringBuilder();
        const string economy = "4차 10HP=1골드 수리+남은 골드 전부 궁수";
        try
        {
            perKillField.SetValue(rules, 0.1f);
            for (int i = 0; i < enemies.Count; i++) atkField.SetValue(enemies[i], atk0[i] * 5);
            ReinforceShare = 0.34f;
            foreach (float last in new[] { 2.0f, 2.5f, 3.0f, 4.0f })
            {
                realByField.SetValue(rules, new[] { 1.5f, 1.5f, 1.5f, last });
                log.AppendLine($"== 현실 4번 배율 {last} (1~3번 1.5) · 정면으로 1/3");
                RunCampaign(lookup, Difficulty.Realistic, economy, log);
            }
        }
        finally
        {
            ReinforceShare = 0f;
            realByField.SetValue(rules, realBy0);
            perKillField.SetValue(rules, perKill0);
            for (int i = 0; i < enemies.Count; i++) atkField.SetValue(enemies[i], atk0[i]);
        }

        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    // 사용자 결정(2026-10-16): 현실 4번은 적 수 대신 오우거 비중으로. 전투 4 오우거 그룹(북·동·서)마다 마릿수를 바꿔
    // 쉬움 1~4 승리, 현실 1~3 승리·4 패배가 되는 값을 찾는다 (정면 1/3 플레이)
    [Test]
    public void OgreSweep()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        var rules = lookup.All<BattleRulesDef>()[0];
        var perKillField = typeof(BattleRulesDef).GetField("corpseHeightPerKill", F);
        var atkField = typeof(EnemyDef).GetField("onWallAttack", F);
        var enemies = lookup.All<EnemyDef>();
        WaveDef wave4 = lookup.All<WaveDef>().First(w => w.Id == "wave_4");
        var ogres = wave4.Groups.Where(g => g.EnemyDefId == "enemy_ogre").ToList();
        int[] ogre0 = ogres.Select(g => g.Count).ToArray();
        object perKill0 = perKillField.GetValue(rules);
        int[] atk0 = enemies.Select(e => (int)atkField.GetValue(e)).ToArray();
        var log = new StringBuilder();
        const string economy = "4차 10HP=1골드 수리+남은 골드 전부 궁수";
        try
        {
            perKillField.SetValue(rules, 0.1f);
            for (int i = 0; i < enemies.Count; i++) atkField.SetValue(enemies[i], atk0[i] * 5);
            ReinforceShare = 0.34f;
            foreach (int perGroup in new[] { 200 })   // 확정값 확인 (20·50·100은 둘 다 승리, 200에서 현실 4 패배 — 2026-10-16)
            {
                foreach (var g in ogres) g.Count = perGroup;
                foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Realistic })
                {
                    log.AppendLine($"== 전투4 오우거 그룹당 {perGroup}마리(×{ogres.Count}) · {difficulty} · 정면 1/3");
                    RunCampaign(lookup, difficulty, economy, log);
                }
            }
        }
        finally
        {
            for (int i = 0; i < ogres.Count; i++) ogres[i].Count = ogre0[i];
            ReinforceShare = 0f;
            perKillField.SetValue(rules, perKill0);
            for (int i = 0; i < enemies.Count; i++) atkField.SetValue(enemies[i], atk0[i]);
        }

        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    // D-073 각도 규칙 + 플레이어가 병력을 정면으로 옮길 때 (옮기는 비율 × 배율)
    [Test]
    public void ReinforceSweep()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        var rules = lookup.All<BattleRulesDef>()[0];
        var perKillField = typeof(BattleRulesDef).GetField("corpseHeightPerKill", F);
        var atkField = typeof(EnemyDef).GetField("onWallAttack", F);
        var enemies = lookup.All<EnemyDef>();
        object perKill0 = perKillField.GetValue(rules);
        int[] atk0 = enemies.Select(e => (int)atkField.GetValue(e)).ToArray();
        var log = new StringBuilder();
        const string economy = "4차 10HP=1골드 수리+남은 골드 전부 궁수";
        try
        {
            perKillField.SetValue(rules, 0.1f);
            for (int i = 0; i < enemies.Count; i++) atkField.SetValue(enemies[i], atk0[i] * 5);
            foreach (float share in new[] { 0.34f, 0.5f, 0.67f })
            {
                ReinforceShare = share;
                foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Realistic })
                {
                    log.AppendLine($"== 옆·뒤 묶음 {share:P0}를 정면으로 · {difficulty}");
                    RunCampaign(lookup, difficulty, economy, log);
                }
            }
        }
        finally
        {
            ReinforceShare = 0f;
            perKillField.SetValue(rules, perKill0);
            for (int i = 0; i < enemies.Count; i++) atkField.SetValue(enemies[i], atk0[i]);
        }

        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    // 원인 가르기: 들판(160/320m) × 자기 성벽 앞만 쏘기(켬/끔), 지금 배율 그대로 쉬움·현실 한 바퀴
    [Test]
    public void CauseSweep()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        var rules = lookup.All<BattleRulesDef>()[0];
        var perKillField = typeof(BattleRulesDef).GetField("corpseHeightPerKill", F);
        var atkField = typeof(EnemyDef).GetField("onWallAttack", F);
        var fieldField = typeof(TerrainDef).GetField("fieldSize", F);
        var enemies = lookup.All<EnemyDef>();
        var terrains = lookup.All<TerrainDef>();
        object perKill0 = perKillField.GetValue(rules);
        int[] atk0 = enemies.Select(e => (int)atkField.GetValue(e)).ToArray();
        float[] field0 = terrains.Select(t => (float)fieldField.GetValue(t)).ToArray();
        float funnel0 = HumanLastRampart.Combat.Battle.BattleSim.FunnelFieldHalfWidth;
        bool arc0 = HumanLastRampart.Combat.Battle.BattleSim.FrontArcOnly;
        var log = new StringBuilder();
        const string economy = "4차 10HP=1골드 수리+남은 골드 전부 궁수";
        try
        {
            perKillField.SetValue(rules, 0.1f);
            for (int i = 0; i < enemies.Count; i++) atkField.SetValue(enemies[i], atk0[i] * 5);
            foreach (float field in new[] { 160f, 320f })
            {
                foreach (TerrainDef t in terrains) fieldField.SetValue(t, field);
                HumanLastRampart.Combat.Battle.BattleSim.FunnelFieldHalfWidth = field * 75f / 160f;
                foreach (bool arc in new[] { true, false })
                {
                    HumanLastRampart.Combat.Battle.BattleSim.FrontArcOnly = arc;
                    foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Realistic })
                    {
                        log.AppendLine($"== 들판 {field}m · 앞만 쏘기 {(arc ? "켬" : "끔")} · {difficulty}");
                        RunCampaign(lookup, difficulty, economy, log);
                    }
                }
            }
        }
        finally
        {
            perKillField.SetValue(rules, perKill0);
            for (int i = 0; i < enemies.Count; i++) atkField.SetValue(enemies[i], atk0[i]);
            for (int i = 0; i < terrains.Count; i++) fieldField.SetValue(terrains[i], field0[i]);
            HumanLastRampart.Combat.Battle.BattleSim.FunnelFieldHalfWidth = funnel0;
            HumanLastRampart.Combat.Battle.BattleSim.FrontArcOnly = arc0;
        }

        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    // 성벽 한 면이 실제로 받는 병사 수 (BattleSim.Slots: 보통 자리와 최소 간격 자리 중 큰 쪽). 대포 사격구 때문에 대포 분대를
    // 그대로 두고 나머지 분대·용병을 1명씩으로 줄인 임시 전투를 만들어 자리표 크기를 읽는다 (규칙은 건드리지 않고 반사로 읽기만)
    static int[] WallCapacities(BattleSetup setup, DefinitionLookup lookup)
    {
        var saved = new List<(SquadState squad, int count)>();
        foreach (SquadState s in setup.Squads.Concat(setup.Mercenaries))
        {
            saved.Add((s, s.Count));
            if (s.Count > 0 && !IsCannonSquad(s, lookup)) s.Count = 1;
        }

        try
        {
            using (var sim = new BattleSim(setup, lookup))
            {
                var wallSlots = (Array)typeof(BattleSim).GetField("wallSlots", F).GetValue(sim);
                var denseSlots = (Array)typeof(BattleSim).GetField("denseSlots", F).GetValue(sim);
                var caps = new int[4];
                for (int side = 0; side < 4; side++)
                    caps[side] = Math.Max(((System.Collections.ICollection)wallSlots.GetValue(side)).Count, ((System.Collections.ICollection)denseSlots.GetValue(side)).Count);
                return caps;
            }
        }
        finally
        {
            foreach (var (squad, count) in saved) squad.Count = count;
        }
    }

    static bool IsCannonSquad(SquadState s, DefinitionLookup lookup)
    {
        UnitDef unit = lookup.Get<UnitDef>(s.UnitDefId);
        WeaponDef weapon = unit != null ? lookup.Get<WeaponDef>(unit.DefaultWeaponId) : null;
        return weapon != null && weapon.AreaRadius > 0f;
    }

    // 전투 준비를 만들되, 정비 정책이 성벽 실제 정원을 넘겨 모집했으면 가장 큰 분대부터 줄여 정원에 맞춘다 (진행 데이터의 분대를 줄이므로
    // 결과 반영기와도 맞는다). 용병은 줄이지 않는다. 정원 규칙 자체는 바꾸지 않는다 — 시험 설정만 규칙에 맞춘다
    static BattleSetup CreateFitted(CampaignState c, DefinitionLookup lookup, string runId, out string error, StringBuilder log)
    {
        BattleSetup setup = BattleSetupFactory.Create(c, lookup, runId, out error);
        if (setup == null) return null;
        int[] caps = WallCapacities(setup, lookup);
        bool trimmed = false;
        for (int side = 0; side < 4; side++)
        {
            Func<SquadState, bool> onWall = s => s.Count > 0 && s.PostId != null && (int)lookup.Get<PostDef>(s.PostId).Wall == side && !IsCannonSquad(s, lookup);
            int total = c.Squads.Where(onWall).Sum(s => s.Count) + setup.Mercenaries.Where(onWall).Sum(s => s.Count);
            int excess = total - caps[side];
            if (excess <= 0) continue;
            int cut = 0;
            while (excess > 0)
            {
                SquadState biggest = c.Squads.Where(s => onWall(s) && s.Count > 1).OrderByDescending(s => s.Count).FirstOrDefault();
                if (biggest == null) break;
                int take = Math.Min(excess, Math.Max(1, biggest.Count / 20));
                biggest.Count -= take;
                excess -= take;
                cut += take;
            }

            log.Append($"(정원 보정 {side}면 -{cut}명, 정원 {caps[side]}) ");
            trimmed = true;
        }

        return trimmed ? BattleSetupFactory.Create(c, lookup, runId, out error) : setup;
    }

    static void RunCampaign(DefinitionLookup lookup, Difficulty difficulty, string economy, StringBuilder log)
    {
        CampaignState c = CampaignFactory.Create(lookup, new NewGameOptions(difficulty), 1234);
        for (int battle = 1; battle <= 4; battle++)
        {
            c.BattleNumber = battle;
            c.LastBattle = null;
            BattleSetup setup = CreateFitted(c, lookup, $"camp_{difficulty}_{economy.Substring(0, 2)}_{battle}", out string error, log);
            if (setup == null) { log.AppendLine($"  전투{battle}: 준비 오류 {error}"); return; }
            c.ActiveBattleSetup = setup;
            string wallsBefore = string.Join("/", c.Wall.Select(w => w.Hp));
            int soldiersBefore = c.Squads.Sum(s => s.Count);
            BattleResult r;
            float elapsed;
            using (var sim = new BattleSim(setup, lookup))
            {
                // 전투 준비(D-071·072) 플레이어 흉내: 옆·뒤 성벽 묶음 중 이 비율을 정면(북)으로 옮긴다
                if (ReinforceShare > 0f) Reinforce(sim, ReinforceShare, log);
                var frameWatch = new System.Diagnostics.Stopwatch();   // 성능 측정: 전투 계산 한 프레임(0.05초) 걸린 시간
                double frameTotal = 0, frameMax = 0;
                int frames = 0;
                while (!sim.Ended && sim.Elapsed < 1500f)
                {
                    frameWatch.Restart();
                    sim.BeginFrame(0.05f);
                    if (sim.Ended) break;
                    sim.Schedule(0.05f, sim.Elapsed);
                    sim.Complete();
                    double ms = frameWatch.Elapsed.TotalMilliseconds;
                    frameTotal += ms;
                    frameMax = Math.Max(frameMax, ms);
                    frames++;
                }

                if (MeasureFrames) log.Append($"    (계산 평균 {frameTotal / Math.Max(1, frames):0.00}ms·최대 {frameMax:0.0}ms, 병사 {sim.SoldierCount}) ");
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
                        if (sim.EffectsStarted <= lastEffects && !sim.Ended) Problem(sim, "마법사 도착했는데 마법 효과가 안 생김");   // 시작 수로 (활성 수는 같은 순간 끝난 효과 때문에 그대로일 수 있음)
                    }
                }
                lastEffects = sim.EffectsStarted;
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

    // K-058 (D-079 U10): 강화 없이 현실 4번은 지고, 강화를 잘 쓰면 이기는지. 두 플레이 모두 D-075 기준(준비 때 옆·뒤 1/3을 정면으로)
    // + 보조(대포·마법·망원경) + 정면 먼저 모집. 강화 사용 = 정비 때 아래 SpendOnUpgrades 정책을 먼저 쓰고 남은 돈으로 궁수
    [Test]
    public void UpgradeCompare()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        var log = new StringBuilder();
        log.AppendLine($"소스 {Environment.GetEnvironmentVariable("HLR_HEAD")} · dt 0.05s · 시드 1234 · 가격 A · 정면 1/3 옮기기 · 보조 · 정면 먼저 모집");
        foreach (Difficulty difficulty in new[] { Difficulty.Realistic, Difficulty.Easy })
            foreach (bool upgrades in new[] { false, true })
            {
                log.AppendLine($"== {difficulty} · {(upgrades ? "강화 사용" : "강화 없이")}");
                RunPriced(lookup, difficulty, new[] { 5, 8, 12 }, log, true, 1, 1234, null, true, upgrades);
            }
        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    // 빛을 잇는 자들 1회차 (W11): 요새 1(밤 2) + 요새 2(밤 3)를 실제 흐름(GameFlow·FlowRouter)과 같은 순서로 끝까지.
    // 난이도 × 등불 밝기 × 정비 정책(강화 없이 / 강화 먼저 / 정원 60%까지 모집 먼저 뒤 강화) × 3판(같은 시드, 전투 실행 ID 끝 번호만 다름).
    // 규칙은 BeaconRules·BattleResultApplier(진 밤 반영 포함)·BattleSetupFactory·PreparationTransactions를 그대로 쓰고, 정비 정책만 시험용이다
    [Test, Timeout(3600000)]
    public void BeaconRun()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        Assert.Greater(catalogs.Count, 0, "정의 묶음 에셋을 못 찾음");
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        const int Reps = 3;
        var body = new StringBuilder();
        var rows = new List<string>();
        var configs = new List<(Difficulty difficulty, LampBrightness lamp, int policy, bool rush)>();
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Realistic })
            foreach (LampBrightness lamp in new[] { LampBrightness.Normal, LampBrightness.High })
                for (int policy = 0; policy < 3; policy++)
                    configs.Add((difficulty, lamp, policy, false));
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Realistic })   // 덤: 골드로 점화선을 바로 채우는 극단 플레이
            configs.Add((difficulty, LampBrightness.Normal, 0, true));

        string[] policyNames = { "강화 없이", "강화 먼저", "정원60% 모집 뒤 강화" };
        foreach (var (difficulty, lamp, policy, rush) in configs)
        {
            string title = $"{(rush ? "[골드 점화 서두름] " : "")}{difficulty} · 등불 {lamp} · {policyNames[policy]}";
            body.AppendLine($"== {title}");
            int complete = 0, winsTotal = 0;
            var fails = new List<string>();
            for (int rep = 1; rep <= Reps; rep++)
            {
                body.AppendLine($"-- {rep}회째");
                (bool done, int wins, string failedAt) = RunBeacon(lookup, difficulty, lamp, policy, rep, body, rush);
                if (done) complete++;
                winsTotal += wins;
                fails.Add(done ? "완료" : failedAt);
            }

            rows.Add($"| {title} | {complete}/{Reps} | {winsTotal / (float)Reps:0.0} | {string.Join(" / ", fails)} |");
        }

        var log = new StringBuilder();
        log.AppendLine($"소스 {Environment.GetEnvironmentVariable("HLR_HEAD")} · dt 0.05s · 진행 시드 판마다 1234+977k · 모집 순서 판마다 다름(정면 먼저/고르게/정면 마지막) · 가격 A(정비 거래 규칙 그대로) · 정면 1/3 옮기기 · 보조 · 빛줄기 가장 위험한 벽으로 돌리기");
        log.AppendLine("정비: 낮 시작 지원금 → 성벽 낮은 순 수리 → 등대 수리(점화선까지, 예정 마지막 밤부터만. 서두름은 처음부터) → [정원 60% 모집] → [강화] → 남은 골드 정원(전체 2000·벽 500·지점 160)까지 모집(궁수·기존 총수 분대, 정면 먼저). 진 밤은 결과(병력·성벽 피해)를 반영하고 복구 절반·지원금.");
        log.AppendLine();
        log.AppendLine("## 요약 (한 줄 = 설정 하나, 3판)");
        log.AppendLine("| 설정 | 완주 | 평균 승리 밤 | 3판 결과(실패한 밤) |");
        log.AppendLine("|---|---|---|---|");
        foreach (string row in rows) log.AppendLine(row);
        log.AppendLine();
        log.AppendLine("## 상세");
        log.Append(body);
        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe.txt", log.ToString(), new UTF8Encoding(false));
    }

    // 정비 거래(PreparationTransactions)로 실제 정원까지 모집한다: 전체 limitTotal·성벽 500·지점 160. 기존 분대는 같은 병종으로, 빈 지점은 궁수로.
    // 정면(북) 지점부터, 그다음 사람 적은 성벽 순으로 25명씩 돌려 채운다. 모집한 수를 돌려준다
    static int HireToCaps(CampaignState c, DefinitionLookup lookup, int limitTotal, int order = 0)
    {
        var tx = new PreparationTransactions(c, lookup);
        var rules = tx.Rules;
        List<PostDef> posts = lookup.All<PostDef>().Where(p => p.Kind == PostKind.Squad).ToList();
        int hired = 0;
        while (true)
        {
            bool progress = false;
            foreach (PostDef post in posts.OrderBy(p => order == 1 ? tx.WallCount(p.WallId) : order == 2 ? (p.Wall == WallSide.North ? int.MaxValue : tx.WallCount(p.WallId)) : (p.Wall == WallSide.North ? -1 : tx.WallCount(p.WallId))))
            {
                int total = tx.TotalCount();
                int limit = Math.Min(rules.GlobalCapacity, limitTotal) - total;
                if (limit <= 0) return hired;
                SquadState existing = c.Squads.Find(s => s.PostId == post.Id);
                string unitId = existing?.UnitDefId ?? "unit_archer";
                int room = Math.Min(rules.SquadPostCapacity - (existing?.Count ?? 0), rules.WallCapacity - tx.WallCount(post.WallId));
                int n = Math.Min(Math.Min(room, limit), 25);
                if (n <= 0) continue;
                UnitKind kind = lookup.Get<UnitDef>(unitId).Kind;
                while (n > 0 && !(HumanLastRampart.Core.Economy.PreparationEconomyRules.TryQuoteRecruit(rules, kind, total, n, out int cost, out _) && cost <= c.Gold)) n--;
                if (n <= 0) continue;
                if (tx.TryRecruit(post.Id, unitId, n, out var quote, out _, out _) && tx.TryCommit(quote, out _, out _)) { hired += n; progress = true; }
            }

            if (!progress) return hired;
        }
    }

    // 병력이 더 들어갈 자리가 없는가 (전체·성벽·지점 정원). 정비 첫머리마다 새로 계산한다
    static bool TroopsFull(CampaignState c, DefinitionLookup lookup)
    {
        var tx = new PreparationTransactions(c, lookup);
        if (tx.TotalCount() >= tx.Rules.GlobalCapacity) return true;
        foreach (PostDef post in lookup.All<PostDef>().Where(p => p.Kind == PostKind.Squad))
        {
            int have = c.Squads.Where(s => s.PostId == post.Id).Sum(s => s.Count);
            if (have < tx.Rules.SquadPostCapacity && tx.WallCount(post.WallId) < tx.Rules.WallCapacity) return false;
        }

        return true;
    }

    // 전투 중 빛줄기를 가장 위험한 벽(붙은 적·오르는 적·성벽 위 적 가중)으로 돌린다. 지금 방향보다 1.3배 넘게 위험할 때만 (플레이어 흉내)
    static void SteerBeam(BattleSim sim)
    {
        float Threat(int side)
        {
            var s = sim.Sides[side];
            return s.AtWall + s.Climbing * 1.5f + s.OnWall * 3f + s.InField * 0.1f;
        }

        int best = sim.BeamSide;
        for (int side = 0; side < 4; side++) if (Threat(side) > Threat(best)) best = side;
        if (best != sim.BeamSide && Threat(best) > Threat(sim.BeamSide) * 1.3f + 1f) sim.RotateBeam(best);
    }

    // 한 판 (1회차). policy 0 = 강화 없이, 1 = 강화 먼저, 2 = 정원 60%까지 모집한 뒤 강화
    static (bool complete, int wins, string failedAt) RunBeacon(DefinitionLookup lookup, Difficulty difficulty, LampBrightness lamp, int policy, int rep, StringBuilder log, bool rush = false)
    {
        int hireOrder = (rep - 1) % 3;   // 0 정면(북) 먼저, 1 사람 적은 성벽부터 고르게, 2 정면 마지막
        CampaignState c = CampaignFactory.Create(lookup, new NewGameOptions(difficulty), 1234 + (rep - 1) * 977);   // 판마다 다른 진행 시드 (지형·예보)
        Assert.IsNotNull(c, $"새 게임 진행 데이터를 못 만듦 (시작 설정 {lookup.All<CampaignStartDef>().Count}개)");
        bool firstNight = true, ended = false, complete = false;
        int won = 0, lostCount = 0, battles = 0;
        string failedAt = "-";
        c.PlannedLamp = lamp;
        int globalCap = lookup.All<PreparationEconomyDef>()[0].GlobalCapacity;
        BattleRulesDef rules = lookup.All<BattleRulesDef>()[0];

        while (!ended && !complete && battles < 12)
        {
            battles++;
            // ── 낮 (첫 밤은 이야기 뒤 바로 전투 — FlowRouter Story→PrepareBattle)
            string prep = "";
            if (!firstNight)
            {
                // 성벽: 낮은 쪽부터 수리 (정비 거래 그대로, 모자라면 살 수 있는 만큼)
                var tx = new PreparationTransactions(c, lookup);
                int wallGold = 0;
                foreach (WallSegmentState w in c.Wall.OrderBy(w => w.Hp / (double)w.MaxHp).ToList())
                {
                    int missing = w.MaxHp - w.Hp;
                    if (missing <= 0) continue;
                    int ask = Math.Min(missing, c.Gold * tx.Rules.RepairHpPerGold);
                    if (ask <= 0) continue;
                    int before = c.Gold;
                    if (tx.TryRepair(w.SegmentId, ask, false, out var quote, out _, out _) && tx.TryCommit(quote, out _, out _)) wallGold += before - c.Gold;
                }

                // 등대 수리: 기본은 예정된 마지막 밤부터만 부족분을 채운다 (rush는 처음부터 — 골드로 밤 건너뛰기 확인용)
                int beaconBuys = 0;
                if (rush || c.NightNumber >= BeaconRules.NightsIn(c.FortressNumber))
                    while (!BeaconRules.RepairReady(c) && BeaconRules.TryBuyRepair(c, out _, rules.BeaconRepairsPerDay)) beaconBuys++;
                prep = $"수리 {wallGold}골드·등대수리 {beaconBuys}회";

                int hired = 0;
                if (policy == 2) hired += HireToCaps(c, lookup, globalCap * 6 / 10, hireOrder);
                if (policy >= 1) prep += SpendOnUpgrades(c, lookup, TroopsFull(c, lookup));   // 정비마다 새로 계산 (어제 값을 끌고 가지 않는다)
                hired += HireToCaps(c, lookup, globalCap, hireOrder);
                prep += $" 모집 +{hired}";
                // 남은 골드는 성벽 보강 (W11): 보강 횟수가 적은 성벽부터, 같으면 정면(북) 먼저
                int reinforced = 0;
                while (true)
                {
                    WallSegmentState target = c.Wall.Where(w => w.Reinforced < rules.WallReinforceMaxPerWall).OrderBy(w => w.Reinforced).ThenBy(w => w.SegmentId == WallIds.North ? 0 : 1).FirstOrDefault();
                    if (target == null || !BeaconRules.TryReinforceWall(c, target.SegmentId, rules, out _)) break;
                    reinforced++;
                }

                if (reinforced > 0) prep += $" 성벽보강 {reinforced}회";
            }

            // ── 밤 (PrepareBattle: 급료 → 준비 정보)
            c.LastBattle = null;
            c.PlannedLamp = lamp;
            foreach (string note in HumanLastRampart.Core.Upgrades.UpgradeRules.PayWages(c, lookup)) log.Append($" ({note})");
            BattleSetup setup = CreateFitted(c, lookup, $"beacon_{difficulty}_{lamp}_{policy}_{rush}_{battles}_r{rep}", out string error, log);
            if (setup == null) { log.AppendLine($"  밤 준비 오류: {error}"); return (false, won, "준비 오류"); }
            c.ActiveBattleSetup = setup;
            string label = BeaconRules.NightLabel(c.FortressNumber, c.NightNumber);
            int enemyTotal = BattleSim.ScaledGroupCounts(lookup.Get<WaveDef>(setup.WaveDefId).Groups.ToList(), setup.EnemyCountMultiplier * (setup.LampCountMultiplier > 0f ? setup.LampCountMultiplier : BattleSim.LampCountMul(setup.LampBrightness))).Sum();
            int gold0 = c.Gold, troops0 = c.Squads.Sum(s => s.Count), garrison0 = BeaconRules.GarrisonSoldiers(c);
            string walls0 = string.Join("/", c.Wall.Select(w => (int)Math.Round(w.Hp * 100.0 / w.MaxHp)));
            string head = $"  {label} | 자리 {setup.BattleNumber} 단계 {setup.EnemyTier} 등불 {setup.LampBrightness} 적 {enemyTotal}{(setup.IsIgnitionNight ? " ★점화의 밤" : "")} | 시작 골드 {gold0} 병력 {troops0} 수비대 {garrison0} 등대 {c.BeaconRepair:0}% 성벽% {walls0} | 정비 {(firstNight ? "-" : prep.Trim())}";

            BattleResult r; float elapsed;
            bool timedOut;
            int rotations = 0;
            using (var sim = new BattleSim(setup, lookup))
            {
                Reinforce(sim, 0.34f, new StringBuilder());   // D-075 기준 플레이
                float nextAssist = 0f;
                while (!sim.Ended && sim.Elapsed < 1500f)
                {
                    if (sim.Elapsed >= nextAssist)
                    {
                        Assist(sim);
                        int sideBefore = sim.BeamSide;
                        SteerBeam(sim);
                        if (sim.BeamSide != sideBefore) rotations++;
                        nextAssist = sim.Elapsed + 5f;
                    }

                    sim.BeginFrame(0.05f); if (sim.Ended) break; sim.Schedule(0.05f, sim.Elapsed); sim.Complete();
                }

                timedOut = !sim.Ended;
                r = sim.BuildResult(); elapsed = sim.Elapsed;
            }

            firstNight = false;
            int lost = r.SquadLosses.Sum(l => l.LostCount);
            bool victory = !timedOut && r.Outcome == BattleOutcome.Victory;
            string outcomeText = timedOut ? "시간초과(패배로 침)" : $"{r.Outcome}{(r.Outcome == BattleOutcome.Defeat ? " " + r.DefeatReason + " " + r.DefeatWallId : "")}";
            log.Append($"{head}\n      → [{outcomeText}] {elapsed:0}s 처치 {r.DefeatedEnemyCount} 보상 {r.EarnedGold} 잃은병사 {lost} 빛 돌림 {rotations}회");

            if (!victory)
            {
                lostCount++;
                if (failedAt == "-") failedAt = label;
                float before = c.BeaconRepair;
                // FlowRouter: 첫 패배 = 결과 반영(병력·성벽 피해 남김) → 복구 절반·지원금, 연속 두 번째 = 패배 엔딩
                if (BeaconRules.LossEndsRun(c)) { ended = true; log.AppendLine($" | 연속 두 번째 패배 → 엔딩 (등대 {before:0}%)"); break; }
                c.LastBattle = r;
                if (timedOut) { log.AppendLine(" | 시간초과는 결과 반영 불가 → 엔딩으로 침"); ended = true; break; }
                if (!BattleResultApplier.TryApply(c, out string lossError, lookup.Get<TerrainDef>(setup.TerrainId), true)) { log.AppendLine($" | 패배 반영 오류 {lossError}"); return (false, won, "패배 반영 오류"); }
                BeaconRules.ApplyLoss(c);
                BeaconRules.RaiseWallFloor(c, lookup.All<BattleRulesDef>()[0].LostNightWallFloorPercent);   // GameFlow와 같은 순서: 복구 절반 → 성벽 최소 비율 → 지원금
                int support = BeaconRules.BeginDay(c);
                log.AppendLine($" | 패배(반영) → 병력 {c.Squads.Sum(s => s.Count)} 성벽% {string.Join("/", c.Wall.Select(w => (int)Math.Round(w.Hp * 100.0 / w.MaxHp)))} 등대 {before:0}→{c.BeaconRepair:0}% 지원금 +{support}");
                continue;
            }

            won++;
            bool ignition = setup.IsIgnitionNight;
            TerrainDef terrain = lookup.Get<TerrainDef>(setup.TerrainId);
            c.LastBattle = r;
            c.BattleNumber++;   // FlowRouter win.AdvanceBattle
            if (!BattleResultApplier.TryApply(c, out string applyError, terrain)) { log.AppendLine($" | 반영 오류 {applyError}"); return (false, won, "반영 오류"); }
            BeaconRules.ApplyWin(c, c.ActiveBattleSetup, r);   // ResultEntry
            string repairText = ignition ? "점화 성공" : $"등대 +{c.LastNightGain:0.#}";
            log.AppendLine($" | 승리 → {repairText}(등대 {c.BeaconRepair:0}%) 골드 {c.Gold} 병력 {c.Squads.Sum(s => s.Count)} 수비대 남김 {c.LastNightGarrison} 성벽% {string.Join("/", c.Wall.Select(w => (int)Math.Round(w.Hp * 100.0 / w.MaxHp)))}");
            if (c.NextStory != null && c.NextStory.Kind == StoryKind.RunComplete) { complete = true; break; }
            BeaconRules.BeginDay(c);   // Result → Prep
        }

        if (!complete && !ended) log.AppendLine("  (밤 12번을 넘겨 중단)");
        log.AppendLine($"  ★ {(complete ? "1회차 완료" : ended ? "패배 엔딩" : "중단")} · 승 {won} 패 {lostCount} · 첫 패배 {failedAt}");
        return (complete, won, failedAt);
    }


    // 강화 사용 정책 (시험 전용): 마법사 → 주인공 휘장 → 소모품 → 시설 → 궁수 훈련(골드 절반 안) → 정원이 차면 북쪽 용병. 남은 골드는 궁수 모집에
    static string SpendOnUpgrades(CampaignState c, DefinitionLookup lookup, bool troopsFull)
    {
        var bought = new List<string>();
        void Try(string item, string wall = null, int keep = 300)
        {
            ItemDef def = lookup.Get<ItemDef>(item);
            if (def == null || c.Gold < def.Price + keep) return;
            if (HumanLastRampart.Core.Upgrades.UpgradeRules.TryBuy(c, lookup, item, wall, out _)) bought.Add(def.DisplayName);
        }

        Try("item_gem_staff");
        Try("item_hourglass_charm");
        Try("item_command_badge");
        if (HumanLastRampart.Core.Upgrades.UpgradeRules.OwnedCount(c, "item_fire_arrows") == 0) Try("item_fire_arrows", null, 100);
        Try("item_surgeon_tent");
        if (!HumanLastRampart.Core.Upgrades.UpgradeRules.OwnsForWall(c, "item_shield_rail", WallIds.North)) Try("item_shield_rail", WallIds.North);
        foreach (string unit in new[] { "unit_archer", "unit_cannon_crew", "unit_gunner" })
            while (HumanLastRampart.Core.Upgrades.UpgradeRules.CanTrain(c, lookup, unit, out TrainingDef next, out _) && next.Price <= c.Gold / 2)
            {
                HumanLastRampart.Core.Upgrades.UpgradeRules.TryTrain(c, lookup, unit, out _);
                bought.Add($"{unit.Replace("unit_", "")} 훈련{next.Level}");
            }
        if (troopsFull)
            foreach (string merc in new[] { "merc_archer", "merc_gunner" })
            {
                MercenaryDef def = lookup.Get<MercenaryDef>(merc);
                int room = def.PerWallCap - HumanLastRampart.Core.Upgrades.UpgradeRules.MercenaryCount(c, merc, WallIds.North);
                int afford = (c.Gold - 200) / Math.Max(1, def.ContractCost + def.Wage);
                int n = Math.Min(room, afford);
                if (n > 0 && HumanLastRampart.Core.Upgrades.UpgradeRules.TryHire(c, lookup, merc, WallIds.North, n, out _)) bought.Add($"{def.DisplayName} {n}");
            }
        return bought.Count > 0 ? " 강화[" + string.Join(", ", bought) + "]" : "";
    }

    static void RunPriced(DefinitionLookup lookup, Difficulty difficulty, int[] prices, StringBuilder log, bool assist, int policy = 0, int seed = 1234, Action<BattleSim> chaos = null, bool reinforce = false, bool upgrades = false)
    {
        const int SlotCap = 160, WallCap = 500, TotalCap = 2000;
        CampaignState c = CampaignFactory.Create(lookup, new NewGameOptions(difficulty), seed);
        CampaignStartDef start = lookup.All<CampaignStartDef>()[0];
        TrapDef bomb = lookup.Get<TrapDef>("trap_bomb");
        WallSide WallOf(SquadState s) => lookup.Get<PostDef>(s.PostId).Wall;
        bool troopsFull = false;
        for (int battle = 1; battle <= 4; battle++)
        {
            c.BattleNumber = battle;
            c.LastBattle = null;
            if (upgrades)   // App과 같이 전투 준비 직전 용병 급료 (K-058)
                foreach (string note in HumanLastRampart.Core.Upgrades.UpgradeRules.PayWages(c, lookup)) log.Append($" ({note})");
            BattleSetup setup = CreateFitted(c, lookup, $"price_{difficulty}_{seed}_{battle}", out string error, log);
            if (setup == null) { log.AppendLine($"  전투{battle}: 준비 오류 {error}"); return; }
            c.ActiveBattleSetup = setup;
            int enemyTotal = 0;
            enemyTotal = BattleSim.ScaledGroupCounts(lookup.Get<WaveDef>(setup.WaveDefId).Groups.ToList(), setup.EnemyCountMultiplier).Sum();
            int gold0 = c.Gold, troops0 = c.Squads.Sum(s => s.Count);
            string walls0 = string.Join("/", c.Wall.Select(w => w.Hp));
            BattleResult r; float elapsed;
            using (var sim = new BattleSim(setup, lookup))
            {
                if (reinforce) Reinforce(sim, 0.34f, log);   // D-075 기준 플레이
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

            string upgradeText = upgrades ? SpendOnUpgrades(c, lookup, troopsFull) : "";
            if (upgrades) repairText += upgradeText;
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
            troopsFull = capRefusals > 0;   // 정원이 찼으면 다음 정비부터 용병 (K-058)
            int freeSlots = c.Squads.Where(s => s.UnitDefId == "unit_archer").Sum(s => SlotCap - s.Count);
            log.AppendLine($" | 정비 {repairText}, 함정 {trapsBought}개(상점 남음 {shop.Count}, 묻힌 함정 {c.Traps.Count}), 궁수 {hired}명 {spent}골드{(capRefusals > 0 ? " (정원으로 멈춤)" : "")} → 골드 {c.Gold} 병력 {c.Squads.Sum(s => s.Count)} 남은 궁수 자리 {freeSlots}");
        }
        log.AppendLine("  ★ 4번까지 모두 승리");
    }

    // K-040 불바다 통과 생존율 (근사 모형 — 전투 계산을 돌리지 않고 같은 규칙을 다시 셈):
    // 고블린이 원을 곧게 지나간다(지나는 줄 위치 고르게), 0.5초 틱마다 직접 피해(소수는 확률 반올림),
    // 불바다마다 처음 맞을 때 한 번 화상 확률, 붙으면 화상 전체 피해(벗어나도 탐). 화살·다른 피해는 없음
    [Test]
    public void FireFieldCrossing()
    {
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
        SpellDef fire = lookup.All<SpellDef>().First(s => s.Duration > 0f);
        var log = new StringBuilder();
        log.AppendLine($"불바다 반경 {fire.Radius}m · 직접 {fire.Damage}/초 · {fire.Duration}초 · 화상 {fire.BurnChance:P0} {fire.BurnDamage}/초 × {fire.BurnSeconds}초");
        foreach (EnemyDef enemy in lookup.All<EnemyDef>())
        {
            foreach (var (weather, multiplier) in new[] { ("맑음", 1f), ("소나기", 0.5f), ("비", 0.1f) })
            {
                var rng = new System.Random(1234);
                const int n = 20000;
                int throughAlive = 0, insideAlive = 0;
                for (int k = 0; k < n; k++)
                {
                    float offset = (float)(rng.NextDouble() * 2 - 1) * fire.Radius;
                    float chord = 2f * Mathf.Sqrt(fire.Radius * fire.Radius - offset * offset);
                    float time = chord / Mathf.Max(0.1f, enemy.MoveSpeed);
                    float startedInside = time * (float)rng.NextDouble();   // 불바다가 생길 때 이미 안에 있던 적
                    if (Survives(time)) throughAlive++;
                    if (Survives(startedInside)) insideAlive++;
                }

                log.AppendLine($"{enemy.Id} ({weather}): 밖에서 들어와 지나감 {throughAlive * 100f / n:0}% 생존 · 생길 때 안에 있었음 {insideAlive * 100f / n:0}% 생존");

                bool Survives(float secondsInside)
                {
                    float tick = 0.5f, damage = 0f, x = fire.Damage * tick * multiplier;
                    int ticks = 0;
                    for (float t = (float)rng.NextDouble() * tick; t < secondsInside && t < fire.Duration; t += tick) ticks++;
                    for (int i = 0; i < ticks; i++) damage += Mathf.Floor(x) + (rng.NextDouble() < x - Mathf.Floor(x) ? 1 : 0);
                    if (ticks > 0 && rng.NextDouble() < fire.BurnChance * multiplier) damage += fire.BurnDamage * multiplier * fire.BurnSeconds;
                    return damage < enemy.MaxHp;
                }
            }
        }

        Debug.Log(log.ToString());
        File.WriteAllText(Environment.GetEnvironmentVariable("HLR_PROBE_OUT") ?? "probe_firefield.txt", log.ToString(), new UTF8Encoding(false));
    }
}