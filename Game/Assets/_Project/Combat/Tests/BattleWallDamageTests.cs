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
    // 성벽 부서짐 그림 (W39, Codex K-126): 단계 기준·돌출부/데칼/잔해 배치 규칙·그림 연결·그림이 없을 때의 대비
    public sealed class BattleWallDamageTests
    {
        const float Half = 60f;
        static readonly float[] MerlonSpots = BuildMerlonSpots();

        static float[] BuildMerlonSpots()
        {
            var list = new List<float>();
            BattleWallLayout.Merlons(Half, new List<float> { -20f, 0f, 20f }, list);
            return list.ToArray();
        }

        static int Rank(WallDamageRules.MerlonState s) => (int)s;

        [Test]
        public void StageOf_MapsHpToCodexStages()
        {
            Assert.AreEqual(0, WallDamageRules.StageOf(1f));
            Assert.AreEqual(0, WallDamageRules.StageOf(0.66f), "66%는 아직 멀쩡 (미만일 때 1단계)");
            Assert.AreEqual(1, WallDamageRules.StageOf(0.6599f));
            Assert.AreEqual(1, WallDamageRules.StageOf(0.33f));
            Assert.AreEqual(2, WallDamageRules.StageOf(0.3299f));
            Assert.AreEqual(2, WallDamageRules.StageOf(0.0001f));
            Assert.AreEqual(3, WallDamageRules.StageOf(0f), "체력 0 = 파공");
            Assert.AreEqual(3, WallDamageRules.StageOf(-1f));
            Assert.AreEqual(0, WallDamageRules.StageOf(100, 100));
            Assert.AreEqual(1, WallDamageRules.StageOf(50, 100));
            Assert.AreEqual(2, WallDamageRules.StageOf(20, 100));
            Assert.AreEqual(3, WallDamageRules.StageOf(0, 100));
            Assert.AreEqual(0, WallDamageRules.StageOf(0, 0), "최대 체력 0이면 멀쩡");
            // 재질 바꿈(K-024)은 손상 그림이 2종뿐 — 3단계도 2번 재질
            Assert.AreEqual(2, WallDamageRules.MaterialStage(3));
            Assert.AreEqual(1, WallDamageRules.MaterialStage(1));
        }

        [Test]
        public void StageOf_NeverGoesBackwardsAsHpFalls()
        {
            int last = 0;
            for (int hp = 1000; hp >= 0; hp--)
            {
                int stage = WallDamageRules.StageOf(hp, 1000);
                Assert.GreaterOrEqual(stage, last, "체력 " + hp);
                last = stage;
            }

            Assert.AreEqual(3, last);
        }

        [Test]
        public void MerlonStates_OnlyGetWorse_AsStagesRise()
        {
            for (int i = 0; i < 1000; i++)
            {
                float h = i / 1000f;
                int prev = 0;
                for (int stage = 0; stage <= 3; stage++)
                {
                    int rank = Rank(WallDamageRules.MerlonAt(h, stage));
                    Assert.GreaterOrEqual(rank, prev, $"h {h} 단계 {stage}");
                    prev = rank;
                }
            }

            Assert.AreEqual(WallDamageRules.MerlonState.Intact, WallDamageRules.MerlonAt(0f, 0));
            Assert.AreEqual(WallDamageRules.MerlonState.Gone, WallDamageRules.MerlonAt(0f, 3));
        }

        [Test]
        public void Decals_OnlyGetWorse_AsStagesRise()
        {
            for (int i = 0; i < 1000; i++)
            {
                float h = i / 1000f;
                int prev = 0;
                for (int stage = 0; stage <= 3; stage++)
                {
                    int rank = (int)WallDamageRules.DecalAt(h, stage);
                    Assert.GreaterOrEqual(rank, prev, $"h {h} 단계 {stage}");
                    prev = rank;
                }
            }

            Assert.AreEqual(WallDamageRules.DecalKind.None, WallDamageRules.DecalAt(0.1f, 0));
        }

        [Test]
        public void Placement_IsDeterministic_AndSeedDependent()
        {
            float from = BattleWallLayout.From(Half), to = BattleWallLayout.To(Half);
            var rubbleA = new List<WallDamageRules.RubblePiece>(); var spotsA = new List<WallDamageRules.Spot>();
            var rubbleB = new List<WallDamageRules.RubblePiece>(); var spotsB = new List<WallDamageRules.Spot>();
            var rubbleC = new List<WallDamageRules.RubblePiece>(); var spotsC = new List<WallDamageRules.Spot>();
            WallDamageRules.Place(12345u, 1, from, to, MerlonSpots, rubbleA, spotsA);
            WallDamageRules.Place(12345u, 1, from, to, MerlonSpots, rubbleB, spotsB);
            WallDamageRules.Place(99999u, 1, from, to, MerlonSpots, rubbleC, spotsC);
            Assert.Greater(rubbleA.Count, 8);
            CollectionAssert.AreEqual(rubbleA, rubbleB, "같은 씨앗·면이면 같은 잔해");
            CollectionAssert.AreEqual(spotsA, spotsB, "같은 연기·불꽃 자리");
            CollectionAssert.AreNotEqual(rubbleA, rubbleC, "씨앗이 다르면 자리도 다르다");
            Assert.AreEqual(WallDamageRules.BreachTile(12345u, 2, 28), WallDamageRules.BreachTile(12345u, 2, 28));
            Assert.AreEqual(WallDamageRules.Hash01(7u, 1, 2, 3), WallDamageRules.Hash01(7u, 1, 2, 3));
            for (int t = 0; t < 4; t++)
            {
                int breach = WallDamageRules.BreachTile(12345u, t, 28);
                Assert.That(breach, Is.InRange(1, 26), "양 끝 칸은 피한다");
            }

            // 면마다 다르다
            var rubbleD = new List<WallDamageRules.RubblePiece>(); var spotsD = new List<WallDamageRules.Spot>();
            WallDamageRules.Place(12345u, 2, from, to, MerlonSpots, rubbleD, spotsD);
            CollectionAssert.AreNotEqual(rubbleA, rubbleD);
        }

        [Test]
        public void Placement_StaysInsideWall_AndGrowsWithStage()
        {
            float from = BattleWallLayout.From(Half), to = BattleWallLayout.To(Half);
            var rubble = new List<WallDamageRules.RubblePiece>(); var spots = new List<WallDamageRules.Spot>();
            WallDamageRules.Place(4242u, 0, from, to, MerlonSpots, rubble, spots);
            int[] visible = new int[4];
            foreach (var piece in rubble)
            {
                Assert.That(piece.Along, Is.InRange(from - 1f, to + 1f), "모서리 발판 밖으로 나가지 않는다");
                Assert.That(piece.MinStage, Is.InRange(1, 3));
                Assert.That(piece.Size, Is.InRange(0, 2));
                Assert.That(piece.Variant, Is.InRange(0, 2));
                for (int s = piece.MinStage; s <= 3; s++) visible[s]++;
            }

            Assert.AreEqual(0, visible[0]);
            Assert.Less(visible[0], visible[1]);
            Assert.LessOrEqual(visible[1], visible[2]);
            Assert.Less(visible[2], visible[3], "3단계에는 파공 잔해가 더 쌓인다");

            int fires = 0;
            foreach (var spot in spots)
            {
                if (spot.Fire) { fires++; Assert.AreEqual(3, spot.MinStage, "불꽃은 파공 단계에만"); }
                Assert.That(spot.Along, Is.InRange(from - 1f, to + 1f));
            }

            Assert.LessOrEqual(fires, 2, "불꽃은 작게, 둘까지");
        }

        [Test]
        public void ArtFiles_Exist_WithCodexSizes()
        {
            for (int i = 0; i < 28; i++)
            {
                string name = BattleWallDamageView.SourceName(i);
                var tex = Resources.Load<Texture2D>("BattleArt/WallDamage/" + name);
                Assert.IsNotNull(tex, name);
                Assert.AreEqual(FilterMode.Point, tex.filterMode, name);
                Assert.IsTrue(tex.isReadable, name + " 실행 중 그림판으로 합친다");
                if (name.StartsWith("wall_stage")) Assert.AreEqual(128, tex.width, name);
                else if (name.EndsWith("_6f")) { Assert.AreEqual(576, tex.width, name); Assert.AreEqual(128, tex.height, name); }
                else { Assert.AreEqual(96, tex.width, name); Assert.AreEqual(128, tex.height, name); }
            }
        }

        [Test]
        public void MissingArt_FallsBackToOldMerlons_WithoutErrors()
        {
            var root = new GameObject("W39 root");
            var cubes = MakeCubes(out List<(Renderer, float)>[] pieces);
            try
            {
                using (var view = new BattleWallDamageView(root.transform, 77u, Half, BattleWorldView.Drafts[1], pieces, name => null))
                {
                    view.SetStage(0, 2, false);
                    view.SetStage(0, 3, false);
                    view.SetNight(true);
                    view.Tick(1f, Vector3.zero);
                    Assert.IsFalse(view.HasArt, "그림이 없다");
                    Assert.AreEqual(3, view.StageOf(0), "단계는 기억한다");
                    Assert.AreEqual(0, view.Rebuilds, "그림이 없으면 아무것도 쌓지 않는다");
                    for (int k = 0; k < view.MerlonCount(0); k++) Assert.IsTrue(view.MerlonBoxVisible(0, k), "옛 3D 돌출부 그대로");
                    Assert.AreEqual(0, view.StaticDrawCalls);
                }
            }
            finally { foreach (var c in cubes) Object.DestroyImmediate(c); Object.DestroyImmediate(root); }
        }

        [Test]
        public void WithArt_StagesBuildFewDrawCalls_HideBrokenMerlons_AndPuffDustOnce()
        {
            var root = new GameObject("W39 root");
            var cubes = MakeCubes(out List<(Renderer, float)>[] pieces);
            try
            {
                using (var view = new BattleWallDamageView(root.transform, 20261026u, Half, BattleWorldView.Drafts[1], pieces))
                {
                    Assert.IsFalse(view.HasArt, "멀쩡하면 그림을 읽지 않는다");
                    view.SetStage(0, 1, true);
                    Assert.IsTrue(view.HasArt, "처음 손상이 생길 때 읽는다");
                    Assert.AreEqual(0, view.ActiveDust, "처음 받은 단계는 먼지 없음");

                    int visible1 = view.VisibleRubble(0);
                    view.SetStage(0, 2, false);
                    Assert.GreaterOrEqual(view.VisibleRubble(0), visible1);
                    Assert.Greater(view.ActiveDust, 0, "단계가 바뀌면 먼지");
                    int broken = 0;
                    for (int k = 0; k < view.MerlonCount(0); k++)
                    {
                        bool intact = view.MerlonStateOf(0, k) == WallDamageRules.MerlonState.Intact;
                        Assert.AreEqual(intact, view.MerlonBoxVisible(0, k), "멀쩡한 돌출부만 3D 돌이 보인다");
                        if (!intact) broken++;
                    }

                    Assert.Greater(broken, 0, "2단계에는 상한 돌출부가 있다");
                    view.SetStage(0, 3, false);
                    view.SetStage(1, 3, false);
                    Assert.LessOrEqual(view.StaticDrawCalls, 3, "정적 그림은 그리기 3번 이하 (그림판 소품 + 외벽 데칼 + 파공 구멍)");

                    // 먼지는 0.6초 뒤 끝난다
                    float now = Time.unscaledTime;
                    view.Tick(now + 0.1f, Vector3.zero);
                    Assert.Greater(view.ActiveDust, 0);
                    view.Tick(now + 1.2f, Vector3.zero);
                    Assert.AreEqual(0, view.ActiveDust, "먼지는 한 번 재생하고 사라진다");

                    // 밤에만 연기·불꽃
                    Assert.AreEqual(0, view.ActiveSpots, "낮에는 없다");
                    view.SetNight(true);
                    Assert.Greater(view.ActiveSpots, 0, "밤에 심하게 부서진 곳에 연기·불꽃");
                    int rebuilds = view.Rebuilds;
                    view.Tick(now + 2f, Vector3.zero);
                    view.Tick(now + 2.1f, new Vector3(5f, 5f, 5f));
                    Assert.AreEqual(rebuilds, view.Rebuilds, "프레임마다 메시를 다시 쌓지 않는다");
                    view.SetNight(false);
                    Assert.AreEqual(0, view.ActiveSpots);

                    // 고쳐서 0단계로 돌아가면 돌출부 3D 돌이 다시 보인다
                    view.SetStage(0, 0, false);
                    for (int k = 0; k < view.MerlonCount(0); k++) Assert.IsTrue(view.MerlonBoxVisible(0, k));
                }
            }
            finally { foreach (var c in cubes) Object.DestroyImmediate(c); Object.DestroyImmediate(root); }
        }

        [Test]
        public void WorldView_FollowsWallHealth_ThroughAllFourStages()
        {
            using (var sim = MakeSim())
            {
                var parent = new GameObject("W39 parent");
                BattleWorldView world = null;
                try
                {
                    world = new BattleWorldView(sim, parent.transform);
                    Assert.IsNotNull(world.Damage);
                    Assert.IsFalse(world.Damage.HasArt, "시작은 멀쩡 — 그림을 읽지 않는다");
                    world.SetWallHealth(0, 1f);
                    Assert.AreEqual(0, world.DamageStage(0));
                    world.SetWallHealth(0, 0.5f);
                    Assert.AreEqual(1, world.DamageStage(0));
                    Assert.AreEqual(1, world.Damage.StageOf(0));
                    world.SetWallHealth(0, 0.2f);
                    Assert.AreEqual(2, world.DamageStage(0));
                    world.SetWallHealth(0, 0f);
                    Assert.AreEqual(3, world.DamageStage(0), "체력 0 = 파공");
                    Assert.AreEqual(3, world.Damage.StageOf(0));
                    Assert.AreEqual(0, world.DamageStage(1), "다른 면은 그대로");
                    Assert.IsTrue(world.Damage.HasArt);
                    Assert.Greater(world.Damage.MerlonCount(0), 0, "돌출부 3D 조각을 모아 둔다");
                    world.SetWallHealth(0, 1f);
                    Assert.AreEqual(0, world.DamageStage(0), "수리하면 되돌아간다");
                }
                finally { world?.Dispose(); Object.DestroyImmediate(parent); }
            }
        }

        static List<GameObject> MakeCubes(out List<(Renderer, float)>[] pieces)
        {
            var cubes = new List<GameObject>();
            pieces = new List<(Renderer, float)>[4];
            for (int s = 0; s < 4; s++)
            {
                pieces[s] = new List<(Renderer, float)>();
                foreach (float m in MerlonSpots)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cubes.Add(cube);
                    pieces[s].Add((cube.GetComponent<Renderer>(), m));
                }
            }

            return cubes;
        }

        static BattleSim MakeSim()
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_wall_damage_w39", out string error);
            Assert.IsNull(error);
            return new BattleSim(setup, lookup);
        }
    }
}
