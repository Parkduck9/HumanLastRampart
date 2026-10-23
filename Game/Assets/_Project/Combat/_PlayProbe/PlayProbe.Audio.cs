using System.Collections;
using HumanLastRampart.App.Audio;
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
        yield return AudioBattle(false);
    }

    // 실제 소리 은행: 전투 20초 동안 오류 없이 소리가 실제로 시작된다 (StartedCount > 0)
    [UnityTest]
    public IEnumerator AudioRealBank()
    {
        yield return AudioBattle(true);
    }

    IEnumerator AudioBattle(bool realBank)
    {
        Time.captureDeltaTime = 1f / 20f;
        try
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);

            // 시험마다 서비스를 처음 상태로 다시 준비한다 (씬을 넘어 살아남는 서비스의 셈이 섞이지 않게)
            AudioService service = AudioService.Ensure();
            Assert.IsNotNull(service, "소리 서비스");
            service.Init(realBank ? Resources.Load<SoundBank>(SoundBank.ResourcePath) : null, AudioService.DesktopVoices);
            if (realBank) Assert.IsTrue(service.HasAnySound, "실제 은행에 소리 파일이 있다");

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

            if (realBank) Assert.Greater(service.StartedCount, 0, "실제 은행이면 소리가 시작된다 (뿔나팔·일제 사격 등)");
            else Assert.AreEqual(0, service.StartedCount, "은행이 비면 아무 소리도 시작하지 않는다");
            // 오류 로그가 있으면 시험이 자동으로 실패한다 (화면 전환 틈의 엔진 안내 '오디오 리스너 없음'은 오류가 아니라 허용)
        }
        finally
        {
            Time.captureDeltaTime = 0f;
        }
    }
}
