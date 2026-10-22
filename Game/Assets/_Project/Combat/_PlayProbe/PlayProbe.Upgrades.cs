using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// 강화·상점·용병 (K-058, D-079): 정비 마당 상점에서 사고 끼우기, 병영 훈련·용병 고용 → 다음 전투 준비에 들어가는지 + 사진
public sealed partial class PlayProbe
{
    static class UpgradeKeys
    {
        // K-047 열쇠 이름 (btn:…): 고르기 칸 → 황동 단추 하나
        public const string Shop = "상점", Lens = "물건:item_telescope_lens", Arrows = "물건:item_fire_arrows", Buy = "확정 구매";
        public const string Training = "병영:훈련", Archer = "병종:unit_archer", Train = "확정 훈련";
        public const string Mercenary = "병영:용병", ArcherMerc = "용병:merc_archer", Hire = "확정 고용";
    }

    [UnityTest]
    public IEnumerator UpgradeShop()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        try
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press(LoopKeys.NewGame, 600);
            yield return Press(LoopKeys.Easy, 300);
            yield return WaitScreen(ScreenId.Story, 600);
            HumanLastRampart.Story.StoryPlayer story = null;
            for (int i = 0; i < 300 && story == null; i++) { story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); if (story == null) yield return null; }
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);
            yield return Press(LoopKeys.TestWin, 300);
            yield return WaitScreen(ScreenId.Result, 900);
            yield return Press(LoopKeys.ResultNext, 300);
            yield return WaitScreen(ScreenId.Prep, 900);

            CampaignState c = GameFlow.Instance.Campaign;
            c.Gold += 5000;   // 시험용: 모두 사 볼 수 있게
            var yard = Object.FindAnyObjectByType<HumanLastRampart.Meta.PrepYard>();
            Assert.IsNotNull(yard, "정비 마당");

            // 상점: 망원경 렌즈(주인공 소지품 칸에 바로 끼움) + 불화살 꾸러미
            Assert.IsTrue(yard.Use(UpgradeKeys.Shop), "상점 장소");
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture("upgrade_00_shop");
            int gold0 = c.Gold;
            yield return Press(UpgradeKeys.Lens, 60);
            yield return Press(UpgradeKeys.Buy, 60);
            yield return Press(UpgradeKeys.Arrows, 60);
            yield return Press(UpgradeKeys.Buy, 60);
            OwnedItem lens = c.Items.FirstOrDefault(i => i.ItemId == "item_telescope_lens");
            Assert.IsNotNull(lens, "렌즈를 샀다");
            Assert.IsFalse(string.IsNullOrEmpty(lens.EquippedBy), "주인공 소지품 칸에 끼움");
            Assert.AreEqual(1, c.Items.Where(i => i.ItemId == "item_fire_arrows").Sum(i => i.Count), "불화살 1개");
            // 값은 데이터에서 읽는다 (2026-10-22 강화 값 내림 뒤 고정 숫자 400·200이 틀렸음)
            var defs = GameFlow.Instance.Definitions;
            int lensPrice = defs.Get<HumanLastRampart.Core.Definitions.ItemDef>("item_telescope_lens").Price;
            int arrowsPrice = defs.Get<HumanLastRampart.Core.Definitions.ItemDef>("item_fire_arrows").Price;
            Assert.AreEqual(gold0 - lensPrice - arrowsPrice, c.Gold, "값만큼 골드");
            yield return Capture("upgrade_01_shop_bought");
            yield return Press(LoopKeys.Close, 60);

            // 병영 → 병종 훈련: 궁수 1단계
            Assert.IsTrue(yard.Use(LoopKeys.Barracks), "병영 장소");
            yield return Press(UpgradeKeys.Training, 60);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture("upgrade_02_training");
            yield return Press(UpgradeKeys.Archer, 60);
            yield return Press(UpgradeKeys.Train, 60);
            Assert.AreEqual(1, c.Training.FirstOrDefault(t => t.UnitDefId == "unit_archer")?.Level ?? 0, "궁수 1단계");
            yield return Press(LoopKeys.Close, 60);

            // 병영 → 용병 고용: 북쪽에 궁수 용병 10명
            Assert.IsTrue(yard.Use(LoopKeys.Barracks), "병영 장소");
            yield return Press(UpgradeKeys.Mercenary, 60);
            yield return Press(UpgradeKeys.ArcherMerc, 60);
            yield return Press(UpgradeKeys.Hire, 60);   // 북쪽(첫 성벽 탭)·10명이 처음 값
            Assert.AreEqual(10, c.Mercenaries.Sum(m => m.Count), "용병 10명");
            yield return Capture("upgrade_03_mercenary");
            yield return Press(LoopKeys.Close, 60);
            int goldBeforeBattle = c.Gold;

            // 다음 전투: 급료가 빠지고, 강화·용병·소모품이 전투 준비에
            Assert.IsTrue(yard.Use(LoopKeys.Stairs), "계단 장소");
            yield return Press(LoopKeys.StartNextBattle, 60);
            yield return WaitScreen(ScreenId.Battle, 900);
            BattleSetup setup = c.ActiveBattleSetup;
            Assert.AreEqual(goldBeforeBattle - 10 * 3, c.Gold, "용병 급료 (궁수 3 × 10명)");
            Assert.AreEqual(1.4f, setup.Boosts.TelescopeRadiusMul, 1e-4f, "망원경 렌즈");
            Assert.AreEqual(1, setup.Boosts.UnitFor("unit_archer", false)?.Level ?? 0, "궁수 훈련 1단계");
            CollectionAssert.Contains(setup.ConsumedItemIds, "item_fire_arrows", "불화살을 이번 전투에 쓴다");
            Assert.AreEqual(10, setup.Mercenaries.Sum(m => m.Count), "용병이 전투 준비에");
            var entry = Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
            var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry).GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
            Assert.AreEqual(10, sim.MercenaryAlive(0), "북쪽에 용병");
            yield return Capture("upgrade_04_battle");
            Note($"강화: 골드 {c.Gold}, 아이템 {string.Join(",", c.Items.Select(i => i.ItemId + "×" + i.Count))}, 훈련 {string.Join(",", c.Training.Select(t => t.UnitDefId + t.Level))}, 용병 {c.Mercenaries.Sum(m => m.Count)}");
        }
        finally
        {
            File.WriteAllText(Path.Combine(Out, "upgrade_shop.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
