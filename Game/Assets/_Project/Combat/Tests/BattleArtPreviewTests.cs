using System.Collections.Generic;
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
    // 그림 미리 연결 (사용자 검토 전): 오우거·사다리 고블린 걷기 4칸, 달리는 병사 그림
    public sealed class BattleArtPreviewTests
    {
        static DefinitionLookup LoadAll()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            return DefinitionLookup.Build(catalogs);
        }

        static BattleSim NewSim(DefinitionLookup lookup, int battle)
        {
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            campaign.BattleNumber = battle;
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_art_test_" + battle, out string error);
            Assert.IsNull(error);
            return new BattleSim(setup, lookup);
        }

        static void Step(BattleSim sim, ref float time)
        {
            const float dt = 0.05f;
            sim.BeginFrame(dt);
            if (sim.Ended) return;
            time += dt;
            sim.Schedule(dt, time);
            sim.Complete();
        }

        [Test]
        public void SpecialEnemies_WalkFourFrames()
        {
            DefinitionLookup lookup = LoadAll();
            Assert.AreEqual(4, PerfLab.PerfLabArt.FrameCount(lookup.Get<EnemyDef>("enemy_ogre").Sheet), "오우거 걷기 4칸");
            Assert.AreEqual(4, PerfLab.PerfLabArt.FrameCount(lookup.Get<EnemyDef>("enemy_goblin_ladder").Sheet), "사다리 고블린 걷기 4칸");
            using (BattleSim sim = NewSim(lookup, 3))
            {
                // 나안(2026-10-22)부터 사다리·오우거는 공성 단계(90초 안팎)에 나온다. 조작 없는 시험 전투가 그 전에 지지 않게 버티게 한다
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(BattleSim).GetField("garrisonLossRatio", flags).SetValue(sim, float.MaxValue);
                var wallHp = (float[])typeof(BattleSim).GetField("wallHp", flags).GetValue(sim);
                for (int w = 0; w < wallHp.Length; w++) wallHp[w] = 1e9f;
                float time = 0f;
                var seen = new HashSet<int>();
                int checkedEnemies = 0;
                for (int s = 0; s < 3000 && !sim.Ended && seen.Count < 4; s++)
                {
                    Step(sim, ref time);
                    foreach (var (def, start, count) in sim.EnemyGroups)
                    {
                        if (def.Id != "enemy_ogre" && def.Id != "enemy_goblin_ladder") continue;
                        for (int i = start; i < start + count; i++)
                        {
                            var inst = sim.EnemyInstances[i];
                            if (inst.TintSize.w <= 0f) continue;
                            int frame = (int)inst.PosFrame.w;
                            Assert.That(frame, Is.InRange(0, 3), "걷기 4칸 안 (공격 칸 없음)");
                            seen.Add(frame);
                            checkedEnemies++;
                        }
                    }
                }

                Assert.Greater(checkedEnemies, 0, "3번 전투에 특수 적이 나온다");
                Assert.AreEqual(4, seen.Count, "4칸을 돌아가며 쓴다");
            }
        }

        [Test]
        public void Messenger_UsesRunSheet_AndHidesStandingPicture()
        {
            DefinitionLookup lookup = LoadAll();
            Assert.IsNotNull(lookup.Get<UnitDef>("unit_archer").RunSheet, "궁수 달리기 그림 연결");
            using (BattleSim sim = NewSim(lookup, 1))
            {
                float time = 0f;
                Step(sim, ref time);
                sim.IssueMessenger(0, 20f, 1, TargetPriority.HighestHpRatio);
                for (int s = 0; s < 10; s++) Step(sim, ref time);
                sim.RefreshSoldierInstances();
                int runners = 0;
                for (int i = 0; i < sim.SoldierCount; i++)
                {
                    if (!sim.IsRunnerPicture(i, out bool flipped, out bool standingHidden)) continue;
                    runners++;
                    Assert.IsTrue(standingHidden, "달리는 동안 서 있는 그림은 숨김");
                    // 예전 달리기 그림은 왼쪽을 보고 달려 둘레 +로 가면 뒤집었다. 6판 그림(D-081)은 오른쪽을 보고 달려 뒤집지 않는다
                    bool sixRun = PerfLab.PerfLabArt.RunFacesRight(lookup.Get<UnitDef>("unit_archer")?.RunSheet);
                    Assert.AreEqual(!sixRun, flipped, "동쪽(둘레 +)으로 달릴 때 그림이 가는 쪽을 본다");
                }

                Assert.AreEqual(1, runners, "전령 한 명");
            }
        }
    }
}
