using HumanLastRampart.Combat.Battle;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 병사 줄 막기 (사용자 2026-11-03 "전투 중 병사가 선 자리로 주인공이 못 지나가게"):
    // 앞줄 병사가 선 곳에서는 그 줄 + 여유 안쪽까지만 들어가고, 없는 곳은 옛 범위, 옆걸음으로 들어서면 한 걸음에 튀지 않고 밀려난다
    public sealed class BattleHeroFormationTests
    {
        const float Half = 20f;
        const float Dt = 0.05f;

        // side 0 성벽 along 2~6m 구간에 줄 안쪽 거리 1.1 병사가 선다
        static float Formation(int side, float along) => side == 0 && along > 2f - 0.45f && along < 6f + 0.45f ? 1.1f : 0f;

        static BattleCameraRig Make(GameObject go, bool withFormation)
        {
            var rig = new BattleCameraRig(go.GetComponent<Camera>(), Half, BattleSim.WallHeight);
            if (withFormation) rig.SetFormation(Formation);
            rig.ShowSide(0);
            return rig;
        }

        [Test]
        public void Hero_CannotWalkOutwardPastFormation()
        {
            var go = new GameObject("Formation Test Camera", typeof(Camera));
            try
            {
                var rig = Make(go, true);
                rig.WalkInput = 1f;   // 병사 구간 안(along 4)까지
                for (int i = 0; i < 400 && rig.Along < 4f; i++) rig.Update(Dt);
                rig.WalkInput = 0f;
                for (int i = 0; i < 60; i++) rig.Update(Dt);
                float min = 1.1f + BattleCameraRig.FormationClearance;
                Assert.GreaterOrEqual(rig.Inset, min - 1e-3f, "병사 줄 뒤로 밀려나 있다");
                rig.DepthInput = 1f;   // 바깥쪽으로 걸어도 더 못 나간다
                for (int i = 0; i < 60; i++) rig.Update(Dt);
                rig.DepthInput = 0f;
                Assert.GreaterOrEqual(rig.Inset, min - 1e-3f, "바깥쪽으로 걸어도 병사 줄을 넘지 못한다");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Hero_CanUseWholePassageWhereNoSoldierStands()
        {
            var go = new GameObject("Formation Test Camera 2", typeof(Camera));
            try
            {
                var rig = Make(go, true);   // along 0 은 비어 있다 (병사 구간은 1.55부터)
                rig.DepthInput = 1f;
                for (int i = 0; i < 100; i++) rig.Update(Dt);
                rig.DepthInput = 0f;
                Assert.AreEqual(BattleCameraRig.InsetMin, rig.Inset, 1e-3f, "병사 없는 곳은 통로 바깥 끝까지");
                Assert.AreEqual(BattleCameraRig.InsetMin, rig.MinInsetAt(0, 0f), 1e-6f);
                Assert.AreEqual(BattleCameraRig.InsetMin, rig.MinInsetAt(1, 4f), 1e-6f, "다른 성벽은 막지 않는다");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Hero_WalkingSidewaysIntoFormation_IsPushedInwardWithoutJump()
        {
            var go = new GameObject("Formation Test Camera 3", typeof(Camera));
            try
            {
                var rig = Make(go, true);
                rig.DepthInput = 1f;   // 먼저 바깥 끝(0.75)까지
                for (int i = 0; i < 100; i++) rig.Update(Dt);
                rig.DepthInput = 0f;
                float last = rig.Inset;
                float maxStep = BattleSim.HeroSpeed * Dt + 1e-3f;
                rig.WalkInput = 1f;
                for (int i = 0; i < 200 && rig.Along < 4f; i++)
                {
                    rig.Update(Dt);
                    Assert.LessOrEqual(Mathf.Abs(rig.Inset - last), maxStep, $"한 프레임에 inset이 튄다 along {rig.Along:0.00}");
                    Assert.GreaterOrEqual(rig.Inset, rig.MinInsetAt(rig.Side, rig.Along) - 1e-3f, $"달리는 중에도 병사 줄 안으로 들어가지 않는다 along {rig.Along:0.00}");
                    last = rig.Inset;
                }

                rig.WalkInput = 0f;
                for (int i = 0; i < 60; i++) rig.Update(Dt);
                Assert.Greater(rig.Inset, BattleCameraRig.InsetMin + 0.3f, "병사 구간에 들어서면 안쪽으로 밀려난다");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Hero_WithoutFormation_KeepsOldBehaviour()
        {
            var go = new GameObject("Formation Test Camera 4", typeof(Camera));
            try
            {
                var rig = Make(go, false);
                Assert.AreEqual(BattleCameraRig.HeroInset, rig.Inset, 1e-6f);
                Assert.AreEqual(BattleCameraRig.InsetMin, rig.MinInsetAt(0, 4f), 1e-6f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ShowSide_StartsBehindFormation()
        {
            var go = new GameObject("Formation Test Camera 5", typeof(Camera));
            try
            {
                var rig = new BattleCameraRig(go.GetComponent<Camera>(), Half, BattleSim.WallHeight);
                rig.SetFormation((side, along) => 1.1f);   // 가운데에도 병사가 선다
                rig.ShowSide(2);
                Assert.GreaterOrEqual(rig.Inset, 1.1f + BattleCameraRig.FormationClearance - 1e-3f, "처음 자리부터 병사 줄 뒤");
                Assert.LessOrEqual(rig.Inset, BattleCameraRig.InsetMax);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
