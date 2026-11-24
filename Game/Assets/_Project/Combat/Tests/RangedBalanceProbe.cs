using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Upgrades;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// 시험 전용: 실제 시뮬레이션을 호출하되 후보 정의는 복제본에만 적용한다.
public sealed class RangedBalanceProbe
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    sealed class Lookup : IDefinitionLookup, IDisposable
    {
        readonly DefinitionLookup source;
        readonly Dictionary<string, DefinitionBase> copies = new Dictionary<string, DefinitionBase>();
        public Lookup(DefinitionLookup source) { this.source = source; }
        public T Copy<T>(string id) where T : DefinitionBase
        {
            var copy = UnityEngine.Object.Instantiate(source.Get<T>(id)); copies.Add(id, copy); return copy;
        }
        public T Get<T>(string id) where T : DefinitionBase => copies.TryGetValue(id, out var copy) ? copy as T : source.Get<T>(id);
        public IReadOnlyList<T> All<T>() where T : DefinitionBase => source.All<T>().Select(d => Get<T>(d.Id)).ToList();
        public void Dispose() { foreach (var copy in copies.Values) UnityEngine.Object.DestroyImmediate(copy); }
    }
    [Serializable] public sealed class Row
    {
        public int round, seed, stage, training, archers, gunners, recruitCost, trainingCost, totalEnemies, kills, lost, wallHp;
        public string scenario, profile, outcome, defeat, variant;
        public float seconds, killRatio;
    }
    [Serializable] public sealed class Results { public List<Row> rows = new List<Row>(); }
    static void Set(object value, string field, object setting) => value.GetType().GetField(field, Private).SetValue(value, setting);

    [Test, Explicit("상황별 실제 시뮬레이션 측정: testFilter RangedBalanceProbe.Compare로 별도 실행")]
    public void Compare()
    {
        int round = int.TryParse(Environment.GetEnvironmentVariable("HLR_BALANCE_ROUND"), out int number) ? number : 1;
        string output = Environment.GetEnvironmentVariable("HLR_BALANCE_OUT");
        if (string.IsNullOrEmpty(output))
            Assert.Ignore("선택 측정 도구: HLR_BALANCE_OUT을 지정한 별도 실행에서만 측정한다");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var catalogs = new List<DefinitionCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
            catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        DefinitionLookup original = DefinitionLookup.Build(catalogs);
        var results = new Results();
        int[] seeds = round == 3 ? new[] { 11, 29, 71 } : new[] { 11 };
        string[] cases = { "swarm", "ogre", "ladder", "mixed", "rain", "night", "flank", "actual_easy_1", "actual_easy_4" };
        string caseFilter = Environment.GetEnvironmentVariable("HLR_BALANCE_CASE");
        if (!string.IsNullOrEmpty(caseFilter)) cases = new[] { caseFilter };
        if (int.TryParse(Environment.GetEnvironmentVariable("HLR_BALANCE_SEED"), out int seedFilter)) seeds = new[] { seedFilter };
        foreach (bool candidate in round == 3 ? new[] { false, true } : new[] { round >= 2 })
        foreach (int seed in seeds)
        foreach (string scenario in cases)
        foreach (int level in new[] { 0, 3 })
        foreach (string profile in new[] { "archer_40", "archer_budget_64", "gunner_40", "mixed_32_20" })
        {
            using (var defs = new Lookup(original))
            {
                if (candidate)
                {
                    WeaponDef gun = defs.Copy<WeaponDef>("weapon_gun");
                    Set(gun, "damage", 30); Set(gun, "range", 40f); Set(gun, "interval", 2f);
                    Set(gun, "accuracyMid", 0.8f); Set(gun, "accuracyFar", 0.45f);
                    if (round == 3)
                    {
                        TrainingDef training = defs.Copy<TrainingDef>("training_gunner_1");
                        Set(training, "effects", new List<UpgradeEffect> {
                            new UpgradeEffect { Stat = UpgradeStat.UnitAccuracyFar, Value = 0.1f, UnitDefId = "unit_gunner" }
                        });
                    }
                }
                CampaignState campaign = CampaignFactory.Create(defs, new NewGameOptions(Difficulty.Easy), seed);
                int stage = scenario == "flank" || scenario == "actual_easy_4" ? 4 : 1;
                campaign.BattleNumber = stage; campaign.Gold = 100000;
                int a = profile == "archer_40" ? 40 : profile == "archer_budget_64" ? 64 : profile == "mixed_32_20" ? 32 : 0;
                int g = profile == "gunner_40" ? 40 : profile == "mixed_32_20" ? 20 : 0;
                int trainingCost = 0;
                foreach (string unit in new[] { "unit_archer", "unit_gunner" })
                    if ((unit == "unit_archer" ? a : g) > 0)
                        for (int l = 0; l < level; l++)
                        {
                            int before = campaign.Gold;
                            Assert.IsTrue(UpgradeRules.TryTrain(campaign, defs, unit, out string reason), reason);
                            trainingCost += before - campaign.Gold;
                        }
                // 같은 위치의 지점으로 배치. 기존 대포·병력은 측정 전용 복사 진행 데이터에서 제외.
                campaign.Squads.Clear();
                foreach (WallSide side in new[] { WallSide.North, WallSide.East, WallSide.South, WallSide.West })
                {
                    PostDef post = defs.All<PostDef>().Where(p => p.Wall == side).OrderBy(p => Math.Abs(p.PositionAlongWall - 0.5f)).First();
                    foreach (var unit in new[] { (id: "unit_archer", count: a), (id: "unit_gunner", count: g) })
                        if (unit.count > 0) campaign.Squads.Add(new SquadState { SquadId = side + unit.id, UnitDefId = unit.id, PostId = post.Id, Count = unit.count });
                }
                // 같은 시나리오/시드에는 편성·후보와 무관하게 동일 run ID 사용.
                BattleSetup setup = BattleSetupFactory.Create(campaign, defs, "ranged_" + scenario + "_" + seed, out string error);
                Assert.IsNull(error);
                setup.Traps.Clear(); setup.Mercenaries.Clear();
                setup.Forecast = new WeatherForecast { InitialWeather = scenario == "rain" ? Weather.Rain : Weather.Clear,
                    TimeOfDay = scenario == "night" ? TimeOfDay.Night : TimeOfDay.Day };
                if (!scenario.StartsWith("actual_"))
                {
                    WaveDef wave = defs.Copy<WaveDef>(setup.WaveDefId);
                    var groups = new List<WaveGroup>();
                    void Group(string enemy, int count, float at) => groups.Add(new WaveGroup { Side = WallSide.North, EnemyDefId = enemy, Count = count, SpawnAtSeconds = at });
                    if (scenario == "swarm") { Group("enemy_goblin", 300, 0f); Group("enemy_goblin", 300, 20f); }
                    else if (scenario == "ogre") { Group("enemy_ogre", 12, 0f); Group("enemy_ogre", 12, 20f); }
                    else if (scenario == "ladder") { Group("enemy_goblin_ladder", 150, 0f); Group("enemy_goblin_ladder", 150, 20f); }
                    else { Group("enemy_goblin", 160, 0f); Group("enemy_ogre", 12, 20f); Group("enemy_goblin_ladder", 100, 30f); }
                    Set(wave, "groups", groups); setup.EnemyCountMultiplier = 1f;
                }
                using (var sim = new BattleSim(setup, defs))
                {
                    float limit = scenario == "flank" ? 420f : 180f;
                    while (!sim.Ended && sim.Elapsed < limit)
                    {
                        sim.BeginFrame(0.05f); if (sim.Ended) break;
                        sim.Schedule(0.05f, sim.Elapsed); sim.Complete();
                    }
                    BattleResult result = sim.BuildResult();
                    var row = new Row { round = round, seed = seed, stage = stage, training = level, archers = a * 4, gunners = g * 4,
                        recruitCost = 4 * (a * defs.Get<UnitDef>("unit_archer").RecruitCost + g * defs.Get<UnitDef>("unit_gunner").RecruitCost),
                        trainingCost = trainingCost, scenario = scenario, profile = profile, seconds = sim.Elapsed,
                        variant = candidate ? (round == 3 ? "weapon_and_training" : "weapon_only") : "baseline",
                        totalEnemies = sim.TotalEnemies, kills = sim.Kills, killRatio = sim.TotalEnemies == 0 ? 0f : (float)sim.Kills / sim.TotalEnemies,
                        lost = result.SquadLosses.Sum(l => l.LostCount), wallHp = sim.Sides.Sum(s => s.WallHp),
                        outcome = sim.Ended ? result.Outcome.ToString() : "Timeout", defeat = sim.Ended ? result.DefeatReason.ToString() : "" };
                    results.rows.Add(row);
                    File.WriteAllText(output, JsonUtility.ToJson(results, true));
                    Debug.Log($"RANGED round={round} {scenario} seed={seed} {profile} L{level} {row.outcome} kills={row.kills}/{row.totalEnemies}");
                }
            }
        }
        Assert.AreEqual(seeds.Length * cases.Length * 2 * 4 * (round == 3 ? 2 : 1), results.rows.Count);
        Assert.IsTrue(results.rows.All(r => r.totalEnemies > 0 && r.seconds > 0f));
    }
}
