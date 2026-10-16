using System.Collections.Generic;
using System.Linq;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEditor;

namespace HumanLastRampart.Combat.Tests
{
    // 정면 공격·우회·발견 전령 (D-053)
    public sealed class BattleFlankTests
    {
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

        [Test]
        public void Battle1_AllFront()
        {
            DefinitionLookup lookup = LoadAll();
            using (var sim = new BattleSim(Setup(lookup, 1, "run_flank_1"), lookup))
            {
                Assert.AreEqual(1f, sim.FrontShare);
                Assert.IsTrue(sim.AttackPlan.All(g => !g.Flank && g.Side == BattleSim.FrontSide), "1번 전투는 정면만");
            }
        }

        [Test]
        public void LaterBattles_SidesUnlockByStage_TimesInRange()
        {
            DefinitionLookup lookup = LoadAll();
            for (int battle = 2; battle <= 4; battle++)
            {
                for (int run = 0; run < 6; run++)
                {
                    using (var sim = new BattleSim(Setup(lookup, battle, $"run_flank_{battle}_{run}"), lookup))
                    {
                        Assert.That(sim.FrontShare, Is.InRange(0.5f, 1f));
                        foreach (BattleSim.GroupPlan g in sim.AttackPlan.Where(g => g.Flank))
                        {
                            Assert.That(g.SpawnAt, Is.InRange(0f, 150f), "우회 무리는 0~150초");
                            Assert.AreNotEqual(BattleSim.FrontSide, g.Side);
                            if (battle == 2) Assert.AreNotEqual(2, g.Side, "2번 전투는 옆 면만 (뒷면은 3번부터)");
                        }

                        Assert.IsFalse(sim.AttackPlan[0].Flank, "첫 무리는 정면");
                    }
                }
            }

            // 같은 전투 실행 ID면 같은 계획
            using (var a = new BattleSim(Setup(lookup, 4, "run_same"), lookup))
            using (var b = new BattleSim(Setup(lookup, 4, "run_same"), lookup))
            {
                Assert.AreEqual(a.FrontShare, b.FrontShare);
                CollectionAssert.AreEqual(a.AttackPlan.Select(g => (g.Side, g.SpawnAt)), b.AttackPlan.Select(g => (g.Side, g.SpawnAt)));
            }
        }

        // 우회 무리가 있는 전투를 찾아 돌린다: 지휘관이 정면(북)에 있으면 병사 2명이 달려 보고하고, 한 바퀴 뒤 돌아온다
        // 시야가 트인 판만 쓴다 (K-045 합친 뒤 실패): 안개(25m)·밤(30m)이면 발견 거리가 궁수 사거리(50m)보다 짧아,
        // 우회 무리가 보이기 전에 성벽 병사에게 다 쓰러져 발견 자체가 안 일어날 수 있다 — 병사 자리·대포 조준이 조금만 바뀌어도 결과가 뒤집혔다
        [Test]
        public void Discovery_SendsRunners_WhoReportAndReturn()
        {
            DefinitionLookup lookup = LoadAll();
            for (int run = 0; run < 40; run++)
            {
                BattleSetup setup = Setup(lookup, 2, $"run_runner_{run}");
                using (var sim = new BattleSim(setup, lookup))
                {
                    if (!sim.AttackPlan.Any(g => g.Flank)) continue;
                    if (setup.Forecast.InitialWeather == Weather.Fog || setup.Forecast.TimeOfDay == TimeOfDay.Night ||
                        (setup.Forecast.Changes && setup.Forecast.ChangedWeather == Weather.Fog)) continue;
                    var notices = new List<string>();
                    sim.Notice += notices.Add;
                    sim.SetCommander(BattleSim.FrontSide, 0f);
                    float time = 0f;
                    bool sawRunners = false;
                    for (int i = 0; i < 6000 && !sim.Ended; i++)
                    {
                        sim.BeginFrame(0.05f);
                        if (sim.Ended) break;
                        time += 0.05f;
                        sim.Schedule(0.05f, time);
                        sim.Complete();
                        if (sim.RunnerPairsActive > 0) sawRunners = true;
                        if (sawRunners && sim.FlankReports > 0 && sim.RunnerPairsActive == 0) break;
                    }

                    Assert.IsTrue(sawRunners, $"우회 무리를 발견하면 전령이 출발 (run_runner_{run}, 끝남 {sim.Ended} {sim.Outcome}, {time:0}초, " +
                        $"알림: {string.Join(" / ", notices.Where(n => n.Contains("적") || n.Contains("병사")).Take(8))})");
                    Assert.Greater(sim.FlankReports, 0, "지휘관에게 보고");
                    Assert.AreEqual(0, sim.RunnerPairsActive, "한 바퀴 뒤 돌아온다");
                    return;
                }
            }

            Assert.Inconclusive("우회 무리가 있는 2번 전투를 찾지 못함");
        }
    }
}
