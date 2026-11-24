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
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 확장 점화 그림의 사격병만 대포와 같은 앞뒤 기준에 선다. 공유 정의는 시험 뒤 반드시 복원한다.
    public sealed class ExpandedIgnitePlacementTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        sealed class Fixture : IDisposable
        {
            readonly Dictionary<UnitDef, Texture2D> originals = new Dictionary<UnitDef, Texture2D>();
            readonly Texture2D sheet;
            public BattleSim Sim { get; private set; }

            public Fixture(string layout)
            {
                var catalogs = new List<DefinitionCatalog>();
                foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                    catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
                DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
                CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
                BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_expanded_ignite_placement", out string error);
                Assert.IsNull(error);
                if (layout != "none")
                    sheet = new Texture2D(layout == "expanded" ? 2592 : 1728, 384, TextureFormat.RGBA32, false)
                    { name = CannonArt.IgniteSheetName };
                try
                {
                    foreach (SquadState squad in setup.Squads)
                    {
                        UnitDef unit = lookup.Get<UnitDef>(squad.UnitDefId);
                        bool cannon = unit != null && unit.Kind == UnitKind.CannonCrew;
                        squad.Count = cannon ? 2 : 0;
                        if (!cannon || originals.ContainsKey(unit)) continue;
                        originals.Add(unit, unit.IgniteSheet);
                        typeof(UnitDef).GetField("igniteSheet", Private).SetValue(unit, sheet);
                    }
                    if (setup.Mercenaries != null)
                        foreach (SquadState squad in setup.Mercenaries) squad.Count = 0;
                    Assert.Greater(originals.Count, 0, "대포병 정의가 있어야 실제 생성 경로를 검증한다");
                    Sim = new BattleSim(setup, lookup);
                }
                catch { Dispose(); throw; }
            }

            public void Dispose()
            {
                Sim?.Dispose();
                Sim = null;
                foreach (var original in originals)
                    typeof(UnitDef).GetField("igniteSheet", Private).SetValue(original.Key, original.Value);
                originals.Clear();
                if (sheet != null) UnityEngine.Object.DestroyImmediate(sheet);
            }
        }

        static int FirstCannon(BattleSim sim, int side)
        {
            for (int i = 0; i < sim.CannonCount; i++)
                if (sim.GetCannon(i).Side == side) return i;
            Assert.Fail($"성벽 {side}의 대포가 없다");
            return -1;
        }

        static void CrewRelativeToCannon(BattleSim sim, int cannon, bool expanded)
        {
            var mapping = (List<(int soldier, int side, int number, string postId)>)
                typeof(BattleSim).GetField("cannons", Private).GetValue(sim);
            var positions = (NativeArray<float3>)typeof(BattleSim).GetField("sPos", Private).GetValue(sim);
            BattleSim.CannonInfo info = sim.GetCannon(cannon);
            float3 center = info.Position;
            float3 outward = WallGeometry.Outward(info.Side), right = WallGeometry.Right(info.Side);
            int shooter = mapping[cannon].soldier;
            float3 shooterDelta = positions[shooter] - center;
            float3 helperDelta = positions[shooter + 1] - center;
            Assert.That(math.dot(shooterDelta, outward), Is.EqualTo(expanded ? 0.23f : 0f).Within(0.0001f),
                "확장 점화 사격병만 0.23m 전진하여 설치 대포보다 2cm 뒤에 선다");
            if (expanded)
                Assert.Less(math.dot(shooterDelta, outward), CannonArt.InstalledForwardOffset,
                    "불꽃과 대포 판이 같은 깊이에 겹치지 않는다");
            Assert.That(math.dot(shooterDelta, right), Is.EqualTo(0.4f).Within(0.0001f), "사격병의 좌우 간격은 유지");
            Assert.That(shooterDelta.y, Is.EqualTo(0f).Within(0.0001f), "높이로 접점을 억지 보정하지 않는다");
            Assert.That(math.dot(helperDelta, outward), Is.EqualTo(0f).Within(0.0001f), "보조병의 앞뒤 자리는 유지");
            Assert.That(math.dot(helperDelta, right), Is.EqualTo(-0.55f).Within(0.0001f), "보조병의 좌우 간격은 유지");
            Assert.That(helperDelta.y, Is.EqualTo(0f).Within(0.0001f));
        }

        [TestCase("expanded")]
        [TestCase("legacy")]
        [TestCase("none")]
        public void InitialPlacement_OnlyExpandedShooterGetsForwardOffset_OnEveryWall(string layout)
        {
            using (var fixture = new Fixture(layout))
                for (int side = 0; side < 4; side++)
                    CrewRelativeToCannon(fixture.Sim, FirstCannon(fixture.Sim, side), layout == "expanded");
        }

        [TestCase("expanded")]
        [TestCase("legacy")]
        [TestCase("none")]
        public void InstantMove_PreservesCrewOffsets_AroundAllFourWalls(string layout)
        {
            using (var fixture = new Fixture(layout))
            {
                BattleSim sim = fixture.Sim;
                int cannon = FirstCannon(sim, 0);
                for (int step = 1; step <= 4; step++)
                {
                    int side = step % 4;
                    Assert.IsTrue(sim.MoveCannon(cannon, side, true), $"성벽 {side}로 즉시 이동");
                    Assert.That(sim.GetCannon(cannon).Side, Is.EqualTo(side));
                    Assert.IsFalse(sim.GetCannon(cannon).Moving);
                    CrewRelativeToCannon(sim, cannon, layout == "expanded");
                }
            }
        }
    }
}
