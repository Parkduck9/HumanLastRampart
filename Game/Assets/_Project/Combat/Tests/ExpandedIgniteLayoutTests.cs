using HumanLastRampart.Combat.PerfLab;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    public sealed class ExpandedIgniteLayoutTests
    {
        static Texture2D Sheet(string name, int width, int height = 384) =>
            new Texture2D(width, height, TextureFormat.RGBA32, false) { name = name };

        [Test]
        public void ExpandedIgniteReadsSixFramesAndOffCenterFeet()
        {
            var sheet = Sheet(CannonArt.IgniteSheetName, 432 * 6);
            try
            {
                var layout = PerfLabArt.FromSheet(sheet);
                Assert.That(PerfLabArt.FrameCount(sheet), Is.EqualTo(6));
                Assert.That(layout.Grid.x, Is.EqualTo(6f));
                Assert.That(layout.Grid.y, Is.EqualTo(1f));
                Assert.That(layout.Aspect * PerfLabArt.CellHeight, Is.EqualTo(432f).Within(0.0001f));
                Assert.That(layout.Pivot.x, Is.EqualTo(2f / 3f).Within(0.000001f));
                Assert.That(layout.Pivot.y, Is.EqualTo(1f / 16f).Within(0.000001f));
                Assert.That(PerfLabArt.FootPx(sheet), Is.EqualTo(360f));
                Assert.That(PerfLabArt.IsSixSheet(sheet), Is.False);
            }
            finally { Object.DestroyImmediate(sheet); }
        }

        [Test]
        public void ExpandedIgnitePreservesCharacterHeightAndFootRegistration()
        {
            var expanded = Sheet(CannonArt.IgniteSheetName, 432 * 6);
            var ordinary = Sheet(CannonArt.IgniteSheetName, 288 * 6);
            try
            {
                Assert.That(PerfLabArt.FigurePx(expanded), Is.EqualTo(219f));
                Assert.That(PerfLabArt.SheetScale(expanded), Is.EqualTo(PerfLabArt.SheetScale(ordinary)).Within(0.000001f));
                const float soldierHeight = 0.35f;
                float canvasHeight = soldierHeight * PerfLabArt.SheetScale(expanded);
                Assert.That(canvasHeight * PerfLabArt.FigurePx(expanded) / PerfLabArt.CellHeight,
                    Is.EqualTo(soldierHeight).Within(0.000001f));
                var layout = PerfLabArt.FromSheet(expanded);
                Assert.That(layout.Pivot.x * layout.Aspect * PerfLabArt.CellHeight,
                    Is.EqualTo(288f).Within(0.0001f));
                Assert.That((1f - layout.Pivot.y) * PerfLabArt.CellHeight,
                    Is.EqualTo(360f).Within(0.0001f));
            }
            finally { Object.DestroyImmediate(expanded); Object.DestroyImmediate(ordinary); }
        }

        [TestCase("cannon_crew_ignite6", 1728, 6, 288, 360)]
        [TestCase("cannon_crew_ignite6", 2304, 6, 384, 352)]
        [TestCase("crew_pull6", 2304, 6, 384, 352)]
        [TestCase("crew_push6", 2304, 6, 384, 352)]
        [TestCase("ordinary_four", 1024, 4, 256, 352)]
        [TestCase("ordinary_four", 1152, 4, 288, 360)]
        [TestCase("different_name", 2592, 9, 288, 360)]
        public void ExistingLayoutsAndUnrelatedNameKeepTheirLayout(string name, int width, int columns, int cellWidth, int foot)
        {
            var sheet = Sheet(name, width);
            try
            {
                var layout = PerfLabArt.FromSheet(sheet);
                Assert.That(PerfLabArt.FrameCount(sheet), Is.EqualTo(columns));
                Assert.That(layout.Grid.x, Is.EqualTo(columns));
                Assert.That(layout.Aspect * PerfLabArt.CellHeight, Is.EqualTo(cellWidth).Within(0.0001f));
                Assert.That(layout.Pivot.x, Is.EqualTo(0.5f));
                Assert.That(PerfLabArt.FootPx(sheet), Is.EqualTo(foot));
            }
            finally { Object.DestroyImmediate(sheet); }
        }

        [Test]
        public void MatchingNameAndWidthWithWrongHeightDoesNotUseExpandedException()
        {
            var sheet = Sheet(CannonArt.IgniteSheetName, 2592, 768);
            try
            {
                var layout = PerfLabArt.FromSheet(sheet);
                Assert.That(layout.Grid.x, Is.EqualTo(9f));
                Assert.That(layout.Grid.y, Is.EqualTo(2f));
                Assert.That(layout.Pivot.x, Is.EqualTo(0.5f));
                Assert.That(PerfLabArt.FrameCount(sheet), Is.EqualTo(18));
            }
            finally { Object.DestroyImmediate(sheet); }
        }
    }
}
