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

// 마당 임시 채우기 (W19): 돌바닥·계단·소품 사진 1920×1080. 성벽 위에서 계단 보기, 내려가는 중, 탑 앞 (낮·밤)
// 결과: HLR_PLAY_OUT/NN_courtyard_<day|night>_*.png, courtyard_<day|night>_probe.txt
public sealed partial class PlayProbe
{
    // 사진 찍는 순간 사진기를 정해진 자리에 붙잡아 둔다 (사진기 갱신보다 늦게, 사진 찍기보다 먼저)
    [DefaultExecutionOrder(-1000)]
    sealed class CameraPin : MonoBehaviour
    {
        public Camera Target;
        public Vector3 Position;
        public Quaternion Rotation;
        void LateUpdate()
        {
            if (Target == null) return;
            Target.transform.SetPositionAndRotation(Position, Rotation);
        }
    }

    [UnityTest]
    public IEnumerator CourtyardDay() { yield return CourtyardShots(TimeOfDay.Day, "day"); }

    [UnityTest]
    public IEnumerator CourtyardNight() { yield return CourtyardShots(TimeOfDay.Night, "night"); }

    IEnumerator CourtyardShots(TimeOfDay time, string label)
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

            // 1) 통로 안쪽 끝(계단 위)으로 걸어가 선다
            rig.DepthInput = -1f;
            yield return WaitFor(() => rig.NearStairs, 6f);
            rig.DepthInput = 0f;
            Assert.IsTrue(rig.NearStairs, "통로 안쪽 끝에서 계단");
            for (int i = 0; i < 20; i++) yield return null;

            Camera cam = entry.BattleCamera;
            float half = sim.CastleHalf;
            float top = BattleSim.WallHeight;
            var pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;

            // (a) 성벽 위(통로 바깥쪽)에서 안쪽 계단·마당을 내려다본다 — 주인공은 계단 위에 서 있다
            pin.Position = new Vector3(0f, top + 1.7f, half - 0.3f);
            pin.Rotation = Quaternion.Euler(32f, 180f, 0f);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"courtyard_{label}_a_wall_top_stairs", PfWidth, PfHeight);

            // (b) 계단 옆 비스듬히 (계단 모양 확인)
            pin.Position = new Vector3(2.6f, top + 0.9f, half - 3.8f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + 0.1f, half - 2.4f) - pin.Position);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"courtyard_{label}_b_stairs_side", PfWidth, PfHeight);
            Object.Destroy(pin);
            yield return null;

            // 2) 내려가는 중 (계단 위): 흐름이 계단 구간 안에서 멈추게 전투 시간을 멈춘다
            Assert.IsTrue(entry.Interact(), "E: 탑으로 내려가기");
            int guard = 0;
            while (rig.TowerBlend < 0.11f && guard++ < 60) yield return null;
            entry.TutorialTimeScale = 0f;
            yield return new WaitForSecondsRealtime(1.2f);
            Vector3 feet = rig.HeroFeet;
            Note($"내려가는 중: blend {rig.TowerBlend:0.000} 발 ({feet.x:0.00}, {feet.y:0.00}, {feet.z:0.00}) 그림 높이 보정 {BattleCourtyard.HeroLift(feet, half):0.000} m (층계참 0.24, 마당 0)");
            yield return Capture($"courtyard_{label}_c_mid_descent", PfWidth, PfHeight);

            // 한 단 더 내려간 모습 (blend 약 0.14)
            entry.TutorialTimeScale = 1f;
            int g2 = 0;
            while (rig.TowerBlend < 0.14f && g2++ < 20) yield return null;
            entry.TutorialTimeScale = 0f;
            yield return new WaitForSecondsRealtime(1.2f);
            feet = rig.HeroFeet;
            Note($"조금 더 내려감: blend {rig.TowerBlend:0.000} 발 z {feet.z:0.00} 보정 {BattleCourtyard.HeroLift(feet, half):0.000} m");
            yield return Capture($"courtyard_{label}_d_mid_descent_lower", PfWidth, PfHeight);
            entry.TutorialTimeScale = 1f;

            // 3) 탑 앞
            yield return WaitFor(() => rig.AtTower, 8f);
            Assert.IsTrue(rig.AtTower, "탑 앞 도착");
            Assert.AreEqual(0f, BattleCourtyard.HeroLift(rig.HeroFeet, half), 1e-4f, "마당에서는 보정 없음");
            for (int i = 0; i < 20; i++) yield return null;
            entry.TutorialTimeScale = 0f;
            yield return new WaitForSecondsRealtime(1.5f);
            entry.TutorialTimeScale = 1f;
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"courtyard_{label}_e_tower", PfWidth, PfHeight);

            // 4) 탑에서 북쪽(계단) 방향으로 돌아 마당 전체를 본다
            pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;
            pin.Position = new Vector3(-6.5f, top + 2.4f, -4.5f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top, 4f) - pin.Position);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"courtyard_{label}_f_courtyard_overview", PfWidth, PfHeight);
            Object.Destroy(pin);
            yield return null;

            entry.LeaveTower();
            yield return WaitFor(() => rig.OnWall, 8f);
            Assert.IsTrue(rig.OnWall, "성벽으로 복귀");
            Note("마당 사진 촬영 완료");
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, $"courtyard_{label}_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
