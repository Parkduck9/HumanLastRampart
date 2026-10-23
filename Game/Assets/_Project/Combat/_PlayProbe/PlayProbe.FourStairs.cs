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

// 네 성벽 계단·십자 길 (W35): 마당 한눈에(낮·밤), 동쪽·남쪽에서 내려가는 중, 탑 앞 안내 글, 서쪽 성벽으로 올라온 모습 사진 1920×1080
// 결과: HLR_PLAY_OUT/NN_w35_<day|night>_*.png, w35_<day|night>_probe.txt
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator FourStairsDay() { yield return FourStairsShots(TimeOfDay.Day, "day"); }

    [UnityTest]
    public IEnumerator FourStairsNight() { yield return FourStairsShots(TimeOfDay.Night, "night"); }

    // 길 위 진행(TowerBlend)이 목표까지 오면 전투 시간을 멈추고 카메라가 따라잡길 기다린다
    IEnumerator FreezeAtBlend(BattleEntry entry, BattleCameraRig rig, float blend)
    {
        for (int i = 0; i < 2000 && rig.TowerBlend < blend && !rig.AtTower; i++) yield return null;
        entry.TutorialTimeScale = 0f;
        yield return new WaitForSecondsRealtime(1.5f);
    }

    IEnumerator FourStairsShots(TimeOfDay time, string label)
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
            Note($"성 반폭 {half:0.0} m, 등불 기둥 {BattleRoad.LampCount(half)}개 (길 하나 {BattleRoad.LampCount(half) / BattleRoad.RoadCount}개), 길 시작 거리 {BattleRoad.RoadStart(half):0.0} m");
            for (int side = 0; side < 4; side++) Note($"성벽 {side}: 길 걷는 시간 {rig.TowerPathSeconds(side, rig.WallFeet(side, 0f, 1.8f)):0.0}초");

            // (a) 마당 한눈에: 남서쪽 높은 데서 가운데를 내려다본다 (네 길이 십자로 광장에 모인다)
            var pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;
            pin.Position = new Vector3(-0.55f * half, top + 0.62f * half, -0.55f * half);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top, 0f) - pin.Position);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"w35_{label}_a_courtyard_cross", PfWidth, PfHeight);
            // 바로 위에서 (십자 모양)
            pin.Position = new Vector3(0f, top + 1.15f * half, -0.01f * half);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top, 0f) - pin.Position, Vector3.forward);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"w35_{label}_b_courtyard_top_down", PfWidth, PfHeight);
            Object.Destroy(pin);
            yield return null;

            if (time == TimeOfDay.Day)
            {
                // 북쪽 성벽 → 광장 가까이 (사진기가 탑 몸체 안에 들어가지 않는지 본다)
                rig.ShowSide(0);
                for (int i = 0; i < 6; i++) yield return null;
                rig.DepthInput = -1f;
                yield return WaitFor(() => rig.NearStairs, 6f);
                rig.DepthInput = 0f;
                Assert.IsTrue(entry.Interact(), "E: 북쪽 계단으로 내려가기");
                yield return FreezeAtBlend(entry, rig, 0.8f);
                Note($"북쪽 광장 가까이: blend {rig.TowerBlend:0.00} 발 ({rig.HeroFeet.x:0.0}, {rig.HeroFeet.z:0.0}) 사진기 ({cam.transform.position.x:0.0}, {cam.transform.position.z:0.0}) 방향 {rig.Yaw:0}도");
                yield return Capture("w35_day_n_north_near_plaza", PfWidth, PfHeight);
                entry.TutorialTimeScale = 1f;
                yield return WaitFor(() => rig.AtTower, 40f);
                Assert.IsTrue(entry.TowerUp(0), "탑에서 W: 북쪽 성벽으로");
                yield return WaitFor(() => rig.OnWall, 40f);
            }

            // 동쪽 성벽 → 계단 → 길 (낮에만 이동 사진을 찍는다)
            rig.ShowSide(1);
            for (int i = 0; i < 6; i++) yield return null;
            rig.DepthInput = -1f;
            yield return WaitFor(() => rig.NearStairs, 6f);
            rig.DepthInput = 0f;
            Assert.IsTrue(rig.NearStairs, "동쪽 성벽 계단");
            if (time == TimeOfDay.Day)
            {
                yield return Capture("w35_day_c_east_wall_stairs", PfWidth, PfHeight);
            }

            Assert.IsTrue(entry.Interact(), "E: 동쪽 계단으로 내려가기");
            if (time == TimeOfDay.Day)
            {
                yield return FreezeAtBlend(entry, rig, 0.4f);
                Note($"동쪽 내려가는 중: blend {rig.TowerBlend:0.00} 발 ({rig.HeroFeet.x:0.0}, {rig.HeroFeet.z:0.0}) 포즈 {rig.Pose} 앞모습 {rig.FacingFront} 사진기 방향 {rig.Yaw:0}도");
                yield return FreshHud();
                yield return Capture("w35_day_d_east_mid_descent", PfWidth, PfHeight);   // 아래에 "E 되돌아가기" 안내와 단추
                entry.TutorialTimeScale = 1f;
            }

            yield return WaitFor(() => rig.AtTower, 40f);
            Assert.IsTrue(rig.AtTower, "탑 앞 도착 (동쪽에서)");
            for (int i = 0; i < 30; i++) yield return null;   // 사진기가 북쪽으로 돈다
            entry.TutorialTimeScale = 0f;
            yield return new WaitForSecondsRealtime(1.5f);
            entry.TutorialTimeScale = 1f;
            yield return FreshHud();
            Note($"탑 앞: 발 ({rig.HeroFeet.x:0.00}, {rig.HeroFeet.z:0.00}) 사진기 방향 {rig.Yaw:0}도");
            Assert.AreEqual(0f, Mathf.DeltaAngle(rig.Yaw, 0f), 2f, "탑 앞에서는 북쪽을 본다");
            yield return Capture($"w35_{label}_e_at_tower_prompt", PfWidth, PfHeight);

            if (time == TimeOfDay.Day)
            {
                // 서쪽(A)으로 올라가는 중, 올라온 뒤
                Assert.IsTrue(entry.TowerUp(3), "탑에서 A: 서쪽 성벽으로");
                yield return FreezeAtBlend(entry, rig, 0.0f);   // 출발 직후 (사진기 정지)
                entry.TutorialTimeScale = 1f;
                for (int i = 0; i < 60; i++) yield return null;
                yield return WaitFor(() => rig.OnWall, 40f);
                Assert.IsTrue(rig.OnWall, "서쪽 성벽에 올라왔다");
                Assert.AreEqual(3, rig.Side);
                Assert.AreEqual(0f, rig.Along, 1e-3f);
                for (int i = 0; i < 30; i++) yield return null;
                entry.TutorialTimeScale = 0f;
                yield return new WaitForSecondsRealtime(1.5f);
                entry.TutorialTimeScale = 1f;
                yield return FreshHud();
                Note($"서쪽 성벽 도착: 면 {rig.Side} 사진기 방향 {rig.Yaw:0}도 (서쪽 = -90)");
                Assert.AreEqual(-90f, Mathf.DeltaAngle(0f, rig.Yaw), 2f, "서쪽 성벽 바깥을 본다");
                yield return Capture("w35_day_f_west_wall_arrived", PfWidth, PfHeight);

                // 남쪽 성벽 → 내려가는 중 → 남쪽(S)으로 되돌아 올라가기
                rig.ShowSide(2);
                for (int i = 0; i < 6; i++) yield return null;
                rig.DepthInput = -1f;
                yield return WaitFor(() => rig.NearStairs, 6f);
                rig.DepthInput = 0f;
                Assert.IsTrue(entry.Interact(), "E: 남쪽 계단으로 내려가기");
                yield return FreezeAtBlend(entry, rig, 0.4f);
                Note($"남쪽 내려가는 중: blend {rig.TowerBlend:0.00} 발 ({rig.HeroFeet.x:0.0}, {rig.HeroFeet.z:0.0}) 포즈 {rig.Pose} 앞모습 {rig.FacingFront} 사진기 방향 {rig.Yaw:0}도");
                yield return Capture("w35_day_g_south_mid_descent", PfWidth, PfHeight);
                entry.TutorialTimeScale = 1f;
                yield return WaitFor(() => rig.AtTower, 40f);
                Assert.IsTrue(rig.AtTower, "탑 앞 도착 (남쪽에서)");
                Assert.IsTrue(entry.TowerUp(2), "탑에서 S: 남쪽 성벽으로");
                yield return WaitFor(() => rig.OnWall, 40f);
                Assert.IsTrue(rig.OnWall);
                Assert.AreEqual(2, rig.Side);
            }
            else
            {
                Assert.IsTrue(entry.TowerUp(1), "탑에서 D: 동쪽 성벽으로");
                yield return WaitFor(() => rig.OnWall, 40f);
                Assert.IsTrue(rig.OnWall);
            }

            Note("네 성벽 계단 사진 촬영 완료");
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, $"w35_{label}_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
