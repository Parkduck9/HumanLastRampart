using System.Collections;
using System.IO;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// 우두머리 첫 예고 안내 두 쪽 사진 1920×1080 (W48): 점화의 밤에서 예고가 뜨면 전투가 멈추고 안내가 뜬다.
// 1쪽 사진 → [다음] → 2쪽 사진 → [다음] → 시간이 다시 흐르고, 같은 전투에서 다시 뜨지 않는다.
// 결과: HLR_PLAY_OUT/w48_boss_tip_page1.png, w48_boss_tip_page2.png, w48_boss_tip_probe.txt
public sealed partial class PlayProbe
{
    [UnityTest, Timeout(900000)]
    public IEnumerator W48BossTipShots()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType, "시각 촬영에는 그래픽 장치가 필요");
        Time.captureDeltaTime = 1f / 20f;
        KeepPreparation = true;
        bool suppressed = TutorialState.Suppressed;
        ITutorialStore oldMain = TutorialState.Store, oldTip = TutorialState.BossTipStore;
        TutorialState.Suppressed = false;
        TutorialState.Store = new MemoryTutorialStore();
        TutorialState.Store.Save(TutorialState.Total);   // 첫 전투 튜토리얼은 이미 마친 것으로 (우두머리 안내만 본다)
        TutorialState.BossTipStore = new MemoryTutorialStore();
        try
        {
            Screen.SetResolution(1920, 1080, false);
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press("게임 시작", 600);
            yield return Press("쉬움", 300);
            yield return WaitScreen(ScreenId.Story, 600);
            CampaignState campaign = GameFlow.Instance.Campaign;
            campaign.BeaconRepair = 100f;
            campaign.PlannedBeamSide = 2;
            var story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>();
            for (int i = 0; i < 300 && story == null; i++) { yield return null; story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); }
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);

            w43Entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
            BattleEntry entry = w43Entry;
            BattleSim sim = entry.Sim;
            Assert.IsTrue(sim.BossPresent, "우두머리가 이 전투에 있다");
            BattleSetup setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
            var forecast = setup.Forecast; forecast.InitialWeather = Weather.Clear; forecast.TimeOfDay = TimeOfDay.Night; forecast.Changes = false; setup.Forecast = forecast;
            sim.WeatherDirty = true;
            entry.StartBattle();
            for (int i = 0; i < 20; i++) yield return null;
            entry.Rig.ShowSide(sim.BossSide);
            var director = Object.FindAnyObjectByType<BossTipDirector>();
            Assert.IsNotNull(director, "우두머리 밤 · 안 본 저장 · 튜토리얼 켬 -> 안내가 달린다");
            Assert.IsFalse(director.Machine.Visible, "예고 전에는 안 보인다");
            Assert.IsFalse(Object.FindAnyObjectByType<TutorialDirector>(), "첫 전투 튜토리얼은 끼지 않는다");

            // 예고 직전까지 빠르게
            w43KeepHp = 1f;
            entry.TutorialTimeScale = 16f;
            int guard = 0;
            while (sim.Elapsed < BattleSim.BossSpawnSeconds - 7f && guard++ < 400) yield return W43Frames(1);
            entry.TutorialTimeScale = 1f;
            guard = 0;
            while (!director.Machine.Visible && guard++ < 400) yield return W43Frames(1);
            Assert.IsTrue(director.Machine.Visible, "예고가 뜨면 안내가 뜬다");
            Assert.IsTrue(sim.TryGetBossWarning(out _, out _), "예고 구간");
            Assert.AreEqual(0f, entry.TutorialTimeScale, "안내가 떠 있는 동안 전투가 멈춘다");
            float frozen = sim.Elapsed;
            yield return W43Frames(20);
            Assert.AreEqual(frozen, sim.Elapsed, 1e-4f, "멈춘 동안 시간이 안 간다");
            Note($"안내 1쪽: 예고 {sim.Elapsed:0.0}s, 저장 {TutorialState.BossTipSeen}");
            Assert.IsTrue(TutorialState.BossTipSeen, "저장됨");
            yield return W43Shot("w48_boss_tip_page1");

            Assert.IsTrue(director.Machine.Next());
            yield return W43Frames(6);
            Assert.AreEqual(1, director.Machine.Page);
            yield return W43Shot("w48_boss_tip_page2");

            Assert.IsTrue(director.Machine.Next());
            yield return W43Frames(30);
            Assert.AreEqual(1f, entry.TutorialTimeScale, "다 읽으면 전투가 다시 흐른다");
            Assert.Greater(sim.Elapsed, frozen, "시간이 다시 간다");
            Assert.IsNull(Object.FindAnyObjectByType<BossTipDirector>(), "다 보면 안내가 정리된다");
            Note("안내 끝, 전투 재개 " + sim.Elapsed.ToString("0.0") + "s");
        }
        finally
        {
            TutorialState.Suppressed = suppressed;
            TutorialState.Store = oldMain;
            TutorialState.BossTipStore = oldTip;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, "w48_boss_tip_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
