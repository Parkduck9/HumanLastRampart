using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TestTools;

// W12: 새 게임 → 첫 이야기(대본 1장) → 요새 1 도착 이야기 → 전투 순서 확인 + 첫 이야기 사진 (1920×1080)
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator BeaconStoryChain()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Click("게임 시작", 600);
        yield return Click("쉬움", 300);
        yield return WaitScreen(ScreenId.Story, 600);

        var seen = new System.Collections.Generic.List<string>();
        int presses = 0;
        bool photo = false;
        int hagenShots = 0;
        string lastHagenText = "";
        while (GameFlow.Instance.CurrentScreen == ScreenId.Story && presses < 80)
        {
            string id = GameFlow.Instance.Campaign?.NextStory?.StoryId;
            if (id != null && (seen.Count == 0 || seen[seen.Count - 1] != id)) { seen.Add(id); Note("이야기 시작: " + id); }
            for (int i = 0; i < 400 && StoryText().Length < 2 && GameFlow.Instance.CurrentScreen == ScreenId.Story; i++) yield return null;
            if (GameFlow.Instance.CurrentScreen != ScreenId.Story) break;
            for (int i = 0; i < 60 && SpeakerText() == "" && id == "story_intro" && StoryText().Contains("수십") == false; i++) yield return null;
            Button advance = FindAdvance();
            if (advance == null) { yield return null; continue; }
            advance.onClick.Invoke();   // 끝까지 보이게
            yield return null;
            for (int i = 0; i < 4; i++) yield return null;
            Note($"{SpeakerText()} — {StoryText()}");
            if (!photo && id == "story_intro" && SpeakerText() == "루멘")
            {
                photo = true;
                yield return Capture("story_intro_lumen", 1920, 1080);
            }

            string lineText = StoryText();
            if (id == "story_intro" && SpeakerText() == "하겐" && lineText != lastHagenText)   // K-111 하겐 표정 사진 (평상·피로·결의) — 대사 줄이 바뀔 때만
            {
                lastHagenText = lineText;
                yield return Capture("story_intro_hagen_" + hagenShots++, 1920, 1080);
            }

            advance = FindAdvance();
            if (advance != null) advance.onClick.Invoke();
            presses += 2;
            yield return null;
        }

        File.WriteAllText(Path.Combine(Out, "story_chain.txt"), log.ToString(), new UTF8Encoding(false));
        CollectionAssert.AreEqual(new[] { "story_intro", "story_fortress1_arrive" }, seen, "첫 이야기 뒤에 요새 1 도착 이야기");
        Assert.IsTrue(photo, "사진");
        yield return WaitScreen(ScreenId.Battle, 900);
        Time.captureDeltaTime = 0f;
    }
}