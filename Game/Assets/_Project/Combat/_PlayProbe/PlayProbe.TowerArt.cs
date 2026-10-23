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

// 등대 탑·안마당 Codex 그림 연결 (W27, K-115·K-116) 사진 1920×1080: 성벽 위에서 탑, 마당 전체, 계단 아래, 탑 문 앞(일꾼이 안에서 미는 모습), 불방 가까이, 꺼진 탑
// 결과: HLR_PLAY_OUT/w27_<day|night|dead>_*.png, w27_<day|night|dead>_probe.txt
public sealed partial class PlayProbe
{
    [UnityTest]
    public IEnumerator TowerArtDay() { yield return TowerArtShots(TimeOfDay.Day, "day", 70f); }

    [UnityTest]
    public IEnumerator TowerArtNight() { yield return TowerArtShots(TimeOfDay.Night, "night", 70f); }

    [UnityTest]
    public IEnumerator TowerArtDead() { yield return TowerArtShots(TimeOfDay.Day, "dead", 0f); }

    IEnumerator TowerArtShots(TimeOfDay time, string label, float repair)
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
            setup.BeaconRepair = repair;   // 0 = 꺼진 탑, 그 밖 = 점등 (불꽃 세기 단계)
            sim.WeatherDirty = true;
            entry.StartBattle();
            for (int i = 0; i < 20; i++) yield return null;

            Camera cam = entry.BattleCamera;
            float half = sim.CastleHalf;
            float top = BattleSim.WallHeight;
            var view = (BattleView)typeof(BattleEntry).GetField("view", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
            Note($"등대 복구 {sim.BeaconRepair:0}, 불꽃 세기 {view.Beacon.FlameStrength:0.00}, 꺼짐 {view.Beacon.Dead}, 돌림대 {view.Beacon.HasCapstan}, 불꽃 그림 {view.Beacon.HasFlameArt}, 화로 그림 {view.Beacon.HasBrazierArt}, 탑 그림 {view.Tower.HasTowerArt}");

            rig.ShowSide(0);
            for (int i = 0; i < 6; i++) yield return null;
            var pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;

            // 1) 성벽 위에서 마당 쪽으로 돌아서 탑 보기 (북쪽·남쪽 성벽)
            pin.Position = new Vector3(0f, top + 1.7f, half - 0.3f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + BattleTowerView.Height * 0.5f, 0f) - pin.Position);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture($"w27_{label}_1_wall_top_tower_north", PfWidth, PfHeight);
            pin.Position = new Vector3(0f, top + 1.7f, -half + 0.3f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + BattleTowerView.Height * 0.5f, 0f) - pin.Position);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture($"w27_{label}_1_wall_top_tower_south", PfWidth, PfHeight);

            // 2) 마당 전체
            pin.Position = new Vector3(-24f, top + 7f, -30f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + BattleTowerView.Height * 0.4f, 0f) - pin.Position);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture($"w27_{label}_2_courtyard_overview", PfWidth, PfHeight);
            pin.Position = new Vector3(0f, top + 14f, -half * 0.9f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top, 2f) - pin.Position);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture($"w27_{label}_2_courtyard_from_south_high", PfWidth, PfHeight);

            // 3) 계단 아래 (돌길 쪽에서 계단 위·데칼·소품)
            float stairFootZ = half - 2f - BattleCourtyard.StepCount * BattleCourtyard.TreadDepth;
            pin.Position = new Vector3(1.0f, top + 0.9f, stairFootZ - 3.2f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + 0.15f, stairFootZ + 0.8f) - pin.Position);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture($"w27_{label}_3_stair_foot", PfWidth, PfHeight);
            pin.Position = new Vector3(4.2f, top + 2.2f, stairFootZ - 4.2f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + 0.1f, stairFootZ + 1.0f) - pin.Position);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture($"w27_{label}_3_stair_foot_wide", PfWidth, PfHeight);

            // 4) 불방 가까이 (창틀·철창·화로·불꽃)
            pin.Position = new Vector3(-1.6f, top + BattleTowerView.LampHeight - 0.2f, -5.6f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + BattleTowerView.LampHeight - 0.2f, 0f) - pin.Position);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture($"w27_{label}_4_lamp_room", PfWidth, PfHeight);

            // 5) 탑 문 앞 가까이, 안이 보이게 (쉬는 모습 → 돌림대를 미는 모습 → 계단 오르는 모습)
            pin.Position = new Vector3(0.9f, top + 1.15f, -3.4f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + 0.75f, 0f) - pin.Position);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture($"w27_{label}_5_door_in_idle", PfWidth, PfHeight);
            sim.RotateBeam(1);
            for (int i = 0; i < 24; i++) yield return null;   // 1.2초
            Assert.IsTrue(view.Beacon.WorkersPushing, "빛줄기가 도는 동안 일꾼이 민다");
            for (int i = 0; i < 3; i++) Assert.IsTrue(Mathf.Abs(view.Beacon.WorkerFeet(i).x) < BattleTowerInterior.InnerHalf && Mathf.Abs(view.Beacon.WorkerFeet(i).z) < BattleTowerInterior.InnerHalf, $"일꾼 {i}는 탑 안");
            yield return Capture($"w27_{label}_5_door_in_pushing_a", PfWidth, PfHeight);
            for (int i = 0; i < 16; i++) yield return null;
            yield return Capture($"w27_{label}_5_door_in_pushing_b", PfWidth, PfHeight);
            pin.Position = new Vector3(-1.2f, top + 1.0f, -2.6f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0.1f, top + 0.6f, 0f) - pin.Position);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture($"w27_{label}_5_door_in_pushing_c_left", PfWidth, PfHeight);
            float capBefore = view.Beacon.CapstanDegrees;
            for (int i = 0; i < 12; i++) yield return null;
            Note($"돌림대 각도 {capBefore:0.0} → {view.Beacon.CapstanDegrees:0.0} (미는 동안 돈다)");
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(capBefore, view.Beacon.CapstanDegrees)), 0.5f, "돌림대가 돈다");
            int frames = 0;
            while (sim.BeamRotating && frames++ < 400) yield return null;

            // 쉬는 동안 한 명이 계단을 오른다 → 벽 속 → 불방에서 돌본다 (전투 시간으로 기다린다)
            pin.Position = new Vector3(0.9f, top + 1.15f, -3.4f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + 0.9f, 0.2f) - pin.Position);
            int guardFrames = 0;
            while ((!view.Beacon.WorkerVisible(2) || view.Beacon.WorkerFeet(2).y < top + 1.2f) && guardFrames++ < 3000) yield return null;
            Note($"계단 오르는 일꾼 높이 {view.Beacon.WorkerFeet(2).y - top:0.00} m (기다린 프레임 {guardFrames})");
            Assert.Less(guardFrames, 3000, "쉬는 동안 한 명이 계단을 오른다");
            yield return Capture($"w27_{label}_5_door_in_climbing", PfWidth, PfHeight);
            bool hidden = false;
            guardFrames = 0;
            while (guardFrames++ < 3000 && !(view.Beacon.WorkerVisible(2) && view.Beacon.WorkerFeet(2).y > top + BattleTowerView.LampBase - 0.1f))
            {
                if (!view.Beacon.WorkerVisible(2)) hidden = true;
                yield return null;
            }

            Assert.IsTrue(hidden, "벽 속을 지나 올라간다");
            Assert.Less(guardFrames, 3000, "불방에 닿는다");
            for (int i = 0; i < 44; i++) yield return null;   // 돌보는 중간 (2초 뒤)
            pin.Position = new Vector3(-1.6f, top + BattleTowerView.LampHeight - 0.2f, -4.6f);
            pin.Rotation = Quaternion.LookRotation(new Vector3(0f, top + BattleTowerView.LampHeight - 0.2f, 0f) - pin.Position);
            for (int i = 0; i < 2; i++) yield return null;
            yield return Capture($"w27_{label}_5_lamp_worker", PfWidth, PfHeight);

            // 7) 마당 소품 가까이 (무기 걸이·화살 묶음·수레, 계단 쪽 성벽 가장자리)
            BattleCourtyard.StairFrame(out Vector3 sn, out Vector3 sr);
            float edge = half - 4f;
            Vector3 rack = sn * (half - 2f - 0.7f) + sr * edge + Vector3.up * top;
            pin.Position = rack - sn * 2.6f + Vector3.up * 1.2f + sr * 0.6f;
            pin.Rotation = Quaternion.LookRotation(rack + Vector3.up * 0.3f - pin.Position);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture($"w27_{label}_7_props_rack", PfWidth, PfHeight);
            Vector3 cart = sn * (half - 2f - 1.5f) + sr * (-edge + 2.4f) + Vector3.up * top;
            pin.Position = cart - sn * 2.6f + Vector3.up * 1.2f - sr * 0.6f;
            pin.Rotation = Quaternion.LookRotation(cart + Vector3.up * 0.3f - pin.Position);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture($"w27_{label}_7_props_cart", PfWidth, PfHeight);
            var decalObject = GameObject.Find("Courtyard Decals");
            if (decalObject != null)
            {
                Vector3[] dv = decalObject.GetComponent<MeshFilter>().sharedMesh.vertices;
                for (int d = 0; d < 3; d++)
                {
                    Vector3 c = (dv[d * 4] + dv[d * 4 + 1] + dv[d * 4 + 2] + dv[d * 4 + 3]) * 0.25f;
                    Vector3 dir = new Vector3(c.x, 0f, c.z).normalized;
                    pin.Position = c - dir * 1.9f + Vector3.up * 1.0f;
                    pin.Rotation = Quaternion.LookRotation(c - pin.Position);
                    for (int i = 0; i < 3; i++) yield return null;
                    yield return Capture($"w27_{label}_7_floor_decal_{d}", PfWidth, PfHeight);
                }
            }
            Object.Destroy(pin);
            yield return null;

            // 6) 탑 앞 (문 앞 주인공)
            rig.ShowSide(0);
            for (int i = 0; i < 6; i++) yield return null;
            rig.DepthInput = -1f;
            yield return WaitFor(() => rig.NearStairs, 6f);
            rig.DepthInput = 0f;
            Assert.IsTrue(entry.Interact(), "E: 탑으로 내려가기");
            yield return WaitFor(() => rig.AtTower, 8f);
            Assert.IsTrue(rig.AtTower, "탑 앞 도착");
            for (int i = 0; i < 20; i++) yield return null;
            sim.RotateBeam(2);
            for (int i = 0; i < 24; i++) yield return null;
            yield return Capture($"w27_{label}_6_at_tower_door", PfWidth, PfHeight);
            entry.LeaveTower();
            yield return WaitFor(() => rig.OnWall, 8f);
            Note("탑·마당 그림 사진 촬영 완료");
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, $"w27_{label}_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
