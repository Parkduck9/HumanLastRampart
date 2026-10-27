using HumanLastRampart.Combat.Battle;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 사진기와 마당 건물 (W47, 사용자 2026-10-26 "플레이어가 성벽돌때 그림 겹쳐서 계속 z-파이팅"):
    // 성벽을 한 바퀴 도는 동안 사진기가 건물(지붕 꼭대기 아래) 안에 들지 않고, 위로 올라갈 때도 튀지 않는다
    public sealed class BattleCameraBuildingLiftTests
    {
        const float Half = 20f;   // 실제 성 반폭 (건물 자리가 이 값 기준)

        static bool InsideBuilding(Vector3 p)
        {
            float floor = BattleSim.WallHeight + 0.01f;
            foreach (var plot in BattleCourtyardBuildings.Plots)
                if (BattleCourtyardBuildings.Footprint(plot, Half).Contains(new Vector2(p.x, p.z)) && p.y < floor + plot.TotalHeight + 0.2f) return true;
            foreach (var piece in BattleCourtyardBuildings.Pieces)
                if (piece.Height > 1f && BattleCourtyardBuildings.Footprint(piece, Half).Contains(new Vector2(p.x, p.z)) && p.y < floor + piece.Y0 + piece.Height + 0.2f) return true;
            return false;
        }

        [Test]
        public void Camera_WalkingAllWalls_NeverInsideBuilding_AndLiftIsSmooth([Values(1f, 1.8f, 0.75f)] float inset)
        {
            var go = new GameObject("Building Lift Test Camera", typeof(Camera));
            try
            {
                var rig = new BattleCameraRig(go.GetComponent<Camera>(), Half, BattleSim.WallHeight);
                rig.ShowSide(0);
                rig.DepthInput = inset > rig.Inset ? -1f : 1f;
                for (int i = 0; i < 100 && Mathf.Abs(rig.Inset - inset) > 0.02f; i++) rig.Update(0.05f);
                rig.DepthInput = 0f;
                rig.WalkInput = 1f;
                int corners = 0, side = rig.Side, lifted = 0;
                float lastY = go.transform.position.y;
                for (int i = 0; i < 3000 && corners < 4; i++)
                {
                    rig.Update(0.05f);
                    if (rig.Side != side) { corners++; side = rig.Side; }
                    Vector3 c = go.transform.position;
                    Assert.IsFalse(InsideBuilding(c), $"성벽 {rig.Side} along {rig.Along:0.0}: 사진기 ({c.x:0.0}, {c.y:0.00}, {c.z:0.0})가 건물 안");
                    Assert.Less(Mathf.Abs(c.y - lastY), 0.3f, $"성벽 {rig.Side} along {rig.Along:0.0}: 사진기 높이가 한 걸음에 튄다 ({lastY:0.00} → {c.y:0.00})");
                    if (c.y > BattleSim.WallHeight + 2.5f) lifted++;
                    lastY = c.y;
                }

                Assert.AreEqual(4, corners, "네 모서리를 모두 돈다");
                Assert.Greater(lifted, 0, "건물 위를 지날 때 사진기가 실제로 올라간다 (시험이 건물을 지나는지 확인)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Camera_FarFromBuildings_IsNotLifted()
        {
            var go = new GameObject("Building Lift Test Camera 2", typeof(Camera));
            try
            {
                var rig = new BattleCameraRig(go.GetComponent<Camera>(), Half, BattleSim.WallHeight);
                rig.ShowSide(0);
                rig.Update(0.05f);
                var preset = BattleCameraRig.Presets[0];
                Assert.AreEqual(BattleSim.WallHeight + preset.Up, go.transform.position.y, 1e-3f, "계단 앞 가운데는 건물이 없어 높이가 그대로 (기존 전투 화면 사진과 같다)");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

