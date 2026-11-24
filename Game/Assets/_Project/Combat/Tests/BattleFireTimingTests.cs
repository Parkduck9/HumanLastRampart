using System;
using System.Collections.Generic;
using System.Reflection;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Combat.PerfLab;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;

namespace HumanLastRampart.Combat.Tests
{
    public sealed class BattleFireTimingTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        sealed class Fixture : IDisposable
        {
            public readonly BattleSim Sim;
            public readonly int Soldier;
            public readonly float Interval;

            public Fixture(bool heroWall, bool rain, float boost)
            {
                var catalogs = new List<DefinitionCatalog>();
                foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                    catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
                DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
                CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 17);
                BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "fire_timing_test", out string error);
                Assert.IsNull(error);
                setup.Boosts = new BattleBoosts { HeroWallIntervalMul = boost };
                Sim = new BattleSim(setup, lookup);
                try
                {
                    Soldier = Sim.FirstSoldierOf("unit_gunner", false);
                    Assert.GreaterOrEqual(Soldier, 0);
                    var sides = Field<NativeArray<byte>>("sSide");
                    Sim.SetCommander(heroWall ? sides[Soldier] : (sides[Soldier] + 1) % 4, 0f);
                    typeof(BattleSim).GetProperty("CurrentWeather").SetValue(Sim, rain ? Weather.Rain : Weather.Clear);
                    float weather = (float)typeof(BattleSim).GetProperty("IntervalMultiplier", Private).GetValue(Sim);
                    // 실제 발사 Job의 간격 계산을 기준으로 그림만 대조한다.
                    var job = new SoldierFireJob
                    {
                        SoldierSide = sides, HeroSide = heroWall ? sides[Soldier] : -1,
                        IntervalMultiplier = weather, HeroSideIntervalMultiplier = boost
                    };
                    float multiplier = (float)typeof(SoldierFireJob).GetMethod("IntervalFor", Private)
                        .Invoke(job, new object[] { Soldier });
                    Interval = Sim.WeaponOfSoldier(Soldier).Interval * multiplier;
                    Field<byte[]>("sFireFrames")[Soldier] = 6;
                    var alive = Field<NativeArray<byte>>("sAlive"); alive[Soldier] = 1;
                    var idle = Field<NativeArray<byte>>("sIdle"); idle[Soldier] = 0;
                    var lateral = Field<NativeArray<float>>("sFireLateral"); lateral[Soldier] = 0f;
                }
                catch { Sim.Dispose(); throw; }
            }

            T Field<T>(string name) => (T)typeof(BattleSim).GetField(name, Private).GetValue(Sim);

            public float Pose(float age, float cooldown)
            {
                var fired = Field<NativeArray<float>>("sFireTime"); fired[Soldier] = Sim.Elapsed - age;
                var remaining = Field<NativeArray<float>>("sCooldown"); remaining[Soldier] = cooldown;
                object[] args = { Soldier, 0f };
                Assert.IsTrue((bool)typeof(BattleSim).GetMethod("SoldierPose", Private).Invoke(Sim, args));
                return (float)args[1];
            }

            public void Dispose() => Sim.Dispose();
        }

        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(false, true)]
        public void ReleaseFramesFollowActualFireInterval(bool heroWall, bool rain)
        {
            using (var f = new Fixture(heroWall, rain, 0.1f))
            {
                float fps = Math.Max(8f, 6f / Math.Max(f.Interval, 0.05f));
                Assert.That(f.Pose(2.2f / fps, f.Interval), Is.EqualTo(PerfLabArt.FireFrame0 + 5),
                    "실제 발사 주기의 놓기→재장전 2칸 진행과 같아야 한다");
            }
        }

        [TestCase(true, false, 0.5f)]
        [TestCase(true, true, 0.5f)]
        [TestCase(false, false, 0.5f)]
        [TestCase(false, true, 0.5f)]
        [TestCase(true, false, 0.9f)]
        [TestCase(true, true, 0.9f)]
        public void ReloadStopsAtActualIntervalWhenNotPreparingNextShot(bool heroWall, bool rain, float boost)
        {
            using (var f = new Fixture(heroWall, rain, boost))
                Assert.That(f.Pose(f.Interval + 0.05f, 10f), Is.EqualTo(0f),
                    "발사 주기 이후 긴 대기에서는 재장전 그림을 계속 붙잡지 않는다");
        }
    }
}
