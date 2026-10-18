using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Traps;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Core.Tests
{
    // 무작위 지형 (D-077, K-049): 같은 씨앗이면 같은 지형, 성벽 10m 안·정면 가운데 통로는 비움, 지형 손질이 칸에 들어감
    public sealed class TerrainGeneratorTests
    {
        static TerrainDef Terrain() => TestDefs.Make<TerrainDef>("terrain_1", ("castleHalfSize", 20f), ("fieldSize", 160f));

        [Test]
        public void SameSeed_SameLayout_DifferentSeed_Differs()
        {
            TerrainDef terrain = Terrain();
            TerrainLayout a = TerrainGenerator.Generate(terrain, 1234, null);
            TerrainLayout b = TerrainGenerator.Generate(terrain, 1234, null);
            TerrainLayout c = TerrainGenerator.Generate(terrain, 99, null);
            CollectionAssert.AreEqual(a.Cells, b.Cells);
            CollectionAssert.AreNotEqual(a.Cells, c.Cells);
            Assert.That(a.Hills.Count, Is.InRange(TerrainRules.HillsMin, TerrainRules.HillsMax));
            Assert.That(a.TreeCells.Count, Is.GreaterThanOrEqualTo(TerrainRules.TreeClustersMin));
        }

        [Test]
        public void WallClearanceAndFrontCorridor_StayFree()
        {
            TerrainDef terrain = Terrain();
            for (int seed = 1; seed <= 20; seed++)
            {
                TerrainLayout layout = TerrainGenerator.Generate(terrain, seed, null);
                for (int c = 0; c < layout.Cells.Length; c++)
                {
                    if (layout.Cells[c] == TerrainCell.Free) continue;
                    Vector2 p = layout.Center(c);
                    float wall = TerrainLayout.WallDistance(terrain.CastleHalfSize, p);
                    Assert.That(wall, Is.GreaterThanOrEqualTo(TerrainRules.WallClearance), $"씨앗 {seed}: 성벽에 너무 가까움 {p}");
                    Assert.IsFalse(TerrainLayout.InFrontCorridor(terrain.CastleHalfSize, p, 0f), $"씨앗 {seed}: 정면 통로 막힘 {p}");
                }
            }
        }

        [Test]
        public void Works_AreStampedIntoCells_AndBlockTraps()
        {
            TerrainDef terrain = Terrain();
            string cell = TrapGrid.CellId(0, 3, 4);
            var works = new List<TerrainWork> { new TerrainWork { TerrainId = "terrain_1", CellId = cell, Kind = TerrainWorkKind.EarthWall } };
            TerrainLayout layout = TerrainGenerator.Generate(terrain, 7, works);
            Assert.AreEqual(TerrainCell.EarthWall, layout.Cells[layout.CellOfTrapCell(terrain, cell)]);
            Assert.IsTrue(layout.BlocksTrap(terrain, cell));
            Assert.IsFalse(TrapGrid.IsPlaceable(terrain, layout, cell));

            // 다른 지형 손질은 무시
            works[0].TerrainId = "terrain_2";
            TerrainLayout other = TerrainGenerator.Generate(terrain, 7, works);
            Assert.AreNotEqual(TerrainCell.EarthWall, other.Cells[other.CellOfTrapCell(terrain, cell)]);
        }

        // K-051: 길은 함정을 놓을 수 있고, 옮기기(옮긴 자리 + 놓은 자리)는 나무를 다른 칸으로 옮긴다
        [Test]
        public void PathAndMove_Works()
        {
            TerrainDef terrain = Terrain();
            string from = TrapGrid.CellId(0, 3, 6), to = TrapGrid.CellId(0, 4, 6), path = TrapGrid.CellId(0, 3, 2);
            var works = new List<TerrainWork>
            {
                new TerrainWork { TerrainId = "terrain_1", CellId = from, Kind = TerrainWorkKind.PlacedTrees },   // 먼저 나무를 놓고
                new TerrainWork { TerrainId = "terrain_1", CellId = from, Kind = TerrainWorkKind.Cleared },       // 옮겨 간 자리
                new TerrainWork { TerrainId = "terrain_1", CellId = to, Kind = TerrainWorkKind.PlacedTrees },     // 놓은 자리
                new TerrainWork { TerrainId = "terrain_1", CellId = path, Kind = TerrainWorkKind.Path },
            };
            TerrainLayout layout = TerrainGenerator.Generate(terrain, 7, works);
            int fromCell = layout.CellOfTrapCell(terrain, from), toCell = layout.CellOfTrapCell(terrain, to);
            Assert.AreEqual(TerrainCell.Free, layout.Cells[fromCell]);
            Assert.AreEqual(TerrainCell.Trees, layout.Cells[toCell]);
            Assert.IsFalse(layout.TreeCells.Contains(fromCell));
            Assert.IsTrue(layout.TreeCells.Contains(toCell));
            Assert.AreEqual(TerrainCell.Path, layout.Cells[layout.CellOfTrapCell(terrain, path)]);
            Assert.IsTrue(TrapGrid.IsPlaceable(terrain, layout, path), "길 위에는 함정을 놓을 수 있다");
            Assert.That(layout.RockCells.Count, Is.GreaterThanOrEqualTo(TerrainRules.RockClustersMin));
        }
    }
}
