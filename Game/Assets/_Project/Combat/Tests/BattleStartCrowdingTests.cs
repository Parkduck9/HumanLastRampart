using System;
using System.Collections.Generic;
using System.Reflection;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;

namespace HumanLastRampart.Combat.Tests
{
    // 첫 무리의 등장 거리·시각과 최소 간격까지 성벽에 끼워 넣기 (피드백 20·2)
    public sealed class BattleStartCrowdingTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        static DefinitionLookup LoadAll()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            return DefinitionLookup.Build(catalogs);
        }

        static BattleSetup Setup(DefinitionLookup lookup, int battle = 1)
        {
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            campaign.BattleNumber = battle;
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_start_crowding_test", out string error);
            Assert.IsNull(error);
            return setup;
        }

        static BattleSim Soldiers(int north, int east)
        {
            DefinitionLookup lookup = LoadAll();
            BattleSetup setup = Setup(lookup);
            SquadState n = setup.Squads.Find(s => lookup.Get<PostDef>(s.PostId)?.Wall == WallSide.North
                && lookup.Get<UnitDef>(s.UnitDefId)?.Kind == UnitKind.Archer);
            SquadState e = setup.Squads.Find(s => lookup.Get<PostDef>(s.PostId)?.Wall == WallSide.East
                && lookup.Get<UnitDef>(s.UnitDefId)?.Kind == UnitKind.Archer);
            Assert.IsNotNull(n);
            Assert.IsNotNull(e);
            foreach (SquadState squad in setup.Squads) squad.Count = squad == n ? north : squad == e ? east : 0;
            return new BattleSim(setup, lookup);
        }

        static int Room(BattleSim sim, int side) => (int)typeof(BattleSim).GetMethod("WallRoom", Private).Invoke(sim, new object[] { side });

        static void Spaced(BattleSim sim, int side, int expected)
        {
            var positions = (NativeArray<float3>)typeof(BattleSim).GetField("sPos", Private).GetValue(sim);
            var points = new List<float3>();
            for (int i = 0; i < sim.SoldierCount; i++)
                if (sim.SoldierRow(i, out int wall, out _) >= 0 && wall == side) points.Add(positions[i]);
            Assert.AreEqual(expected, points.Count, "받은 병사 모두 자리 하나씩");
            float minimum = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
                for (int j = i + 1; j < points.Count; j++) minimum = math.min(minimum, math.distance(points[i], points[j]));
            Assert.GreaterOrEqual(minimum, 0.24f, "같은 자리 중첩 없이 최소 간격 유지");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void FirstGroup_SpawnsNearWallWithinTwoSeconds_EnemyPresentAtFive(int battle)
        {
            DefinitionLookup lookup = LoadAll();
            using (var sim = new BattleSim(Setup(lookup, battle), lookup))
            {
                Array entries = (Array)typeof(BattleSim).GetField("spawnOrder", Private).GetValue(sim);
                var groups = (NativeArray<int>)typeof(BattleSim).GetField("eGroup", Private).GetValue(sim);
                int first = 0;
                foreach (object entry in entries)
                {
                    Type type = entry.GetType();
                    int slot = (int)type.GetField("Slot").GetValue(entry);
                    float depth = (float)type.GetField("Depth").GetValue(entry);
                    float time = (float)type.GetField("Time").GetValue(entry);
                    if (sim.IsVanguard(slot))
                    {   // 나안: 2초 안에 가까이 나오는 것은 첫 무리에서 뗀 선봉뿐 (나머지 첫 무리는 본대로 멀리서 흘러온다)
                        first++;
                        Assert.That(time, Is.InRange(0f, 2f));
                        Assert.That(depth - sim.CastleHalf, Is.InRange(60f, 70f));
                    }
                    else
                    {
                        Assert.That(depth, Is.InRange(sim.FieldSize - 40f, sim.FieldSize), "나중 무리의 들판 띠 유지");
                        Assert.GreaterOrEqual(time, sim.AttackPlan[groups[slot]].SpawnAt);
                    }
                }
                Assert.Greater(first, 0);
                Assert.AreEqual(sim.VanguardCount, first, "선봉 수만큼만 가까이 나온다");
                for (int step = 1; step <= 100; step++)
                {
                    sim.BeginFrame(0.05f);
                    sim.Schedule(0.05f, step * 0.05f);
                    sim.Complete();
                }
                bool near = false;
                foreach (var enemy in sim.EnemyInstances)
                {
                    if (enemy.TintSize.w <= 0f) continue;
                    float3 position = enemy.PosFrame.xyz;
                    float dx = math.max(0f, math.abs(position.x) - sim.CastleHalf);
                    float dz = math.max(0f, math.abs(position.z) - sim.CastleHalf);
                    if (math.length(new float2(dx, dz)) <= 75f) near = true;
                }
                Assert.IsTrue(near, "시뮬레이션 5초 뒤 살아 있는 적이 성벽 75m 안에 있다");
            }
        }

        [Test]
        public void FullWall_SpreadsBeyondOriginalSlots_WithoutStacking()
        {
            int slots = 0;
            using (BattleSim baseline = Soldiers(10, 10))
                for (int row = 0; row < 5; row++) slots += baseline.SlotCount(0, row);
            using (BattleSim sim = Soldiers(slots + 100, 10)) Spaced(sim, 0, slots + 100);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MinimumSpacing_OnlyRoomMoves_OverflowStaysOnSource(bool instant)
        {
            int capacity;
            using (BattleSim baseline = Soldiers(10, 10)) capacity = Room(baseline, 1) + 10;
            using (BattleSim sim = Soldiers(10, capacity - 3))
            {
                int group = -1;
                for (int g = 0; g < sim.GroupCount; g++) if (sim.GetGroup(g).Side == 0) { group = g; break; }
                var notices = new List<string>();
                sim.Notice += notices.Add;
                Assert.IsTrue(sim.MoveGroup(group, 1, instant));
                Assert.AreEqual(3, sim.GetGroup(group).Alive, "들어갈 수 있는 세 명만 출발");
                Assert.AreEqual(7, sim.Sides[0].GarrisonAlive, "남은 일곱 명은 원래 성벽에 남음");
                Assert.AreEqual(0, Room(sim, 1), "달리는 동안도 도착 자리를 예약");
                int stayed = sim.GroupCount - 1;
                Assert.AreEqual(0, sim.GetGroup(stayed).Side);
                Assert.IsFalse(sim.MoveGroup(stayed, 1, instant));
                if (!instant)
                    for (int step = 0; step < 4000 && sim.GetGroup(group).Moving; step++)
                        typeof(BattleSim).GetMethod("UpdateGroupMoves", Private).Invoke(sim, new object[] { 0.05f });
                Assert.IsFalse(sim.GetGroup(group).Moving);
                Assert.AreEqual(capacity, sim.Sides[1].GarrisonAlive);
                Assert.IsTrue(notices.Contains("동쪽 성벽 가득 참 — 7명 남음"));
                Spaced(sim, 0, 7);
                Spaced(sim, 1, capacity);
            }
        }

        [Test]
        public void WallCommand_ReportsRefusedCount_AndKeepsMinimumGarrison()
        {
            int capacity;
            using (BattleSim baseline = Soldiers(10, 10)) capacity = Room(baseline, 1) + 10;
            using (BattleSim sim = Soldiers(50, capacity - 3))
            {
                var notices = new List<string>();
                sim.Notice += notices.Add;
                Assert.AreEqual(3, sim.MoveWallGroups(0, 1, true));
                Assert.AreEqual(47, sim.Sides[0].GarrisonAlive);
                StringAssert.Contains("성벽 가득 참", notices[notices.Count - 1]);
                Spaced(sim, 1, capacity);
            }
        }
    }
}
