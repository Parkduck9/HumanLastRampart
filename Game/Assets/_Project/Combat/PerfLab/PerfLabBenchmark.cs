using System;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace HumanLastRampart.Combat.PerfLab
{
    // 자동 측정 (PERF_PROTOTYPE.md 5장·8장). 에디터는 B 키로 시작.
    // 해상도 2모드(내부 절반+2배 / 네이티브) × S1~S3. 각 실행: 준비 5초 → 측정 10초. 적은 사거리 안에서 출발(교전 최대).
    // 결과는 로그(에디터 Editor.log / 빌드 Player.log)와 CSV에 남긴다. 측정 중 프레임마다 메모리를 새로 잡지 않는다.
    // 성능 시험 빌드에서는 실행 2초 뒤 자동으로 시작하고, 끝나면 스스로 종료한다 (D-023).
    public sealed class PerfLabBenchmark : MonoBehaviour
    {
        const float WarmupSeconds = 5f;
        const float BuildAutoStartSeconds = 2f;
        const float MeasureSeconds = 10f;
        const int MaxSamples = 4096;

        struct Run
        {
            public int Stage;
            public bool HalfScale;
        }

        static readonly Run[] Runs =
        {
            new Run { Stage = 0, HalfScale = true },
            new Run { Stage = 1, HalfScale = true },
            new Run { Stage = 2, HalfScale = true },
            new Run { Stage = 0, HalfScale = false },
            new Run { Stage = 1, HalfScale = false },
            new Run { Stage = 2, HalfScale = false },
        };

        PerfLabBootstrap bootstrap;
        PerfLabResolution resolution;

        readonly float[] frameMs = new float[MaxSamples];
        readonly FrameTiming[] timings = new FrameTiming[1];
        int sampleCount;
        double mainThreadMsSum;
        double gpuMsSum;
        int gpuSamples;
        long gcBytesSum;
        long arrowsSum;
        int deathsAtStart;
        int overflowAtStart;

        ProfilerRecorder mainThreadRecorder;
        ProfilerRecorder gcRecorder;

        int runIndex = -1;
        float phaseTime;
        bool measuring;
        StringBuilder csv;
        StringBuilder summary;

        public bool Running => runIndex >= 0;
        public string LastSummary { get; private set; } = "";

        public void Init(PerfLabBootstrap owner, PerfLabResolution res)
        {
            bootstrap = owner;
            resolution = res;
        }

        public void Begin()
        {
            if (Running)
            {
                return;
            }

            mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            csv = new StringBuilder();
            csv.AppendLine("run,stage,allies,enemies,resolution,avg_ms,avg_fps,p99_ms,max_ms,main_thread_ms,gpu_ms,gc_bytes_per_frame,avg_arrows,deaths_per_sec,pool_overflow,samples");
            summary = new StringBuilder();

            string device = DeviceInfo();
            Debug.Log("[PerfLab][BENCH] 시작 · " + device);
            summary.AppendLine("측정 장치: " + device);

            StartRun(0);
        }

        void OnDestroy()
        {
            StopRecorders();
        }

        bool autoStarted;
        float autoStartTimer;

        void Update()
        {
            if (!Running)
            {
                if (!Application.isEditor && !autoStarted && bootstrap != null)
                {
                    autoStartTimer += Time.unscaledDeltaTime;
                    if (autoStartTimer >= BuildAutoStartSeconds)
                    {
                        autoStarted = true;
                        bootstrap.PrepareBenchmark();
                        Begin();
                    }
                }

                return;
            }

            phaseTime += Time.unscaledDeltaTime;
            if (!measuring)
            {
                if (phaseTime >= WarmupSeconds)
                {
                    measuring = true;
                    phaseTime = 0f;
                    ResetSamples();
                }

                return;
            }

            Sample();
            if (phaseTime >= MeasureSeconds)
            {
                FinishRun();
                if (runIndex + 1 < Runs.Length)
                {
                    StartRun(runIndex + 1);
                }
                else
                {
                    FinishAll();
                }
            }
        }

        void StartRun(int index)
        {
            runIndex = index;
            measuring = false;
            phaseTime = 0f;
            resolution.SetHalfScale(Runs[index].HalfScale);
            bootstrap.RequestStage(Runs[index].Stage, true);
        }

        void ResetSamples()
        {
            sampleCount = 0;
            mainThreadMsSum = 0;
            gpuMsSum = 0;
            gpuSamples = 0;
            gcBytesSum = 0;
            arrowsSum = 0;
            deathsAtStart = bootstrap.DeathsTotal;
            overflowAtStart = bootstrap.PoolOverflowTotal;
        }

        void Sample()
        {
            if (sampleCount >= MaxSamples)
            {
                return;
            }

            frameMs[sampleCount] = Time.unscaledDeltaTime * 1000f;
            sampleCount++;

            if (mainThreadRecorder.Valid)
            {
                mainThreadMsSum += mainThreadRecorder.LastValue * 1e-6;
            }

            if (gcRecorder.Valid)
            {
                gcBytesSum += gcRecorder.LastValue;
            }

            arrowsSum += bootstrap.ActiveArrows;

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
            {
                gpuMsSum += timings[0].gpuFrameTime;
                gpuSamples++;
            }
        }

        void FinishRun()
        {
            Run run = Runs[runIndex];
            int n = Mathf.Max(1, sampleCount);
            double sum = 0;
            float max = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                sum += frameMs[i];
                max = Mathf.Max(max, frameMs[i]);
            }

            Array.Sort(frameMs, 0, sampleCount);
            float p99 = sampleCount > 0 ? frameMs[Mathf.Clamp((int)Math.Ceiling(sampleCount * 0.99) - 1, 0, sampleCount - 1)] : 0f;
            double avgMs = sum / n;
            double fps = avgMs > 0 ? 1000.0 / avgMs : 0;
            double mainMs = mainThreadRecorder.Valid ? mainThreadMsSum / n : -1;
            double gpuMs = gpuSamples > 0 ? gpuMsSum / gpuSamples : -1;
            double gcPerFrame = gcRecorder.Valid ? (double)gcBytesSum / n : -1;
            double arrows = (double)arrowsSum / n;
            double deathsPerSec = (bootstrap.DeathsTotal - deathsAtStart) / MeasureSeconds;
            int overflow = bootstrap.PoolOverflowTotal - overflowAtStart;
            string res = resolution.Describe();
            string stage = bootstrap.StageLabel;

            csv.AppendLine(string.Join(",",
                runIndex + 1, stage, bootstrap.AllyCount, bootstrap.EnemyCount, "\"" + res + "\"",
                avgMs.ToString("F2"), fps.ToString("F1"), p99.ToString("F2"), max.ToString("F2"),
                Fmt(mainMs), Fmt(gpuMs), Fmt(gcPerFrame, "F0"), arrows.ToString("F0"),
                deathsPerSec.ToString("F0"), overflow, sampleCount));

            string line = $"{runIndex + 1}/{Runs.Length} {stage} 아군 {bootstrap.AllyCount:N0}·적 {bootstrap.EnemyCount:N0} | {res} | 평균 {avgMs:F2}ms ({fps:F1} FPS) · 상위1% {p99:F2}ms · 최대 {max:F2}ms | 메인 {Fmt(mainMs)}ms · GPU {Fmt(gpuMs)}ms | GC {Fmt(gcPerFrame, "F0")}B/프레임 | 화살 {arrows:F0} · 초당 처치 {deathsPerSec:F0} · 풀 부족 {overflow}";
            Debug.Log("[PerfLab][BENCH] " + line);
            summary.AppendLine(line);
        }

        void FinishAll()
        {
            runIndex = -1;
            StopRecorders();

            string path = WriteCsv();
            Debug.Log("[PerfLab][BENCH] 끝 · CSV: " + path);
            summary.AppendLine("CSV: " + path);
            LastSummary = summary.ToString();

            if (!Application.isEditor)
            {
                Application.Quit();
            }
        }

        string WriteCsv()
        {
            try
            {
#if UNITY_EDITOR
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/collab/live/review"));
#else
                string folder = Application.persistentDataPath;
#endif
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "perflab_bench_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
                File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
                return path;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PerfLab][BENCH] CSV 저장 실패: " + exception.Message);
                return "(저장 실패)";
            }
        }

        void StopRecorders()
        {
            if (mainThreadRecorder.Valid) mainThreadRecorder.Dispose();
            if (gcRecorder.Valid) gcRecorder.Dispose();
        }

        static string Fmt(double value, string format = "F2") => value < 0 ? "-" : value.ToString(format);

        static string DeviceInfo() =>
            $"{SystemInfo.graphicsDeviceName} (VRAM {SystemInfo.graphicsMemorySize}MB, {SystemInfo.graphicsDeviceType}) · " +
            $"{SystemInfo.processorType} ({SystemInfo.processorCount}스레드) · RAM {SystemInfo.systemMemorySize}MB · " +
            $"{(Application.isEditor ? "에디터" : "빌드")} · Unity {Application.unityVersion}";
    }
}
