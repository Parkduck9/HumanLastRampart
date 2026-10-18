using System.Collections.Generic;
using System.IO;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Save;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Core.Tests
{
    // 진행 저장·불러오기 (K-062): 저장 → 불러오기가 같은 상태, null 필드 유지, 판 번호, 깨진 파일
    public sealed class CampaignSaveTests
    {
        string folder;
        string previousFolder;

        [SetUp]
        public void SetUp()
        {
            previousFolder = CampaignSave.Folder;
            folder = Path.Combine(Path.GetTempPath(), "hlr_save_test_" + System.Guid.NewGuid().ToString("N"));
            CampaignSave.Folder = folder;
        }

        [TearDown]
        public void TearDown()
        {
            CampaignSave.Folder = previousFolder;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        static CampaignState Sample()
        {
            var c = new CampaignState { BattleNumber = 3, Gold = 1234, Difficulty = Difficulty.Realistic, RngSeed = -98765, TrapShopRestockedForBattle = 3, NextInstanceNumber = 41 };
            foreach (string id in WallIds.All) c.Wall.Add(new WallSegmentState { SegmentId = id, MaxHp = 10000, Hp = 7321 });
            c.Heroes.Add(new HeroState { InstanceId = "hero_1", UnitDefId = "unit_hero", Hp = 88, PostId = null });
            c.Squads.Add(new SquadState { SquadId = "squad_2", UnitDefId = "unit_archer", Count = 17, PostId = "post_north_squad_1" });
            c.Traps.Add(new TrapPlacement { TrapId = "trap_3", TrapDefId = "trap_bomb", TerrainId = "terrain_1", SlotId = "cell_0_2_3", ActualX = 1.5f, ActualZ = 40.25f, Marked = true, MarkX = 1f, MarkZ = 40f });
            c.TrapStock.Add(new TrapStockEntry { TrapDefId = "trap_bomb", Count = 2 });
            c.TerrainWorks.Add(new TerrainWork { TerrainId = "terrain_1", CellId = "cell_1_0_4", Kind = TerrainWorkKind.EarthWall });
            c.NextStory = new StoryRequest { Kind = StoryKind.Normal, StoryId = "story_intro" };
            c.LastBattle = new BattleResult { BattleRunId = "run_x", BattleNumber = 2, Outcome = BattleOutcome.Victory, EarnedGold = 50 };
            c.ActiveBattleSetup = null;   // 정비에서는 비어 있을 수 있다 — 불러와도 null이어야 한다
            c.LastAppliedBattleRunId = "run_x";
            return c;
        }

        [Test]
        public void SaveThenLoad_SameState()
        {
            CampaignState original = Sample();
            Assert.IsTrue(CampaignSave.Save(original, ScreenId.Prep, out string error), error);
            Assert.IsTrue(CampaignSave.HasSave);
            Assert.IsTrue(CampaignSave.TryLoad(out CampaignState loaded, out ScreenId resume, out error), error);
            Assert.AreEqual(ScreenId.Prep, resume);
            // 필드 목록을 손으로 비교하지 않고 같은 직렬화 결과인지 본다 (필드가 늘어도 그대로 검사)
            Assert.AreEqual(JsonUtility.ToJson(original), JsonUtility.ToJson(loaded), "저장 → 불러오기 같은 상태");
            Assert.IsNull(loaded.ActiveBattleSetup, "null이던 필드는 null 그대로 (빈 객체로 바뀌지 않음)");
            Assert.IsNotNull(loaded.LastBattle);
            Assert.AreEqual("story_intro", loaded.NextStory.StoryId);
            Assert.AreEqual(17, loaded.FindSquad("squad_2").Count);
            Assert.AreEqual("squad_42", loaded.NewInstanceId("squad"), "고유 ID 번호도 이어진다");
        }

        [Test]
        public void Peek_ShowsBattleAndDifficulty()
        {
            Assert.IsFalse(CampaignSave.TryPeek(out _), "저장 없음");
            CampaignSave.Save(Sample(), ScreenId.Prep, out _);
            Assert.IsTrue(CampaignSave.TryPeek(out CampaignSave.Summary summary));
            Assert.AreEqual(3, summary.BattleNumber);
            Assert.AreEqual(Difficulty.Realistic, summary.Difficulty);
            CampaignSave.Delete();
            Assert.IsFalse(CampaignSave.HasSave, "지우면 없음");
        }

        [Test]
        public void MissingNewField_GetsDefault()
        {
            // 예전 판 파일 흉내: TerrainWorks·Targeting이 없는 진행 JSON
            CampaignSave.Save(Sample(), ScreenId.Prep, out _);
            string text = File.ReadAllText(CampaignSave.SlotPath);
            var file = JsonUtility.FromJson<Wrapper>(text);
            var campaignJson = file.Campaign.Replace("\"TerrainWorks\"", "\"Removed_TerrainWorks\"").Replace("\"Targeting\"", "\"Removed_Targeting\"");
            file.Campaign = campaignJson;
            File.WriteAllText(CampaignSave.SlotPath, JsonUtility.ToJson(file));
            Assert.IsTrue(CampaignSave.TryLoad(out CampaignState loaded, out _, out string error), error);
            Assert.IsNotNull(loaded.TerrainWorks, "없는 목록은 기본값(빈 목록)");
            Assert.AreEqual(0, loaded.TerrainWorks.Count);
            Assert.IsNotNull(loaded.Targeting, "없는 객체는 기본값");
            Assert.AreEqual(1234, loaded.Gold);
        }

        [Test]
        public void NewerOrBrokenFile_Refused()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(CampaignSave.SlotPath, "{ 이건 JSON이 아님");
            Assert.IsFalse(CampaignSave.TryLoad(out _, out _, out string broken));
            Assert.IsNotEmpty(broken);

            CampaignSave.Save(Sample(), ScreenId.Prep, out _);
            var file = JsonUtility.FromJson<Wrapper>(File.ReadAllText(CampaignSave.SlotPath));
            file.FormatVersion = CampaignSave.FormatVersion + 1;
            File.WriteAllText(CampaignSave.SlotPath, JsonUtility.ToJson(file));
            Assert.IsFalse(CampaignSave.TryLoad(out _, out _, out string newer), "더 새 판은 읽지 않는다");
            StringAssert.Contains("더 새 판", newer);
        }

        // 저장 파일 머리 (CampaignSave.SaveFile과 같은 필드 이름)
        [System.Serializable]
        sealed class Wrapper
        {
            public int FormatVersion;
            public string Contract;
            public string SavedAtUtc;
            public int BattleNumber;
            public Difficulty Difficulty;
            public ScreenId ResumeScreen;
            public List<string> NullFields = new List<string>();
            public string Campaign;
        }
    }
}
