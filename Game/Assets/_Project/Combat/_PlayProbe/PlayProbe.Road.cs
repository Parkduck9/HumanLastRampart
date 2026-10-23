using System.Collections;
using System.IO;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// 계단 → 탑 돌길·광장·등불 기둥·깃발 (W26) + 밤 빛 밖 밝기 사진 1920×1080, 낮·밤
// 결과: HLR_PLAY_OUT/NN_road_<day|night>_*.png, road_<day|night>_probe.txt
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator RoadDay() { yield return RoadShots(TimeOfDay.Day, "day"); }

    [UnityTest]
    public IEnumerator RoadNight() { yield return RoadShots(TimeOfDay.Night, "night"); }

    IEnumerator RoadShots(TimeOfDay time, string label)
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType, "시각 촬영에는 그래픽 장치가 필요");
        Time.captureDeltaTime = 1f / 20f;
        KeepPreparation = true;
        bool suppressed = HumanLastRampart.Tutorial.TutorialState.Suppressed;
        HumanLastRampart.Tutorial.TutorialState.Suppressed = true;
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
            var story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>();
            for (int i = 0; i < 300 && story == null; i++) { yield return null; story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); }
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);

            var entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
            BattleSim sim = entry.Sim;
            BattleCameraRig rig = entry.Rig;
            BattleSetup setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
            var forecast = setup.Forecast; forecast.InitialWeather = Weather.Clear; forecast.TimeOfDay = time; forecast.Changes = false; setup.Forecast = forecast;
            sim.WeatherDirty = true;
            entry.StartBattle();
            for (int i = 0; i < 20; i++) yield return null;

            Camera cam = entry.BattleCamera;
            float half = sim.CastleHalf;
            float top = BattleSim.WallHeight;
            Note($"성 반폭 {half:0.0} m, 등불 기둥 {BattleRoad.LampCount(half)}개, 길 시작 거리 {BattleRoad.RoadStart(half):0.0} m");

            var pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;

            // (a) 성벽 위에서 안쪽 길을 따라 탑을 본다
            pin.Position = new Vector3(0f, top + 1.7f, half - 0.3f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + 1.2f, half - 18f) - pin.Position);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"road_{label}_a_wall_top_road", PfWidth, PfHeight);

            // (b) 계단 아래 비스듬히 (계단·돌담·연석·첫 기둥)
            pin.Position = new Vector3(3.6f, top + 1.0f, half - 7f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + 0.1f, half - 3.4f) - pin.Position);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"road_{label}_b_stairs_foot", PfWidth, PfHeight);

            // (c) 마당 한복판 위에서 (광장·길·기둥 줄·깃발)
            pin.Position = new Vector3(-7f, top + 3.2f, 11f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + 1.5f, 0f) - pin.Position);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"road_{label}_c_courtyard_overview", PfWidth, PfHeight);

            // (d) 탑 문 앞 광장 (깃발)
            pin.Position = new Vector3(-3.4f, top + 1.0f, -5.5f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + 1.2f, -1.2f) - pin.Position);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"road_{label}_d_plaza_door", PfWidth, PfHeight);

            if (time == TimeOfDay.Night)
            {
                // 빛줄기 밖 들판·성벽 안쪽 면 (밤 밝기): 빛줄기가 안 향한 성벽과 향한 성벽의 기본 사진기 화면
                pin.enabled = false;
                int beamSide = sim.BeamSide;
                int other = (beamSide + 2) % 4;
                rig.ShowSide(other);
                for (int i = 0; i < 8; i++) yield return null;
                yield return Capture("road_night_e_field_no_beam", PfWidth, PfHeight);
                rig.ShowSide(beamSide);
                for (int i = 0; i < 8; i++) yield return null;
                yield return Capture("road_night_f_field_with_beam", PfWidth, PfHeight);
                pin.enabled = true;
                // 성벽 안쪽 면·마당 구석 (빛 밖)
                pin.Position = new Vector3(half - 4f, top + 2.2f, -half + 4f);
                pin.Rotation = Quaternion.LookRotation(new Vector3(-8f, top, 8f) - pin.Position);
                for (int i = 0; i < 2; i++) yield return null;
                yield return Capture("road_night_g_courtyard_corner", PfWidth, PfHeight);
            }

            Object.Destroy(pin);
            yield return null;

            // 탑으로 내려가기 (길을 따라 걷는 주인공)
            rig.ShowSide(0);
            for (int i = 0; i < 6; i++) yield return null;
            rig.DepthInput = -1f;
            yield return WaitFor(() => rig.NearStairs, 6f);
            rig.DepthInput = 0f;
            Assert.IsTrue(entry.Interact(), "E: 탑으로 내려가기");
            yield return WaitFor(() => rig.AtTower, 40f);
            Assert.IsTrue(rig.AtTower, "탑 앞 도착");
            Assert.AreEqual(BattleSim.WallHeight, rig.HeroFeet.y, 1e-3f, "발은 마당 윗면 높이");
            for (int i = 0; i < 20; i++) yield return null;
            yield return Capture($"road_{label}_h_at_tower", PfWidth, PfHeight);
            entry.LeaveTower();
            yield return WaitFor(() => rig.OnWall, 40f);
            Assert.IsTrue(rig.OnWall, "성벽으로 복귀");
            Note("돌길 사진 촬영 완료");
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, $"road_{label}_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
