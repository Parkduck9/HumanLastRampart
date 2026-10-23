using System.Collections;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Audio;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed partial class PlayProbe
{
    // 소리 (W24): 소리 은행이 비어 있어도 전투가 오류·경고 없이 돌고, 서비스는 아무 소리도 내지 않는다
    [UnityTest]
    public IEnumerator AudioEmptyBank()
    {
        Time.captureDeltaTime = 1f / 20f;
        try
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press("게임 시작", 600);
            yield return Press("쉬움", 300);
            yield return WaitScreen(ScreenId.Story, 600);

            var story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>();
            for (int i = 0; i < 300 && story == null; i++) { yield return null; story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); }
            Assert.IsNotNull(story, "스토리 재생기");
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);

            var entry = Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
            var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry)
                .GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
            entry.StartBattle();
            yield return Press("×4", 120);
            for (int i = 0; i < 6000 && sim.Elapsed < 20f && !sim.Ended; i++) yield return null;

            AudioService service = AudioService.Instance;
            Assert.IsNotNull(service, "소리 서비스가 만들어짐");
            Assert.IsFalse(service.HasAnySound, "은행이 비어 있다");
            Assert.AreEqual(0, service.StartedCount, "은행이 비면 아무 소리도 시작하지 않는다");
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Time.captureDeltaTime = 0f;
        }
    }
}
