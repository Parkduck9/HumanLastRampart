using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;

namespace HumanLastRampart.Combat.Tests
{
    // 단계가 있는 전투 흐름 (나안): 선봉·본대 흐름·공성 예고·측면 시각·최후 돌격
    public sealed class BattlePhaseTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        struct Spawn
        {
            public int Slot;
            public float Time;
            public int Side;
        }

        static DefinitionLookup LoadAll()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            return DefinitionLookup.Build(catalogs);
        }

        static BattleSetup Setup(DefinitionLookup lookup, int battle, string runId)
        {
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 5);
            campaign.BattleNumber = battle;
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, runId, out string error);
            Assert.IsNull(error);
            return setup;
        }

        static List<Spawn> Entries(BattleSim sim)
        {
            var list = new List<Spawn>();
            foreach (object entry in (Array)typeof(BattleSim).GetField("spawnOrder", Private).GetValue(sim))
            {
                Type type = entry.GetType();
                list.Add(new Spawn
                {
                    Slot = (int)type.GetField("Slot").GetValue(entry),
                    Time = (float)type.GetField("Time").GetValue(entry),
                    Side = (int)type.GetField("Side").GetValue(entry),
                });
            }

            return list;
        }

        static NativeArray<int> Groups(BattleSim sim) => (NativeArray<int>)typeof(BattleSim).GetField("eGroup", Private).GetValue(sim);

        static void SetElapsed(BattleSim sim, float seconds) => typeof(BattleSim).GetProperty("Elapsed").SetValue(sim, seconds);

        static void SetKills(BattleSim sim, int kills)
        {
            var stats = (NativeArray<int>)typeof(BattleSim).GetField("stats", Private).GetValue(sim);
            stats[BattleStat.Kills] = kills;   // NativeArray는 같은 버퍼를 가리킨다
        }

        [Test]
        public void Battle4Easy_OnlyVanguardAppearsInFirstTwoSeconds()
        {
            DefinitionLookup lookup = LoadAll();
            using (var sim = new BattleSim(Setup(lookup, 4, "run_phase_vanguard"), lookup))
            {
                Assert.That(sim.VanguardCount, Is.InRange(10, 60));
                List<Spawn> entries = Entries(sim);
                int early = entries.Count(e => e.Time <= 2f);
                int vanguard = entries.Count(e => sim.IsVanguard(e.Slot));
                Assert.AreEqual(sim.VanguardCount, vanguard);
                Assert.IsTrue(entries.Where(e => sim.IsVanguard(e.Slot)).All(e => e.Time <= 2f), "선봉은 모두 2초 안");
                // 본대는 같은 시각부터 초당 40마리씩 흘러온다 — 2초 안에는 선봉 + 약 80마리, 수천 마리가 아니다
                Assert.LessOrEqual(early, sim.VanguardCount + 100, "2초 안에 나오는 적이 선봉과 본대 첫 흐름뿐");
                Assert.Less(entries.Count, sim.TotalEnemies + 1);
            }
        }

        [Test]
        public void MainGroups_StreamAtGroupRate()
        {
            DefinitionLookup lookup = LoadAll();
            BattleSetup setup = Setup(lookup, 4, "run_phase_stream");
            WaveDef wave = lookup.Get<WaveDef>(setup.WaveDefId);
            using (var sim = new BattleSim(setup, lookup))
            {
                List<Spawn> entries = Entries(sim);
                NativeArray<int> groups = Groups(sim);
                int checkedGroups = 0;
                for (int g = 0; g < sim.AttackPlan.Count; g++)
                {
                    if (sim.GroupPhaseOf(g) != WavePhase.Main || sim.AttackPlan[g].Count < 500) continue;
                    float rate = wave.Groups[g].StreamPerSecond > 0f ? wave.Groups[g].StreamPerSecond : 15f;
                    List<float> times = entries.Where(e => groups[e.Slot] == g && !sim.IsVanguard(e.Slot)).Select(e => e.Time).OrderBy(t => t).ToList();
                    Assert.Greater(times.Count, 400);
                    float spacing = (times[times.Count - 1] - times[0]) / (times.Count - 1);
                    Assert.That(spacing, Is.EqualTo(1f / rate).Within(0.25f / rate), $"무리 {g}: 초당 {rate}마리 간격");
                    Assert.GreaterOrEqual(times[0], sim.AttackPlan[g].SpawnAt, "무리 시작 시각 뒤에 나온다");
                    checkedGroups++;
                }

                Assert.GreaterOrEqual(checkedGroups, 2);
            }
        }

        [Test]
        public void SiegeGroup_WarnsFiveSecondsBeforeFirstEnemy()
        {
            DefinitionLookup lookup = LoadAll();
            using (var sim = new BattleSim(Setup(lookup, 4, "run_phase_siege"), lookup))
            {
                int siege = -1;
                for (int g = 0; g < sim.AttackPlan.Count; g++)
                    if (sim.GroupPhaseOf(g) == WavePhase.Siege && (siege < 0 || sim.GroupFirstSpawn(g) < sim.GroupFirstSpawn(siege))) siege = g;
                Assert.GreaterOrEqual(siege, 0, "공성 무리가 있다 (사다리 고블린·오우거)");
                float first = sim.GroupFirstSpawn(siege);
                Assert.Greater(first, 26f, "본대 예고와 겹치지 않는 시험 조건");

                SetElapsed(sim, first - 6f);
                Assert.IsFalse(sim.TryGetPhaseWarning(out _), "6초 전에는 예고가 없다");
                SetElapsed(sim, first - 4.9f);
                Assert.IsTrue(sim.TryGetPhaseWarning(out BattleSim.PhaseWarning warning));
                Assert.AreEqual(WavePhase.Siege, warning.Phase);
                Assert.AreEqual(sim.AttackPlan[siege].Side, warning.Side);
                Assert.That(warning.SecondsLeft, Is.EqualTo(4.9f).Within(0.01f));
                SetElapsed(sim, first + 0.1f);
                Assert.IsFalse(sim.TryGetPhaseWarning(out _), "나온 뒤에는 예고가 없다");
                Assert.AreEqual(WavePhase.Siege, sim.CurrentPhase);
            }
        }

        [Test]
        public void FlankGroups_SpawnNoEarlierThanSixtySeconds()
        {
            DefinitionLookup lookup = LoadAll();
            int flankGroups = 0;
            for (int battle = 2; battle <= 4; battle++)
            {
                for (int run = 0; run < 4; run++)
                {
                    using (var sim = new BattleSim(Setup(lookup, battle, $"run_phase_flank_{battle}_{run}"), lookup))
                    {
                        Assert.AreEqual(0.8f, sim.FrontShare, 1e-4f, "정면:우회 = 80:20");
                        NativeArray<int> groups = Groups(sim);
                        foreach (Spawn e in Entries(sim))
                        {
                            BattleSim.GroupPlan plan = sim.AttackPlan[groups[e.Slot]];
                            if (!plan.Flank) continue;
                            Assert.GreaterOrEqual(plan.SpawnAt, 60f);
                            Assert.GreaterOrEqual(e.Time, 60f, "우회 무리의 적은 60초 전에 나오지 않는다");
                        }

                        flankGroups += sim.AttackPlan.Count(g => g.Flank);
                    }
                }
            }

            Assert.Greater(flankGroups, 0);
        }

        [Test]
        public void FinalAssault_CompressesRemainingSpawnsAtTwentyPercent()
        {
            DefinitionLookup lookup = LoadAll();
            using (var sim = new BattleSim(Setup(lookup, 4, "run_phase_final"), lookup))
            {
                int total = sim.TotalEnemies;
                SetKills(sim, (int)(total * 0.79f));   // 남은 적 21%
                sim.BeginFrame(0.05f);
                Assert.IsFalse(sim.FinalAssaultStarted, "21%는 아직");
                Assert.AreNotEqual(WavePhase.Final, sim.CurrentPhase);

                SetKills(sim, (int)(total * 0.81f));   // 남은 적 19%
                sim.BeginFrame(0.05f);
                Assert.IsTrue(sim.FinalAssaultStarted);
                Assert.AreEqual(WavePhase.Final, sim.CurrentPhase);
                float now = sim.Elapsed;
                List<Spawn> tail = Entries(sim).Skip(sim.Spawned).ToList();
                Assert.Greater(tail.Count, 1000, "안 나온 적이 많이 남아 있었다");
                Assert.IsTrue(tail.All(e => e.Time >= now - 1e-3f && e.Time <= now + 10f + 1e-3f), "남은 적은 모두 앞으로 10초 안");
                var planSides = sim.AttackPlan.Select(g => g.Side).Distinct().OrderBy(s => s).ToList();
                CollectionAssert.AreEqual(planSides, tail.Select(e => e.Side).Distinct().OrderBy(s => s).ToList(), "이 전투가 공격하는 면에만 나뉜다");
                Assert.IsTrue(Enumerable.Range(1, tail.Count - 1).All(i => tail[i].Time >= tail[i - 1].Time), "시각 순서 유지");
            }
        }

        [Test]
        public void FinalAssault_Battle1StaysOnOriginalSide()
        {
            DefinitionLookup lookup = LoadAll();
            using (var sim = new BattleSim(Setup(lookup, 1, "run_phase_final_b1"), lookup))
            {
                SetKills(sim, (int)(sim.TotalEnemies * 0.85f));
                sim.BeginFrame(0.05f);
                Assert.IsTrue(sim.FinalAssaultStarted);
                Assert.IsTrue(Entries(sim).Skip(sim.Spawned).All(e => e.Side == BattleSim.FrontSide), "1번 전투는 정면뿐");
            }
        }

        [Test]
        public void FinalAssault_ResidedEnemiesDoNotTriggerFlankDiscovery()
        {
            DefinitionLookup lookup = LoadAll();
            using (var sim = new BattleSim(Setup(lookup, 4, "run_phase_final_flank"), lookup))
            {
                Assert.IsTrue(sim.AttackPlan.Any(g => g.Flank), "우회 무리가 있는 판");
                var notices = new List<string>();
                sim.Notice += notices.Add;
                SetKills(sim, (int)(sim.TotalEnemies * 0.85f));
                sim.BeginFrame(0.05f);
                Assert.IsTrue(sim.FinalAssaultStarted);
                notices.Clear();
                SetElapsed(sim, 70f);   // 우회 무리의 출현 시각을 지난 뒤
                for (int i = 0; i < 20; i++) sim.BeginFrame(0.05f);
                Assert.AreEqual(0, sim.FlankReports);
                Assert.AreEqual(0, sim.RunnerPairsActive);
                Assert.IsFalse(notices.Any(n => n.Contains("돌아옵니다") || n.Contains("발견")), "면이 바뀐 적으로 우회 발견 알림이 뜨지 않는다");
            }
        }

        [Test]
        public void SameSeed_GivesSameSpawnsAndSameFinalAssault()
        {
            DefinitionLookup lookup = LoadAll();
            using (var a = new BattleSim(Setup(lookup, 4, "run_phase_same"), lookup))
            using (var b = new BattleSim(Setup(lookup, 4, "run_phase_same"), lookup))
            {
                AssertSame(Entries(a), Entries(b));
                foreach (BattleSim sim in new[] { a, b })
                {
                    SetKills(sim, (int)(sim.TotalEnemies * 0.85f));
                    sim.BeginFrame(0.05f);
                    Assert.IsTrue(sim.FinalAssaultStarted);
                }

                AssertSame(Entries(a), Entries(b));
            }
        }

        static void AssertSame(List<Spawn> a, List<Spawn> b)
        {
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Slot, b[i].Slot);
                Assert.AreEqual(a[i].Time, b[i].Time);
                Assert.AreEqual(a[i].Side, b[i].Side);
            }
        }
    }
}
