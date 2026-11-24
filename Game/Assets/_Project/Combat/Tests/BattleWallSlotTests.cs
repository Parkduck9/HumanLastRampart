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
    // 병사 총안 자리 (K-045): 앞줄은 성벽 3D 돌출부 사이 총안과 같은 자리, 한 자리에 한 명, 횃불 돌 뒤는 비움
    public sealed class BattleWallSlotTests
    {
        static BattleSim NewSim()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            campaign.BattleNumber = 1;
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_wall_slot_test", out string error);
            Assert.IsNull(error);
            return new BattleSim(setup, lookup);
        }

        [Test]
        public void FrontRow_StandsInEmbrasures_OneSoldierPerSlot()
        {
            using (BattleSim sim = NewSim())
            {
                var embrasures = new List<float>[4];
                for (int side = 0; side < 4; side++)
                {
                    embrasures[side] = new List<float>();
                    BattleWallLayout.Embrasures(sim.CastleHalf, sim.CannonBays(side), embrasures[side]);
                    Assert.AreEqual(embrasures[side].Count, sim.SlotCount(side, 0), "앞줄 자리 = 성벽 3D 총안 수");
                    Assert.Greater(embrasures[side].Count, 40, "성벽 한 면 총안 40칸 넘게");
                    Assert.Greater(sim.TorchSpots(side).Count, 5, "횃불이 성벽마다 여러 개");
                }

                var taken = new HashSet<(int, int, int)>();
                int front = 0, placed = 0;
                for (int i = 0; i < sim.SoldierCount; i++)
                {
                    int row = sim.SoldierRow(i, out int side, out float lateral);
                    if (row < 0) continue;   // 대포 운용병
                    placed++;
                    Assert.IsTrue(taken.Add((side, row, Mathf.RoundToInt(lateral * 1000f))), $"병사 {i}: 한 자리에 한 명");
                    if (row % 2 == 0)
                    {
                        bool inEmbrasure = embrasures[side].Exists(e => Mathf.Abs(e - lateral) < 1e-3f);
                        Assert.IsTrue(inEmbrasure, $"병사 {i}: 총안 줄은 총안 자리");
                        if (row == 0) front++;
                    }
                    else
                    {
                        foreach (float torch in sim.TorchSpots(side))
                            Assert.Greater(Mathf.Abs(torch - lateral), 1e-3f, $"병사 {i}: 횃불 돌 뒤에 서지 않는다");
                    }
                }

                Assert.Greater(placed, 0, "총안 자리에 선 병사가 있다");
                Assert.Greater(front, placed / 3, "앞줄(총안)부터 채운다");
            }
        }

        [Test]
        public void Torches_SitBehindMerlons()
        {
            using (BattleSim sim = NewSim())
            {
                var merlons = new List<float>();
                for (int side = 0; side < 4; side++)
                {
                    BattleWallLayout.Merlons(sim.CastleHalf, sim.CannonBays(side), merlons);
                    foreach (float torch in sim.TorchSpots(side))
                        Assert.IsTrue(merlons.Exists(m => Mathf.Abs(m - torch) < 1e-3f), "횃불은 돌출부 자리");
                }
            }
        }
    }
}
