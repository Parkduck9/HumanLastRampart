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

// 등대 탑 (W9): 주인공이 북쪽 성벽 계단으로 내려가 탑에서 동쪽 빛줄기를 명령하고, 일꾼이 6초 동안 돌리고, 탑 전령이 성벽으로 달려 나가고, 다시 올라온다.
// 사진 1920×1080: 탑 앞 주인공·일꾼, 명령 지도(지금 방향), 도는 중 지도(목표 방향), 일꾼이 미는 모습, 도착 뒤. 결과: HLR_PLAY_OUT/NN_tower_*.png, tower_probe.txt
public sealed partial class PlayProbe
{
    // 주인공 이동·계단은 전투 시간(S1: 멈추면 멈춘다)을 따르므로 프레임 수(프레임당 0.05초)로 기다린다
    static IEnumerator WaitFor(System.Func<bool> condition, float seconds)
    {
        // 걷기 입력은 실제 시간, 계단은 전투 시간(프레임당 0.05초)이라 둘 다 지나야 포기한다
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; !condition() && (i < (int)(seconds * 20f) || watch.Elapsed.TotalSeconds < seconds); i++) yield return null;
    }

    [UnityTest]
    public IEnumerator BeaconTowerRotation()
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
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var view = (BattleView)typeof(BattleEntry).GetField("view", flags).GetValue(entry);
            BattleSetup setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
            var forecast = setup.Forecast; forecast.InitialWeather = Weather.Clear; forecast.TimeOfDay = TimeOfDay.Night; forecast.Changes = false; setup.Forecast = forecast;
            sim.WeatherDirty = true;
            entry.StartBattle();
            for (int i = 0; i < 20; i++) yield return null;
            Assert.IsTrue(view.Beacon.HasArt, "탑 그림 연결");
            Assert.AreEqual(0, sim.BeamSide, "처음 빛줄기 = 북");

            // 1) 성벽 위: 통로 안쪽 끝(계단)으로 걸어가 E
            Assert.IsTrue(rig.OnWall);
            Assert.IsFalse(entry.Interact(), "계단에서 멀면 E는 아무 일도 없다");
            rig.DepthInput = -1f;
            yield return WaitFor(() => rig.NearStairs, 6f);
            rig.DepthInput = 0f;
            Assert.IsTrue(rig.NearStairs, $"통로 안쪽 끝에서 계단 (면 {rig.Side}, along {rig.Along:0.0}, inset {rig.Inset:0.00}, 지도 {entry.MapOpen}, 준비 {entry.Preparing})");
            yield return Capture("tower_00_stairs", PfWidth, PfHeight);
            Assert.IsTrue(entry.Interact(), "E: 탑으로 내려가기");
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsFalse(sim.CommanderOnWall, "내려가는 순간 성벽은 지휘관 없음");
            yield return WaitFor(() => rig.AtTower, 8f);
            Assert.IsTrue(rig.AtTower, "탑 앞 도착");
            for (int i = 0; i < 20; i++) yield return null;
            // 카메라는 실제 시간으로 따라온다: 사진 기다리는 동안만 전투를 멈춰 둔다 (계단 걷기 자체는 전투 시간이다)
            entry.TutorialTimeScale = 0f;
            yield return new WaitForSecondsRealtime(1.5f);
            entry.TutorialTimeScale = 1f;
            for (int i = 0; i < 2; i++) yield return null;
            Note($"탑 도착: 발 ({rig.HeroFeet.x:0.00}, {rig.HeroFeet.y:0.00}, {rig.HeroFeet.z:0.00}), 지휘관 성벽 위 {sim.CommanderOnWall}");
            yield return Capture("tower_01_idle", PfWidth, PfHeight);

            // 2) 탑에서 E: 명령 지도
            entry.BeamMapStaySeconds = 99f;   // 사진 찍는 동안 지도를 닫지 않는다
            Assert.IsTrue(entry.Interact(), "E: 빛줄기 명령");
            for (int i = 0; i < 6; i++) yield return null;
            Assert.IsTrue(entry.MapOpen && entry.BeamOrderMode, "명령 지도 열림");
            yield return Capture("tower_02_map_before", PfWidth, PfHeight);

            // 3) 동쪽 명령 (지도의 방향 단추)
            yield return Press("빛 동", 120);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(sim.BeamRotating, "명령하면 돌기 시작");
            Assert.AreEqual(1, sim.BeamSide);
            yield return Capture("tower_03_map_ordered", PfWidth, PfHeight);
            entry.CloseBeamOrder();
            Assert.IsFalse(entry.MapOpen, "명령 뒤 지도를 접는다");

            // 4) 일꾼이 도는 동안 (쉬움은 지도가 닫혀 시간이 흐른다)
            float startAngle = sim.BeamAngleDegrees;
            for (int i = 0; i < 40; i++) yield return null;   // 약 2초
            Assert.IsTrue(sim.BeamRotating, "6초 도는 중");
            Assert.IsTrue(view.Beacon.WorkersPushing, "일꾼이 미는 동작");
            Assert.Less(sim.BeamAngleDegrees, startAngle, "북 → 동으로 줄어드는 각도");
            Assert.Greater(sim.BeamAngleDegrees, 0f, "아직 도착 전");
            yield return Capture("tower_04_workers_pushing", PfWidth, PfHeight);
            for (int i = 0; i < 20; i++) yield return null;
            yield return Capture("tower_05_workers_pushing_later", PfWidth, PfHeight);

            int frames = 0;
            while (sim.BeamRotating && frames++ < 400) yield return null;
            Assert.IsFalse(sim.BeamRotating);
            for (int i = 0; i < 3; i++) yield return null;   // 그리기가 한 번 더 돌아야 일꾼 상태가 바뀐다
            Assert.AreEqual(0f, sim.BeamAngleDegrees, 0.01f, "동쪽 = 0도");
            Note($"회전 완료: 명령 뒤 약 {frames * 0.05f + 3f:0.0}초 (기대 6초)");
            Assert.IsFalse(view.Beacon.WorkersPushing, "도착하면 일꾼이 멈춘다");
            for (int i = 0; i < 6; i++) yield return null;
            yield return Capture("tower_06_done", PfWidth, PfHeight);

            // 4-b) 탑 전령 3명: 명령 4개 -> 3명이 달려 나가고 하나는 줄을 선다 (B1)
            var notices = new System.Collections.Generic.List<string>();
            sim.Notice += n => notices.Add(n);
            for (int s = 0; s < 3; s++) sim.IssueMessenger(BattleSim.TowerOrigin, 0f, new[] { 0, 2, 3 }[s], HumanLastRampart.Core.Campaign.TargetPriority.HighestHpRatio);
            sim.IssueMessenger(BattleSim.TowerOrigin, 0f, 0, HumanLastRampart.Core.Campaign.TargetPriority.Nearest);
            for (int i = 0; i < 40; i++) yield return null;   // 약 2초
            Assert.AreEqual(0, sim.TowerCouriersFree, "전령 3명 모두 출발");
            Assert.AreEqual(1, sim.TowerOrdersQueued, "네 번째는 줄");
            Assert.IsTrue(notices.Exists(n => n.Contains("줄을 섭니다")));
            Assert.IsTrue(sim.GetTowerCourier(0).Busy);
            yield return Capture("tower_07_couriers_running", PfWidth, PfHeight);
            for (int i = 0; i < 60; i++) yield return null;
            yield return Capture("tower_08_couriers_running_later", PfWidth, PfHeight);

            // 5) 올라가기
            entry.LeaveTower();
            yield return WaitFor(() => rig.OnWall, 8f);
            Assert.IsTrue(rig.OnWall, "성벽으로 복귀");
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(sim.CommanderOnWall, "복귀하면 다시 성벽 지휘관");
            Note("계단 내려가기 → 탑 명령 → 6초 회전 → 올라오기 통과");
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, "tower_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
