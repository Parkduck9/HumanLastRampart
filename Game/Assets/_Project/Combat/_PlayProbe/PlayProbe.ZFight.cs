using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// 바닥 깜박임(z-fighting) 점검 (W41, 사용자 2026-10-26 "바닥이 위아래로 깜박인다"):
// - ZFightNoOverlap: 실제 전투 장면의 위 평면이 높이 5mm 안에서 겹치는 곳(보이는 것)이 0개
// - ZFightShotsDay/Night: 사용자 자리(성벽 안쪽 가장자리 계단 곁)·네 계단 위·길과 광장 이음·걷는 두 장(0.1초 간격) 사진
// 결과: HLR_PLAY_OUT/NN_w41_<day|night>_*.png, w41_<day|night>_probe.txt, w41_zfight_scan.txt
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator ZFightNoOverlap()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        KeepPreparation = true;
        bool suppressed = HumanLastRampart.Tutorial.TutorialState.Suppressed;
        HumanLastRampart.Tutorial.TutorialState.Suppressed = true;
        try
        {
            yield return EnterBattle(TimeOfDay.Day);
            var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
            float half = entry.Sim.CastleHalf;
            var stats = new StringBuilder();
            var findings = ZFightScan.Scan(half + 8f, stats);
            Note(stats.ToString().TrimEnd());
            int visible = 0;
            foreach (var f in findings.OrderByDescending(x => x.VisibleArea))
            {
                if (f.VisibleArea > 0f) visible++;
                Note($"{(f.VisibleArea > 0f ? "보임" : "숨음")} {f.A}  <->  {f.B}  dy={f.Dy * 100f:0.00}cm 보이는 {f.VisibleArea:0.00}m² 가려진 {f.HiddenArea:0.00}m² 예: ({f.Where.x:0.0}, {f.Where.y:0.000}, {f.Where.z:0.0})");
            }

            Note($"겹침 쌍 {findings.Count}개, 보이는 것 {visible}개");
            File.WriteAllText(Path.Combine(Out, "w41_zfight_scan.txt"), log.ToString(), new UTF8Encoding(false));
            Assert.AreEqual(0, visible, "보이는 곳에서 5mm 안에 겹친 위 평면이 없다 (깜박임 금지):\n" + log);
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
        }
    }

    [UnityTest]
    public IEnumerator ZFightShotsDay() { yield return ZFightShots(TimeOfDay.Day, "day"); }

    [UnityTest]
    public IEnumerator ZFightShotsNight() { yield return ZFightShots(TimeOfDay.Night, "night"); }

    IEnumerator EnterBattle(TimeOfDay time)
    {
        Screen.SetResolution(1920, 1080, false);
        SceneManager.LoadScene("Boot");
        yield return null;
        yield return null;
        yield return WaitScreen(ScreenId.MainMenu, 900);
        yield return Press("게임 시작", 600);
        yield return Press("쉬움", 300);
        yield return WaitScreen(ScreenId.Story, 600);
        var story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>();
        for (int i = 0; i < 300 && story == null; i++) { yield return null; story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); }
        story.Skip();
        yield return WaitScreen(ScreenId.Battle, 900);
        var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
        BattleSetup setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
        var forecast = setup.Forecast; forecast.InitialWeather = Weather.Clear; forecast.TimeOfDay = time; forecast.Changes = false; setup.Forecast = forecast;
        entry.Sim.WeatherDirty = true;
        entry.StartBattle();
        for (int i = 0; i < 20; i++) yield return null;
    }

    IEnumerator ZFightShots(TimeOfDay time, string label)
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType, "시각 촬영에는 그래픽 장치가 필요");
        Time.captureDeltaTime = 1f / 20f;
        KeepPreparation = true;
        bool suppressed = HumanLastRampart.Tutorial.TutorialState.Suppressed;
        HumanLastRampart.Tutorial.TutorialState.Suppressed = true;
        BattleCameraRig rig = null;
        Component pinToRemove = null;
        try
        {
            yield return EnterBattle(time);
            var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
            BattleSim sim = entry.Sim;
            rig = entry.Rig;
            Camera cam = entry.BattleCamera;
            float half = sim.CastleHalf;
            float top = BattleSim.WallHeight;
            Note($"성 반폭 {half:0.0} m, 사진기 근접면 {cam.nearClipPlane} m, 먼면 {cam.farClipPlane} m");

            // (a) 사용자 자리: 성벽 안쪽 가장자리, 계단 곁 (네 성벽 모두 계단 위)
            string[] names = { "north", "east", "south", "west" };
            for (int side = 0; side < 4; side++)
            {
                rig.ShowSide(side);
                for (int i = 0; i < 6; i++) yield return null;
                rig.DepthInput = -1f;
                yield return WaitFor(() => rig.NearStairs, 6f);
                rig.DepthInput = 0f;
                for (int i = 0; i < 6; i++) yield return null;
                yield return Capture($"w41_{label}_stairtop_{names[side]}", PfWidth, PfHeight);
            }

            // (b) 걷는 중 두 장 (0.1초 = 2프레임 간격): 성벽 안쪽 가장자리를 따라 모서리 쪽으로
            rig.ShowSide(0);
            for (int i = 0; i < 6; i++) yield return null;
            rig.DepthInput = -1f;
            rig.WalkInput = 1f;
            for (int i = 0; i < 40; i++) yield return null;
            yield return Capture($"w41_{label}_walk_a", PfWidth, PfHeight);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"w41_{label}_walk_b", PfWidth, PfHeight);
            for (int i = 0; i < 60; i++) yield return null;   // 모서리 근처까지
            yield return Capture($"w41_{label}_walk_corner_a", PfWidth, PfHeight);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"w41_{label}_walk_corner_b", PfWidth, PfHeight);
            rig.WalkInput = 0f;
            rig.DepthInput = 0f;

            // (c) 길과 광장 이음, 계단 아래, 성벽-마당 이음: 낮은 사진기 고정
            var pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;
            pinToRemove = pin;
            void Pin(Vector3 pos, Vector3 look)
            {
                pin.Position = pos;
                pin.Rotation = Quaternion.LookRotation(look - pos);
            }

            Pin(new Vector3(2.2f, top + 1.0f, 8.5f), new Vector3(0f, top, 3.2f));
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"w41_{label}_c1_road_plaza_join", PfWidth, PfHeight);
            Pin(new Vector3(3.0f, top + 0.8f, half - 6.5f), new Vector3(0f, top, half - 2.8f));
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"w41_{label}_c2_stair_foot", PfWidth, PfHeight);
            Pin(new Vector3(half - 7f, top + 1.2f, half - 7f), new Vector3(half - 1.2f, top, half - 1.2f));
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"w41_{label}_c3_corner_platform", PfWidth, PfHeight);
            Pin(new Vector3(-6f, top + 1.6f, half - 9f), new Vector3(-9f, top, half - 2.2f));
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"w41_{label}_c4_wall_yard_edge", PfWidth, PfHeight);
            Pin(new Vector3(-0.6f * half, top + 0.55f * half, -0.55f * half), new Vector3(0f, top, 0f));
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"w41_{label}_c5_overview", PfWidth, PfHeight);
            Object.Destroy(pin);
            yield return null;
            Note("바닥 깜박임 사진 촬영 완료");
        }
        finally
        {
            if (rig != null) { rig.WalkInput = 0f; rig.DepthInput = 0f; }   // 시간 초과·예외에도 입력과 사진기 고정을 되돌린다
            if (pinToRemove != null) Object.Destroy(pinToRemove);
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, $"w41_{label}_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
