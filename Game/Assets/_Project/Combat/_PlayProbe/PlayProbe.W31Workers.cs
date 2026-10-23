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

// 등대 일꾼 K-119 시트 연결 (W31) 사진 1920×1080: 탑 문 앞에서 안쪽 보기 — 밀기 · 기름통 운반 · 계단 오르기 · 불방 화로 관리 · 계단 내려오기
// 결과: HLR_PLAY_OUT/NN_w31_<day|night>_*.png, w31_<day|night>_probe.txt
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator W31WorkersDay() { yield return W31WorkerShots(TimeOfDay.Day, "day"); }

    [UnityTest]
    public IEnumerator W31WorkersNight() { yield return W31WorkerShots(TimeOfDay.Night, "night"); }

    IEnumerator W31WorkerShots(TimeOfDay time, string label)
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
            setup.BeaconRepair = 70f;
            sim.WeatherDirty = true;
            entry.StartBattle();
            for (int i = 0; i < 20; i++) yield return null;

            Camera cam = entry.BattleCamera;
            float top = BattleSim.WallHeight;
            var view = (BattleView)typeof(BattleEntry).GetField("view", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
            BattleBeaconView beacon = view.Beacon;
            Note($"K-119 시트 {beacon.HasMotionArt}");
            Assert.IsTrue(beacon.HasMotionArt, "K-119 시트가 불러와진다");
            rig.ShowSide(0);
            for (int i = 0; i < 6; i++) yield return null;
            var pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;
            Vector3 doorEye = new Vector3(0.9f, top + 1.15f, -3.4f), doorLook = new Vector3(0f, top + 0.8f, 0f);
            pin.Position = doorEye;
            pin.Rotation = Quaternion.LookRotation(doorLook - pin.Position);

            // 밀기 (옛 그림 그대로)
            sim.RotateBeam(1);
            for (int i = 0; i < 30; i++) yield return null;
            yield return Capture($"w31_{label}_1_push", PfWidth, PfHeight);
            int frames = 0;
            while (sim.BeamRotating && frames++ < 600) yield return null;

            // 운반 걷기 → 계단 오르기 → 화로 관리 → 계단 내려오기. 각 동작의 첫 장면을 기다려 찍는다
            yield return W31WaitMotion(beacon, BattleBeaconView.WorkMotion.Carry, 3000);
            for (int i = 0; i < 6; i++) yield return null;
            Note($"운반: {beacon.WorkerSheetName(2)}");
            yield return Capture($"w31_{label}_2_carry", PfWidth, PfHeight);
            yield return W31WaitMotion(beacon, BattleBeaconView.WorkMotion.ClimbUp, 3000);
            for (int i = 0; i < 30; i++) yield return null;
            Note($"오르기: {beacon.WorkerSheetName(2)} 높이 {beacon.WorkerFeet(2).y - top:0.00}");
            yield return Capture($"w31_{label}_3_climb_up", PfWidth, PfHeight);
            pin.Position = new Vector3(-1.4f, top + 1.0f, -3.0f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(-0.5f, top + 1.2f, 0.3f) - pin.Position);
            for (int i = 0; i < 20; i++) yield return null;
            yield return Capture($"w31_{label}_3_climb_up_b", PfWidth, PfHeight);

            pin.Position = new Vector3(-1.6f, top + BattleTowerView.LampHeight - 0.2f, -4.6f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + BattleTowerView.LampHeight - 0.2f, 0f) - pin.Position);
            yield return W31WaitMotion(beacon, BattleBeaconView.WorkMotion.Tend, 3000);
            for (int i = 0; i < 16; i++) yield return null;
            Note($"화로 관리: {beacon.WorkerSheetName(2)}");
            yield return Capture($"w31_{label}_4_tend", PfWidth, PfHeight);
            for (int i = 0; i < 10; i++) yield return null;
            yield return Capture($"w31_{label}_4_tend_b", PfWidth, PfHeight);
            pin.Position = new Vector3(-0.1f, top + BattleTowerView.LampBase + 0.75f, -2.1f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(-0.2f, top + BattleTowerView.LampBase + 0.45f, -0.1f) - pin.Position);
            for (int i = 0; i < 6; i++) yield return null;
            yield return Capture($"w31_{label}_4_tend_c_close", PfWidth, PfHeight);

            pin.Position = doorEye;
            pin.Rotation = Quaternion.LookRotation(doorLook - pin.Position);
            yield return W31WaitMotion(beacon, BattleBeaconView.WorkMotion.ClimbDown, 3000);
            for (int i = 0; i < 30; i++) yield return null;
            Note($"내려오기: {beacon.WorkerSheetName(2)}");
            yield return Capture($"w31_{label}_5_climb_down", PfWidth, PfHeight);

            Object.Destroy(pin);
            yield return null;
            Note("일꾼 K-119 사진 촬영 완료");
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, $"w31_{label}_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }

    [UnityTest]
    public IEnumerator W31FacadesDay() { yield return W31FacadeShots(TimeOfDay.Day, "day"); }

    [UnityTest]
    public IEnumerator W31FacadesNight() { yield return W31FacadeShots(TimeOfDay.Night, "night"); }

    // 기능 건물 네 채 앞면 가까이 (문·간판·밤 창문) + 소품 (굴뚝·종탑·울타리·우물)
    IEnumerator W31FacadeShots(TimeOfDay time, string label)
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
            float half = sim.CastleHalf, top = BattleSim.WallHeight;
            var pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;
            float plane = half - 2f - 0.3f;
            foreach (var p in BattleCourtyardBuildings.Plots)
            {
                if (p.Front == BattleCourtyardBuildings.Facade.None && p.Name != "대장간" && p.Name != "예배당" && p.Name != "마구간") continue;
                var o = WallGeometry.Outward(p.Side); var r = WallGeometry.Right(p.Side);
                Vector3 n = new Vector3(o.x, 0f, o.z), right = new Vector3(r.x, 0f, r.z);
                Vector3 face = n * (plane - p.Depth) + right * p.Along;
                pin.Position = face - n * 5.2f + Vector3.up * (top + 1.5f) - right * 0.6f;
                pin.Rotation = Quaternion.LookRotation(face + Vector3.up * (top + 1.0f) - pin.Position);
                for (int i = 0; i < 3; i++) yield return null;
                yield return Capture($"w31_{label}_facade_{p.Name}", PfWidth, PfHeight);
            }

            Object.Destroy(pin);
            yield return null;
            Note("건물 앞면 사진 촬영 완료");
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, $"w31_facades_{label}_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
    // 계단 오르는 일꾼(2번)이 이 동작 새 시트로 그려질 때까지 기다린다
    static IEnumerator W31WaitMotion(BattleBeaconView beacon, BattleBeaconView.WorkMotion motion, int guardFrames)
    {
        int n = 0;
        while (n++ < guardFrames && !(beacon.WorkerUsesMotionSheet(2) && beacon.WorkerMotionKind(2) == motion)) yield return null;
        Assert.Less(n, guardFrames, $"일꾼이 {motion} 시트로 그려진다");
    }
}
