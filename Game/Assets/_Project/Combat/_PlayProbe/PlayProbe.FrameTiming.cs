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
                sb.Append(ns ? $"{name} {avg / 1e6:0.00}ms(최대 {max / 1e6:0.0}) · " : $"{name} {avg:0} (최대 {max:0}) · ");
            }

            return sb.ToString().TrimEnd(' ', '·');
        }

        public void Dispose() { foreach (var (_, recorder, _) in list) recorder.Dispose(); }
    }

    [UnityTest]
    public IEnumerator FrameTiming()
    {
        Directory.CreateDirectory(Out);
        log = new StringBuilder();
        Time.captureDeltaTime = 1f / 20f;
        KeepPreparation = true;
        bool look0 = HumanLastRampart.Combat.Render.BattleLook.Enabled;
        var recorders = new TimingRecorders();
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
            var story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>();
            for (int i = 0; i < 300 && story == null; i++) { yield return null; story = Object.FindAnyObjectByType<HumanLastRampart.Story.StoryPlayer>(); }
            Assert.IsNotNull(story, "스토리 재생기");
            story.Skip();
            yield return WaitScreen(ScreenId.Battle, 900);

            BattleSetup setup = c.ActiveBattleSetup;
            var f = setup.Forecast; f.InitialWeather = Weather.Clear; f.TimeOfDay = TimeOfDay.Day; f.Changes = false; setup.Forecast = f;   // 맑은 낮으로 고정 (비교 기준)
            var entry = Object.FindObjectsByType<HumanLastRampart.Combat.Battle.BattleEntry>(FindObjectsSortMode.None)[0];
            var sim = (HumanLastRampart.Combat.Battle.BattleSim)typeof(HumanLastRampart.Combat.Battle.BattleEntry)
                .GetField("sim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(entry);
            sim.WeatherDirty = true;
            Note($"설정: 현실 {setup.BattleNumber}번, 적 배율 {setup.EnemyCountMultiplier}, 병사 {sim.SoldierCount}명, 적 최대 동시 {setup.MaxConcurrentEnemies}, 화면 {Screen.width}×{Screen.height}, " +
                 $"그래픽 {SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceName}, 프레임 통계 기능 {FrameTimingManager.IsFeatureEnabled()}");
            entry.StartBattle();
            for (int i = 0; i < 30; i++) yield return null;

            // ① 초반
            yield return MeasureWindow("① 초반 (맑은 낮, 효과 켬)", sim, recorders);
            yield return Capture("timing_1_early");

            // ② 적이 성벽에 몰린 중반: 게임 시간 75초까지 (×4)
            yield return Press("×4", 120);
            for (int i = 0; i < 6000 && sim.Elapsed < 75f && !sim.Ended; i++) yield return null;
            yield return Press("×1", 120);
            yield return MeasureWindow("② 중반 (성벽에 몰림, 효과 켬)", sim, recorders);
            yield return Capture("timing_2_mid");

            HumanLastRampart.Combat.Render.BattleLook.Enabled = false;
            for (int i = 0; i < 5; i++) yield return null;
            yield return MeasureWindow("② 중반 · 화면 효과 끔 (빛 받기·그림자도 꺼짐)", sim, recorders);
            yield return Capture("timing_2_mid_look_off");
            HumanLastRampart.Combat.Render.BattleLook.Enabled = true;

            f = setup.Forecast; f.InitialWeather = Weather.Fog; setup.Forecast = f; sim.WeatherDirty = true;
            for (int i = 0; i < 5; i++) yield return null;
            yield return MeasureWindow("② 중반 · 안개 켬", sim, recorders);
            yield return Capture("timing_2_mid_fog");
            f = setup.Forecast; f.InitialWeather = Weather.Clear; f.TimeOfDay = TimeOfDay.Night; setup.Forecast = f; sim.WeatherDirty = true;
            for (int i = 0; i < 5; i++) yield return null;
            yield return MeasureWindow("② 중반 · 밤 (횃불 켬)", sim, recorders);
            yield return Capture("timing_2_mid_night");
            f = setup.Forecast; f.TimeOfDay = TimeOfDay.Day; setup.Forecast = f; sim.WeatherDirty = true;
            for (int i = 0; i < 5; i++) yield return null;

            // ③ 불바다 + 폭발 동시: 정면 성벽 앞 20m에 둘 다
            sim.Complete();
            var at = new Vector3(0f, 0f, sim.CastleHalf + 20f);
            int fire = -1, blast = -1;
            for (int s = 0; s < sim.SpellCount; s++)
            {
                if (sim.GetSpell(s).IsFire) { if (fire < 0) fire = s; }
                else if (blast < 0) blast = s;
            }

            bool fireOk = fire >= 0 && sim.CastSpell(fire, at);
            bool blastOk = blast >= 0 && sim.CastSpell(blast, at + new Vector3(6f, 0f, 0f));
            Note($"마법: 불바다 {(fireOk ? "시전" : "실패")}, 폭발 {(blastOk ? "시전" : "실패")} (마법사 {(sim.WizardPresent ? "있음" : "없음")})");
            for (int i = 0; i < 10; i++) yield return null;   // 폭발은 1초 뒤 — 터지는 순간이 구간 안에 들게 0.5초만 기다림
            yield return MeasureWindow("③ 불바다+폭발", sim, recorders, 60);
            yield return Capture("timing_3_fire_blast");
        }
        finally
        {
            KeepPreparation = false;
            HumanLastRampart.Combat.Render.BattleLook.Enabled = look0;
            recorders.Dispose();
            File.WriteAllText(Path.Combine(Out, "frame_timing.txt"), log.ToString(), new UTF8Encoding(false));
            Time.captureDeltaTime = 0f;
        }
    }

    // frames 프레임 동안 실제 프레임 시간(스톱워치)과 계수기 평균
    IEnumerator MeasureWindow(string title, HumanLastRampart.Combat.Battle.BattleSim sim, TimingRecorders recorders, int frames = TimingWindowFrames)
    {
        for (int i = 0; i < 5; i++) yield return null;   // 바꾼 설정이 자리 잡게
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

        times.Sort();
        double avg = times.Average();
        double top1 = times.Skip(Mathf.Max(0, times.Count - Mathf.Max(1, times.Count / 100))).Average();
        sim.Complete();
        Note($"{title}: 프레임 평균 {avg:0.00}ms (약 {1000.0 / avg:0}fps) · 상위 1% {top1:0.0}ms · 최대 {times[times.Count - 1]:0.0}ms · 게임 {sim.Elapsed:0}초 · 처치 {sim.Kills}");
        Note($"    계수기: {recorders.Summary(frames)}");
        Note(timingCount > 0 ? $"    FrameTimingManager: 메인 {cpuSum / timingCount:0.00}ms · GPU {gpuSum / timingCount:0.00}ms" : "    FrameTimingManager: 값 없음 (프로젝트 설정 Frame Timing Stats 꺼짐)");
    }
}
