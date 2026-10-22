using HumanLastRampart.Combat.Battle;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 마당 계단 높이 보정 (W19): 순수 계산 HeroLift — 층계참·단마다 높이, 다른 성벽·마당·층계참 밖에서는 0, 좌우 대칭
    public sealed class BattleCourtyardTests
    {
        const float Half = 20f;

        // 계단 쪽 성벽 안쪽 가장자리 가운데에서 (lz: 통로 쪽 +, lx: 성벽 따라) 떨어진 발 위치
        static Vector3 At(float lz, float lx)
        {
            var n = WallGeometry.Outward(BattleCameraRig.StairsSide);
            var r = WallGeometry.Right(BattleCameraRig.StairsSide);
            var nv = new Vector3(n.x, 0f, n.z);
            var rv = new Vector3(r.x, 0f, r.z);
            return nv * (Half - 2f + lz) + rv * lx + Vector3.up * BattleSim.WallHeight;
        }

        [Test]
        public void Landing_HasFullHeight()
        {
            Assert.AreEqual(BattleCourtyard.LandingHeight, BattleCourtyard.HeroLift(At(0.1f, 0f), Half), 1e-4f);
        }

        [Test]
        public void Treads_StepDownOneByOne()
        {
            float previous = BattleCourtyard.LandingHeight;
            for (int k = 0; k < BattleCourtyard.StepCount - 1; k++)
            {
                float expected = BattleCourtyard.LandingHeight * (1f - (k + 1f) / BattleCourtyard.StepCount);
                float lift = BattleCourtyard.HeroLift(At(-(k + 0.5f) * BattleCourtyard.TreadDepth, 0f), Half);
                Assert.AreEqual(expected, lift, 1e-4f, $"{k}번째 단");
                Assert.Less(lift, previous, "단마다 낮아진다");
                previous = lift;
            }

            Assert.AreEqual(0f, BattleCourtyard.HeroLift(At(-(BattleCourtyard.StepCount - 0.5f) * BattleCourtyard.TreadDepth, 0f), Half), 1e-4f, "마당 바닥");
        }

        [Test]
        public void OutsideStairs_IsZero()
        {
            Assert.AreEqual(0f, BattleCourtyard.HeroLift(At(BattleCourtyard.LandingDepth + 0.3f, 0f), Half), 1e-4f, "층계참보다 바깥(처음 서는 자리·병사 길)");
            Assert.AreEqual(0f, BattleCourtyard.HeroLift(At(0.1f, BattleCourtyard.StairHalfWidth + 0.2f), Half), 1e-4f, "계단 폭 밖");
            Assert.AreEqual(0f, BattleCourtyard.HeroLift(new Vector3(0f, BattleSim.WallHeight, 0f), Half), 1e-4f, "탑 앞");
            for (int side = 0; side < 4; side++)
            {
                if (side == BattleCameraRig.StairsSide) continue;
                var n = WallGeometry.Outward(side);
                var feet = new Vector3(n.x, 0f, n.z) * (Half - 1.8f) + Vector3.up * BattleSim.WallHeight;
                Assert.AreEqual(0f, BattleCourtyard.HeroLift(feet, Half), 1e-4f, $"성벽 {side} 가운데");
            }
        }

        [Test]
        public void IsSymmetric_AndNeverBuriesPastLandingEdge()
        {
            foreach (float lz in new[] { 0.1f, 0.4f, -0.1f, -0.6f })
                foreach (float lx in new[] { 0.3f, 0.9f })
                    Assert.AreEqual(BattleCourtyard.HeroLift(At(lz, lx), Half), BattleCourtyard.HeroLift(At(lz, -lx), Half), 1e-5f);

            // 층계참은 바깥 모서리에서 1.5m보다 안쪽에서 끝나고 0.1m 이하 (W19 검토)
            Assert.LessOrEqual(BattleCourtyard.LandingHeight, 0.1f);
            Assert.GreaterOrEqual(2f - BattleCourtyard.LandingDepth, 1.5f);
            Assert.AreEqual(0f, BattleCourtyard.HeroLift(At(BattleCourtyard.LandingDepth, 0f), Half), 1e-4f, "상자 모서리에서 보정 0");
        }
    }
}
