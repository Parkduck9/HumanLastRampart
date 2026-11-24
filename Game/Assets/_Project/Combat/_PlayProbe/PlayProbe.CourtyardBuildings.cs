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

// 마당 건물 블록아웃 (W28): 마당 전체 + 네 성벽 위에서 마당 안쪽 보기 (낮·밤)
// 결과: HLR_PLAY_OUT/NN_bldg_<day|night>_*.png
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator CourtyardBuildingsDay() { yield return CourtyardBuildingShots(TimeOfDay.Day, "day"); }

    [UnityTest]
    public IEnumerator CourtyardBuildingsNight() { yield return CourtyardBuildingShots(TimeOfDay.Night, "night"); }

    IEnumerator CourtyardBuildingShots(TimeOfDay time, string label)
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
            BattleSetup setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
            var forecast = setup.Forecast; forecast.InitialWeather = Weather.Clear; forecast.TimeOfDay = time; forecast.Changes = false; setup.Forecast = forecast;
            sim.WeatherDirty = true;
            entry.StartBattle();
            for (int i = 0; i < 20; i++) yield return null;

            Camera cam = entry.BattleCamera;
            float half = sim.CastleHalf;
            float top = BattleSim.WallHeight;
            var pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;

            // 마당 전체: 남서쪽 위에서 비스듬히 내려다본다
            pin.Position = new Vector3(-half * 0.9f, top + 17f, -half * 0.9f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + 1f, 0f) - pin.Position);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"bldg_{label}_0_overview", PfWidth, PfHeight);

            // 위에서 수직에 가깝게 (배치 확인)
            pin.Position = new Vector3(0f, top + 34f, -4f);
            pin.Rotation = Quaternion.Euler(86f, 0f, 0f);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"bldg_{label}_1_topdown", PfWidth, PfHeight);

            // 네 성벽 위에서 마당 안쪽 보기
            string[] names = { "north", "east", "south", "west" };
            for (int side = 0; side < 4; side++)
            {
                var o = WallGeometry.Outward(side);
                var n = new Vector3(o.x, 0f, o.z);
                pin.Position = n * (half - 0.4f) + Vector3.up * (top + 2.6f);
                pin.Rotation = Quaternion.LookRotation(-n * 6f + Vector3.down * 0.9f);
                for (int i = 0; i < 2; i++) yield return null;
                yield return Capture($"bldg_{label}_wall_{names[side]}", PfWidth, PfHeight);
            }

            Object.Destroy(pin);
            yield return null;
            Note("마당 건물 사진 촬영 완료");
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, $"bldg_{label}_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
