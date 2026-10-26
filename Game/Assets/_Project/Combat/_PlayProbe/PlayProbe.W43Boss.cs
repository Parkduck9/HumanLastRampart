using System.Collections;
using System.IO;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Combat.Battle;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// 우두머리 성벽손 오우거 사진 1920×1080 (W43, Codex K-129 그림): 단계 띠 예고 · 걷기(빛 밖·빛 안) · 오르기(바깥·통로 위에서) ·
// 떨어짐·착지·누움·일어남·함정 넘어짐 · 통로 위 부수기 · 체력 막대 · 지도(큰 표식·가려는 자리·망원경) · 죽음 · 결과 줄
// 결과: HLR_PLAY_OUT/w43_*.png, w43_boss_probe.txt
public sealed partial class PlayProbe
{
    [UnityTest, Timeout(900000)]
    public IEnumerator W43BossShots() { yield return W43Shots(); }

    BattleEntry w43Entry;
    float w43KeepHp = 1f;   // 사진 동안 우두머리 체력이 이 비율 밑으로 안 내려가게 (0 = 끔)

    BattleBossArt W43Art() => ((BattleView)typeof(BattleEntry).GetField("view", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(w43Entry)).BossArt;

    BattleHud W43Hud() => (BattleHud)typeof(BattleEntry).GetField("hud", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(w43Entry);

    // 일반 적은 매 틱 모두 쓰러뜨려 우두머리만 보이게 (사진용)
    static void W43KillNormals(BattleSim sim)
    {
        for (int e = 0; e < sim.TotalEnemies; e++)
            if (e != sim.BossSlot && sim.DebugAlive(e)) sim.DebugHit(e, 1000000);
    }

    // 사진 한 장 뒤 그 프레임의 작업을 끝내 둔다 (바로 이어 모의 전투 값을 쓸 수 있게)
    IEnumerator W43Shot(string name)
    {
        if (w43Entry != null && w43Entry.Sim != null)
        {
            // 화면 글이 최신이도록 한 프레임 더 (글 갱신은 초당 5번으로 줄여 있어 빠르게 감은 직후에는 옛 값일 수 있다)
            W43Hud()?.ForceRefresh();
            yield return W43Frames(1);
        }
        yield return Capture(name, PfWidth, PfHeight);
        w43Entry?.Sim?.Complete();   // 결과 화면 사진에서는 전투가 이미 끝나 모의 전투가 없다
    }

    IEnumerator W43Frames(int n)
    {
        // 한 프레임 뒤 그 프레임의 작업을 직접 끝낸다 (배치 모드에는 프레임 끝 기다리기가 없다) — 작업이 끝난 뒤에만 모의 전투 값을 읽고 쓸 수 있다
        for (int i = 0; i < n; i++)
        {
            yield return null;
            w43Entry.Sim.Complete();
            W43KillNormals(w43Entry.Sim);
            BattleSim s = w43Entry.Sim;
            if (s.BossActive && w43KeepHp > 0f) s.DebugSetHp(s.BossSlot, Mathf.Max(s.BossHp, Mathf.RoundToInt(s.BossMaxHp * w43KeepHp)));   // 병사 사격에 죽지 않게 (사진용)
        }
    }

    IEnumerator W43Shots()
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
            // 점화의 밤으로 만든다: 등대 복구 100% (성벽은 처음부터 가득) + 빛줄기는 남쪽(북쪽 정면 적과 반대)
            CampaignState campaign = GameFlow.Instance.Campaign;
            campaign.BeaconRepair = 100f;
            campaign.PlannedBeamSide = 2;
            var story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>();
            for (int i = 0; i < 300 && story == null; i++) { yield return null; story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); }
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);

            w43Entry = Object.FindObjectsByType<BattleEntry>(FindObjectsSortMode.None)[0];
            BattleEntry entry = w43Entry;
            BattleSim sim = entry.Sim;
            BattleSetup setup = GameFlow.Instance.Campaign.ActiveBattleSetup;
            Assert.IsTrue(setup.IsIgnitionNight, "점화의 밤 전투");
            Assert.IsTrue(sim.BossPresent, "우두머리가 이 전투에 있다");
            var forecast = setup.Forecast; forecast.InitialWeather = Weather.Clear; forecast.TimeOfDay = TimeOfDay.Night; forecast.Changes = false; setup.Forecast = forecast;
            sim.WeatherDirty = true;
            entry.StartBattle();
            for (int i = 0; i < 20; i++) yield return null;

            int side = sim.BossSide;
            float half = sim.CastleHalf, top = BattleSim.WallHeight;
            Vector3 n = new Vector3(WallGeometry.Outward(side).x, 0f, WallGeometry.Outward(side).z), r = new Vector3(WallGeometry.Right(side).x, 0f, WallGeometry.Right(side).z);
            float along = sim.BossTargetAlong;
            Note($"우두머리 면 {side}, 가려는 자리 성벽 따라 {along:0.0} m, 체력 {sim.BossMaxHp}, 빛줄기 {sim.BeamSide}");
            entry.Rig.ShowSide(side);
            for (int i = 0; i < 6; i++) yield return null;

            // ── 1 단계 띠 예고 (나오기 5초 전부터) ──
            Note("단계 1: 등장 직전으로 건너뜀");
            w43KeepHp = 1f;
            entry.TutorialTimeScale = 16f;   // 등장 직전까지 빠르게 (건너뛰지 않고 흘려 보낸다 — 일반 적은 나오는 대로 쓰러뜨린다)
            int skipGuard = 0;
            while (sim.Elapsed < BattleSim.BossSpawnSeconds - 7f && skipGuard++ < 400) yield return W43Frames(1);
            Note($"단계 1: 등장 직전 {sim.Elapsed:0.0}s, 틱 {skipGuard}");
            entry.TutorialTimeScale = 1f;
            yield return W43Frames(1);
            int guard = 0;
            while (!sim.TryGetBossWarning(out _, out _) && guard++ < 200) yield return W43Frames(1);
            Note("단계 1: 예고 구간 도착");
            yield return W43Frames(30);   // 1.5초 — 남은 약 3.5초
            Assert.IsTrue(sim.TryGetBossWarning(out int warnSide, out float warnLeft), "예고 띠 구간");
            Note($"예고: 면 {warnSide}, 남은 {warnLeft:0.0}초");
            yield return W43Shot("w43_01_banner");
            guard = 0;
            while (!sim.BossSpawned && guard++ < 400) yield return W43Frames(1);
            Assert.IsTrue(sim.BossSpawned, "우두머리 등장");
            Note($"등장 {sim.BossAppearedAt:0.0}s, 자리 {sim.BossPosition}");

            // ── 2 걷기 (빛 밖) — 빠르게 감아 성벽 앞 약 45m까지 ──
            entry.TutorialTimeScale = 4f;
            guard = 0;
            while (sim.BossActive && Mathf.Max(Mathf.Abs(sim.BossPosition.x), Mathf.Abs(sim.BossPosition.z)) - half > 30f && guard++ < 1500) yield return W43Frames(1);
            entry.TutorialTimeScale = 0.001f;
            yield return W43Frames(8);
            Note($"빛 밖 걷기: 그림 {W43Art()?.CurrentPose} 칸 {W43Art()?.CurrentFrame}, 빛 안 {sim.BossLit}");
            Assert.IsFalse(sim.BossLit, "빛줄기 밖");
            yield return W43Shot("w43_02_walk_unlit");

            // 빛줄기를 우두머리 쪽으로 돌려 빛 안 (붉게, 빨라짐)
            sim.RotateBeam(side);
            entry.TutorialTimeScale = 4f;
            guard = 0;
            while (sim.BeamRotating && guard++ < 400) yield return W43Frames(1);
            entry.TutorialTimeScale = 0.001f;
            sim.DebugPlaceEnemy(sim.BossSlot, new Unity.Mathematics.float3(n.x, 0f, n.z) * (half + 24f) + new Unity.Mathematics.float3(r.x, 0f, r.z) * (along * 0.5f));
            yield return W43Frames(8);
            Note($"빛 안 걷기: 빛 안 {sim.BossLit}");
            Assert.IsTrue(sim.BossLit, "빛줄기 안");
            yield return W43Shot("w43_03_walk_lit");

            // ── 체력 막대 (55%) ──
            w43KeepHp = 0.55f;
            sim.DebugSetHp(sim.BossSlot, Mathf.RoundToInt(sim.BossMaxHp * 0.55f));
            yield return W43Frames(12);
            yield return W43Shot("w43_04_hpbar");

            // ── 지도: 망원경으로 확인 → 큰 표식·가려는 자리·"우두머리" 글 ──
            sim.LookThroughTelescope(new Vector3(sim.BossPosition.x, 0f, sim.BossPosition.z));
            yield return W43Frames(3);
            sim.RefreshSpottedGroups();
            entry.ToggleMap();
            yield return W43Frames(12);
            sim.RefreshSpottedGroups();
            yield return W43Frames(6);
            Assert.IsTrue(W43Hud().BossMapMarkersVisible, "지도가 열려 있으면 우두머리 표식이 보인다");
            yield return W43Shot("w43_05_map");
            entry.ToggleMap();
            yield return W43Frames(6);
            Assert.IsFalse(W43Hud().BossMapMarkersVisible, "지도를 닫으면 우두머리 표식도 사라진다 (화면에 남지 않는다)");

            // ── 오르기: 바깥에서(등 뒤) · 통로 위에서 내려다보며(앞) ──
            Camera cam = entry.BattleCamera;
            var pin = cam.gameObject.AddComponent<CameraPin>();
            pin.Target = cam;
            Vector3 foot = n * (half + 0.6f) + r * along;
            sim.DebugPlaceEnemy(sim.BossSlot, new Unity.Mathematics.float3(foot.x, 1.2f, foot.z));
            sim.DebugSetState(sim.BossSlot, EnemyState.Climbing);
            yield return W43Frames(4);
            pin.Position = n * (half + 11f) + r * (along + 2f) + Vector3.up * 3.2f;
            pin.Rotation = Quaternion.LookRotation(n * half + r * along + Vector3.up * 3.2f - pin.Position);
            yield return W43Frames(6);
            Note($"오르기(바깥): {W43Art()?.CurrentPose}");
            yield return W43Shot("w43_06_climb_outside");
            sim.DebugPlaceEnemy(sim.BossSlot, new Unity.Mathematics.float3(foot.x, 3.4f, foot.z));   // 끝에 가까이 올랐을 때 (손이 난간에 닿는다)
            pin.Position = n * (half - 2.6f) + r * (along - 1.0f) + Vector3.up * (top + 1.6f);
            pin.Rotation = Quaternion.LookRotation(n * (half + 0.6f) + r * along + Vector3.up * 3.0f - pin.Position);
            yield return W43Frames(6);
            Note($"오르기(위에서): {W43Art()?.CurrentPose}");
            yield return W43Shot("w43_07_climb_from_wall");

            // ── 떨어짐 → 착지 → 누움 → 일어남 (6초), 함정 넘어짐 (4초) ──
            Vector3 down = n * (half + 3.5f) + r * along;
            sim.DebugPlaceEnemy(sim.BossSlot, new Unity.Mathematics.float3(down.x, 0f, down.z));
            sim.DebugSetState(sim.BossSlot, EnemyState.Walking);
            pin.Position = n * (half + 13f) + r * (along + 3f) + Vector3.up * 3.4f;
            pin.Rotation = Quaternion.LookRotation(n * (half + 3.5f) + r * along + Vector3.up * 1.6f - pin.Position);
            (string name, float elapsed, float total, bool byFall)[] downs =
            {
                ("w43_08_fall", 0.3f, 6f, true), ("w43_09_land", 1.1f, 6f, true), ("w43_10_lie", 3.2f, 6f, true), ("w43_11_getup", 5.4f, 6f, true), ("w43_12_trapfall", 0.3f, 4f, false),
            };
            foreach (var d in downs)
            {
                sim.DebugKnockdown(d.total - d.elapsed, d.total, d.byFall);
                yield return W43Frames(8);
                Note($"{d.name}: 그림 {W43Art()?.CurrentPose} 칸 {W43Art()?.CurrentFrame}");
                yield return W43Shot(d.name);
            }

            sim.DebugKnockdown(0f, 0f, false);

            // ── 통로 위: 부수는 중 (가까이·성벽 위에서 본 기본 시점) ──
            Vector3 walkway = n * (half - 0.4f) + r * along;
            sim.DebugPlaceEnemy(sim.BossSlot, new Unity.Mathematics.float3(walkway.x, top, walkway.z));
            sim.DebugSetState(sim.BossSlot, EnemyState.OnWall);
            pin.Position = n * (half - 4.5f) + r * (along - 4f) + Vector3.up * (top + 1.8f);
            pin.Rotation = Quaternion.LookRotation(walkway + Vector3.up * (top + 0.9f) - pin.Position);
            yield return W43Frames(8);
            Note($"통로 위: {W43Art()?.CurrentPose}");
            yield return W43Shot("w43_13_onwalkway_close");
            Object.Destroy(pin);
            yield return null;
            entry.Rig.ShowSide(side);
            // 기본 시점은 성벽 가운데를 비춘다: 우두머리를 가운데 통로로 옮겨 찍는다 (실제 전투에서는 가려는 자리에 있다)
            sim.Complete();
            Vector3 centerSpot = n * (half - 0.4f);
            sim.DebugPlaceEnemy(sim.BossSlot, new Unity.Mathematics.float3(centerSpot.x, top, centerSpot.z));
            yield return W43Frames(10);
            {
                Vector3 bp = sim.BossPosition;
                Vector3 feetScreen = cam.WorldToScreenPoint(bp), headScreen = cam.WorldToScreenPoint(bp + Vector3.up * 4.5f);
                Note($"통로 기본 시점: 보임 {W43Art()?.Visible} 상태 {sim.BossState} 위치 {bp} 카메라 {cam.transform.position} 앞 {cam.transform.forward} 발 화면 {feetScreen} 머리 화면 {headScreen}");
                Assert.IsTrue(W43Art() != null && W43Art().Visible, "통로 위 우두머리가 기본 시점에서 그려진다");
                Note($"카메라 시야 {cam.fieldOfView} 가로세로 {cam.aspect} 화면 {Screen.width}x{Screen.height} 회전 {cam.transform.rotation.eulerAngles}");
            }
            yield return W43Shot("w43_14_onwalkway_default_view");

            // ── 죽음 → 결과 줄 ──
            w43KeepHp = 0f;
            sim.DebugHit(sim.BossSlot, 1000000);
            entry.TutorialTimeScale = 1f;
            yield return W43Frames(6);
            Assert.IsTrue(sim.BossKilled, "처치");
            yield return W43Frames(8);
            Note($"죽음: {W43Art()?.CurrentPose} 칸 {W43Art()?.CurrentFrame}");
            yield return W43Shot("w43_15_death");
            // 진짜 끝나는 길로: 모의 전투를 승리로 끝내면 BattleEntry.ShowEnd가 BuildResult(우두머리 기록 포함)로 결과 화면을 연다
            entry.TutorialTimeScale = 1f;
            sim.Complete();
            Assert.IsNotNull(sim.BossEndLine, "결과 줄");
            Note("결과 줄: " + sim.BossEndLine);
            sim.ForceEnd(BattleOutcome.Victory, DefeatReason.None, 0);
            for (int i = 0; i < 40; i++) yield return null;
            yield return Capture("w43_16_endpanel", PfWidth, PfHeight);   // 전투 안 끝 판 (우두머리 줄 포함)
            yield return Press("보고서 보기", 300);
            yield return WaitScreen(ScreenId.Result, 900);
            for (int i = 0; i < 60; i++) yield return null;
            yield return W43Shot("w43_17_result");
            Note("우두머리 사진 촬영 완료");
        }
        finally
        {
            HumanLastRampart.Tutorial.TutorialState.Suppressed = suppressed;
            KeepPreparation = false;
            File.WriteAllText(Path.Combine(Out, "w43_boss_probe.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }
}
