using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator InstalledCharacterArtBindings()
    {
        Directory.CreateDirectory(Out); log = new StringBuilder();
        bool keep = KeepPreparation; KeepPreparation = true;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object Field(object obj, string key) => obj.GetType().GetField(key, flags).GetValue(obj);
        try
        {
            SceneManager.LoadScene("Boot"); yield return null; yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press("게임 시작", 600); yield return Press("쉬움", 300);
            yield return WaitScreen(ScreenId.Story, 600);
            Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>().Skip();
            yield return WaitScreen(ScreenId.Battle, 900);
            var entry = Object.FindAnyObjectByType<BattleEntry>();
            var sim = (BattleSim)Field(entry, "sim");
            var view = (BattleView)Field(entry, "view");
            var hero = (MaterialPropertyBlock)Field(view, "heroBlock");
            Assert.AreEqual(1152, hero.GetTexture("_MainTex").width, "실제 주인공 최신 288칸");
            Assert.AreEqual(0.75f, hero.GetFloat("_Aspect"), 0.0001f);
            Assert.AreEqual(1f, hero.GetFloat("_HasNormal"));
            foreach (string key in new[] { "wizardBlock", "wizardCastBlock", "wizardRunBlock", "wizardFrontBlock", "heroCommandBlock", "heroIdleBlock" })
            {
                var block = (MaterialPropertyBlock)Field(view, key);
                Assert.IsNotNull(block, key);
                Assert.AreEqual(1f, block.GetFloat("_HasNormal"), key + " 실제 그리기 노멀 연결");
                Assert.IsNotNull(block.GetTexture("_NormalTex"));
            }
            foreach (string key in new[] { "enemyPoseDraws", "soldierPoseDraws" })
            {
                var draws = (List<(MaterialPropertyBlock block, int count)>)Field(view, key);
                foreach (var draw in draws) Assert.AreEqual(1f, draw.block.GetFloat("_HasNormal"), draw.block.GetTexture("_MainTex").name);
            }
            var rig = (BattleCameraRig)Field(entry, "rig");
            if (entry.MapOpen) entry.ToggleMap();
            rig.ShowSide(0); rig.SetPreset(0);
            for (int i = 0; i < 5; i++) yield return null;
            yield return Capture("characters_00_installed_battle", 1920, 1080);
            Assert.IsTrue(sim.CastSpell(0, new Vector3(sim.CastleHalf + 40f, 0f, 0f)), "다른 성벽 마법 요청");
            Assert.IsTrue(sim.WizardMoving, "마법사 이동 동작 선택 가능");
            Note("주인공 최신288칸 및 영웅/마법사4블록/병사·적동작 노멀 실제그리기연결 확인");
        }
        finally
        {
            KeepPreparation = keep;
            File.WriteAllText(Path.Combine(Out, "installed_character_art.txt"), log.ToString());
        }
    }
}
