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

// 성벽 부서짐 그림(W39, Codex K-126)과 일꾼 바퀴 밀기(K-127) 사진 1920×1080
// - WallDamage<Day|Night>: 0~3단계마다 성벽 위(파공 칸 곁), 안뜰 내려다보기, 성벽 바깥, 단계가 바뀐 직후 먼지
// - CapstanPushClose: 탑 문 앞에서 일꾼이 돌림대를 미는 모습 (시작 → 밀기 → 멈춤)
// 결과: HLR_PLAY_OUT/w39_*.png, w39_*_probe.txt
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator W39WallDamageDay() { yield return W39WallDamageShots(TimeOfDay.Day, "day"); }

    [UnityTest]
    public IEnumerator W39WallDamageNight() { yield return W39WallDamageShots(TimeOfDay.Night, "night"); }

    [UnityTest]
    public IEnumerator W39CapstanPushClose() { yield return W39CapstanShots(); }

    BattleEntry w39Entry;

    IEnumerator W39Enter(TimeOfDay time)
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

        w39Entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
        BattleSetup setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
        var forecast = setup.Forecast; forecast.InitialWeather = Weather.Clear; forecast.TimeOfDay = time; forecast.Changes = false; setup.Forecast = forecast;
        setup.BeaconRepair = 70f;
        w39Entry.Sim.WeatherDirty = true;
        w39Entry.StartBattle();
        for (int i = 0; i < 20; i++) yield return null;
    }

    IEnumerator W39WallDamageShots(TimeOfDay time, string label)
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
            yield return W39Enter(time);
            BattleEntry entry = w39Entry;
            BattleSim sim = entry.Sim;
            Camera cam = entry.BattleCamera;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var view = (BattleView)typeof(BattleEntry).GetField("view", flags).GetValue(entry);
            var world = (BattleWorldView)typeof(BattleView).GetField("world", flags).GetValue(view);
            Assert.IsNotNull(world, "3D 성벽 목업이 켜져 있다");
            entry.Rig.ShowSide(0);
            for (int i = 0; i < 6; i++) yield return null;
            var pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;

            float half = sim.CastleHalf, top = BattleSim.WallHeight;
            Vector3 n = new Vector3(WallGeometry.Outward(0).x, 0f, WallGeometry.Outward(0).z), r = new Vector3(WallGeometry.Right(0).x, 0f, WallGeometry.Right(0).z);
            float from = BattleWallLayout.From(half), to = BattleWallLayout.To(half);
            int breach = WallDamageRules.BreachTile(sim.BattleSeed, 0, WallDamageRules.TileCount(from, to));
            float bc = WallDamageRules.TileCenter(from, to, breach);
            Note($"씨앗 {sim.BattleSeed}, 북쪽 파공 칸 {breach} (성벽 따라 {bc:0.0} m), 밤 {time == TimeOfDay.Night}");

            float[] ratios = { 1f, 0.5f, 0.2f, 0f };
            for (int stage = 0; stage < 4; stage++)
            {
                for (int side = 0; side < 4; side++) world.SetWallHealth(side, ratios[stage]);
                Assert.AreEqual(stage, world.DamageStage(0), "단계 " + stage);
                BattleWallDamageView damage = world.Damage;
                Note($"단계 {stage}: 그림 {damage.HasArt}, 잔해 {damage.VisibleRubble(0)}/{damage.RubbleTotal(0)}, 정적 그리기 {damage.StaticDrawCalls}, 먼지 {damage.ActiveDust}, 연기·불꽃 {damage.ActiveSpots}");
                if (stage > 0) Assert.IsTrue(damage.HasArt, "부서짐 그림이 불러와졌다");

                // 먼지: 단계가 바뀐 직후 (10fps 6칸 = 0.6초)
                pin.Position = n * (half - 2.3f) + r * (bc - 1.8f) + Vector3.up * (top + 1.1f);
                pin.Rotation = Quaternion.LookRotation(n * (half + 1f) + r * bc + Vector3.up * (top + 0.05f) - pin.Position);
                for (int i = 0; i < 3; i++) yield return null;
                if (stage > 0) yield return Capture($"w39_{label}_stage{stage}_0_dust", PfWidth, PfHeight);
                for (int i = 0; i < 20; i++) yield return null;
                yield return Capture($"w39_{label}_stage{stage}_1_walltop", PfWidth, PfHeight);

                // 안뜰 내려다보기
                pin.Position = n * (half - 15f) + r * (bc - 9f) + Vector3.up * (top + 10f);
                pin.Rotation = Quaternion.LookRotation(n * (half - 1f) + r * bc + Vector3.up * top - pin.Position);
                for (int i = 0; i < 3; i++) yield return null;
                yield return Capture($"w39_{label}_stage{stage}_2_courtyard", PfWidth, PfHeight);

                // 성벽 바깥 (외벽 데칼·파공·발치 잔해)
                pin.Position = n * (half + 9f) + r * (bc + 2f) + Vector3.up * 2.4f;
                pin.Rotation = Quaternion.LookRotation(n * half + r * bc + Vector3.up * 2f - pin.Position);
                for (int i = 0; i < 3; i++) yield return null;
                yield return Capture($"w39_{label}_stage{stage}_3_outside", PfWidth, PfHeight);
            }

            Object.Destroy(pin);
            yield return null;
            Note("성벽 부서짐 사진 촬영 완료");
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, $"w39_{label}_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }

    IEnumerator W39CapstanShots()
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
            yield return W39Enter(TimeOfDay.Day);
            BattleEntry entry = w39Entry;
            BattleSim sim = entry.Sim;
            Camera cam = entry.BattleCamera;
            var view = (BattleView)typeof(BattleEntry).GetField("view", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
            float top = BattleSim.WallHeight;
            entry.Rig.ShowSide(0);
            for (int i = 0; i < 6; i++) yield return null;
            var pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;
            pin.Position = new Vector3(0.9f, top + 1.15f, -3.4f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + 0.6f, 0f) - pin.Position);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture("w39_capstan_0_idle", PfWidth, PfHeight);

            sim.RotateBeam(1);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(view.Beacon.WorkersPushing, "빛줄기가 도는 동안 일꾼이 민다");
            Assert.IsTrue(view.Beacon.WorkerUsesPushSheet(0), "K-127 밀기 시트로 그린다");
            Note($"시작: {view.Beacon.WorkerPushSheetName(0)} / {view.Beacon.WorkerPushSheetName(1)}");
            yield return Capture("w39_capstan_1_start", PfWidth, PfHeight);
            for (int i = 0; i < 20; i++) yield return null;
            Note($"밀기: {view.Beacon.WorkerPushSheetName(0)} / {view.Beacon.WorkerPushSheetName(1)}, 힘줌 {view.Beacon.BeamStraining}");
            yield return Capture("w39_capstan_2_push_a", PfWidth, PfHeight);
            // 문 앞에서 안이 보이게 (K-127 검토와 같은 시선): 벽 속에 들어가지 않는다
            pin.Position = new Vector3(0.45f, top + 1.0f, -3.3f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + 0.55f, 0f) - pin.Position);
            for (int i = 0; i < 6; i++) yield return null;
            yield return Capture("w39_capstan_3_push_b_close", PfWidth, PfHeight);
            for (int i = 0; i < 10; i++) yield return null;
            yield return Capture("w39_capstan_4_push_c_close", PfWidth, PfHeight);

            int frames = 0;
            while (sim.BeamRotating && frames++ < 400) yield return null;
            for (int i = 0; i < 4; i++) yield return null;
            Note($"멈춤: {view.Beacon.WorkerPushSheetName(0)} 종류 {view.Beacon.WorkerPushKind(0)}");
            Assert.IsTrue(view.Beacon.WorkerUsesPushSheet(0), "멈춘 뒤에도 K-127 멈춤 시트");
            yield return Capture("w39_capstan_5_stop", PfWidth, PfHeight);
            Object.Destroy(pin);
            yield return null;
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, "w39_capstan_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
