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
    // 대포 옮기기 (K-060): 성벽마다 빈 사격구, 다른 성벽 빈 사격구로 옮기기(바로·끌고), 빈 사격구가 없으면 거절, 성벽 병력·칠 수 있는 들판 갱신
    public sealed class BattleCannonMoveTests
    {
        static BattleSim NewSim()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_cannon_move_test", out string error);
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

        static int CannonsOn(BattleSim sim, int side)
        {
            int n = 0;
            for (int i = 0; i < sim.CannonCount; i++) if (sim.GetCannon(i).Side == side && !sim.GetCannon(i).Moving) n++;
            return n;
        }

        static int FirstCannonOn(BattleSim sim, int side)
        {
            for (int i = 0; i < sim.CannonCount; i++) if (sim.GetCannon(i).Side == side && !sim.GetCannon(i).Moving) return i;
            return -1;
        }

        [Test]
        public void EveryWall_HasSpareBays()
        {
            using (BattleSim sim = NewSim())
            {
                for (int side = 0; side < 4; side++)
                {
                    Assert.AreEqual(BattleSim.CannonBaysPerSide, sim.CannonBays(side).Count, $"성벽 {side}: 사격구 수");
                    Assert.AreEqual(BattleSim.CannonBaysPerSide - CannonsOn(sim, side), sim.FreeCannonBays(side), $"성벽 {side}: 빈 사격구 = 사격구 − 대포");
                }
            }
        }

        [Test]
        public void InstantMove_LandsInFreeBay_AndMovesGarrison()
        {
            using (BattleSim sim = NewSim())
            {
                int cannon = FirstCannonOn(sim, 0);
                Assert.GreaterOrEqual(cannon, 0, "북쪽 대포");
                int northStart = sim.Sides[0].GarrisonStart, eastStart = sim.Sides[1].GarrisonStart;
                int freeEast = sim.FreeCannonBays(1);
                Assert.IsTrue(sim.MoveCannon(cannon, 1, true));
                BattleSim.CannonInfo info = sim.GetCannon(cannon);
                Assert.AreEqual(1, info.Side, "동쪽 대포가 됨");
                Assert.IsFalse(info.Moving);
                float lateral = Vector3.Dot(info.Position, new Vector3(0f, 0f, -1f));   // 동쪽: 오른쪽 = −z
                Assert.IsTrue(new List<float>(sim.CannonBays(1)).Exists(b => Mathf.Abs(b - lateral) < 0.05f), "동쪽 사격구 자리");
                Assert.AreEqual(freeEast - 1, sim.FreeCannonBays(1), "동쪽 빈 사격구 하나 줄어듦");
                Assert.Less(sim.Sides[0].GarrisonStart, northStart, "북쪽 병력에서 빠짐");
                Assert.Greater(sim.Sides[1].GarrisonStart, eastStart, "동쪽 병력에 더해짐");
                Assert.AreNotEqual(0, info.Reach & (1 << 1), "동쪽 들판을 칠 수 있다");
            }
        }

        [Test]
        public void TimedMove_Hauls_ThenArrives()
        {
            using (BattleSim sim = NewSim())
            {
                int cannon = FirstCannonOn(sim, 0);
                Assert.IsTrue(sim.MoveCannon(cannon, 1, false));
                Assert.IsTrue(sim.GetCannon(cannon).Moving);
                float time = 0f;
                Step(sim, ref time);
                Assert.AreEqual(1, sim.CannonHauls.Count, "끄는 대포 그림 하나");
                for (int i = 0; i < 4000 && sim.GetCannon(cannon).Moving && !sim.Ended; i++) Step(sim, ref time);
                Assert.IsFalse(sim.GetCannon(cannon).Moving, "도착");
                Assert.AreEqual(1, sim.GetCannon(cannon).Side);
                Assert.AreEqual(0, sim.CannonHauls.Count);
            }
        }

        [Test]
        public void FullWall_RefusesMove()
        {
            using (BattleSim sim = NewSim())
            {
                int free = sim.FreeCannonBays(1);
                for (int k = 0; k < free; k++) Assert.IsTrue(sim.MoveCannon(FirstCannonOn(sim, 0), 1, true), $"{k + 1}번째는 빈 사격구로");
                Assert.AreEqual(0, sim.FreeCannonBays(1));
                int another = FirstCannonOn(sim, 2);
                Assert.GreaterOrEqual(another, 0);
                Assert.IsFalse(sim.MoveCannon(another, 1, true), "빈 사격구가 없으면 거절");
                Assert.AreEqual(2, sim.GetCannon(another).Side);
            }
        }
    }
}
