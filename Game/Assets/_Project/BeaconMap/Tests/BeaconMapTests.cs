using HumanLastRampart.Core.Campaign;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace HumanLastRampart.BeaconMap.Tests
{
    // 봉화선 지도 (W10): 진행 데이터 → 요새 상태 대응, 모형 만들기
    public sealed class BeaconMapTests
    {
        static CampaignState Campaign(int fortress, bool complete = false, params (int fortress, int count)[] garrisons)
        {
            var campaign = new CampaignState { FortressNumber = fortress };
            foreach (var g in garrisons) campaign.Garrisons.Add(new GarrisonState { FortressNumber = g.fortress, Count = g.count });
            if (complete) campaign.NextStory = new StoryRequest { Kind = StoryKind.RunComplete, StoryId = BeaconRules.RunCompleteStoryId };
            return campaign;
        }

        static FortressMapState[] States(BeaconMapModel model)
        {
            var states = new FortressMapState[model.Nodes.Count];
            for (int i = 0; i < states.Length; i++) states[i] = model.Nodes[i].State;
            return states;
        }

        [Test]
        public void AfterFirstFortressRelit_FirstLit_SecondFoundDead_RestFogged()
        {
            BeaconMapModel model = BeaconMapModel.Build(Campaign(2, false, (1, 12)), false);   // 점화 직후: 번호는 이미 2
            CollectionAssert.AreEqual(new[]
            {
                FortressMapState.Lit, FortressMapState.Dead, FortressMapState.Fogged, FortressMapState.Fogged, FortressMapState.Fogged,
            }, States(model));
            Assert.AreEqual(1, model.LitCount);
            Assert.AreEqual(12, model.Nodes[0].Garrison, "수비대는 점화한 요새 옆에");
            Assert.AreEqual(0, model.Nodes[1].Garrison);
            Assert.AreEqual("빛의 봉화선", model.Title);
            Assert.AreEqual("요새 1 · 다음: 요새 2", model.Subtitle);
            Assert.IsFalse(model.RunComplete);
        }

        [Test]
        public void PreviewDuringFortress_ShowsCurrentAsDeadAndNextNumber()
        {
            BeaconMapModel first = BeaconMapModel.Build(Campaign(1), true);
            Assert.AreEqual(0, first.LitCount);
            Assert.AreEqual(FortressMapState.Dead, first.Nodes[0].State, "지금 지키는 요새는 아직 꺼져 있다");
            Assert.AreEqual(FortressMapState.Fogged, first.Nodes[1].State);
            Assert.AreEqual("요새 1 · 다음: 요새 2", first.Subtitle);

            BeaconMapModel second = BeaconMapModel.Build(Campaign(2, false, (1, 7)), true);
            Assert.AreEqual(FortressMapState.Lit, second.Nodes[0].State);
            Assert.AreEqual(FortressMapState.Dead, second.Nodes[1].State);
            Assert.AreEqual("요새 2 · 다음: 요새 3", second.Subtitle);
            Assert.IsTrue(second.Preview);
        }

        [Test]
        public void RunComplete_AllRealFortressesLit_NextStaysFogged()
        {
            BeaconMapModel model = BeaconMapModel.Build(Campaign(BeaconRules.FortressCount, true, (1, 7), (2, 9)), false);
            Assert.IsTrue(model.RunComplete);
            Assert.AreEqual(BeaconRules.FortressCount, model.LitCount);
            Assert.AreEqual(FortressMapState.Lit, model.Nodes[1].State);
            Assert.AreEqual(FortressMapState.Fogged, model.Nodes[2].State, "1회차 밖의 요새는 안개 속");
            Assert.AreEqual(9, model.Nodes[1].Garrison);
            StringAssert.Contains("1회차 완료", model.Subtitle);
        }

        [Test]
        public void NullCampaign_BuildsFirstFortressDead()
        {
            BeaconMapModel model = BeaconMapModel.Build(null, true);
            Assert.AreEqual(FortressMapState.Dead, model.Nodes[0].State);
            Assert.GreaterOrEqual(model.Nodes.Count, 5, "1회차 2개 + 안개 속 앞으로의 요새");
        }

        [Test]
        public void Sites_AreSeparatedAndGroundIsFlatUnderEach()
        {
            Vector2[] sites = BeaconMapModel.Sites;
            for (int i = 0; i < sites.Length; i++)
            {
                for (int j = i + 1; j < sites.Length; j++)
                    Assert.Greater(Vector2.Distance(sites[i], sites[j]), 6f, $"요새 {i + 1}·{j + 1} 사이가 가깝다");
                Assert.AreEqual(0f, BeaconDiorama.Height(sites[i].x, sites[i].y), 0.001f, "요새 터는 평평");
            }

            float max = 0f;
            for (float x = -20f; x <= 28f; x += 1f)
                for (float z = -10f; z <= 14f; z += 1f) max = Mathf.Max(max, BeaconDiorama.Height(x, z));
            Assert.Less(max, 1.5f, "언덕은 낮게");
            Assert.Greater(max, 0.3f, "언덕이 있어야 한다");
        }

        [Test]
        public void Diorama_LightsAndShadowsOnlyForLitFortresses()
        {
            var camera = new GameObject("Test Camera", typeof(Camera)).GetComponent<Camera>();
            BeaconDiorama diorama = null;
            try
            {
                diorama = BeaconDiorama.Create(null, camera, BeaconMapModel.Build(Campaign(2, false, (1, 5)), false));
                Assert.AreEqual(5, diorama.FortressCount);
                Assert.AreEqual(1, diorama.LightCount, "점화한 요새 하나 = 점광원 하나");
                Light light = diorama.Lights[0];
                Assert.AreEqual(LightType.Point, light.type);
                Assert.AreNotEqual(LightShadows.None, light.shadows, "진짜 그림자");
                Assert.Greater(light.range, 5f, "빛이 땅 위로 퍼진다");
                Assert.AreEqual(FortressMapState.Dead, diorama.StateOf(1));
                Assert.Greater(diorama.DotCount, 6, "다음 요새 쪽 어두운 점선");
                Assert.IsNotNull(diorama.GroundMesh);
                Assert.Greater(diorama.GroundMesh.vertexCount, 1000);
                Assert.Less(camera.transform.position.y, 40f);
                Assert.Greater(camera.transform.position.y, 5f, "비스듬히 내려다본다");

                diorama.Release();
                Object.DestroyImmediate(diorama.gameObject);
                diorama = BeaconDiorama.Create(null, camera, BeaconMapModel.Build(Campaign(2, true, (1, 5), (2, 8)), false));
                Assert.AreEqual(2, diorama.LightCount, "마지막 요새까지 점화하면 둘");
            }
            finally
            {
                if (diorama != null)
                {
                    diorama.Release();
                    Object.DestroyImmediate(diorama.gameObject);
                }

                Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
