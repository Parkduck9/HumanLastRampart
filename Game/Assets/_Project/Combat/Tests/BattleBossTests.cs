using System;
using System.Collections.Generic;
using System.Text;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Traps;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 우두머리 성벽손 오우거 (Docs/GDD/Boss_WallhandOgre_v1.md): 점화의 밤에만, 빛 밖 화살·총은 정의 비율, 빛 안 100%·걷기 ×1.2,
    // 사다리 없이 오르기, 정의 비율만큼 잃으면 떨어짐 (값은 모두 EnemyDef 데이터), 함정 쓰러짐, 가장 약한 성벽 자리, 통로 위 1순위·성벽 피해, 같은 씨앗 = 같은 결과
    public sealed class BattleBossTests
    {
        const float Dt = 0.05f;
        bool terrainWas;

        [SetUp]
        public void DisableTerrain()
        {
            terrainWas = BattleSim.TerrainEnabled;
            BattleSim.TerrainEnabled = false;   // 무작위 막힘이 걷기 측정에 끼지 않게
        }

        [TearDown]
        public void RestoreTerrain() => BattleSim.TerrainEnabled = terrainWas;

        static DefinitionLookup Lookup()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            return DefinitionLookup.Build(catalogs);
        }

        // 일반 적은 무리마다 1마리뿐(배율 0.001)이라 우두머리만 눈에 띈다. soldiers = false면 병사도 없다 (우두머리 규칙만 보기)
        static BattleSim NewSim(string run = "run_boss_a", bool ignition = true, int fortress = 1, Difficulty difficulty = Difficulty.Easy, int tier = 0,
            int beamSide = 0, bool soldiers = false, int slot = 1, LampBrightness lamp = LampBrightness.Normal, Action<BattleSetup> tweak = null)
        {
            DefinitionLookup lookup = Lookup();
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(difficulty), 11);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, run, out string error);
            Assert.IsNull(error);
            setup.IsIgnitionNight = ignition;
            setup.FortressNumber = fortress;
            setup.EnemyTier = tier;
            setup.BeamSide = beamSide;
            setup.LampBrightness = lamp;
            setup.LampCountMultiplier = 1f;
            setup.BattleNumber = slot;
            setup.Difficulty = difficulty;
            setup.EnemyCountMultiplier = 0.001f;
            if (!soldiers) { setup.Squads.Clear(); setup.Mercenaries.Clear(); }
            tweak?.Invoke(setup);
            return new BattleSim(setup, lookup);
        }

        static void Step(BattleSim sim, int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                sim.BeginFrame(Dt);
                if (sim.Ended) return;
                sim.Schedule(Dt, sim.Elapsed);
                sim.Complete();
            }
        }

        // 시간만 앞으로 (작업 없이 등장 처리만) — 80초 등장까지 빠르게
        static void FastForward(BattleSim sim, float to)
        {
            while (sim.Elapsed < to && !sim.Ended) sim.BeginFrame(Dt);
        }

        static BattleSim SpawnedBoss(string run = "run_boss_a", int fortress = 1, Difficulty difficulty = Difficulty.Easy, int beamSide = -1, bool soldiers = false, LampBrightness lamp = LampBrightness.Normal, int tier = 0)
        {
            if (beamSide < 0)
            {
                int side;   // 기본: 우두머리가 오는 면의 반대쪽을 비추어 빛 밖에서 시험한다
                using (BattleSim probe = NewSim(run, true, fortress, difficulty)) side = probe.BossSide;
                beamSide = (side + 2) % 4;
            }

            BattleSim sim = NewSim(run, true, fortress, difficulty, tier, beamSide, soldiers, 1, lamp);
            FastForward(sim, BattleSim.BossSpawnSeconds + 0.1f);
            Assert.IsTrue(sim.BossSpawned, "80초에 우두머리가 나온다");
            return sim;
        }

        // 우두머리 정의 (요새 1·2). 값은 데이터라 시험이 숫자를 따로 갖지 않는다
        static EnemyDef Def(int fortress) => Lookup().Get<EnemyDef>(fortress >= 2 ? BattleSim.BossDefIdFortress2 : BattleSim.BossDefId);

        static float3 Out(BattleSim sim) => WallGeometry.Outward(sim.BossSide);
        static float3 Right(BattleSim sim) => WallGeometry.Right(sim.BossSide);

        // 우두머리를 성벽 앞 땅(바깥 distance m)·성벽 따라 along 자리에 놓는다
        static void PlaceBoss(BattleSim sim, float distanceFromWall, float along)
        {
            float3 p = Out(sim) * (sim.CastleHalf + distanceFromWall) + Right(sim) * along;
            sim.DebugPlaceEnemy(sim.BossSlot, p);
        }

        // ── 언제·어디서 ──

        [Test]
        public void OnlyOnIgnitionNight_OneBoss_NoTierBonus()
        {
            int baseCount;
            using (var sim = NewSim("run_boss_a", ignition: false)) { baseCount = sim.TotalEnemies; Assert.IsFalse(sim.BossPresent, "점화의 밤이 아니면 우두머리 없음"); }
            using (var sim = NewSim("run_boss_a", ignition: true))
            {
                Assert.IsTrue(sim.BossPresent);
                Assert.AreEqual(baseCount + 1, sim.TotalEnemies, "정확히 한 마리 더");
                Assert.IsTrue(sim.EnemyGroups[sim.EnemyGroups.Count - 1].def.IsBoss, "맨 끝 무리");
                EnemyDef d1 = Def(1);
                Assert.AreEqual(Mathf.RoundToInt(d1.MaxHp * d1.EasyHpMul), sim.BossMaxHp, "쉬움: 정의 체력 × 쉬움 배율 (값은 데이터)");
                Assert.Less(sim.BossMaxHp, d1.MaxHp, "쉬움이 더 쉽다");
            }

            using (var sim = NewSim("run_boss_a", fortress: 1, difficulty: Difficulty.Realistic)) Assert.AreEqual(Def(1).MaxHp, sim.BossMaxHp, "현실: 그대로");
            using (var sim = NewSim("run_boss_a", fortress: 2, difficulty: Difficulty.Realistic)) Assert.AreEqual(Def(2).MaxHp, sim.BossMaxHp, "요새 2 정의 체력");
            using (var sim = NewSim("run_boss_a", fortress: 2, difficulty: Difficulty.Easy)) Assert.AreEqual(Mathf.RoundToInt(Def(2).MaxHp * Def(2).EasyHpMul), sim.BossMaxHp, "요새 2 쉬움");
            // 변형 단계(요새 2 점화의 밤 = 2단계) 체력 보너스는 우두머리에 안 붙는다
            using (var sim = NewSim("run_boss_a", fortress: 2, difficulty: Difficulty.Realistic, tier: 2))
            {
                Assert.AreEqual(Def(2).MaxHp, sim.BossMaxHp, "변형 단계 체력 보너스 없음");
                Assert.AreEqual(0, sim.TierOf(sim.BossSlot));
                Assert.AreEqual(Def(2).MoveSpeed, sim.EnemySpeedOf(sim.BossSlot), 1e-4f, "속도 보너스·흔들림 없음");
            }
        }

        [Test]
        public void Spawn_At80s_OnSeededAttackSide_WarnedFiveSecondsBefore()
        {
            using (var sim = NewSim("run_boss_a", slot: 4))
            {
                var planned = new HashSet<int>();
                for (int g = 0; g < sim.AttackPlan.Count - 1; g++) planned.Add(sim.AttackPlan[g].Side);   // 맨 끝은 우두머리 무리
                Assert.IsTrue(planned.Contains(sim.BossSide), "그날 밤 적이 오는 면 중 하나");
                Assert.AreEqual(sim.BossSide, sim.AttackPlan[sim.AttackPlan.Count - 1].Side);
                Assert.IsFalse(sim.AttackPlan[sim.AttackPlan.Count - 1].Flank, "우회 발견·전령 대상이 아니다");

                FastForward(sim, 70f);
                Assert.IsFalse(sim.TryGetBossWarning(out _, out _), "5초보다 이르면 예고 없음");
                FastForward(sim, 76f);
                Assert.IsTrue(sim.TryGetBossWarning(out int side, out float left), "나오기 5초 전부터 예고");
                Assert.AreEqual(sim.BossSide, side);
                Assert.LessOrEqual(left, 5f);
                Assert.IsFalse(sim.BossSpawned);
                FastForward(sim, BattleSim.BossSpawnSeconds - 0.1f);
                Assert.IsFalse(sim.BossSpawned, "80초 전에는 없다");
                WavePhase phaseBefore = sim.CurrentPhase;
                FastForward(sim, BattleSim.BossSpawnSeconds + 0.1f);
                Assert.IsTrue(sim.BossSpawned);
                Assert.IsFalse(sim.TryGetBossWarning(out _, out _), "나온 뒤에는 예고 끝");
                Assert.AreEqual(BattleSim.BossSpawnSeconds, sim.BossAppearedAt, 0.1f);
                Assert.AreEqual(EnemyState.Walking, sim.BossState);
                Assert.AreEqual(phaseBefore, sim.CurrentPhase, "우두머리가 나와도 단계(공성·최후 돌격)가 바뀌지 않는다");
                Vector3 p = sim.BossPosition;
                float3 pos = new float3(p.x, p.y, p.z);
                Assert.AreEqual(sim.BossTargetAlong, math.dot(pos, WallGeometry.Right(sim.BossSide)), 0.01f, "그 자리 앞에서 나온다");
                Assert.AreEqual(sim.CastleHalf + BattleSim.BossSpawnReach, math.dot(pos, WallGeometry.Outward(sim.BossSide)), 0.01f);
            }
        }

        [Test]
        public void Side_IsSeeded_SameRunSameSide_DifferentRunsDiffer()
        {
            int a, b;
            using (var sim = NewSim("run_boss_x", slot: 4)) a = sim.BossSide;
            using (var sim = NewSim("run_boss_x", slot: 4)) b = sim.BossSide;
            Assert.AreEqual(a, b, "같은 전투 씨앗 = 같은 면");
            var seen = new HashSet<int>();
            for (int i = 0; i < 24; i++) using (var sim = NewSim("run_boss_" + i, slot: 4)) seen.Add(sim.BossSide);
            Assert.GreaterOrEqual(seen.Count, 2, "전투 씨앗이 바뀌면 면도 바뀐다 (공격 면이 둘 이상일 때)");
        }

        [Test]
        public void TargetSpot_IsWeakestSpotOfItsSide_TheBreachTile()
        {
            using (var sim = NewSim("run_boss_a", slot: 4))
            {
                for (int side = 0; side < 4; side++)
                {
                    float from = BattleWallLayout.From(sim.CastleHalf), to = BattleWallLayout.To(sim.CastleHalf);
                    int tiles = WallDamageRules.TileCount(from, to);
                    int breach = WallDamageRules.BreachTile(sim.BattleSeed, side, tiles);
                    Assert.AreEqual(WallDamageRules.TileCenter(from, to, breach), sim.BossSpotAlong(side), 1e-4f, "성벽 부서짐 파공 칸 = 가장 먼저 뚫리는 자리");
                    Assert.Greater(breach, 0, "양 끝 칸은 피한다");
                    Assert.Less(breach, tiles - 1);
                }

                Assert.AreEqual(sim.BossSpotAlong(sim.BossSide), sim.BossTargetAlong, 1e-4f);
                Vector3 target = sim.BossTargetPoint;
                Assert.AreEqual(BattleSim.WallHeight, target.y, 1e-4f, "지도 표식은 통로 높이");
            }
        }

        // ── 피해 규칙 ──

        // 우두머리를 pos에 놓고 damage 한 번(area면 범위)을 맞힌 뒤 잃은 체력
        static int HpLost(BattleSim sim, float3 pos, int damage, bool area, byte state = EnemyState.Walking)
        {
            sim.DebugPlaceEnemy(sim.BossSlot, pos);
            sim.DebugSetState(sim.BossSlot, state);
            int before = sim.BossHp;
            if (area) sim.DebugHitArea(sim.BossSlot, damage); else sim.DebugHit(sim.BossSlot, damage);
            Step(sim);
            return before - sim.BossHp;
        }

        [Test]
        public void UnlitPointDamage_UsesDefinitionRatio_LitIsFullPlusBeam_AreaIsFull()
        {
            // 같은 자리(그 면 정면 들판 110m)를 빛줄기가 비출 때와 비추지 않을 때
            int unlitPoint = Mathf.RoundToInt(100 * Def(1).UnlitPointDamageMul);
            using (BattleSim sim = SpawnedBoss())
            {
                var pos = Out(sim) * 110f;
                Assert.AreEqual(unlitPoint, HpLost(sim, pos, 100, false), "빛 밖 화살·총은 정의 비율 (값은 데이터)");
                Assert.IsFalse(sim.BossLit);
                Assert.AreEqual(100, HpLost(sim, pos, 100, true), "빛 밖 범위 피해는 그대로");
            }

            int side;
            using (BattleSim probe = SpawnedBoss()) side = probe.BossSide;
            using (BattleSim sim = SpawnedBoss(beamSide: side))
            {
                var pos = Out(sim) * 110f;
                int lit = Mathf.RoundToInt(100 * (1f + BattleSim.BeamDamageBonus[(int)LampBrightness.Normal]));   // 빛줄기 보통 추가 피해
                Assert.AreEqual(lit, HpLost(sim, pos, 100, false), "빛 안: 100% + 빛줄기 보통 추가 피해");
                Assert.IsTrue(sim.BossLit);
                Assert.AreEqual(lit, HpLost(sim, pos, 100, true), "빛 안 범위 피해도 빛줄기 추가 피해");
            }
        }

        [Test]
        public void OnWalkway_AlwaysTakesFullDamage()
        {
            using (BattleSim sim = SpawnedBoss())
            {
                // 동쪽 통로 자리 = 북쪽 빛줄기 밖
                var east = new float3(sim.CastleHalf - 0.4f, BattleSim.WallHeight, 0f);
                Assert.AreEqual(100, HpLost(sim, east, 100, false, EnemyState.OnWall), "통로 위: 빛 밖이어도 화살·총 100%");
                Assert.IsFalse(sim.BossLit);
                Assert.AreEqual(100, HpLost(sim, east, 100, true, EnemyState.OnWall), "통로 위 범위 피해는 배수 없음");
            }
        }

        [Test]
        public void ClimbingTakesDoubleAreaDamage_PointStillUnlitRatio()
        {
            using (BattleSim sim = SpawnedBoss())
            {
                var dark = new float3(60.6f, 1f, 0f);
                Assert.AreEqual(Mathf.RoundToInt(100 * Def(1).ClimbAreaDamageMul), HpLost(sim, dark, 100, true, EnemyState.Climbing), "오르는 동안 범위 피해 정의 배율");
                Assert.AreEqual(Mathf.RoundToInt(100 * Def(1).UnlitPointDamageMul), HpLost(sim, dark, 100, false, EnemyState.Climbing), "오르는 동안에도 빛 밖 화살은 정의 비율");
            }
        }

        // ── 걷기 ──

        // 우두머리를 성벽 중심선에서 distance 앞 정면에 놓고 1초 걸은 거리
        static float WalkedInOneSecond(BattleSim sim)
        {
            var start = Out(sim) * 120f;
            sim.DebugPlaceEnemy(sim.BossSlot, start);
            sim.DebugSetState(sim.BossSlot, EnemyState.Walking);
            Step(sim, 20);
            Vector3 p = sim.BossPosition;
            return 120f - math.dot(new float3(p.x, p.y, p.z), Out(sim));
        }

        [Test]
        public void LitWalksTwentyPercentFaster_AndIgnoresBeamSlow()
        {
            int side;
            using (BattleSim probe = SpawnedBoss()) side = probe.BossSide;
            float unlit, litWalk;
            using (BattleSim sim = SpawnedBoss(beamSide: (side + 2) % 4)) unlit = WalkedInOneSecond(sim);
            using (BattleSim sim = SpawnedBoss(beamSide: side)) { litWalk = WalkedInOneSecond(sim); Assert.IsTrue(sim.BossLit, "정면 빛줄기 안"); }
            EnemyDef def = Def(1);
            Assert.AreEqual(def.MoveSpeed, unlit, 0.04f, "빛 밖은 정의 속도");
            Assert.AreEqual(def.MoveSpeed * def.LitSpeedMul, litWalk, 0.05f, "빛 안 ×1.2 (보통 빛줄기 느려짐 −25%는 받지 않는다)");
            // 비교: 같은 빛줄기 안의 일반 적은 느려진다
            Assert.Greater(litWalk, unlit * 1.15f);
        }

        // ── 기어오르기 ──

        [Test]
        public void ReachingWall_ClimbsDirectlyAtHalfMeterPerSecond_ThenHoldsWalkway()
        {
            using (BattleSim sim = SpawnedBoss())
            {
                PlaceBoss(sim, 0.8f, sim.BossTargetAlong);
                Step(sim, 6);
                Assert.AreEqual(EnemyState.Climbing, sim.BossState, "성벽에 닿으면 사다리 없이 바로 오른다");
                Assert.IsFalse(sim.EnemyGroups[sim.EnemyGroups.Count - 1].def.CarriesLadder);
                float y0 = sim.BossPosition.y;
                Step(sim, 20);   // 1초
                Assert.AreEqual(0.5f, sim.BossPosition.y - y0, 0.06f, "초속 0.5m");
                Step(sim, 20 * 8);
                Assert.AreEqual(EnemyState.OnWall, sim.BossState, "4m를 8초 안팎에 다 오른다");
                Assert.AreEqual(BattleSim.WallHeight, sim.BossPosition.y, 1e-3f);
                Assert.IsTrue(sim.BossReachedWalkway);
                Assert.AreEqual(1, sim.Sides[sim.BossSide].OnWall);
                Step(sim, 100);
                Assert.AreEqual(EnemyState.OnWall, sim.BossState, "죽을 때까지 내려가지 않는다");
            }
        }

        [Test]
        public void NaturalApproach_WalksToTheSpot_ClimbsWithoutLadder()
        {
            using (BattleSim sim = SpawnedBoss())
            {
                int guard = 0;
                while (sim.BossState == EnemyState.Walking && guard++ < 4000) Step(sim);
                Assert.AreEqual(EnemyState.Climbing, sim.BossState);
                float travel = sim.Elapsed - BattleSim.BossSpawnSeconds;
                Assert.AreEqual((BattleSim.BossSpawnReach - 0.6f) / Def(1).MoveSpeed, travel, 1.5f, "85m를 정의 속도로");
                Vector3 p = sim.BossPosition;
                float lateral = math.dot(new float3(p.x, p.y, p.z), Right(sim));
                Assert.AreEqual(sim.BossTargetAlong, lateral, 0.5f, "가장 약한 자리 앞에서 오른다");
            }
        }

        [Test]
        public void KnockOff_AfterFifteenPercentInOneClimb_ThenWalksBackAndClimbsAgain()
        {
            using (BattleSim sim = SpawnedBoss())
            {
                var messages = new List<string>();
                sim.Notice += messages.Add;
                int max = sim.BossMaxHp;
                float limit = max * Def(1).KnockOffRatio;   // 한 번 오르는 동안의 한계 (체력 15%)
                int u = Mathf.RoundToInt(limit / 4.2f);     // 범위 피해 한 번 (등반 중 ×2). 두 번 = 한계의 0.95, 세 번 = 1.43
                int walkHit = Mathf.RoundToInt(limit * 2.4f);
                // 걸을 때 잃은 체력은 등반 몫이 아니다
                sim.DebugHitArea(sim.BossSlot, walkHit);
                Step(sim);
                PlaceBoss(sim, 0.7f, sim.BossTargetAlong);
                Step(sim, 6);
                Assert.AreEqual(EnemyState.Climbing, sim.BossState);
                Step(sim, 5);
                Assert.AreEqual(0, sim.BossFalls, "오르기 전에 잃은 체력은 세지 않는다");

                sim.DebugHitArea(sim.BossSlot, u); sim.DebugHitArea(sim.BossSlot, u);   // 범위 ×2 = 한계 아래
                Step(sim, 3);
                Assert.AreEqual(EnemyState.Climbing, sim.BossState, "한계(15%) 아래는 버틴다");
                Assert.AreEqual(0, sim.BossFalls);
                sim.DebugHitArea(sim.BossSlot, u);   // 누적 한계 이상
                Step(sim, 3);
                Assert.AreEqual(1, sim.BossFalls, "15% 이상 잃으면 손을 놓는다");
                Assert.IsTrue(messages.Contains("손을 놓쳤다!"));
                Assert.AreEqual(EnemyState.Walking, sim.BossState, "땅으로 떨어져 걷는 상태");
                Assert.AreEqual(0f, sim.BossPosition.y, 1e-4f);
                Assert.Greater(sim.BossDownLeft, 5.5f, "6초 쓰러짐");
                Assert.LessOrEqual(sim.BossDownLeft, 6f);
                Assert.Less(sim.BossHp, max - walkHit);

                Vector3 down = sim.BossPosition;
                Step(sim, 100);   // 5초
                Assert.AreEqual(down.x, sim.BossPosition.x, 1e-3f, "쓰러져 있는 동안 제자리");
                Assert.AreEqual(down.z, sim.BossPosition.z, 1e-3f);
                Assert.AreEqual(EnemyState.Walking, sim.BossState);
                Step(sim, 60);   // 쓰러짐이 끝나고(약 0.9초) 걸어서 성벽에 닿는다(약 1.2초)
                Assert.AreEqual(EnemyState.Climbing, sim.BossState, "다시 걸어와 오른다");
                Assert.Less(sim.BossPosition.y, 1f, "처음부터 다시");

                // 새 등반은 새로 센다: 한계 아래를 맞아도 버틴다
                sim.DebugHitArea(sim.BossSlot, u); sim.DebugHitArea(sim.BossSlot, u);
                Step(sim, 4);
                Assert.AreEqual(1, sim.BossFalls, "이전 등반에서 잃은 체력을 이어 세지 않는다");
                Assert.AreEqual(EnemyState.Climbing, sim.BossState);
                sim.DebugHitArea(sim.BossSlot, u);
                Step(sim, 3);
                Assert.AreEqual(2, sim.BossFalls, "한 번 더 떨어뜨릴 수 있다");
            }
        }

        // ── 함정 ──

        [Test]
        public void TrapBlast_InRange_KnocksDownFourSeconds_OutOfRangeDoesNot()
        {
            using (BattleSim sim = SpawnedBoss())
            {
                sim.DebugPlaceEnemy(sim.BossSlot, Out(sim) * 100f);
                Vector3 at = sim.BossPosition;
                sim.DebugTrapBlast(new Vector3(at.x, 0f, at.z) + new Vector3(0f, 0f, 30f), 5f);
                Assert.AreEqual(0f, sim.BossDownLeft, "범위 밖은 쓰러지지 않는다");
                sim.DebugTrapBlast(new Vector3(at.x + 2f, 0f, at.z), 5f);
                Assert.AreEqual(4f, sim.BossDownLeft, 1e-4f, "범위 안 4초");
                Step(sim, 20 * 3);
                Vector3 now = sim.BossPosition;
                Assert.AreEqual(at.x, now.x, 1e-3f, "쓰러진 동안 제자리");
                Assert.AreEqual(at.z, now.z, 1e-3f);
                Step(sim, 20 * 2);
                Assert.Less(math.dot(new float3(sim.BossPosition.x, 0f, sim.BossPosition.z), Out(sim)), math.dot(new float3(at.x, 0f, at.z), Out(sim)) - 0.3f, "일어나 다시 걷는다");
            }
        }

        [Test]
        public void RealTrapExplosion_KnocksDownBoss()
        {
            DefinitionLookup lookup = Lookup();
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            TerrainDef terrain = lookup.Get<TerrainDef>("terrain_1");
            Vector2 c = TrapGrid.Center(terrain, 0, TrapGrid.Columns(terrain) / 2, 12);
            campaign.Traps.Add(new TrapPlacement
            {
                TrapId = campaign.NewInstanceId("trap"), TrapDefId = "trap_bomb", TerrainId = "terrain_1",
                SlotId = TrapGrid.CellId(0, TrapGrid.Columns(terrain) / 2, 12), ActualX = c.x, ActualZ = c.y, Marked = true, MarkX = c.x, MarkZ = c.y,
            });
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_trap_test", out string error);
            Assert.IsNull(error);
            setup.IsIgnitionNight = true; setup.FortressNumber = 1; setup.EnemyCountMultiplier = 0.001f; setup.Squads.Clear(); setup.Mercenaries.Clear();
            using (var sim = new BattleSim(setup, lookup))
            {
                FastForward(sim, BattleSim.BossSpawnSeconds + 0.1f);
                sim.DebugPlaceEnemy(sim.BossSlot, new float3(c.x + 1f, 0f, c.y));
                Step(sim);
                Assert.IsTrue(sim.DetonateTrap(0));
                float fuse = 0f;
                while (sim.GetTrap(0).State != BattleSim.TrapState.Exploded && fuse < 10f) { Step(sim); fuse += Dt; }
                Assert.AreEqual(BattleSim.TrapState.Exploded, sim.GetTrap(0).State);
                Assert.Greater(sim.BossDownLeft, 3f, "폭발 범위 안에서 터지면 4초 쓰러짐 (터진 뒤 몇 틱 지남)");
            }
        }

        // ── 통로 위 ──

        [Test]
        public void OnWalkway_DamagesItsWallPerSecond_AndKillsGarrisonThroughExistingPath()
        {
            BattleSim sim = null;
            for (int i = 0; i < 40 && sim == null; i++)
            {
                BattleSim candidate = NewSim("run_boss_g" + i, soldiers: true, slot: 4);
                if (candidate.Sides[candidate.BossSide].GarrisonStart >= 10) sim = candidate; else candidate.Dispose();
            }

            Assert.IsNotNull(sim, "수비대가 선 면에 나오는 씨앗");
            using (sim)
            {
                FastForward(sim, BattleSim.BossSpawnSeconds + 0.1f);
                int side = sim.BossSide;
                // 통로 위에 바로 올려 놓고, 병사 사격에 죽지 않게 체력을 채워 가며 3초 (피해 경로만 본다)
                sim.DebugPlaceEnemy(sim.BossSlot, Out(sim) * (sim.CastleHalf - 0.4f) + Right(sim) * sim.BossTargetAlong + new float3(0f, BattleSim.WallHeight, 0f));
                sim.DebugSetState(sim.BossSlot, EnemyState.OnWall);
                Step(sim);
                int wall0 = sim.Sides[side].WallHp, alive0 = sim.Sides[side].GarrisonAlive;
                for (int i = 0; i < 60; i++) { sim.DebugSetHp(sim.BossSlot, sim.BossMaxHp); Step(sim); }   // 3초
                Assert.AreEqual(EnemyState.OnWall, sim.BossState);
                int wallLoss = wall0 - sim.Sides[side].WallHp;
                Assert.AreEqual(Def(1).OnWallWallDamage * 3, wallLoss, 12, "그 성벽 체력을 초당 정의값씩 3초 (일반 적 몇 마리 몫 포함)");
                Assert.Greater(alive0 - sim.Sides[side].GarrisonAlive, 5, "병사를 빠르게 쓰러뜨린다 (기존 병사 피해 경로)");
            }
        }

        [Test]
        public void OnWalkwayBoss_IsAlwaysFirstTargetOverOtherOnWallEnemies()
        {
            BattleSim sim = null;
            for (int i = 0; i < 40 && sim == null; i++)
            {
                BattleSim candidate = NewSim("run_boss_t" + i, soldiers: true, slot: 4);
                if (candidate.Sides[candidate.BossSide].GarrisonStart >= 20) sim = candidate; else candidate.Dispose();
            }

            Assert.IsNotNull(sim);
            using (sim)
            {
                FastForward(sim, BattleSim.BossSpawnSeconds + 0.1f);
                int side = sim.BossSide;
                int other = -1;
                for (int e = 0; e < sim.TotalEnemies && other < 0; e++) if (e != sim.BossSlot && sim.DebugAlive(e)) other = e;
                Assert.GreaterOrEqual(other, 0, "다른 적이 있어야 비교할 수 있다");
                float3 spot = Out(sim) * (sim.CastleHalf - 0.4f) + Right(sim) * sim.BossTargetAlong + new float3(0f, BattleSim.WallHeight, 0f);
                sim.DebugPlaceEnemy(sim.BossSlot, spot);
                sim.DebugSetState(sim.BossSlot, EnemyState.OnWall);
                sim.DebugPlaceEnemy(other, spot + Right(sim) * 3f);
                sim.DebugSetState(other, EnemyState.OnWall);
                int otherHp = sim.DebugEnemyHp(other), bossHp = sim.BossHp;
                for (int p = 0; p < 6; p++)   // 어떤 설정이어도
                {
                    sim.IssueDirect(side, (TargetPriority)p);
                    Step(sim, 10);
                }

                Assert.Less(sim.BossHp, bossHp, "통로 위 우두머리가 맞고 있다");
                Assert.AreEqual(otherHp, sim.DebugEnemyHp(other), "같은 통로 위 다른 적은 우두머리가 있는 동안 표적이 아니다");
            }
        }

        [Test]
        public void SpecialFirst_AimsAtBossBeforeAWoundedOgre()
        {
            BattleSim sim = null;
            int ogre = -1;
            for (int i = 0; i < 60 && sim == null; i++)
            {
                BattleSim candidate = NewSim("run_boss_sf" + i, soldiers: true, slot: 4, tweak: s => { s.EnemyCountMultiplier = 0.3f; s.WaveDefId = "wave_4"; });   // 4번 웨이브에 오우거 무리가 있다
                FastForward(candidate, BattleSim.BossSpawnSeconds + 0.1f);
                int found = -1;
                foreach (var g in candidate.EnemyGroups)
                    if (found < 0 && g.def.IsSpecial && !g.def.IsBoss && g.count > 0) found = g.start;   // 이 특수 적을 다쳐서 우두머리 옆에 세운다
                if (found >= 0 && candidate.Sides[candidate.BossSide].GarrisonStart >= 10) { sim = candidate; ogre = found; }
                else candidate.Dispose();
            }

            Assert.IsNotNull(sim, "다른 특수 적(오우거)이 있고 수비대가 선 면에 우두머리가 나오는 씨앗");
            using (sim)
            {
                int side = sim.BossSide;
                // 같은 거리·같은 각도 띠에 우두머리(체력 가득)와 크게 다친 다른 특수 적을 나란히 둔다
                float3 front = Out(sim) * (sim.CastleHalf + 25f);
                sim.DebugPlaceEnemy(sim.BossSlot, front + Right(sim) * sim.BossTargetAlong);
                sim.DebugSetState(sim.BossSlot, EnemyState.Walking);
                sim.DebugPlaceEnemy(ogre, front + Right(sim) * (sim.BossTargetAlong + 5f));
                sim.DebugSetState(ogre, EnemyState.Walking);
                const int woundedHp = 20;
                sim.DebugSetHp(ogre, woundedHp);
                int bossHp = sim.BossHp;
                sim.IssueDirect(side, TargetPriority.SpecialFirst);
                Step(sim, 10);
                Assert.Less(sim.BossHp, bossHp, "특수 적 먼저: 우두머리가 맞는다");
                Assert.AreEqual(woundedHp, sim.DebugEnemyHp(ogre), "체력 낮은 다른 특수 적보다 우두머리가 먼저다");
            }
        }

        [Test]
        public void ClimbStartHp_CountsDamageOfTheFirstClimbingTick()
        {
            using (BattleSim sim = SpawnedBoss())
            {
                int max = sim.BossMaxHp;
                float limit = max * Def(1).KnockOffRatio;
                PlaceBoss(sim, 0.6f, sim.BossTargetAlong);   // 성벽 바로 앞(멈춤 자리) — 이 틱에 오르기로 바뀐다
                sim.DebugHitArea(sim.BossSlot, Mathf.CeilToInt(limit) + 10);   // 바뀌는 틱에 한꺼번에 한계 넘게 맞는다 (오르는 중이라 ×배율)
                Step(sim, 3);
                Assert.AreEqual(1, sim.BossFalls, "오르기로 바뀐 틱에 맞은 피해도 그 등반 몫이다");
            }
        }

        // ── 같은 씨앗 = 같은 결과 ──

        static string Run(string run, int frames)
        {
            var log = new StringBuilder();
            using (BattleSim sim = NewSim(run, soldiers: true, slot: 3))
            {
                FastForward(sim, BattleSim.BossSpawnSeconds - 1f);
                for (int t = 0; t < frames; t++)
                {
                    if (t == 700 && sim.BossSpawned) sim.DebugHitArea(sim.BossSlot, 500);
                    Step(sim);
                    if (sim.Ended) break;
                    log.Append(t).Append(' ').Append(sim.BossState).Append(' ').Append(sim.BossHp).Append(' ').Append(sim.BossPosition.x.ToString("R")).Append(' ').Append(sim.BossPosition.z.ToString("R"))
                        .Append(' ').Append(sim.BossDownLeft.ToString("R")).Append(' ').Append(sim.BossFalls).Append(' ').Append(sim.Kills);
                    for (int s = 0; s < 4; s++) log.Append(' ').Append(sim.Sides[s].WallHp).Append('/').Append(sim.Sides[s].GarrisonAlive);
                    log.Append('\n');
                }
            }

            return log.ToString();
        }

        [Test]
        public void SameSeed_GivesSameBossFight()
        {
            string first = Run("run_boss_det", 1600);
            Assert.Greater(first.Length, 5000, "빈 기록이 아니다");
            Assert.AreEqual(first, Run("run_boss_det", 1600), "같은 씨앗·같은 입력 = 같은 결과");
        }

        // ── 단계 흐름을 깨지 않는다 ──

        [Test]
        public void BossIsNotCountedAsSiegeOrFinalAssault_AndKeepsItsOwnTime()
        {
            using (var sim = NewSim("run_boss_a", slot: 4))
            {
                int planned = sim.BossSide;
                // 나온 일반 적을 매 틱 모두 죽인다 — 남은 적이 20% 이하가 되어 최후 돌격이 일어나도 우두머리는 제 시각·제 면
                for (int guard = 0; guard < 4000 && sim.Elapsed < BattleSim.BossSpawnSeconds + 0.3f; guard++)
                {
                    for (int e = 0; e < sim.TotalEnemies; e++) if (e != sim.BossSlot && sim.DebugAlive(e)) sim.DebugHit(e, 100000);
                    Step(sim);
                }

                Assert.IsTrue(sim.BossSpawned, "최후 돌격이 있어도 80초에 나온다");
                Assert.AreEqual(BattleSim.BossSpawnSeconds, sim.BossAppearedAt, 0.2f);
                Assert.AreEqual(planned, sim.BossSide, "면이 바뀌지 않는다");
                Assert.AreNotEqual(WavePhase.Siege, sim.CurrentPhase, "우두머리(오우거 취급) 때문에 공성 단계가 되지 않는다");
            }
        }

        [Test]
        public void BossOnlyPending_DoesNotTriggerFinalAssault()
        {
            // 일반 적이 하나도 안 남아 있고 우두머리만 안 나왔을 때는 최후 돌격이 아니다
            using (var sim = NewSim("run_boss_a", slot: 1, tweak: s => { }))
            {
                for (int guard = 0; guard < 1500 && sim.Elapsed < 70f; guard++)
                {
                    for (int e = 0; e < sim.TotalEnemies; e++) if (e != sim.BossSlot && sim.DebugAlive(e)) sim.DebugHit(e, 100000);
                    Step(sim);
                }

                Assert.GreaterOrEqual(sim.Spawned, sim.TotalEnemies - 1, "일반 적은 모두 나왔고 우두머리만 남았다 (비교 조건)");
                Assert.IsFalse(sim.FinalAssaultStarted, "우두머리만 남았다고 최후 돌격이 시작되지 않는다");
                Assert.IsFalse(sim.BossSpawned);
            }
        }

        // 사진 시험이 쓰는 흐름: 등장 직전으로 건너뛰어 쌓인 일반 적이 한꺼번에 나온 뒤 매 틱 모두 쓰러뜨려도 멈추지 않는다
        [Test, Timeout(120000)]
        public void SkipToBossTime_ThenKillingAllNormals_KeepsRunning()
        {
            BattleSim.TerrainEnabled = true;   // 실제 전투처럼 무작위 지형(토벽·나무·바위)도 켠다
            using (BattleSim sim = NewSim("run_boss_skip", soldiers: true, tweak: s => s.EnemyCountMultiplier = 0.75f))
            {
                sim.DebugSkipTo(BattleSim.BossSpawnSeconds - 7f);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                int guard = 0;
                while (!sim.BossSpawned && guard++ < 600)
                {
                    for (int e = 0; e < sim.TotalEnemies; e++) if (e != sim.BossSlot && sim.DebugAlive(e)) sim.DebugHit(e, 1000000);
                    Step(sim);
                }

                Assert.IsTrue(sim.BossSpawned, $"등장했다 ({guard}틱, {watch.ElapsedMilliseconds}ms)");
                Assert.Less(watch.ElapsedMilliseconds, 60000, "틱이 멈추지 않는다");
            }
        }

        // ── 결과·망원경 ──

        [Test]
        public void Result_RecordsBossDefeatAndTime()
        {
            using (BattleSim sim = SpawnedBoss())
            {
                Step(sim, 20);
                sim.ForceEnd(BattleOutcome.Victory, DefeatReason.None, -1);
                BattleResult early = sim.BuildResult();
                Assert.IsTrue(early.BossPresent);
                Assert.IsFalse(early.BossDefeated);
            }

            using (BattleSim sim = SpawnedBoss())
            {
                var messages = new List<string>();
                sim.Notice += messages.Add;
                sim.DebugPlaceEnemy(sim.BossSlot, new float3(0f, 0f, 110f));
                sim.DebugHitArea(sim.BossSlot, sim.BossMaxHp * 2);   // 범위 피해라 빛 밖 비율이 없다
                Step(sim, 3);
                Assert.IsTrue(sim.BossKilled);
                Assert.IsFalse(sim.BossActive);
                sim.ForceEnd(BattleOutcome.Victory, DefeatReason.None, -1);
                BattleResult result = sim.BuildResult();
                Assert.IsTrue(result.BossPresent);
                Assert.IsTrue(result.BossDefeated);
                Assert.Greater(result.BossSeconds, 0f);
                Assert.IsNotNull(sim.BossEndLine);
                Assert.IsTrue(sim.BossEndLine.Contains("처치"));
                Assert.IsTrue(sim.Gold >= 200, "금화 200");
            }

            using (BattleSim sim = NewSim("run_boss_a", ignition: false))
            {
                sim.ForceEnd(BattleOutcome.Victory, DefeatReason.None, -1);
                Assert.IsFalse(sim.BuildResult().BossPresent, "점화의 밤이 아니면 기록 없음");
            }
        }

        [Test]
        public void Telescope_ShowsBossAsSpecialAndChief()
        {
            using (BattleSim sim = SpawnedBoss())
            {
                var at = new float3(0f, 0f, 90f);
                sim.DebugPlaceEnemy(sim.BossSlot, at);
                Step(sim);
                sim.LookThroughTelescope(new Vector3(0f, 0f, 90f));
                Step(sim);
                sim.RefreshSpottedGroups();
                bool found = false;
                foreach (BattleSim.GroupSummary g in sim.SpottedGroups) if (g.Boss) { found = true; Assert.AreEqual(1, g.Special, "특수 적 수에 포함"); }
                Assert.IsTrue(found, "망원경으로 우두머리 무리를 확인");
            }
        }
    }
}
