using System.Collections.Generic;
using System.Text;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 같은 씨앗·같은 입력이면 전투 모의 결과가 늘 같다 (W22). 병렬 작업의 큐 순서·실제 시간이 결과에 새면 여기서 갈린다
    public sealed class BattleDeterminismTests
    {
        static BattleSim NewSim(DefinitionLookup lookup)
        {
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_determinism_test", out string error);
            Assert.IsNull(error);
            return new BattleSim(setup, lookup);
        }

        // N틱을 돌리며 매 틱의 요약(경과·처치·금화·성벽·대포 쉼·발사 중 투사체)을 한 줄 글로 쌓는다
        static string Run(DefinitionLookup lookup, int ticks)
        {
            var log = new StringBuilder();
            using (BattleSim sim = NewSim(lookup))
            {
                // 모든 대포가 각자 성벽 바로 앞을 노린다 (대포 조준 시험과 같은 입력)
                var dirs = new[] { new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(-1f, 0f) };
                for (int c = 0; c < sim.CannonCount; c++)
                {
                    BattleSim.CannonInfo info = sim.GetCannon(c);
                    Vector2 p = new Vector2(info.Position.x, info.Position.z), d = dirs[info.Side];
                    Vector2 aim = p - d * Vector2.Dot(p, d) + d * (sim.CastleHalf + 5f);
                    sim.SetCannonAim(c, new Vector3(aim.x, 0f, aim.y));
                }

                const float dt = 0.05f;
                float time = 0f;
                for (int t = 0; t < ticks; t++)
                {
                    sim.BeginFrame(dt);
                    if (sim.Ended) break;
                    time += dt;
                    sim.Schedule(dt, time);
                    sim.Complete();
                    log.Append(t).Append(':').Append(sim.Elapsed.ToString("R")).Append(' ').Append(sim.Spawned).Append(' ').Append(sim.Kills).Append(' ').Append(sim.Gold).Append(' ').Append(sim.ActiveShots);
                    for (int s = 0; s < 4; s++) log.Append(' ').Append(sim.Sides[s].WallHp).Append('/').Append(sim.Sides[s].InField).Append('/').Append(sim.Sides[s].AtWall).Append('/').Append(sim.Sides[s].GarrisonAlive);
                    log.Append(' ');
                    for (int c = 0; c < sim.CannonCount; c++) log.Append(sim.GetCannon(c).Idle ? 'I' : 'F');
                    log.Append('\n');
                }
            }

            return log.ToString();
        }

        [Test]
        public void SameSeedAndInputs_GiveSameOutcome_OverManyTicks()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            DefinitionLookup lookup = DefinitionLookup.Build(catalogs);

            const int ticks = 600;   // 게임 안 30초 — 선봉이 성벽 앞에 닿아 교전이 시작된 뒤까지
            string first = Run(lookup, ticks);
            for (int again = 0; again < 3; again++)
                Assert.AreEqual(first, Run(lookup, ticks), $"같은 씨앗·같은 입력인데 {again + 2}번째 실행 결과가 다르다");
            StringAssert.Contains("599:", first, "끝까지 돌았다");
        }
    }
}
