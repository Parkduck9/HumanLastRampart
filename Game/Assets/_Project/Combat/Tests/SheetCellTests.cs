using HumanLastRampart.Combat.PerfLab;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // D-081: 6판 73점 시트(칸 288×384, 발 360)와 예전 시트(칸 256×384, 발 352)를 가로 크기로 구분한다
    public sealed class SheetCellTests
    {
        static Texture2D Sheet(int w, int h, string name) => new Texture2D(w, h) { name = name };

        [Test]
        public void SixSheetUses288Cells()
        {
            var four = Sheet(1152, 384, "six_walk4");
            Assert.IsTrue(PerfLabArt.IsSixSheet(four));
            Assert.AreEqual(4, PerfLabArt.FrameCount(four));
            var layout = PerfLabArt.FromSheet(four);
            Assert.AreEqual(24f / 384f, layout.Pivot.y, 1e-5f, "발 360");
            Assert.AreEqual(288f / 384f, layout.Aspect, 1e-5f);
            Assert.AreEqual(219f, PerfLabArt.FigurePx(four), 1e-3f, "표에 없으면 73점 × 3");

            var six = Sheet(1728, 384, "six_fire6");
            Assert.AreEqual(6, PerfLabArt.FrameCount(six));
            var hero = Sheet(1152, 1536, "six_hero");
            Assert.AreEqual(24f / 384f, PerfLabArt.HeroLayoutOf(hero).Pivot.y, 1e-5f);
            Object.DestroyImmediate(four); Object.DestroyImmediate(six); Object.DestroyImmediate(hero);
        }

        [Test]
        public void OldSheetsUnchanged()
        {
            var walk = Sheet(1024, 384, "old_walk4");
            Assert.IsFalse(PerfLabArt.IsSixSheet(walk));
            Assert.AreEqual(4, PerfLabArt.FrameCount(walk));
            Assert.AreEqual(32f / 384f, PerfLabArt.FromSheet(walk).Pivot.y, 1e-5f, "발 352");
            var hero = Sheet(1024, 1536, "old_hero");
            Assert.AreEqual(PerfLabArt.HeroLayout.Aspect, PerfLabArt.HeroLayoutOf(hero).Aspect, 1e-5f);
            Object.DestroyImmediate(walk); Object.DestroyImmediate(hero);
        }
    }
}
