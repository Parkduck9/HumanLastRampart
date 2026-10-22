using System.Collections.Generic;
using HumanLastRampart.App.Data;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 높은 등대 탑 (W20): 키가 성벽의 3배, 빛·불빛이 꼭대기 불방에서 나감, 주인공은 문 앞(오르지 않음), 전령은 몸체 밖
    public sealed class BattleTowerViewTests
    {
        [Test]
        public void Height_IsAboutThreeTimesWall()
        {
            Assert.AreEqual(3f, BattleTowerView.Height / BattleSim.WallHeight, 0.01f, "탑 키 = 성벽 높이의 3배");
            Assert.Greater(BattleTowerView.LampHeight, BattleTowerView.Height * 0.75f, "불방은 탑 위쪽");
            Assert.Less(BattleTowerView.LampHeight, BattleTowerView.Height, "불방은 꼭대기 지붕 아래");
        }

        [Test]
        public void BeamOrigin_IsAtTopLampRoom()
        {
            Assert.AreEqual(BattleSim.WallHeight + BattleTowerView.LampHeight, BattleBeamView.OriginHeight, 1e-4f);
            Assert.Greater(BattleBeamView.OriginHeight, BattleSim.WallHeight * 3f, "빛은 성벽 높이 3배보다 위에서 나간다");
        }

        [Test]
        public void Mesh_ReachesTowerTop_FewDrawCalls()
        {
            var parent = new GameObject("Tower View Test");
            try
            {
                using (var view = new BattleTowerView(parent.transform))
                {
                    Assert.LessOrEqual(view.RendererCount, 3, "모바일: 합친 메시 몇 개만");
                    float maxY = float.MinValue;
                    foreach (var f in parent.GetComponentsInChildren<MeshFilter>())
                        maxY = Mathf.Max(maxY, f.transform.TransformPoint(f.sharedMesh.bounds.max).y);
                    Assert.AreEqual(BattleSim.WallHeight + BattleTowerView.Height, maxY, 0.05f, "꼭대기 = 마당 윗면 + 탑 키");
                    Assert.AreEqual(view.LampWorld.y, BattleBeamView.OriginHeight, 1e-4f);
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void HeroPath_StairsToDoor_NeverEntersTowerBody()
        {
            var camera = new GameObject("Tower Path Camera", typeof(Camera));
            try
            {
                var rig = new BattleCameraRig(camera.GetComponent<Camera>(), 60f, BattleSim.WallHeight);
                float limit = BattleTowerView.BaseHalf + 0.25f;   // 몸체 + 받침(0.15m) 바깥
                foreach (float x in new[] { -3f, -1f, -0.01f, 0f, 1f, 3f })
                    for (int i = 0; i <= 200; i++)
                    {
                        Vector3 p = rig.TowerPath(new Vector3(x, BattleSim.WallHeight, 58f), i / 200f);
                        Assert.IsTrue(Mathf.Abs(p.x) > limit || Mathf.Abs(p.z) > limit, $"시작 x={x}, t={i / 200f:0.000}: 발 ({p.x:0.00}, {p.z:0.00})이 탑 몸체 안");
                        Assert.AreEqual(BattleSim.WallHeight, p.y, 1e-4f);
                    }

                Vector3 end = rig.TowerPath(new Vector3(0f, BattleSim.WallHeight, 58f), 1f);
                Assert.Less((end - rig.TowerFeet).magnitude, 1e-4f, "끝은 문 앞");
            }
            finally { Object.DestroyImmediate(camera); }
        }

        [Test]
        public void Mesh_HasDownFaces_SoOverhangsAreNotHollow()
        {
            var parent = new GameObject("Tower Down Faces");
            try
            {
                using (new BattleTowerView(parent.transform))
                {
                    int down = 0;
                    foreach (var f in parent.GetComponentsInChildren<MeshFilter>())
                        foreach (Vector3 n in f.sharedMesh.normals) if (n.y < -0.99f) down++;
                    Assert.Greater(down, 20, "받침·띠·둘레길·지붕 테에 아랫면이 있다");
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void HeroDoor_AndCouriers_AreOutsideTheBody()
        {
            Assert.Greater(BattleCameraRig.TowerHeroBack, BattleTowerView.BaseHalf + 0.2f, "주인공은 문 앞 마당에 선다 (탑 안이 아니다)");
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_tower_view", out string error);
            Assert.IsNull(error);
            using (var sim = new BattleSim(setup, lookup))
            {
                for (int i = 0; i < BattleSim.TowerCourierCount; i++)
                {
                    Vector3 p = sim.GetTowerCourier(i).Position;
                    Assert.AreEqual(BattleSim.WallHeight, p.y, 1e-4f);
                    Assert.IsTrue(Mathf.Abs(p.x) > BattleTowerView.BaseHalf + 0.15f || Mathf.Abs(p.z) > BattleTowerView.BaseHalf + 0.15f, $"전령 {i}는 탑 몸체 밖");
                }
            }
        }
    }
}
