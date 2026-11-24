using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.BeaconMap;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// W30 화면 회귀 시험: 정해진 8장을 960×540 렌더 텍스처로 찍어 기준 사진(Combat/Tests/GoldenBaselines)과 견준다.
// 고정: 씨앗(TestSeedOverride), captureDeltaTime 1/20, 해상도, 프레임 수로만 기다림, 장면 사진은 표시판 없이(시계·HUD 제외).
// 기준 갱신: 환경 변수 HLR_UPDATE_BASELINES=1. 자세한 것은 GoldenBaselines/README.md
public sealed partial class PlayProbe
{
    const int GoldenW = 960, GoldenH = 540;

    sealed class GoldenShot { public string Name; public GoldenKind Kind; public string File; }

    IEnumerator GoldenCapture(List<GoldenShot> shots, string name, GoldenKind kind, bool sceneOnly)
    {
        yield return Capture("golden_" + name, GoldenW, GoldenH, sceneOnly);
        shots.Add(new GoldenShot { Name = name, Kind = kind, File = Path.Combine(Out, $"{shot:00}_golden_{name}.png") });
    }

    [UnityTest]
    public IEnumerator GoldenScreenshots()
    {
        Directory.CreateDirectory(Out); log = new StringBuilder();
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("그래픽 장치가 없어 화면 회귀 시험을 건너뜀");
        var shots = new List<GoldenShot>();
        Time.captureDeltaTime = 1f / 20f;
        bool keep = KeepPreparation; KeepPreparation = true;
        bool suppressed = HumanLastRampart.Tutorial.TutorialState.Suppressed;
        HumanLastRampart.Tutorial.TutorialState.Suppressed = true;   // 튜토리얼 안내 판이 찍히지 않게
        try
        {
            Screen.SetResolution(1280, 720, false);
            SceneManager.LoadScene("Boot"); yield return null; yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            for (int i = 0; i < 40; i++) yield return null;
            yield return GoldenCapture(shots, "main_menu", GoldenKind.Ui, false);

            yield return Press("게임 시작", 600); yield return Press("쉬움", 300);
            yield return WaitScreen(ScreenId.Story, 600);
            for (int i = 0; i < 100; i++) yield return null;   // 암전이 열리고 첫 줄이 나온 뒤 (프레임 수로 고정)
            yield return new WaitForSecondsRealtime(3f);          // 실제 시간으로 흐르는 암전·글자 연출이 끝나길 기다린다
            yield return GoldenCapture(shots, "story", GoldenKind.Ui, false);
            Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>().Skip();
            yield return WaitScreen(ScreenId.Battle, 900);

            var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
            BattleSim sim = entry.Sim;
            BattleCameraRig rig = entry.Rig;
            BattleSetup setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
            var f = setup.Forecast; f.InitialWeather = Weather.Clear; f.TimeOfDay = TimeOfDay.Day; f.Changes = false; setup.Forecast = f;
            sim.WeatherDirty = true;
            entry.StartBattle();
            for (int i = 0; i < 100; i++) yield return null;   // 게임 시간 5초
            entry.TutorialTimeScale = 0f;                       // 전투 시간을 멈춰 사진 사이에 흐르지 않게
            for (int i = 0; i < 5; i++) yield return null;
            yield return GoldenCapture(shots, "day_wall_top", GoldenKind.Day, true);

            // 밤으로 바꾸고 빛줄기 장면 (북쪽 성벽 기본 화면 + 마당·탑 전경)
            f = setup.Forecast; f.TimeOfDay = TimeOfDay.Night; setup.Forecast = f;
            sim.WeatherDirty = true;
            for (int i = 0; i < 20; i++) yield return null;
            yield return new WaitForSecondsRealtime(3f);          // 낮→밤 빛 전환(실제 시간)이 끝나길 기다린다
            rig.ShowSide(sim.BeamSide);
            for (int i = 0; i < 10; i++) yield return null;
            yield return GoldenCapture(shots, "night_wall_beam", GoldenKind.Night, true);

            Camera cam = entry.BattleCamera;
            float top = BattleSim.WallHeight;
            var pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;
            pin.Position = new Vector3(-24f, top + 7f, -30f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + BattleTowerView.Height * 0.45f, 0f) - pin.Position);
            for (int i = 0; i < 4; i++) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);
            yield return GoldenCapture(shots, "night_courtyard_tower", GoldenKind.Night, true);
            Object.Destroy(pin);
            entry.TutorialTimeScale = 1f;
            yield return null;

            // 결과 → 봉화선 지도 → 정비 (BeaconMapAfterFortressRelit과 같은 진행 데이터 조작)
            yield return Press("[시험] 승리", 300);
            yield return WaitScreen(ScreenId.Result, 900);
            for (int i = 0; i < 60; i++) yield return null;
            yield return GoldenCapture(shots, "result", GoldenKind.Ui, false);

            CampaignState campaign = GameFlow.Instance.Campaign;
            campaign.LastNightRelit = true;
            campaign.FortressNumber = 2; campaign.NightNumber = 1; campaign.BeaconRepair = 0f;
            campaign.Garrisons.Add(new GarrisonState { FortressNumber = 1, Count = 14 });
            yield return Press("다음", 300);
            yield return WaitScreen(ScreenId.BeaconMap, 900);
            var mapEntry = Object.FindAnyObjectByType<BeaconMapEntry>();
            for (int i = 0; i < 400 && !mapEntry.Diorama.IntroDone; i++) yield return null;   // 실제 시간이 아니라 프레임으로
            for (int i = 0; i < 40; i++) yield return null;
            yield return GoldenCapture(shots, "map", GoldenKind.Ui, false);

            yield return Press("계속", 300);
            yield return WaitScreen(ScreenId.Prep, 900);
            for (int i = 0; i < 60; i++) yield return null;
            yield return GoldenCapture(shots, "prep", GoldenKind.Ui, false);
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = keep;
            Time.captureDeltaTime = 0f;
        }

        // 사진을 다 찍은 뒤 한꺼번에 견준다 (한 장이 틀려도 나머지 결과가 남는다)
        var problems = new List<string>();
        foreach (GoldenShot s in shots)
        {
            if (!File.Exists(s.File)) { problems.Add($"{s.Name}: 사진 파일 없음 ({s.File})"); continue; }
            GoldenImage.Check(s.Name, s.File, s.Kind, problems, out string line);
            Note(line);
        }
        File.WriteAllText(Path.Combine(Out, "golden.txt"), log.ToString(), new UTF8Encoding(false));
        Assert.AreEqual(8, shots.Count, "찍어야 할 사진 수");
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }
}

// 비교기 자체 시험 (그래픽 필요 없음)
public class GoldenImageTests
{
    static Color32[] Fill(Color32 c) { var a = new Color32[GoldenImage.W * GoldenImage.H]; for (int i = 0; i < a.Length; i++) a[i] = c; return a; }
    static Color32[] Gradient()
    {
        var a = new Color32[GoldenImage.W * GoldenImage.H];
        for (int y = 0; y < GoldenImage.H; y++)
            for (int x = 0; x < GoldenImage.W; x++) { byte r = (byte)(x * 255 / GoldenImage.W), g = (byte)(y * 255 / GoldenImage.H), b = (byte)((x + y) * 255 / (GoldenImage.W + GoldenImage.H)); a[y * GoldenImage.W + x] = new Color32(r, g, b, 255); }
        return a;
    }

    [Test]
    public void IdenticalImagesHaveNoDifference()
    {
        var d = GoldenImage.Compare(Gradient(), Gradient());
        Assert.AreEqual(0.0, d.DiffPercent, 1e-9);
        Assert.AreEqual(0.0, d.MeanError, 1e-9);
    }

    [Test]
    public void ShiftedImageExceedsThresholds()
    {
        Color32[] a = Gradient(), b = Gradient();
        for (int i = 0; i < b.Length; i++) if (i % 4 == 0) b[i] = new Color32(255, 255, 255, 255);   // 화소 25%가 크게 다름
        var d = GoldenImage.Compare(a, b);
        Assert.Greater(d.DiffPercent, GoldenImage.MaxDiffPercent);
    }

    [Test]
    public void SmallNoiseStaysWithinTolerance()
    {
        Color32[] a = Gradient(), b = Gradient();
        for (int i = 0; i < b.Length; i++) b[i] = new Color32((byte)System.Math.Min(255, b[i].r + 5), b[i].g, b[i].b, 255);
        var d = GoldenImage.Compare(a, b);
        Assert.AreEqual(0.0, d.DiffPercent, 1e-9);
        Assert.Less(d.MeanError, GoldenImage.MaxMeanError);
    }

    [Test]
    public void SanityCatchesOneColourMagentaAndBrightness()
    {
        Assert.IsNotNull(GoldenImage.SanityProblem(Fill(new Color32(90, 120, 60, 255)), GoldenKind.Day, out _, out _, out _), "한 색");
        Assert.IsNotNull(GoldenImage.SanityProblem(Fill(new Color32(255, 0, 255, 255)), GoldenKind.Ui, out _, out _, out _), "마젠타");
        Assert.IsNull(GoldenImage.SanityProblem(Gradient(), GoldenKind.Day, out _, out _, out _), "정상 그림");
        Color32[] dark = Gradient();
        for (int i = 0; i < dark.Length; i++) dark[i] = new Color32((byte)(dark[i].r / 40), (byte)(dark[i].g / 40), (byte)(dark[i].b / 40), 255);
        Assert.IsNotNull(GoldenImage.SanityProblem(dark, GoldenKind.Day, out _, out _, out _), "낮인데 너무 어두움");
    }

    [Test]
    public void DownscaleAveragesBoxes()
    {
        var src = new Color32[4 * 2];
        for (int i = 0; i < src.Length; i++) src[i] = (i % 2 == 0) ? new Color32(0, 0, 0, 255) : new Color32(100, 100, 100, 255);
        var dst = GoldenImage.Downscale(src, 4, 2, 2, 1);
        Assert.AreEqual(50, dst[0].r);
        Assert.AreEqual(50, dst[1].r);
    }
}
