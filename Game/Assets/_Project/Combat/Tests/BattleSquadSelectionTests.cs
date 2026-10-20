using System.Collections.Generic;
using System.Reflection;
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
    // 지도 상자 선택과 수량 명령: 화면 자리·전달 경로·잔류 병력을 함께 검사한다
    public sealed class BattleSquadSelectionTests
    {
        static BattleSim NewSim(int northGroups, bool messenger = false)
        {
            var catalogs = new List<DefinitionCatalog>();
            foreach (string guid in AssetDatabase.FindAssets("t:DefinitionCatalog"))
                catalogs.Add(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
            DefinitionLookup lookup = DefinitionLookup.Build(catalogs);
            CampaignState campaign = CampaignFactory.Create(lookup, new NewGameOptions(Difficulty.Easy), 11);
            BattleSetup setup = BattleSetupFactory.Create(campaign, lookup, "run_squad_selection_test", out string error);
            Assert.IsNull(error);
            SquadState north = setup.Squads.Find(s => lookup.Get<PostDef>(s.PostId)?.Wall == WallSide.North
                && lookup.Get<UnitDef>(s.UnitDefId)?.Kind == UnitKind.Archer);
            Assert.IsNotNull(north, "북쪽 궁수 시험 분대");
            foreach (SquadState squad in setup.Squads) squad.Count = squad == north ? northGroups * BattleSim.GroupSize : 0;
            if (messenger)
            {
                SquadState south = setup.Squads.Find(s => lookup.Get<PostDef>(s.PostId)?.Wall == WallSide.South
                    && lookup.Get<UnitDef>(s.UnitDefId)?.Kind == UnitKind.Archer);
                Assert.IsNotNull(south, "남쪽 전령 분대");
                south.Count = 10;
            }
            return new BattleSim(setup, lookup);
        }

        static void Set(BattleEntry entry, string name, object value) => typeof(BattleEntry)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entry, value);

        // 적의 공격과 무관하게 실제 전령·묶음 도착 갱신을 진행해 수량 전달만 검사한다
        static void Step(BattleSim sim)
        {
            const float dt = 0.05f;
            typeof(BattleSim).GetMethod("UpdateMessengers", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sim, new object[] { dt });
            typeof(BattleSim).GetMethod("UpdateGroupMoves", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sim, new object[] { dt });
        }

        [Test]
        public void ScreenRect_SelectsMarkers_AndEntrySendsAllSelected()
        {
            using (BattleSim sim = NewSim(7))
            {
                var owner = new GameObject("상자 선택 시험");
                BattleEntry entry = owner.AddComponent<BattleEntry>();
                var cameraObject = new GameObject("지도 시험 카메라", typeof(Camera));
                cameraObject.transform.SetParent(owner.transform);
                Camera camera = cameraObject.GetComponent<Camera>();
                new BattleMapCamera(camera, sim.CastleHalf, sim.FieldSize).OpenCastle();
                var hud = new BattleHud(owner.transform, "", () => { }, s => { }, new System.Action[0], new string[0]);
                Set(entry, "sim", sim);
                Set(entry, "hud", hud);
                Set(entry, "battleCamera", camera);
                Set(entry, "preparing", true);
                try
                {
                    hud.SetMapMode(true, true);
                    int before = sim.Sides[0].GarrisonAlive, east = sim.Sides[1].GarrisonAlive;
                    Assert.AreEqual(0, entry.SelectGroupsInScreenRect(new Rect(-100f, -100f, 1f, 1f)), "빈 상자는 선택 없음");
                    Assert.AreEqual(7, entry.SelectGroupsInScreenRect(new Rect(0f, 0f, Screen.width, Screen.height)), "화면 안 일곱 깃발");
                    entry.SendSelectedGroups(1);
                    Assert.AreEqual(0, sim.Sides[0].GarrisonAlive, "개별 선택은 기존 묶음 이동 규칙");
                    Assert.AreEqual(east + before, sim.Sides[1].GarrisonAlive, "준비 중엔 동쪽에 바로 전달");
                    for (int g = 0; g < sim.GroupCount; g++) Assert.AreEqual(1, sim.GetGroup(g).Side);
                }
                finally
                {
                    Set(entry, "sim", null);
                    Set(entry, "hud", null);
                    Object.DestroyImmediate(owner);
                }
            }
        }

        [TestCase(7, 4)]
        [TestCase(5, 3)]
        [TestCase(3, 1)]
        public void HalfChoice_RoundsUp_AndKeepsMinimum(int groups, int expected)
        {
            using (BattleSim sim = NewSim(groups))
            {
                int count = sim.WallMoveGroupCount(0, 1);
                Assert.AreEqual((groups + 1) / 2, count, "절반은 묶음 수 올림");
                int east = sim.Sides[1].GarrisonAlive;
                sim.IssueWallMove(0, 1, true, count);
                Assert.AreEqual(east + expected * 10, sim.Sides[1].GarrisonAlive);
                Assert.AreEqual((groups - expected) * 10, sim.Sides[0].GarrisonAlive);
                Assert.GreaterOrEqual(sim.Sides[0].GarrisonAlive, BattleSim.KeepPerWall);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CountOrder_DirectOrMessenger_DeliversOnlyRequestedGroups(bool messenger)
        {
            using (BattleSim sim = NewSim(7, messenger))
            {
                int count = sim.WallMoveGroupCount(0, 1);
                int east = sim.Sides[1].GarrisonAlive;
                if (messenger) sim.IssueWallMoveByMessenger(2, 0f, 0, 1, count);
                else sim.IssueWallMove(0, 1, false, count);
                Assert.AreEqual(east, sim.Sides[1].GarrisonAlive, "도착 전에는 동쪽 병력이 늘지 않는다");
                Assert.AreEqual(messenger ? 70 : 30, sim.Sides[0].GarrisonAlive, "다른 성벽의 전령 명령은 전달 전에 출발하지 않는다");
                for (int i = 0; i < 1800 && sim.Sides[1].GarrisonAlive < east + 40 && !sim.Ended; i++) Step(sim);
                Assert.IsFalse(sim.Ended, "이동 검사가 끝나기 전에 패배하지 않는다");
                Assert.AreEqual(east + 40, sim.Sides[1].GarrisonAlive, "전달 뒤 네 묶음만 동쪽 도착");
                Assert.AreEqual(30, sim.Sides[0].GarrisonAlive, "북쪽 세 묶음 잔류");
            }
        }

        [TestCase(0, 10)]
        [TestCase(2, 50)]
        public void OneAndAllChoices_UseExistingWallMinimum(int choice, int expected)
        {
            using (BattleSim sim = NewSim(7))
            {
                sim.IssueWallMove(0, 1, true, sim.WallMoveGroupCount(0, choice));
                Assert.AreEqual(expected, sim.Sides[1].GarrisonAlive);
                Assert.AreEqual(70 - expected, sim.Sides[0].GarrisonAlive);
            }
        }

        [Test]
        public void WallBand_RejectsFieldAndCourtyard_AndCameraKeepsNorthUp()
        {
            const float half = 20f;
            Assert.AreEqual(0, BattleEntry.WallAtPoint(new Vector3(0f, 0f, 19f), half));
            Assert.AreEqual(1, BattleEntry.WallAtPoint(new Vector3(19f, 0f, 0f), half));
            Assert.AreEqual(2, BattleEntry.WallAtPoint(new Vector3(0f, 0f, -19f), half));
            Assert.AreEqual(3, BattleEntry.WallAtPoint(new Vector3(-19f, 0f, 0f), half));
            Assert.AreEqual(-1, BattleEntry.WallAtPoint(Vector3.zero, half));
            Assert.AreEqual(-1, BattleEntry.WallAtPoint(new Vector3(0f, 0f, 25f), half));
            var go = new GameObject("지도 방향 시험", typeof(Camera));
            try
            {
                Camera camera = go.GetComponent<Camera>();
                new BattleMapCamera(camera, half, 160f).OpenCastle();
                Vector3 center = camera.WorldToScreenPoint(Vector3.zero);
                Assert.Greater(camera.WorldToScreenPoint(Vector3.forward * half).y, center.y);
                Assert.Greater(camera.WorldToScreenPoint(Vector3.right * half).x, center.x);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
