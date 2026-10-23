using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using HumanLastRampart.App.Flow;
using HumanLastRampart.Core.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace HumanLastRampart.App.Dev
{
    // 빌드된 플레이어의 자동 연기 시험 (W29). 실행 인자 -hlrSmoke 가 있을 때만 켜진다 (없으면 아무 일도 안 한다).
    // 흐름: 부팅 → 메인 → 새 게임(쉬움) → 이야기 건너뛰기 → 첫 전투 약 20초(×4) → 강제 승리 → 결과 → 정비 마당 → 종료.
    // 단추는 PlayProbe와 같은 길(이름 "btn:글자" 또는 보이는 글자)로 찾아 onClick을 부른다.
    // 옵션: -hlrSmokeOut <폴더> (기본 C:\Unity\1\HLR-builds\win), -hlrSmokeBattleSeconds <초> (기본 20)
    public sealed class SmokeRunner : MonoBehaviour
    {
        const string FlagName = "-hlrSmoke";
        const string DefaultOut = @"C:\Unity\1\HLR-builds\win";
        const string KeyPrefix = "btn:";   // HumanLastRampart.UI.Ui.KeyPrefix (App은 UI 어셈블리를 참조하지 않는다)

        readonly StringBuilder log = new StringBuilder();
        readonly List<string> firstErrors = new List<string>();
        string outDir;
        float battleSeconds = 20f;
        int errorCount, exceptionCount;
        bool failed;
        string reached = "시작 전";
        Stopwatch wall;

        // 창이 앞에 나오지 않아도 멈추지 않게 (자동 실행은 포커스를 못 받는 경우가 있다)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void KeepRunning()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), FlagName) >= 0) Application.runInBackground = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, FlagName) < 0)
            {
                return;
            }

            var go = new GameObject("SmokeRunner");
            DontDestroyOnLoad(go);
            go.AddComponent<SmokeRunner>().Configure(args);
        }

        void Configure(string[] args)
        {
            outDir = DefaultOut;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == FlagName + "Out") outDir = args[i + 1];
                else if (args[i] == FlagName + "BattleSeconds" && float.TryParse(args[i + 1], out float s)) battleSeconds = s;
            }
        }

        void Start()
        {
            Directory.CreateDirectory(outDir);
            Application.logMessageReceived += OnLog;
            wall = Stopwatch.StartNew();
            Note($"연기 시험 시작: {Application.productName} {Application.version}, Unity {Application.unityVersion}, {Screen.width}x{Screen.height}, GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})");
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Exception) exceptionCount++;
            else if (type == LogType.Error || type == LogType.Assert) errorCount++;
            else return;
            if (firstErrors.Count < 25) firstErrors.Add($"[{type}] {condition}");
        }

        void Note(string line)
        {
            log.AppendLine($"[{(wall != null ? wall.Elapsed.TotalSeconds : 0):F1}s] {line}");
            try { File.WriteAllText(Path.Combine(outDir, "smoke.progress.log"), log.ToString(), new UTF8Encoding(false)); } catch { }   // 멈췄을 때 어디까지 갔는지 보려고 매번 적는다
        }

        IEnumerator Run()
        {
            yield return Step("부팅 → 메인 메뉴", WaitScreen(ScreenId.MainMenu, 30f));
            yield return Shot("00_main");
            yield return Step("새 게임 → 쉬움", PressRoutine("게임 시작", 15f));
            yield return Step("난이도 쉬움", PressRoutine("쉬움", 15f));
            yield return Step("이야기 화면", WaitScreen(ScreenId.Story, 30f));
            yield return Step("이야기 건너뛰기 → 전투", WaitScreen(ScreenId.Battle, 60f));
            for (int i = 0; i < 10; i++) yield return null;
            yield return Shot("01_battle_prepare");

            // 첫 전투는 튜토리얼이 먼저 뜨고 준비 지도가 없을 수 있다: 있으면 건너뛰고, [전투 시작]도 있을 때만 누른다
            yield return Step("튜토리얼·준비 넘기기", OptionalPress("튜토리얼 건너뛰기", 3f));
            yield return Step("전투 시작(있을 때만)", OptionalPress("전투 시작", 3f));
            yield return Step("배속 ×4", PressRoutine("×4", 15f));
            yield return BattleLoop();
            yield return Shot("02_battle");

            yield return Step("강제 승리", PressRoutine("[시험] 승리", 15f));
            yield return Step("결과 화면", WaitScreen(ScreenId.Result, 30f));
            yield return Step("결과 [다음] 준비", WaitReady("다음", 30f));
            for (int i = 0; i < 30; i++) yield return null;
            yield return Shot("03_result");

            yield return Step("결과 → 다음", PressRoutine("다음", 15f));
            yield return Step("정비 마당", WaitScreen(ScreenId.Prep, 30f));
            float until = Time.unscaledTime + 3f;
            while (Time.unscaledTime < until) yield return null;
            yield return Shot("04_prep");

            Finish();
        }

        // 단계 하나: 시간 안에 못 끝나면 실패로 적고 종료한다
        IEnumerator Step(string name, IEnumerator body)
        {
            if (failed) yield break;
            while (true)
            {
                object current;
                try
                {
                    if (!body.MoveNext()) break;
                    current = body.Current;
                }
                catch (Exception e)
                {
                    Fail($"{name}: {e.Message}");
                    yield break;
                }
                yield return current;
            }

            if (!failed)
            {
                reached = name;
                Note("통과: " + name);
            }
        }

        void Fail(string reason)
        {
            failed = true;
            StopAllCoroutines();
            Note($"실패: {reason} (화면 {GameFlow.Instance?.CurrentScreen}, 지금 단추: {PressableList()})");
            Finish();
        }

        IEnumerator BattleLoop()
        {
            int frames = 0;
            double sum = 0, worst = 0;
            float start = Time.unscaledTime;
            long peakAlloc = 0;
            while (Time.unscaledTime - start < battleSeconds)
            {
                yield return null;
                double dt = Time.unscaledDeltaTime;
                frames++;
                sum += dt;
                if (dt > worst) worst = dt;
                if (frames % 30 == 0) peakAlloc = Math.Max(peakAlloc, Profiler.GetTotalAllocatedMemoryLong());
                if (GameFlow.Instance != null && GameFlow.Instance.CurrentScreen != ScreenId.Battle) break;
            }

            double avgFps = sum > 0 ? frames / sum : 0;
            Note($"전투 {frames}프레임 / {sum:F1}초: 평균 FPS {avgFps:F1}, 최악 프레임 {worst * 1000:F0}ms");
            Note($"전투 중 메모리: 관리 할당 최대 {peakAlloc / 1048576f:F0}MB");
            avgBattleFps = avgFps;
        }

        double avgBattleFps;

        void Finish()
        {
            if (finished) return;
            finished = true;
            long peakWs = 0;
            try { peakWs = PeakWorkingSet(); } catch { }
            Note($"메모리: 작업 집합 최대 {peakWs / 1048576f:F0}MB, 유니티 할당 {Profiler.GetTotalAllocatedMemoryLong() / 1048576f:F0}MB / 예약 {Profiler.GetTotalReservedMemoryLong() / 1048576f:F0}MB");
            bool ok = !failed && errorCount == 0 && exceptionCount == 0;
            var sb = new StringBuilder();
            sb.AppendLine($"결과: {(failed ? "실패" : ok ? "성공" : "오류 있음")}, 마지막 통과 단계: {reached}");
            sb.AppendLine($"오류 {errorCount}, 예외 {exceptionCount}, 평균 전투 FPS {avgBattleFps:F1}, 작업 집합 최대 {peakWs / 1048576f:F0}MB, 걸린 시간 {wall.Elapsed.TotalSeconds:F0}초");
            foreach (string e in firstErrors) sb.AppendLine("  " + e);
            sb.AppendLine("--- 단계 기록 ---");
            sb.Append(log);
            File.WriteAllText(Path.Combine(outDir, "smoke.log"), sb.ToString(), new UTF8Encoding(false));
            Application.logMessageReceived -= OnLog;
            Application.Quit(failed ? 1 : errorCount + exceptionCount > 0 ? 2 : 0);
        }

        bool finished;

        // 작업 집합 최대치(바이트). Mono의 Process.PeakWorkingSet64는 0을 돌려줘서 윈도 API를 직접 부른다
#if UNITY_STANDALONE_WIN
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct ProcessMemoryCounters
        {
            public uint cb, PageFaultCount;
            public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage, QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage;
        }

        [System.Runtime.InteropServices.DllImport("psapi.dll")]
        static extern bool GetProcessMemoryInfo(IntPtr process, out ProcessMemoryCounters counters, uint size);

        static long PeakWorkingSet()
        {
            var counters = new ProcessMemoryCounters();
            counters.cb = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(ProcessMemoryCounters));
            return GetProcessMemoryInfo(Process.GetCurrentProcess().Handle, out counters, counters.cb) ? (long)(ulong)counters.PeakWorkingSetSize : 0;
        }
#else
        static long PeakWorkingSet() => 0;
#endif

        // ── 화면·단추 도우미 ──

        static IEnumerator WaitScreen(ScreenId screen, float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
            {
                GameFlow flow = GameFlow.Instance;
                if (flow != null && flow.CurrentScreen == screen && InputOpen())
                {
                    for (int i = 0; i < 5; i++) yield return null;
                    yield break;
                }

                // 이야기 화면을 기다리는 게 아니면 이야기는 [넘기기]로 건너뛴다 (이야기 사슬)
                if (screen != ScreenId.Story && flow != null && flow.CurrentScreen == ScreenId.Story)
                {
                    Button skip = FindReady("넘기기");
                    if (skip != null) skip.onClick.Invoke();
                }

                yield return null;
            }

            throw new TimeoutException($"{screen} 화면이 {seconds}초 안에 열리지 않음");
        }

        static IEnumerator PressRoutine(string label, float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
            {
                Button b = FindReady(label);
                if (b != null)
                {
                    b.onClick.Invoke();
                    yield return null;
                    yield break;
                }

                yield return null;
            }

            throw new TimeoutException($"'{label}' 단추가 {seconds}초 안에 준비되지 않음");
        }

        // 있으면 누르고, 시간 안에 안 나타나면 그냥 지나간다
        static IEnumerator OptionalPress(string label, float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
            {
                Button b = FindReady(label);
                if (b != null)
                {
                    b.onClick.Invoke();
                    yield return null;
                    yield break;
                }

                yield return null;
            }
        }

        static IEnumerator WaitReady(string label, float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
            {
                if (FindReady(label) != null) yield break;
                yield return null;
            }

            throw new TimeoutException($"'{label}' 단추가 {seconds}초 안에 준비되지 않음");
        }

        static bool InputOpen()
        {
            foreach (EventSystem e in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (e.isActiveAndEnabled) return true;
            }

            return false;
        }

        static Button FindReady(string label)
        {
            if (!InputOpen()) return null;
            foreach (Button b in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (!b.isActiveAndEnabled || !b.IsInteractable()) continue;
                if (b.name == KeyPrefix + label) return b;
                Text t = b.GetComponentInChildren<Text>();
                if (t != null && t.text == label) return b;
            }

            return null;
        }

        static string PressableList()
        {
            var names = new List<string>();
            foreach (Button b in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (b.isActiveAndEnabled && b.IsInteractable() && names.Count < 20 && !names.Contains(b.name)) names.Add(b.name);
            }

            return string.Join(" / ", names);
        }

        IEnumerator Shot(string tag)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(outDir, $"smoke_{tag}.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Note($"사진: {path}");
        }
    }
}
