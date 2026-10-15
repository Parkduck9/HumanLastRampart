using System.Collections.Generic;
using HumanLastRampart.App.Data;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEditor;

namespace HumanLastRampart.Combat.Tests
{
    // 튜토리얼 연습 전투 (D-063, C-005 v1.5 초안): 연습이면 적이 성벽을 때려도 성벽 실제 체력이 그대로, 실전이면 줄어든다
    public sealed class BattlePracticeTests
    {
        const int MaxSteps = 12000;   // 0.05초 × 12,000 = 10분

        static DefinitionLookup LoadAll()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            return DefinitionLookup.Build(catalogs);
        }

        // 병사를 모두 빼서(인원 0 = 이번 전투에 서지 않음) 적이 살아서 성벽까지 오게 한다
        static BattleSetup NewSetup(DefinitionLookup lookup, bool practice)
        {
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_practice_test", out string error);
            Assert.IsNull(error);
            Assert.IsFalse(setup.IsPractice, "기본은 실전");
            foreach (SquadState squad in setup.Squads) squad.Count = 0;
            setup.IsPractice = practice;
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

        static int[] WallHps(BattleSim sim)
        {
            var hps = new int[4];
            for (int side = 0; side < 4; side++) hps[side] = sim.Sides[side].WallHp;
            return hps;
        }

        static bool AnyWallDamaged(BattleSim sim, int[] start)
        {
            for (int side = 0; side < 4; side++)
                if (sim.Sides[side].WallHp < start[side]) return true;
            return false;
        }

        // 실전에서 성벽이 처음 깎일 때까지 걸린 걸음 수
        static int StepsUntilWallDamaged(DefinitionLookup lookup)
        {
            using (var sim = new BattleSim(NewSetup(lookup, false), lookup))
            {
                int[] start = WallHps(sim);
                float time = 0f;
                for (int i = 0; i < MaxSteps && !sim.Ended; i++)
                {
                    Step(sim, ref time);
                    if (AnyWallDamaged(sim, start)) return i + 1;
                }

                return -1;
            }
        }

        [Test]
        public void RealBattle_WallTakesDamage()
        {
            DefinitionLookup lookup = LoadAll();
            Assert.Greater(StepsUntilWallDamaged(lookup), 0, "실전에서는 성벽 앞 적이 성벽 체력을 깎는다");
        }

        [Test]
        public void Practice_WallHpStays_WhileEnemiesAttack()
        {
            DefinitionLookup lookup = LoadAll();
            int damagedAt = StepsUntilWallDamaged(lookup);
            Assume.That(damagedAt > 0, "실전에서 성벽이 깎이지 않으면 비교할 수 없다");

            BattleSetup setup = NewSetup(lookup, true);
            using (var sim = new BattleSim(setup, lookup))
            {
                float time = 0f;
                int attackers = 0;
                // 실전에서 깎인 시점보다 넉넉히 더 돌린다
                for (int i = 0; i < damagedAt + 2000 && !sim.Ended; i++)
                {
                    Step(sim, ref time);
                    for (int side = 0; side < 4; side++) attackers = System.Math.Max(attackers, sim.Sides[side].AtWall);
                }

                Assert.Greater(attackers, 0, "적은 성벽 앞에서 공격한다 (동작은 그대로)");
                for (int side = 0; side < 4; side++)
                {
                    WallSegmentState wall = setup.Wall.Find(w => w.SegmentId == sim.Sides[side].WallId);
                    Assert.AreEqual(wall.Hp, sim.Sides[side].WallHp, "연습: 성벽 실제 체력 그대로 " + wall.SegmentId);
                }

                BattleResult result = sim.BuildResult();
                Assert.AreEqual(0, result.WallOutcomes.Count, "연습 결과에 성벽 피해가 없다");
                Assert.AreNotEqual(DefeatReason.WallDestroyed, result.DefeatReason, "연습에서 성벽이 무너지지 않는다");
            }
        }

        [Test]
        public void Router_PracticeDone_SkipsResult_ToFreshBattle()
        {
            var state = new FlowState { LastBattleWasPractice = true, LastBattleOutcome = BattleOutcome.Victory };
            FlowStep step = FlowRouter.Next(ScreenId.Battle, ScreenOutcome.Done, state);
            Assert.AreEqual(ScreenId.Battle, step.Screen, "결과 화면(결과 반영)으로 가지 않는다");
            Assert.IsTrue(step.PrepareBattle, "실전 준비 정보를 새로 만든다 (연습 표시가 새지 않음)");
            Assert.IsFalse(step.AdvanceBattle, "전투 번호를 올리지 않는다");
            Assert.IsFalse(step.QueueDefeatEnding);

            state.LastBattleOutcome = BattleOutcome.Defeat;
            step = FlowRouter.Next(ScreenId.Battle, ScreenOutcome.Done, state);
            Assert.IsFalse(step.QueueDefeatEnding, "연습 패배는 패배 엔딩이 아니다");

            step = FlowRouter.Next(ScreenId.Battle, ScreenOutcome.BackToMainMenu, state);
            Assert.AreEqual(ScreenId.MainMenu, step.Screen, "메인 복귀는 그대로");
        }
    }
}
