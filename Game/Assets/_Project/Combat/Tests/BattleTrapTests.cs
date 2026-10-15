using System.Collections.Generic;
using System.Linq;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Traps;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 전투 중 함정 (CY4-C1, D-050): 실제 정의 데이터로 전투를 돌려 점화·피해·소비·구덩이·다른 지형을 확인한다
    public sealed class BattleTrapTests
    {
        static DefinitionLookup LoadAll()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            return DefinitionLookup.Build(catalogs);
        }

        // 북쪽 성벽 가운데 앞 칸 (성벽에서 약 72m — 궁수 사거리 밖이라 적이 살아서 지나간다)에 표시한 함정 하나
        static BattleSetup SetupWithTrap(DefinitionLookup lookup, string terrainOfTrap, out TrapPlacement trap)
        {
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            TerrainDef terrain = lookup.Get<TerrainDef>(terrainOfTrap);
            Vector2 c = TrapGrid.Center(terrain, 0, TrapGrid.Columns(terrain) / 2, 12);
            trap = new TrapPlacement
            {
                TrapId = campaign.NewInstanceId("trap"), TrapDefId = "trap_bomb", TerrainId = terrainOfTrap,
                SlotId = TrapGrid.CellId(0, TrapGrid.Columns(terrain) / 2, 12), ActualX = c.x, ActualZ = c.y, Marked = true, MarkX = c.x, MarkZ = c.y,
            };
            campaign.Traps.Add(trap);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_trap_test", out string error);
            Assert.IsNull(error);
            return setup;
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
        public void Detonate_DamagesNearbyEnemies_ConsumesOnce_LeavesCrater()
        {
            DefinitionLookup lookup = LoadAll();
            BattleSetup setup = SetupWithTrap(lookup, "terrain_1", out TrapPlacement placement);
            Assert.AreEqual(1, setup.Traps.Count, "전투 준비에 이 지형 함정");
            using (var sim = new BattleSim(setup, lookup))
            {
                Assert.AreEqual(1, sim.TrapCount);
                float time = 0f;
                int near = 0;
                for (int i = 0; i < 4000 && !sim.Ended; i++)
                {
                    Step(sim, ref time);
                    near = sim.EnemiesInTrapRange(0);
                    if (near >= 3) break;
                }

                Assert.GreaterOrEqual(near, 3, "적이 함정 근처로 와야 한다");
                Assert.AreEqual(BattleSim.TrapState.Armed, sim.GetTrap(0).State, "누르기 전에는 묻혀 있다 (저절로 1%가 안 걸린 씨앗)");
                int kills0 = sim.Kills;
                Assert.IsTrue(sim.DetonateTrap(0));
                BattleSim.TrapState after = sim.GetTrap(0).State;
                Assert.IsTrue(after == BattleSim.TrapState.Fusing || after == BattleSim.TrapState.Dud);
                Assume.That(after == BattleSim.TrapState.Fusing, "이 씨앗에서 불발(1%)이면 아래 확인은 건너뛴다");
                for (int i = 0; i < 20; i++) Step(sim, ref time);   // 0.5초 지연 + 피해 처리
                Assert.AreEqual(BattleSim.TrapState.Exploded, sim.GetTrap(0).State);
                Assert.Greater(sim.Kills, kills0, "폭발로 적이 죽는다 (피해 300, 고블린 30)");
                Assert.IsFalse(sim.DetonateTrap(0), "터진 함정은 다시 누를 수 없다");

                BattleResult result = sim.BuildResult();
                CollectionAssert.AreEqual(new[] { placement.TrapId }, result.ConsumedTrapIds, "터진 함정 한 번만 기록");
                Assert.IsFalse(result.TrapMoves.Any(m => m.TrapId == placement.TrapId), "터진 함정은 밀려나지 않는다");
            }
        }

        [Test]
        public void TrapOnOtherTerrain_IsNotInBattle()
        {
            DefinitionLookup lookup = LoadAll();
            BattleSetup setup = SetupWithTrap(lookup, "terrain_2", out _);   // 1번 전투는 terrain_1
            Assert.AreEqual(0, setup.Traps.Count, "다른 지형 함정은 전투 준비에 없다 (C-005 v1.1)");
            using (var sim = new BattleSim(setup, lookup))
            {
                Assert.AreEqual(0, sim.TrapCount);
            }
        }

        [Test]
        public void Grid_SizeHillsAndIds()
        {
            DefinitionLookup lookup = LoadAll();
            TerrainDef terrain = lookup.Get<TerrainDef>("terrain_1");
            Assert.AreEqual(8, TrapGrid.Columns(terrain), "성벽 40m ÷ 5m (D-061)");
            Assert.AreEqual(14, TrapGrid.Rows(terrain), "10~80m ÷ 5m");
            Vector2 first = TrapGrid.Center(terrain, 0, 0, 0);
            Assert.AreEqual(-17.5f, first.x, 0.01f);
            Assert.AreEqual(32.5f, first.y, 0.01f, "북쪽 첫 줄 = 성벽 20 + 10 + 2.5");
            Assert.IsTrue(TrapGrid.TryParse(TrapGrid.CellId(3, 7, 9), out int side, out int column, out int row));
            Assert.AreEqual((3, 7, 9), (side, column, row));
            Assert.IsFalse(TrapGrid.TryParse("post_north_squad_1", out _, out _, out _));

            int blocked = 0;
            for (int s = 0; s < 4; s++)
                for (int col = 0; col < TrapGrid.Columns(terrain); col++)
                    for (int r = 0; r < TrapGrid.Rows(terrain); r++)
                        if (!TrapGrid.IsPlaceable(terrain, s, col, r)) blocked++;
            Assert.Greater(blocked, 0, "지형 1 언덕 칸은 놓을 수 없다");
            Assert.IsFalse(TrapGrid.IsPlaceable(terrain, 0, 8, 0), "범위 밖");
        }
    }
}
