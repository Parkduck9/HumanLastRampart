using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Flow;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

// 실제 화면 그리기 비용 (K-061): 현실 4번 전투를 병사 약 4,200명으로 화면을 켜고 돌려 구간마다 프레임 시간을 잰다.
// 구간: 전투 초반 · 적이 성벽에 몰린 중반 · 불바다+폭발 동시. 중반에서 화면 효과(BattleLook) 켬/끔, 안개 켬/끔 비교.
// 잰 값: 실제 프레임 시간(스톱워치 — 평균·상위 1%·최대), 프로파일러 계수기(메인·렌더 스레드·GPU 시간, 그리기 호출·배치·SetPass,
// 프레임당 관리 메모리 할당) — 얻을 수 없는 값은 '없음'. 결과: HLR_PLAY_OUT/frame_timing.txt
// 게임 시간은 프레임마다 0.05초로 고정(captureDeltaTime)이라 전투 진행은 실제 속도와 무관하다
public sealed partial class PlayProbe
{
    const int TimingSoldierTarget = 4200;
    const int TimingWindowFrames = 120;

    sealed class TimingRecorders
    {
        readonly List<(string name, ProfilerRecorder recorder, bool nanoseconds)> list = new List<(string, ProfilerRecorder, bool)>();

        public TimingRecorders()
        {
            Add(ProfilerCategory.Internal, "Main Thread", true);
            Add(ProfilerCategory.Internal, "CPU Main Thread Frame Time", true);
            Add(ProfilerCategory.Internal, "CPU Render Thread Frame Time", true);
            Add(ProfilerCategory.Render, "GPU Frame Time", true);
            Add(ProfilerCategory.Render, "Draw Calls Count", false);
            Add(ProfilerCategory.Render, "Batches Count", false);
            Add(ProfilerCategory.Render, "SetPass Calls Count", false);
            Add(ProfilerCategory.Render, "Triangles Count", false);
            Add(ProfilerCategory.Memory, "GC Allocated In Frame", false);
        }

        void Add(ProfilerCategory category, string name, bool ns) => list.Add((name, ProfilerRecorder.StartNew(category, name, TimingWindowFrames + 8), ns));

        // 지난 frames 프레임 평균 (없으면 '없음')
        public string Summary(int frames)
        {
            var sb = new StringBuilder();
            foreach (var (name, recorder, ns) in list)
            {
                if (!recorder.Valid || recorder.Count == 0) { sb.Append($"{name} 없음 · "); continue; }
                int n = Mathf.Min(frames, recorder.Count);
                double sum = 0, max = 0;
                for (int i = recorder.Count - n; i < recorder.Count; i++)
                {
                    double v = recorder.GetSample(i).Value;
                    sum += v;
                    if (v > max) max = v;
                }

                double avg = sum / n;
                if (name == "GPU Frame Time" && max <= 0) { sb.Append($"{name} 사용 불가(원시 계수 0) · "); continue; }   // 0을 값으로 적지 않는다
                sb.Append(ns ? $"{name} {avg / 1e6:0.00}ms(최대 {max / 1e6:0.0}) · " : $"{name} {avg:0} (최대 {max:0}) · ");
            }

            return sb.ToString().TrimEnd(' ', '·');
        }

        // GPU 원시 계수 평균(ms). 계수기가 없거나 0뿐이면 -1 = 사용 불가
        public double GpuMs(int frames)
        {
            foreach (var (name, recorder, _) in list)
            {
                if (name != "GPU Frame Time" || !recorder.Valid || recorder.Count == 0) continue;
                int n = Mathf.Min(frames, recorder.Count);
                double sum = 0;
                for (int i = recorder.Count - n; i < recorder.Count; i++) sum += recorder.GetSample(i).Value;
                return sum > 0 ? sum / n / 1e6 : -1;
            }

            return -1;
        }

        public void Dispose() { foreach (var (_, recorder, _) in list) recorder.Dispose(); }
    }

    // 배치 실행(-batchmode)에서는 유니티가 화면을 그리지 않는다(사진 찍을 때만 카메라를 직접 그림) — 프레임 시간에 그리기가 빠진다.
    // 그래서 배치 실행이면 측정 구간 동안 매 프레임 이 도우미가 모든 카메라를 렌더 텍스처(기본 1920×1080)에 직접 그리고,
    // 1화소를 읽어 GPU가 다 그릴 때까지 기다린 시간(= CPU 그리기 제출 + GPU 완료)을 잰다. 화면 위 표시판(오버레이 UI)은 빠진다
    ProbeRenderTimer renderTimer;

    // 4,200명 부하 시험은 최소 간격을 지킬 수 있는 80m 시험 성벽을 쓴다.
    // 에셋은 저장하지 않고 메모리 값만 바꿨다가 반드시 복원한다. 40m 성벽의 과거 성능값과 직접 비교하지 않는다.
    sealed class CrowdedProbeTerrain : System.IDisposable
    {
        readonly List<(HumanLastRampart.Core.Definitions.TerrainDef terrain, float half)> saved = new List<(HumanLastRampart.Core.Definitions.TerrainDef, float)>();
        readonly System.Reflection.FieldInfo field = typeof(HumanLastRampart.Core.Definitions.TerrainDef)
            .GetField("castleHalfSize", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        public CrowdedProbeTerrain()
        {
            Assert.IsNotNull(field);
            foreach (var terrain in GameFlow.Instance.Definitions.All<HumanLastRampart.Core.Definitions.TerrainDef>())
            {
                saved.Add((terrain, terrain.CastleHalfSize));
                field.SetValue(terrain, Mathf.Max(40f, terrain.CastleHalfSize));
            }
        }

        public void Dispose()
        {
            foreach (var item in saved) field.SetValue(item.terrain, item.half);
        }
    }

    // 부하를 줄여 통과시키지 않도록 인원과 줄별 간격도 확인한다
    static void AssertCrowdedProbe(HumanLastRampart.Combat.Battle.BattleSim sim)
    {
        Assert.GreaterOrEqual(sim.SoldierCount, TimingSoldierTarget, "4,200명 부하 유지");
        var rows = new Dictionary<int, List<float>>();
        for (int i = 0; i < sim.SoldierCount; i++)
        {
            int row = sim.SoldierRow(i, out int side, out float along);
            if (row < 0) continue;   // 대포 운용병은 별도 자리
            int key = side * 10 + row;
            if (!rows.TryGetValue(key, out var values)) rows[key] = values = new List<float>();
            values.Add(along);
        }
        foreach (var values in rows.Values)
        {
            values.Sort();
            for (int i = 1; i < values.Count; i++) Assert.GreaterOrEqual(values[i] - values[i - 1], 0.24f, "부하 시험도 같은 줄에서 겹치지 않음");
        }
    }

    [UnityTest]
    public IEnumerator FrameTiming()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        KeepPreparation = true;
        bool look0 = HumanLastRampart.Combat.Render.BattleLook.Enabled;
        bool farSprites0 = HumanLastRampart.Combat.Render.FarSprites.Enabled;
        var recorders = new TimingRecorders();
        bool batch = Application.isBatchMode;
        if (!batch) Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);   // 창 실행: 실제 화면을 1920×1080으로
        CrowdedProbeTerrain crowdedTerrain = null;
        try
        {
            SceneManager.LoadScene("Boot");
            yield return null;
            yield return null;
            yield return WaitScreen(ScreenId.MainMenu, 900);
            yield return Press("게임 시작", 600);
            yield return Press("현실", 300);
            yield return WaitScreen(ScreenId.Story, 600);

            // 현실 4번 전투, 분대 인원을 늘려 병사 약 4,200명 (SimProbe.FrameCost 규모). 전투 준비는 스토리가 끝날 때 만들어진다
            CampaignState c = GameFlow.Instance.Campaign;
            c.BattleNumber = 4;
            var line = c.Squads.Where(s => !s.UnitDefId.Contains("cannon")).ToList();
            int cannonCrew = c.Squads.Where(s => s.UnitDefId.Contains("cannon")).Sum(s => s.Count);
            int each = Mathf.CeilToInt((TimingSoldierTarget - cannonCrew) / (float)Mathf.Max(1, line.Count));
            foreach (SquadState s in line) s.Count = each;
            crowdedTerrain = new CrowdedProbeTerrain();
            Note("4,200명 · 시험 성벽 80m: 기존 40m 성능 기준과 직접 비교하지 않음");
            var story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>();
            for (int i = 0; i < 300 && story == null; i++) { yield return null; story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); }
            Assert.IsNotNull(story, "스토리 재생기");
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);
            // 마지막 장면 전환이 끝난 뒤 만들어야 측정 중 지워지지 않는다.
            if (batch) renderTimer = new GameObject("PlayProbe Render Timer").AddComponent<ProbeRenderTimer>();

            BattleSetup setup = c.ActiveBattleSetup;
            var f = setup.Forecast; f.InitialWeather = Weather.Clear; f.TimeOfDay = TimeOfDay.Day; f.Changes = false; setup.Forecast = f;   // 맑은 낮으로 고정 (비교 기준)
            var entry = Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
            var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry)
                .GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
            sim.WeatherDirty = true;
            Note($"설정: 현실 {setup.BattleNumber}번, 적 배율 {setup.EnemyCountMultiplier}, 병사 {sim.SoldierCount}명, 적 최대 동시 {setup.MaxConcurrentEnemies}, 화면 {Screen.width}×{Screen.height}, " +
                 $"그래픽 {SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceName}, 프레임 통계 기능 {FrameTimingManager.IsFeatureEnabled()}, " +
                 (batch ? $"배치 실행 — 카메라 직접 그리기 {renderTimer.Width}×{renderTimer.Height}로 그리기 시간 측정" : "창 실행 — 실제 화면 그리기 포함"));
            AssertCrowdedProbe(sim);
            // 시험 전용: 90초(공성 단계) 넘어 75초 중반·마법까지 닿도록 패배 조건을 풀고 성벽 체력을 매우 크게 한다
            // (BattleArtPreviewTests와 같은 반사 방식. 게임 규칙·데이터는 그대로, 이 시뮬레이션 개체에서만)
            var simFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(HumanLastRampart.Combat.Battle.BattleSim).GetField("garrisonLossRatio", simFlags).SetValue(sim, float.MaxValue);
            var simWallHp = (float[])typeof(HumanLastRampart.Combat.Battle.BattleSim).GetField("wallHp", simFlags).GetValue(sim);
            for (int w = 0; w < simWallHp.Length; w++) simWallHp[w] = 1e9f;
            Note("시험 전용: 병력 손실·성벽 파괴 패배 해제 (이 측정 개체만) — 90초 이후 단계까지 살아남기 위함");
            entry.StartBattle();
            for (int i = 0; i < 30; i++) yield return null;

            // ① 10초 (초반·선봉 단계)
            yield return AdvanceTo(sim, 10f);
            if (EndedEarly(sim, "10초")) yield break;
            yield return MeasureWindow("① 10초 (맑은 낮, 효과 켬)", sim, recorders);
            yield return Capture("timing_1_10s");

            // ② 45초
            yield return AdvanceTo(sim, 45f);
            if (EndedEarly(sim, "45초")) yield break;
            yield return MeasureWindow("② 45초", sim, recorders);
            yield return Capture("timing_2_45s");

            // ③ 적이 성벽에 몰린 중반: 게임 시간 75초
            yield return AdvanceTo(sim, 75f);
            if (EndedEarly(sim, "75초")) yield break;
            yield return MeasureWindow("③ 75초 중반 (성벽에 몰림, 효과 켬)", sim, recorders);
            yield return Capture("timing_3_mid");

            // 먼 적 저비용 그림 끔 (W4b, K-099) — 위 ③과 같은 장면 비교 한 줄
            // 켬/끔을 각각 명시적으로 강제해 비교한다 (기본값이 꺼짐이어도 의미 있게). 원래 값은 finally에서 복원
            HumanLastRampart.Combat.Render.FarSprites.Enabled = true;
            for (int i = 0; i < 5; i++) yield return null;
            yield return MeasureWindow("③ 75초대 · 먼 적 그림 켬", sim, recorders);
            HumanLastRampart.Combat.Render.FarSprites.Enabled = false;
            for (int i = 0; i < 5; i++) yield return null;
            yield return MeasureWindow("③ 75초대 · 먼 적 그림 끔 (원래 크기만)", sim, recorders);
            HumanLastRampart.Combat.Render.FarSprites.Enabled = farSprites0;
            for (int i = 0; i < 5; i++) yield return null;

            // 같은 장면을 640×480으로 (지난 측정 해상도와 비교)
            if (batch) { renderTimer.Width = 640; renderTimer.Height = 480; }
            else Screen.SetResolution(640, 480, FullScreenMode.Windowed);
            yield return MeasureWindow("③ 75초대 · 640×480", sim, recorders);
            if (batch) { renderTimer.Width = 1920; renderTimer.Height = 1080; }
            else Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);

            HumanLastRampart.Combat.Render.BattleLook.Enabled = false;
            for (int i = 0; i < 5; i++) yield return null;
            yield return MeasureWindow("③ 75초대 · 화면 효과 끔 (빛 받기·그림자도 꺼짐)", sim, recorders);
            yield return Capture("timing_2_mid_look_off");
            HumanLastRampart.Combat.Render.BattleLook.Enabled = true;

            f = setup.Forecast; f.InitialWeather = Weather.Fog; setup.Forecast = f; sim.WeatherDirty = true;
            for (int i = 0; i < 5; i++) yield return null;
            yield return MeasureWindow("③ 75초대 · 안개 켬", sim, recorders);
            yield return Capture("timing_2_mid_fog");
            f = setup.Forecast; f.InitialWeather = Weather.Clear; f.TimeOfDay = TimeOfDay.Night; setup.Forecast = f; sim.WeatherDirty = true;
            for (int i = 0; i < 5; i++) yield return null;
            yield return MeasureWindow("③ 75초대 · 밤 (횃불 켬)", sim, recorders);
            yield return Capture("timing_2_mid_night");
            f = setup.Forecast; f.TimeOfDay = TimeOfDay.Day; setup.Forecast = f; sim.WeatherDirty = true;
            for (int i = 0; i < 5; i++) yield return null;

            // ④ 마법 시전 중: 불바다 + 폭발 동시, 정면 성벽 앞 20m에 둘 다 (전투 입력과 같은 BattleSim.CastSpell).
            // 마법사가 다른 성벽에 있으면 걸어가 도착해야 효과가 생기므로(이동 중엔 다음 시전 거절) 효과가 시작될 때까지 기다린다
            sim.Complete();
            var at = new Vector3(0f, 0f, sim.CastleHalf + 20f);
            int fire = -1, blast = -1;
            for (int s = 0; s < sim.SpellCount; s++)
            {
                if (sim.GetSpell(s).IsFire) { if (fire < 0) fire = s; }
                else if (blast < 0) blast = s;
            }

            int started0 = sim.EffectsStarted;
            bool fireOk = fire >= 0 && sim.CastSpell(fire, at);
            for (int i = 0; i < 900 && fireOk && sim.EffectsStarted <= started0 && !sim.Ended; i++) yield return null;   // 마법사 도착·불바다 시작 (최대 45초)
            bool blastOk = blast >= 0 && !sim.Ended && sim.CastSpell(blast, at + new Vector3(6f, 0f, 0f));
            for (int i = 0; i < 900 && blastOk && sim.EffectsStarted <= started0 + 1 && !sim.Ended; i++) yield return null;
            Note($"마법: 불바다 {(fireOk ? "시전" : "실패")}, 폭발 {(blastOk ? "시전" : "실패")} (마법사 {(sim.WizardPresent ? "있음" : "없음")}), 시작된 효과 {sim.EffectsStarted - started0}개, 동시 효과 {sim.Effects.Count}개");
            for (int i = 0; i < 10; i++) yield return null;   // 폭발은 시작 1초 뒤 — 터지는 순간이 구간 안에 들게 0.5초만 기다림
            yield return MeasureWindow("④ 마법 시전 중 (불바다+폭발)", sim, recorders, 60);
            yield return Capture("timing_4_fire_blast");

            // ⑤ 95초 이후 (공성 단계 — 사다리·오우거가 나오는 때)
            yield return AdvanceTo(sim, 95f);
            if (EndedEarly(sim, "95초")) yield break;
            yield return MeasureWindow("⑤ 95초 이후 (공성 단계)", sim, recorders);
            yield return Capture("timing_5_siege");
        }
        finally
        {
            crowdedTerrain?.Dispose();
            KeepPreparation = false;
            HumanLastRampart.Combat.Render.BattleLook.Enabled = look0;
            HumanLastRampart.Combat.Render.FarSprites.Enabled = farSprites0;
            recorders.Dispose();
            if (renderTimer != null) Object.Destroy(renderTimer.gameObject);
            renderTimer = null;
            File.WriteAllText(Path.Combine(Out, "frame_timing.txt"), log.ToString() + TimingTable(), new UTF8Encoding(false));
            timingRows.Clear();
            Time.captureDeltaTime = 0f;
        }
    }

    // 구간별 결과 표 (측정이 끝나면 frame_timing.txt 맨 아래에 붙인다)
    readonly List<string> timingRows = new List<string>();

    static double Percentile(List<double> sorted, double q) => sorted[Mathf.Clamp(Mathf.CeilToInt((float)(q * sorted.Count)) - 1, 0, sorted.Count - 1)];

    string TimingTable()
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("== 구간별 프레임 시간 표 (ms, 낮을수록 좋음; 게임초 = 구간 끝 시각; 적 = 살아 있는 수; 직접 그리기 = 배치 실행만)");
        sb.AppendLine("| 구간 | 게임초 | 평균 | p50 | p95 | p99 | 최대 | GPU ms (출처) | 직접 그리기 평균 | 적 | 병사 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (string row in timingRows) sb.AppendLine(row);
        return sb.ToString();
    }

    // 게임 시간이 seconds초가 될 때까지 ×4로 돌린 뒤 ×1로 되돌린다 (이미 지났으면 그대로)
    IEnumerator AdvanceTo(HumanLastRampart.Combat.Battle.BattleSim sim, float seconds)
    {
        if (sim.Elapsed >= seconds || sim.Ended) yield break;
        yield return Press("×4", 120);
        for (int i = 0; i < 6000 && sim.Elapsed < seconds && !sim.Ended; i++) yield return null;
        yield return Press("×1", 120);
    }

    // 전투가 먼저 끝났으면 이유를 적고 true (종료 화면을 전투 성능으로 집계하지 않는다)
    bool EndedEarly(HumanLastRampart.Combat.Battle.BattleSim sim, string stage)
    {
        if (!sim.Ended) return false;
        Note($"{stage} 측정 보류: 전투가 {sim.Elapsed:0.0}초에 끝남 ({sim.Outcome}, {sim.DefeatReason}). 이후 구간은 측정하지 못함.");
        return true;
    }

    // frames 프레임 동안 실제 프레임 시간(스톱워치)과 계수기 평균
    IEnumerator MeasureWindow(string title, HumanLastRampart.Combat.Battle.BattleSim sim, TimingRecorders recorders, int frames = TimingWindowFrames)
    {
        for (int i = 0; i < 5; i++) yield return null;   // 바꾼 설정이 자리 잡게
        if (renderTimer != null) { renderTimer.Samples.Clear(); renderTimer.Recording = true; }
        var times = new List<double>(frames);
        var watch = Stopwatch.StartNew();
        var timings = new UnityEngine.FrameTiming[1];   // 이 클래스의 FrameTiming 시험 함수와 이름이 겹쳐 전체 이름으로
        double gpuSum = 0, cpuSum = 0;
        int timingCount = 0;
        for (int i = 0; i < frames; i++)
        {
            yield return null;
            times.Add(watch.Elapsed.TotalMilliseconds);
            watch.Restart();
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].cpuFrameTime > 0)
            {
                gpuSum += timings[0].gpuFrameTime;
                cpuSum += timings[0].cpuMainThreadFrameTime;
                timingCount++;
            }
        }

        if (renderTimer != null) renderTimer.Recording = false;
        times.Sort();
        double avg = times.Average();
        double top1 = times.Skip(Mathf.Max(0, times.Count - Mathf.Max(1, times.Count / 100))).Average();
        sim.Complete();
        double p50 = Percentile(times, 0.50), p95 = Percentile(times, 0.95), p99 = Percentile(times, 0.99);
        Note($"{title}: 프레임 평균 {avg:0.00}ms (약 {1000.0 / avg:0}fps) · p50 {p50:0.00} · p95 {p95:0.00} · p99 {p99:0.00} · 상위 1% {top1:0.0}ms · 최대 {times[times.Count - 1]:0.0}ms · 게임 {sim.Elapsed:0}초 · 처치 {sim.Kills}");
        Note($"    계수기: {recorders.Summary(frames)}");
        double gpuRaw = recorders.GpuMs(frames);
        double gpuFtm = timingCount > 0 && gpuSum > 0 ? gpuSum / timingCount : -1;
        string gpuText = gpuRaw > 0 ? $"{gpuRaw:0.00} (원시 계수)" : gpuFtm > 0 ? $"{gpuFtm:0.00} (FrameTimingManager)" : "사용 불가";
        Note(timingCount > 0 ? $"    FrameTimingManager: 메인 {cpuSum / timingCount:0.00}ms · GPU {(gpuFtm > 0 ? gpuFtm.ToString("0.00") + "ms" : "사용 불가(0)")}" : "    FrameTimingManager: 값 없음 (프로젝트 설정 Frame Timing Stats 꺼짐 또는 배치 실행)");
        Note($"    GPU 시간: {gpuText}");
        string drawText = "없음";
        if (renderTimer != null && renderTimer.Samples.Count > 0) drawText = $"{renderTimer.Samples.Average():0.00}";
        timingRows.Add($"| {title} | {sim.Elapsed:0} | {avg:0.00} | {p50:0.00} | {p95:0.00} | {p99:0.00} | {times[times.Count - 1]:0.0} | {gpuText} | {drawText} | {sim.Spawned - sim.Kills} | {sim.SoldierCount} |");
        if (Application.isBatchMode)
        {
            Assert.IsNotNull(renderTimer, "배치 그리기 측정기는 장면 전환 뒤에도 살아 있어야 함");
            Assert.Greater(renderTimer.Samples.Count, 0, "실제 카메라 그리기 표본 없이 성능 측정 통과 금지");
        }
        if (renderTimer != null && renderTimer.Samples.Count > 0)
        {
            var draw = new List<double>(renderTimer.Samples);
            draw.Sort();
            double drawTop1 = draw.Skip(Mathf.Max(0, draw.Count - Mathf.Max(1, draw.Count / 100))).Average();
            Note($"    카메라 직접 그리기 {renderTimer.Width}×{renderTimer.Height} (GPU 완료까지): 평균 {draw.Average():0.00}ms · 상위 1% {drawTop1:0.0}ms · 최대 {draw[draw.Count - 1]:0.0}ms — 위 프레임 시간에 이미 들어 있다");
        }
    }
}

// 배치 실행용 그리기 시간 재기 (K-061): 기록 중이면 매 프레임 끝에 모든 카메라를 렌더 텍스처에 그리고 1화소를 읽어 GPU 완료를 기다린다.
// 게임의 그리기 예약(DrawMeshInstancedProcedural 등)이 끝난 뒤에 돌도록 실행 순서를 맨 뒤 바로 앞으로 둔다 (사진 찍개 ProbeShooter보다 앞)
[DefaultExecutionOrder(int.MaxValue - 1)]
public sealed class ProbeRenderTimer : MonoBehaviour
{
    public int Width = 1920, Height = 1080;
    public bool Recording;
    public readonly List<double> Samples = new List<double>(256);
    RenderTexture target;
    Texture2D pixel;
    Camera[] cameras = new Camera[8];
    readonly Stopwatch watch = new Stopwatch();

    void LateUpdate()
    {
        if (!Recording) return;
        if (target == null || target.width != Width || target.height != Height)
        {
            if (target != null) Destroy(target);
            target = new RenderTexture(Width, Height, 24);
        }

        if (pixel == null) pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        if (cameras.Length < Camera.allCamerasCount) cameras = new Camera[Camera.allCamerasCount];
        int count = Camera.GetAllCameras(cameras);
        watch.Restart();
        for (int i = 0; i < count; i++)
        {
            Camera cam = cameras[i];
            RenderTexture old = cam.targetTexture;
            cam.targetTexture = target;
            cam.Render();
            cam.targetTexture = old;
        }

        RenderTexture.active = target;
        pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);   // GPU가 다 그릴 때까지 기다린다
        RenderTexture.active = null;
        Samples.Add(watch.Elapsed.TotalMilliseconds);
    }

    void OnDestroy()
    {
        if (target != null) Destroy(target);
        if (pixel != null) Destroy(pixel);
    }
}
