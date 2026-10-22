using System.Collections;
using System.IO;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.BeaconMap;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// 봉화선 지도 (W10): 요새 1을 점화한 뒤 결과 → 지도 → 정비, 정비의 작은 단추로 보기 전용 지도.
// 사진 1920×1080 (HLR_PLAY_OUT/NN_beacon_map_*.png): 점화 직후 / 정비에서 연 보기 전용
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator BeaconMapAfterFortressRelit()
    {
        Directory.CreateDirectory(Out); log = new StringBuilder();
        bool keep = KeepPreparation; KeepPreparation = true;
        try
        {
            Screen.SetResolution(1920, 1080, false);
            SceneManager.LoadScene("Boot"); yield return null; yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press("게임 시작", 600); yield return Press("쉬움", 300);
            yield return WaitScreen(ScreenId.Story, 600);
            Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>().Skip();
            yield return WaitScreen(ScreenId.Battle, 900);
            yield return Press("[시험] 승리", 300);
            yield return WaitScreen(ScreenId.Result, 900);

            // 요새 1 점화를 진행 데이터에 직접 적는다 (결과 화면이 이미 반영한 뒤의 모습: 번호는 2, 수비대 14명)
            CampaignState campaign = GameFlow.Instance.Campaign;
            campaign.LastNightRelit = true;
            campaign.FortressNumber = 2; campaign.NightNumber = 1; campaign.BeaconRepair = 0f;
            campaign.Garrisons.Add(new GarrisonState { FortressNumber = 1, Count = 14 });
            yield return Press("다음", 300);

            yield return WaitScreen(ScreenId.BeaconMap, 900);
            var entry = Object.FindAnyObjectByType<BeaconMapEntry>();
            Assert.IsNotNull(entry, "봉화선 지도 진입 컴포넌트");
            yield return WaitMapIntro(entry);
            Note($"지도 경과 {entry.Diorama.Elapsed:0.00}초, 프레임 간격 {Time.unscaledDeltaTime:0.0000}, captureDeltaTime {Time.captureDeltaTime}, 해상도 {Screen.width}x{Screen.height}");
            Assert.IsTrue(entry.Diorama.IntroDone, "지도 도입 연출이 끝나지 않음");
            Assert.IsFalse(entry.Model.Preview, "점화 직후는 보기 전용이 아니다");
            Assert.AreEqual(1, entry.Diorama.LightCount, "점화한 요새 하나 = 진짜 점광원 하나");
            Assert.AreEqual("요새 1 · 다음: 요새 2", entry.Model.Subtitle);
            Assert.IsNotNull(FindReady("계속"), "계속 단추");
            yield return Capture("beacon_map_relit", 1920, 1080);
            AssertNightReadable($"{shot:00}_beacon_map_relit.png");

            int goldBefore = campaign.Gold;
            yield return Press("계속", 300);
            yield return WaitScreen(ScreenId.Prep, 900);
            Assert.AreEqual(2, GameFlow.Instance.Campaign.FortressNumber);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.IsNull(GameObject.Find("Beacon Diorama"), "지도를 닫으면 모형 정리");

            // 정비의 작은 단추 → 보기 전용 지도 → 돌아가기 → 정비
            int goldInPrep = GameFlow.Instance.Campaign.Gold;
            yield return Press("봉화선 지도", 300);
            yield return WaitScreen(ScreenId.BeaconMap, 900);
            entry = Object.FindAnyObjectByType<BeaconMapEntry>();
            Assert.IsTrue(entry.Model.Preview, "정비에서 연 지도는 보기 전용");
            Assert.AreEqual("요새 2 · 마지막 요새", entry.Model.Subtitle);
            yield return WaitMapIntro(entry);
            Assert.IsNotNull(FindReady("돌아가기"), "돌아가기 단추");
            yield return Capture("beacon_map_preview", 1920, 1080);
            yield return Press("돌아가기", 300);
            yield return WaitScreen(ScreenId.Prep, 900);
            Assert.AreEqual(goldInPrep, GameFlow.Instance.Campaign.Gold, "보기만 했으니 낮 지원금을 또 받지 않는다");
            Assert.Greater(GameFlow.Instance.Campaign.LastSupportGold, 0, "수비대 지원금이 있어야 이 검사가 의미 있다");
            Assert.AreEqual(goldBefore + GameFlow.Instance.Campaign.LastSupportGold, goldInPrep, "지도 → 정비 길에서 낮 지원금은 정확히 한 번");
            Note($"점화 직후·정비 보기 전용 지도 사진, 골드 {goldBefore} → {goldInPrep} → {GameFlow.Instance.Campaign.Gold}");
        }
        finally
        {
            KeepPreparation = keep;
            File.WriteAllText(Path.Combine(Out, "beacon_map.txt"), log.ToString(), new UTF8Encoding(false));
        }
    }

    // 정비 튜토리얼이 도는 동안은 지도 단추가 잠기고, 마친 뒤 지도를 보고 와도 튜토리얼이 다시 시작되지 않는다
    [UnityTest]
    public IEnumerator BeaconMapButtonLockedDuringPrepTutorial()
    {
        Directory.CreateDirectory(Out); log = new StringBuilder();
        bool keep = KeepPreparation; KeepPreparation = true;
        var oldStore = HumanLastRampart.Tutorial.TutorialState.Store;
        bool oldSuppressed = HumanLastRampart.Tutorial.TutorialState.Suppressed;
        try
        {
            HumanLastRampart.Tutorial.TutorialState.Store = new HumanLastRampart.Tutorial.MemoryTutorialStore();
            HumanLastRampart.Tutorial.TutorialState.Store.Save(HumanLastRampart.Tutorial.TutorialState.BattleSteps);   // 전투 구간은 이미 마친 것으로
            HumanLastRampart.Tutorial.TutorialState.Suppressed = false;
            SceneManager.LoadScene("Boot"); yield return null; yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press("게임 시작", 600); yield return Press("쉬움", 300);
            yield return WaitScreen(ScreenId.Story, 600);
            Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>().Skip();
            yield return WaitScreen(ScreenId.Battle, 900);
            yield return Press("[시험] 승리", 300);
            yield return WaitScreen(ScreenId.Result, 900);
            yield return Press("다음", 300);
            yield return WaitScreen(ScreenId.Prep, 900);
            for (int i = 0; i < 10; i++) yield return null;

            var director = Object.FindAnyObjectByType<HumanLastRampart.Tutorial.TutorialDirector>();
            Assert.IsNotNull(director, "정비 튜토리얼이 시작되어야 이 검사가 의미 있다");
            Assert.IsFalse(director.Finished);
            Assert.IsNull(FindReady("봉화선 지도"), "튜토리얼 중에는 지도 단추가 잠긴다");

            director.Machine.Skip();
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue(director.Finished);
            Assert.IsNotNull(FindReady("봉화선 지도"), "튜토리얼이 끝나면 열린다");
            yield return Press("봉화선 지도", 300);
            yield return WaitScreen(ScreenId.BeaconMap, 900);
            yield return Press("돌아가기", 300);
            yield return WaitScreen(ScreenId.Prep, 900);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsNull(Object.FindAnyObjectByType<HumanLastRampart.Tutorial.TutorialDirector>(), "지도를 보고 와도 정비 튜토리얼은 다시 시작되지 않는다");
            Note("정비 튜토리얼 중 지도 단추 잠김, 끝난 뒤 열림, 지도 다녀와도 재시작 없음");
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Store = oldStore;
            HumanLastRampart.Tutorial.TutorialState.Suppressed = oldSuppressed;
            KeepPreparation = keep;
            File.WriteAllText(Path.Combine(Out, "beacon_map_tutorial.txt"), log.ToString(), new UTF8Encoding(false));
        }
    }
    // 도입 연출(실시간 약 2초)이 끝날 때까지. 배치 실행은 프레임이 매우 빨라 프레임 수가 아니라 실제 시간으로 기다린다
    static IEnumerator WaitMapIntro(BeaconMapEntry entry)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!entry.Diorama.IntroDone && watch.Elapsed.TotalSeconds < 12) yield return null;
        for (int i = 0; i < 20; i++) yield return null;
    }

    // 밤이 어둡고 읽혀야 한다: 평균 밝기가 낮되 완전한 검정은 아니고, 따뜻한 밝은 점(불빛)이 있다
    void AssertNightReadable(string file)
    {
        string path = Path.Combine(Out, file);
        if (!File.Exists(path)) return;   // 그래픽 없는 실행
        var texture = new Texture2D(2, 2);
        try
        {
            texture.LoadImage(File.ReadAllBytes(path));
            Color32[] pixels = texture.GetPixels32();
            double sum = 0; int bright = 0, warm = 0, dark = 0;
            foreach (Color32 p in pixels)
            {
                float lum = (0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b) / 255f;
                sum += lum;
                if (lum > 0.55f) bright++;
                if (lum < 0.02f) dark++;
                if (p.r > 150 && p.r > p.b + 40) warm++;
            }

            double mean = sum / pixels.Length;
            Note($"밝기 평균 {mean:0.000}, 밝은 화소 {bright * 100.0 / pixels.Length:0.00}%, 거의 검정 {dark * 100.0 / pixels.Length:0.0}%, 따뜻한 밝은 화소 {warm}");
            Assert.Less(mean, 0.30, "밤이 어두워야 한다 (회색 안개막 금지)");
            Assert.Greater(mean, 0.02, "너무 어두워 읽히지 않는다");
            Assert.Greater(warm, 200, "점화한 요새의 따뜻한 불빛이 보여야 한다");
        }
        finally { Object.Destroy(texture); }
    }
}
