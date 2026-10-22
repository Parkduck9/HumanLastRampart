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

// 높은 등대 탑 (W20) 사진 1920×1080, 낮·밤: 네 성벽 기본 화면, 성벽 위에서 탑 보기, 마당 전체, 탑 앞, 밤 빛줄기
// 결과: HLR_PLAY_OUT/NN_tower_tall_<day|night>_*.png, tower_tall_<day|night>_probe.txt
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator TowerTallDay() { yield return TowerTallShots(TimeOfDay.Day, "day"); }

    [UnityTest]
    public IEnumerator TowerTallNight() { yield return TowerTallShots(TimeOfDay.Night, "night"); }

    IEnumerator TowerTallShots(TimeOfDay time, string label)
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
            float towerTop = top + BattleTowerView.Height;
            Note($"탑 키 {BattleTowerView.Height:0.0} m (성벽 {top:0.0} m의 {BattleTowerView.Height / top:0.0}배), 불방 높이 {BattleBeamView.OriginHeight:0.0} m");

            // 1) 네 성벽 기본 화면 (탑이 시야·HUD를 가리지 않는지)
            for (int side = 0; side < 4; side++)
            {
                rig.ShowSide(side);
                for (int i = 0; i < 6; i++) yield return null;
                yield return Capture($"tower_tall_{label}_1_wall{side}_default", PfWidth, PfHeight);
            }

            rig.ShowSide(0);
            for (int i = 0; i < 6; i++) yield return null;
            var pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;

            // 2) 성벽 위에서 마당 쪽으로 돌아서 탑 보기 (북쪽·남쪽 성벽)
            pin.Position = new Vector3(0f, top + 1.7f, half - 0.3f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + BattleTowerView.Height * 0.55f, 0f) - pin.Position);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"tower_tall_{label}_2_wall_top_look_tower_north", PfWidth, PfHeight);
            pin.Position = new Vector3(0f, top + 1.7f, -half + 0.3f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + BattleTowerView.Height * 0.55f, 0f) - pin.Position);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"tower_tall_{label}_2_wall_top_look_tower_south", PfWidth, PfHeight);

            // 3) 마당 전체 (탑 키가 한눈에)
            pin.Position = new Vector3(-24f, top + 7f, -30f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + BattleTowerView.Height * 0.45f, 0f) - pin.Position);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"tower_tall_{label}_3_courtyard_overview", PfWidth, PfHeight);

            // 4) 밤: 불방에서 나가는 빛줄기 (탑을 올려다보는 자리 + 성벽 위에서 들판 쪽)
            if (time == TimeOfDay.Night)
            {
                pin.Position = new Vector3(-9f, top + 1.2f, -13f);
                pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + BattleTowerView.Height * 0.8f, 0f) - pin.Position);
                for (int i = 0; i < 2; i++) yield return null;
                yield return Capture($"tower_tall_{label}_4_beam_lamp_closeup", PfWidth, PfHeight);

                // 빛줄기가 향한 쪽 성벽의 기본 사진기 화면 (W15 155_night_beam_normal과 같은 구도)
                float a = sim.BeamAngleDegrees * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                pin.enabled = false;
                rig.ShowSide(sim.BeamSide);
                for (int i = 0; i < 6; i++) yield return null;
                yield return Capture($"tower_tall_{label}_4_beam_field_from_wall", PfWidth, PfHeight);
                pin.enabled = true;
                // 탑 쪽을 돌아보며 빛이 꼭대기에서 나가는지
                pin.Position = dir * (half - 1f) + Vector3.up * (top + 1.8f);
                pin.Rotation = Quaternion.LookRotation(new Vector3(0f, towerTop - 1.5f, 0f) - pin.Position);
                for (int i = 0; i < 2; i++) yield return null;
                yield return Capture($"tower_tall_{label}_4_beam_back_to_tower", PfWidth, PfHeight);
            }

            Object.Destroy(pin);
            yield return null;

            // 5) 탑 앞 (문 앞 주인공과 전령·일꾼)
            rig.ShowSide(0);
            for (int i = 0; i < 6; i++) yield return null;
            rig.DepthInput = -1f;
            yield return WaitFor(() => rig.NearStairs, 6f);
            rig.DepthInput = 0f;
            Assert.IsTrue(entry.Interact(), "E: 탑으로 내려가기");
            yield return WaitFor(() => rig.AtTower, 8f);
            Assert.IsTrue(rig.AtTower, "탑 앞 도착");
            Assert.AreEqual(BattleSim.WallHeight, rig.HeroFeet.y, 1e-3f, "발은 마당 윗면 높이 (오르지 않는다)");
            for (int i = 0; i < 20; i++) yield return null;
            yield return Capture($"tower_tall_{label}_5_at_tower_door", PfWidth, PfHeight);

            entry.LeaveTower();
            yield return WaitFor(() => rig.OnWall, 8f);
            Assert.IsTrue(rig.OnWall, "성벽으로 복귀");
            Note("높은 탑 사진 촬영 완료");
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, $"tower_tall_{label}_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
