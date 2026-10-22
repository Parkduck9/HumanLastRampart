// HLR 작업 현황판 (실행 파일판) — 알림 영역 아이콘 2개(클로드·코덱스) + 마우스를 올리면 뜨는 현황 창
// tray.ps1 의 동작을 그대로 옮긴 것이다. 인공지능 토큰을 쓰지 않고, 파일만 읽는다.
// 컴파일: app\build.cmd (.NET Framework 4 내장 csc, C# 5 문법)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace HlrBoard
{
    // ───────────────────────── 공통 도구 ─────────────────────────
    static class Native
    {
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        [StructLayout(LayoutKind.Sequential)]
        public struct NOTIFYICONIDENTIFIER { public int cbSize; public IntPtr hWnd; public uint uID; public Guid guidItem; }
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }
        [DllImport("shell32.dll")] public static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER id, out RECT r);
    }

    static class J
    {
        public static JavaScriptSerializer Ser()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 200;
            return s;
        }
        public static object G(object o, string k)
        {
            Dictionary<string, object> d = o as Dictionary<string, object>;
            if (d == null) return null;
            object v;
            return d.TryGetValue(k, out v) ? v : null;
        }
        public static double D(object o, double def)
        {
            if (o == null) return def;
            try { return Convert.ToDouble(o, CultureInfo.InvariantCulture); } catch { return def; }
        }
        public static string S(object o)
        {
            return o == null ? "" : Convert.ToString(o, CultureInfo.InvariantCulture);
        }
    }

    static class Log
    {
        public static string Path;
        public static void W(string msg)
        {
            try
            {
                if (Path == null) return;
                FileInfo fi = new FileInfo(Path);
                if (fi.Exists && fi.Length > 200000) fi.Delete();
                File.AppendAllText(Path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + msg + "\r\n", Encoding.UTF8);
            }
            catch { }
        }
    }

    // ───────────────────────── 설정 ─────────────────────────
    class Settings
    {
        public string ExeDir;
        public string FilePath;
        public string StagesFile = "stages.json";
        public string ClaudeUsageFile = "claude_usage.json";
        public string BoardDataFile = "";
        public string CodexDir = "";
        public string ProjectFolder = "C--Unity-1-HumanLastRampart";
        public int RefreshSeconds = 5;
        public bool Floating = false;
        public int FloatX = -1;
        public int FloatY = -1;
        public bool ShowClaude = true;
        public bool ShowCodex = true;

        public static Settings Load(string exeDir)
        {
            Settings s = new Settings();
            s.ExeDir = exeDir;
            s.FilePath = Path.Combine(exeDir, "settings.json");
            bool exists = File.Exists(s.FilePath);
            if (!exists)
            {
                // 처음 실행: 저장소 안이면 웹 자료 경로를 잡고, 아무 자료 파일도 없으면 친구 모드(클로드 숨김)
                string live = Path.GetFullPath(Path.Combine(exeDir, @"..\..\_HTML\live"));
                if (Directory.Exists(live)) s.BoardDataFile = Path.Combine(live, "board_data.js");
                if (!File.Exists(Path.Combine(exeDir, "stages.json")) && !File.Exists(Path.Combine(exeDir, "claude_usage.json")))
                    s.ShowClaude = false;
                s.Save();
                return s;
            }
            try
            {
                object o = J.Ser().DeserializeObject(File.ReadAllText(s.FilePath, Encoding.UTF8));
                if (J.G(o, "stagesFile") != null) s.StagesFile = J.S(J.G(o, "stagesFile"));
                if (J.G(o, "claudeUsageFile") != null) s.ClaudeUsageFile = J.S(J.G(o, "claudeUsageFile"));
                if (J.G(o, "boardDataFile") != null) s.BoardDataFile = J.S(J.G(o, "boardDataFile"));
                if (J.G(o, "codexDir") != null) s.CodexDir = J.S(J.G(o, "codexDir"));
                if (J.G(o, "projectFolder") != null) s.ProjectFolder = J.S(J.G(o, "projectFolder"));
                if (J.G(o, "refreshSeconds") != null) s.RefreshSeconds = (int)J.D(J.G(o, "refreshSeconds"), 5);
                if (J.G(o, "floating") is bool) s.Floating = (bool)J.G(o, "floating");
                if (J.G(o, "floatX") != null) s.FloatX = (int)J.D(J.G(o, "floatX"), -1);
                if (J.G(o, "floatY") != null) s.FloatY = (int)J.D(J.G(o, "floatY"), -1);
                if (J.G(o, "showClaude") is bool) s.ShowClaude = (bool)J.G(o, "showClaude");
                if (J.G(o, "showCodex") is bool) s.ShowCodex = (bool)J.G(o, "showCodex");
            }
            catch (Exception ex) { Log.W("설정 읽기 실패: " + ex.Message); }
            if (s.RefreshSeconds < 1) s.RefreshSeconds = 1;
            if (s.RefreshSeconds > 600) s.RefreshSeconds = 600;
            return s;
        }

        string Q(string v) { return J.Ser().Serialize(v ?? ""); }

        public void Save()
        {
            try
            {
                StringBuilder b = new StringBuilder();
                b.AppendLine("{");
                b.AppendLine("  \"_설명\": \"경로가 비어 있으면 그 기능을 쓰지 않는다. 상대 경로는 이 파일이 있는 폴더 기준. 고친 뒤에는 프로그램을 다시 시작한다.\",");
                b.AppendLine("  \"stagesFile\": " + Q(StagesFile) + ",");
                b.AppendLine("  \"claudeUsageFile\": " + Q(ClaudeUsageFile) + ",");
                b.AppendLine("  \"boardDataFile\": " + Q(BoardDataFile) + ",");
                b.AppendLine("  \"codexDir\": " + Q(CodexDir) + ",");
                b.AppendLine("  \"projectFolder\": " + Q(ProjectFolder) + ",");
                b.AppendLine("  \"refreshSeconds\": " + RefreshSeconds + ",");
                b.AppendLine("  \"floating\": " + (Floating ? "true" : "false") + ",");
                b.AppendLine("  \"floatX\": " + FloatX + ",");
                b.AppendLine("  \"floatY\": " + FloatY + ",");
                b.AppendLine("  \"showClaude\": " + (ShowClaude ? "true" : "false") + ",");
                b.AppendLine("  \"showCodex\": " + (ShowCodex ? "true" : "false"));
                b.AppendLine("}");
                File.WriteAllText(FilePath, b.ToString(), new UTF8Encoding(true));
            }
            catch (Exception ex) { Log.W("설정 저장 실패: " + ex.Message); }
        }

        public string Resolve(string p)
        {
            if (string.IsNullOrEmpty(p)) return "";
            try { return Path.GetFullPath(Path.Combine(ExeDir, Environment.ExpandEnvironmentVariables(p))); }
            catch { return ""; }
        }
        public string CodexRoot()
        {
            if (!string.IsNullOrEmpty(CodexDir)) return Resolve(CodexDir);
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        }
    }

    // ───────────────────────── 자료 ─────────────────────────
    class Usage
    {
        public bool Known;
        public double Five, Week;
        public string FiveReset = "", WeekReset = "", Checked = "";
        public int AgeMin;
        public bool Blocked { get { return Known && (Five >= 100 || Week >= 100); } }
    }
    class TaskRow
    {
        public string Id, Name, Who, Kind, Desc, Model, Stage, Detail;
        public int Pct, Idle;
    }
    class Snapshot
    {
        public DateTime Time;
        public Usage Claude, Codex;
        public bool HasStages;
        public List<TaskRow> Tasks = new List<TaskRow>();
        public bool UnityRunning;
        public double UnityMin;
        public int UnityPct;
        public string Fail;
    }

    class Collector
    {
        Settings st;
        string[] stageNames = new string[] { "대기", "코딩", "검토", "합침", "시험", "끝" };
        string[] cardStageNames = new string[] { "대기", "그리는 중", "검토", "합침", "확인", "끝" };
        int[][] band = new int[][] { new int[] { 0, 0 }, new int[] { 5, 60 }, new int[] { 60, 75 }, new int[] { 75, 85 }, new int[] { 85, 99 }, new int[] { 100, 100 } };
        Dictionary<string, long[]> counts = new Dictionary<string, long[]>();
        Dictionary<string, string> agentPaths = new Dictionary<string, string>();
        List<string> codexFiles = new List<string>();
        DateTime codexScanAt = DateTime.MinValue;
        Snapshot last;

        public Collector(Settings s) { st = s; }

        // 파일 끝 부분만 읽는다
        static string ReadTail(string path, int maxBytes)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long len = fs.Length;
                long start = Math.Max(0, len - maxBytes);
                fs.Seek(start, SeekOrigin.Begin);
                byte[] buf = new byte[len - start];
                int got = 0;
                while (got < buf.Length) { int n = fs.Read(buf, got, buf.Length - got); if (n <= 0) break; got += n; }
                string t = Encoding.UTF8.GetString(buf, 0, got);
                if (start > 0) { int i = t.IndexOf('\n'); if (i >= 0) t = t.Substring(i + 1); }
                return t;
            }
        }

        // 덧붙기만 하는 기록 파일에서 글자 수를 센다 — 새로 붙은 부분만 읽는다
        long CountMatches(string path, string pattern)
        {
            if (!File.Exists(path)) return 0;
            string key = path + "|" + pattern;
            long[] e;
            if (!counts.TryGetValue(key, out e)) { e = new long[] { 0, 0 }; counts[key] = e; }
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long len = fs.Length;
                if (len < e[0]) { e[0] = 0; e[1] = 0; }
                if (len > e[0])
                {
                    fs.Seek(e[0], SeekOrigin.Begin);
                    byte[] buf = new byte[len - e[0]];
                    int got = 0;
                    while (got < buf.Length) { int n = fs.Read(buf, got, buf.Length - got); if (n <= 0) break; got += n; }
                    int nl = Array.LastIndexOf(buf, (byte)10, got - 1 < 0 ? 0 : got - 1);
                    if (got > 0 && nl >= 0)
                    {
                        string t = Encoding.UTF8.GetString(buf, 0, nl + 1);
                        int idx = 0, c = 0;
                        while ((idx = t.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0) { c++; idx += pattern.Length; }
                        e[1] += c;
                        e[0] += nl + 1;
                    }
                }
                return e[1];
            }
        }

        static string ModelName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            Match m = Regex.Match(id, @"claude-(sonnet|opus|haiku|fable)-(\d+)-(\d+)");
            if (m.Success)
            {
                string n = m.Groups[1].Value;
                if (n == "sonnet") n = "소넷"; else if (n == "opus") n = "오퍼스"; else if (n == "haiku") n = "하이쿠"; else n = "페이블";
                return n + " " + m.Groups[2].Value + "." + m.Groups[3].Value;
            }
            return id;
        }

        string CodexModel()
        {
            try
            {
                string cfg = Path.Combine(st.CodexRoot(), "config.toml");
                if (!File.Exists(cfg)) return "";
                Match m = Regex.Match(File.ReadAllText(cfg, Encoding.UTF8), "^\\s*model\\s*=\\s*\"([^\"]+)\"", RegexOptions.Multiline);
                return m.Success ? m.Groups[1].Value : "";
            }
            catch { return ""; }
        }

        static string When(long unix)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix).ToLocalTime().ToString("MM-dd HH:mm");
        }

        Usage ReadCodex(DateTime now)
        {
            string root = Path.Combine(st.CodexRoot(), "sessions");
            if (!Directory.Exists(root)) return null;
            // 폴더 전체 훑기는 30초에 한 번만, 그 사이에는 찾아 둔 최근 파일만 읽는다
            if ((now - codexScanAt).TotalSeconds > 30 || codexFiles.Count == 0)
            {
                List<FileInfo> all = new List<FileInfo>();
                foreach (string f in Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories))
                {
                    try { all.Add(new FileInfo(f)); } catch { }
                }
                all.Sort(delegate(FileInfo a, FileInfo b) { return b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc); });
                codexFiles.Clear();
                for (int i = 0; i < all.Count && i < 4; i++) codexFiles.Add(all[i].FullName);
                codexScanAt = now;
            }
            long nowUnix = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            foreach (string file in codexFiles)
            {
                try
                {
                    string tail = ReadTail(file, 400 * 1024);
                    string[] lines = tail.Split('\n');
                    for (int i = lines.Length - 1; i >= 0; i--)
                    {
                        string ln = lines[i];
                        if (ln.IndexOf("\"rate_limits\"", StringComparison.Ordinal) < 0) continue;
                        object j;
                        try { j = J.Ser().DeserializeObject(ln.Trim()); } catch { continue; }
                        object r = J.G(J.G(j, "payload"), "rate_limits");
                        if (r == null) r = J.G(j, "rate_limits");
                        object p = J.G(r, "primary"), s = J.G(r, "secondary");
                        if (p == null || s == null) continue;
                        Usage u = new Usage();
                        u.Known = true;
                        long pr = (long)J.D(J.G(p, "resets_at"), 0), sr = (long)J.D(J.G(s, "resets_at"), 0);
                        // 코덱스가 일할 때만 값이 새로 기록된다 — 초기화 시각이 지났으면 0%로 본다
                        u.Five = pr <= nowUnix ? 0 : J.D(J.G(p, "used_percent"), 0);
                        u.Week = sr <= nowUnix ? 0 : J.D(J.G(s, "used_percent"), 0);
                        u.FiveReset = When(pr);
                        u.WeekReset = When(sr);
                        u.AgeMin = (int)(now - File.GetLastWriteTime(file)).TotalMinutes;
                        return u;
                    }
                }
                catch { }
            }
            return null;
        }

        Usage ReadClaude(DateTime now, out Dictionary<string, object> raw)
        {
            raw = null;
            string path = st.Resolve(st.ClaudeUsageFile);
            if (path == "" || !File.Exists(path)) return null;
            object o = J.Ser().DeserializeObject(File.ReadAllText(path, Encoding.UTF8));
            raw = o as Dictionary<string, object>;
            if (raw == null) return null;
            Usage u = new Usage();
            u.Known = true;
            u.Five = J.D(J.G(o, "five"), 0);
            u.Week = J.D(J.G(o, "week"), 0);
            u.Checked = J.S(J.G(o, "checked"));
            DateTime t;
            // 클로드 값은 Claude가 확인할 때만 적힌다 — 초기화 시각이 지났으면 0%
            string fr = J.S(J.G(o, "fiveResetAt")), wr = J.S(J.G(o, "weekResetAt"));
            if (fr != "" && DateTime.TryParse(fr, out t) && t <= now) { u.Five = 0; raw["five"] = 0; }
            if (wr != "" && DateTime.TryParse(wr, out t) && t <= now) { u.Week = 0; raw["week"] = 0; }
            return u;
        }

        string FindAgentLog(string id)
        {
            string cached;
            if (agentPaths.TryGetValue(id, out cached) && File.Exists(cached)) return cached;
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".claude\projects");
            if (!string.IsNullOrEmpty(st.ProjectFolder)) root = Path.Combine(root, st.ProjectFolder);
            if (!Directory.Exists(root)) return null;
            string[] f = Directory.GetFiles(root, "agent-" + id + ".jsonl", SearchOption.AllDirectories);
            if (f.Length == 0) return null;
            agentPaths[id] = f[0];
            return f[0];
        }

        List<TaskRow> ReadTasks(DateTime now)
        {
            List<TaskRow> rows = new List<TaskRow>();
            string path = st.Resolve(st.StagesFile);
            object cfg = J.Ser().DeserializeObject(File.ReadAllText(path, Encoding.UTF8));
            IEnumerable items = J.G(cfg, "items") as IEnumerable;
            if (items == null) return rows;
            string codexModel = CodexModel();
            foreach (object it in items)
            {
                int stage = (int)J.D(J.G(it, "stage"), 0);
                if (stage >= 5) continue;   // 끝난 작업은 보이지 않는다
                if (stage < 0) stage = 0;
                string kind = J.S(J.G(it, "kind"));
                string detail = "", model = kind == "card" ? codexModel : "";
                double inner = 0;
                DateTime? lastW = null;
                try
                {
                    if (kind == "coder")
                    {
                        string log = FindAgentLog(J.S(J.G(it, "agent")));
                        if (log != null)
                        {
                            long uses = CountMatches(log, "\"type\":\"tool_use\"");
                            // 끝부분 응답 줄에서 모델을 읽는다 (첫 줄의 model은 작업자 모델이 아님)
                            string tail = ReadTail(log, 200 * 1024);
                            string[] ls = tail.Split('\n');
                            int seen = 0;
                            for (int i = ls.Length - 1; i >= 0 && seen < 40; i--)
                            {
                                if (ls[i].Length == 0) continue;
                                seen++;
                                Match m = Regex.Match(ls[i], "\"model\":\"(claude-[^\"]+)\"");
                                if (m.Success) { model = ModelName(m.Groups[1].Value); break; }
                            }
                            inner = Math.Min(1.0, uses / 70.0);
                            lastW = File.GetLastWriteTime(log);
                            detail = "도구 사용 " + uses + "번";
                        }
                    }
                    else if (kind == "card")
                    {
                        string folder = J.S(J.G(it, "folder"));
                        string log = folder == "" ? "" : Path.Combine(folder, "CARD_CODEX_LOG.jsonl");
                        if (log != "" && File.Exists(log))
                        {
                            long done = CountMatches(log, "\"item.completed\"");
                            inner = Math.Min(1.0, done / 60.0);
                            lastW = File.GetLastWriteTime(log);
                            detail = "그림 작업 단계 " + done + "개";
                            if (File.Exists(Path.Combine(folder, "CARD_RESULT.md"))) { if (stage < 3) stage = 3; detail += " · 결과 보고 나옴"; }
                        }
                        else detail = "시작 전 (차례 기다림)";
                    }
                }
                catch (Exception ex) { Log.W("작업 줄 읽기 실패: " + ex.Message); }
                int lo = band[stage][0], hi = band[stage][1];
                TaskRow r = new TaskRow();
                r.Id = J.S(J.G(it, "id")); r.Name = J.S(J.G(it, "name")); r.Who = J.S(J.G(it, "who"));
                r.Kind = kind; r.Desc = J.S(J.G(it, "desc")); r.Model = model;
                r.Stage = (kind == "card" ? cardStageNames : stageNames)[stage];
                r.Pct = stage == 1 ? (int)(lo + (hi - lo) * inner) : lo;
                r.Detail = detail;
                r.Idle = lastW.HasValue ? (int)(now - lastW.Value).TotalSeconds : -1;
                rows.Add(r);
            }
            return rows;
        }

        public Snapshot Collect()
        {
            DateTime now = DateTime.Now;
            Snapshot s = new Snapshot();
            s.Time = now;
            List<string> fails = new List<string>();
            Dictionary<string, object> claudeRaw = null;

            string sp = st.Resolve(st.StagesFile);
            s.HasStages = sp != "" && File.Exists(sp);
            if (s.HasStages)
            {
                try { s.Tasks = ReadTasks(now); }
                catch (Exception ex) { fails.Add(ex.Message); if (last != null) s.Tasks = last.Tasks; }
            }
            try
            {
                Process[] ps = Process.GetProcessesByName("Unity");
                if (ps.Length > 0)
                {
                    DateTime first = DateTime.MaxValue;
                    foreach (Process p in ps) { try { if (p.StartTime < first) first = p.StartTime; } catch { } }
                    if (first != DateTime.MaxValue)
                    {
                        s.UnityRunning = true;
                        s.UnityMin = Math.Round((now - first).TotalMinutes, 1);
                        s.UnityPct = (int)Math.Min(99, s.UnityMin / 9.0 * 100);
                    }
                }
            }
            catch { }
            try { s.Codex = ReadCodex(now); }
            catch (Exception ex) { fails.Add(ex.Message); if (last != null) s.Codex = last.Codex; }
            try { s.Claude = ReadClaude(now, out claudeRaw); }
            catch (Exception ex) { fails.Add(ex.Message); if (last != null) s.Claude = last.Claude; }

            if (fails.Count > 0) s.Fail = "갱신 실패 " + now.ToString("HH:mm:ss") + " — 위는 마지막으로 읽은 값: " + fails[0];
            WriteBoardData(s, claudeRaw, now);
            last = s;
            return s;
        }

        // 웹 페이지(board.html)용 자료를 tray.ps1 과 같은 모양으로 쓴다
        void WriteBoardData(Snapshot s, Dictionary<string, object> claudeRaw, DateTime now)
        {
            string path = st.Resolve(st.BoardDataFile);
            if (path == "" || !Directory.Exists(Path.GetDirectoryName(path))) return;
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["updated"] = now.ToString("yyyy-MM-dd HH:mm:ss");
                List<object> items = new List<object>();
                foreach (TaskRow r in s.Tasks)
                {
                    Dictionary<string, object> o = new Dictionary<string, object>();
                    o["id"] = r.Id; o["name"] = r.Name; o["who"] = r.Who; o["kind"] = r.Kind; o["desc"] = r.Desc;
                    o["model"] = r.Model; o["stage"] = r.Stage; o["pct"] = r.Pct; o["detail"] = r.Detail; o["idle"] = r.Idle;
                    items.Add(o);
                }
                d["items"] = items;
                Dictionary<string, object> un = new Dictionary<string, object>();
                un["running"] = s.UnityRunning;
                if (s.UnityRunning) { un["minutes"] = s.UnityMin; un["pct"] = s.UnityPct; }
                d["unity"] = un;
                if (s.Codex != null)
                {
                    Dictionary<string, object> c = new Dictionary<string, object>();
                    c["five"] = s.Codex.Five; c["fiveReset"] = s.Codex.FiveReset; c["week"] = s.Codex.Week; c["weekReset"] = s.Codex.WeekReset; c["age"] = s.Codex.AgeMin;
                    d["codex"] = c;
                }
                else d["codex"] = null;
                d["claude"] = claudeRaw;
                File.WriteAllText(path, "window.BOARD = " + J.Ser().Serialize(d) + ";", new UTF8Encoding(false));
            }
            catch (Exception ex) { Log.W("board_data 쓰기 실패: " + ex.Message); }
        }
    }

    // ───────────────────────── 화면 줄 ─────────────────────────
    class RowInfo
    {
        public string Kind, Left, Right;
        public double Pct;
        public bool Red;
        public RowInfo(string k, string l, string r, double p, bool red) { Kind = k; Left = l; Right = r; Pct = p; Red = red; }
    }

    static class Theme
    {
        public static Color Back = Color.FromArgb(29, 24, 19);
        public static Color Fore = Color.FromArgb(239, 227, 204);
        public static Color Brass = Color.FromArgb(212, 166, 74);
        public static Color Dim = Color.FromArgb(168, 151, 124);
        public static Color Track = Color.FromArgb(18, 14, 10);
        public static Color Red = Color.FromArgb(226, 88, 76);
        public static Font Normal = new Font("Malgun Gothic", 10);
        public static Font Bold = new Font("Malgun Gothic", 10, FontStyle.Bold);
        public static Font Head = new Font("Malgun Gothic", 12, FontStyle.Bold);
        public static Font Small = new Font("Malgun Gothic", 9);
    }

    static class RowBuilder
    {
        static void Usage(List<RowInfo> R, Usage u, string note, bool red)
        {
            if (u != null && u.Known)
            {
                R.Add(new RowInfo("meter", "이번 주", ((int)u.Week) + "%", u.Week, red));
                R.Add(new RowInfo("meter", "5시간", ((int)u.Five) + "%", u.Five, red));
            }
            else R.Add(new RowInfo("note", note, "", 0, false));
        }
        static void Tasks(List<RowInfo> R, List<TaskRow> list, string empty)
        {
            if (list.Count == 0) { R.Add(new RowInfo("note", empty, "", 0, false)); return; }
            foreach (TaskRow t in list)
            {
                string right = t.Stage + "  " + t.Pct + "%";
                if (!string.IsNullOrEmpty(t.Model)) right += "  ·  " + t.Model;
                R.Add(new RowInfo("task", t.Name, right, t.Pct, false));
            }
        }

        public static List<RowInfo> Build(Snapshot d, Settings st)
        {
            List<RowInfo> R = new List<RowInfo>();
            bool first = true;
            if (st.ShowClaude)
            {
                bool blocked = d.Claude != null && d.Claude.Blocked;
                string at = "";
                if (d.Claude != null && d.Claude.Checked.Length > 6) at = "확인 " + d.Claude.Checked.Substring(6);
                R.Add(new RowInfo("title", blocked ? "클로드 (사용 불가)" : "클로드", at, 0, blocked));
                Usage(R, d.Claude, "기록 없음", blocked);
                if (d.HasStages)
                {
                    List<TaskRow> mine = d.Tasks.FindAll(delegate(TaskRow t) { return t.Kind != "card"; });
                    if (d.UnityRunning) R.Add(new RowInfo("task", "유니티 시험", ((int)d.UnityMin) + "분째", d.UnityPct, false));
                    if (!(d.UnityRunning && mine.Count == 0)) Tasks(R, mine, "진행 중인 작업 없음");
                }
                first = false;
            }
            if (st.ShowCodex)
            {
                if (!first) R.Add(new RowInfo("gap", "", "", 0, false));
                bool blocked = d.Codex != null && d.Codex.Blocked;
                string at = "";
                if (d.Codex != null) at = "초기화 " + d.Codex.WeekReset + " · 5시간 " + (d.Codex.FiveReset.Length > 6 ? d.Codex.FiveReset.Substring(6) : d.Codex.FiveReset);
                R.Add(new RowInfo("title", blocked ? "코덱스 (사용 불가)" : "코덱스", at, 0, blocked));
                Usage(R, d.Codex, "사용량을 읽지 못함", blocked);
                if (d.HasStages) Tasks(R, d.Tasks.FindAll(delegate(TaskRow t) { return t.Kind == "card"; }), "진행 중인 그림 없음");
                first = false;
            }
            if (!first) R.Add(new RowInfo("gap", "", "", 0, false));
            R.Add(new RowInfo("note", "갱신 " + d.Time.ToString("HH:mm:ss"), "", 0, false));
            if (!string.IsNullOrEmpty(d.Fail)) R.Add(new RowInfo("note", d.Fail, "", 0, false));
            return R;
        }
    }

    // ───────────────────────── 화면 조각 ─────────────────────────
    class DBPanel : Panel
    {
        public DBPanel() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
    }

    class Slot { public Label A, B; public Panel Fill; public int BarW; }

    class BoardView : DBPanel
    {
        public const int W = 470;
        string shape = "";
        List<Slot> slots = new List<Slot>();
        public bool Draggable;
        public ContextMenuStrip Menu;

        public BoardView() { BackColor = Theme.Back; ForeColor = Theme.Fore; Font = Theme.Normal; }

        Label NewLabel(Font f, Color c, int x, int y, int w, ContentAlignment al)
        {
            Label l = new Label();
            l.AutoSize = false; l.Font = f; l.ForeColor = c; l.Text = "";
            l.Location = new Point(x, y); l.Size = new Size(w, 22); l.TextAlign = al;
            l.AutoEllipsis = true; l.UseMnemonic = false;
            return l;
        }

        void Hook(Control c)
        {
            if (Menu != null) c.ContextMenuStrip = Menu;
            if (Draggable) c.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                Form f = FindForm();
                if (f == null) return;
                Native.ReleaseCapture();
                Native.SendMessage(f.Handle, 0xA1, (IntPtr)2, IntPtr.Zero);   // WM_NCLBUTTONDOWN, HTCAPTION
            };
        }

        public int Render(List<RowInfo> rows)
        {
            StringBuilder sb = new StringBuilder();
            foreach (RowInfo r in rows) { sb.Append(r.Kind); sb.Append(';'); }
            string sh = sb.ToString();
            int total = 20;
            if (sh != shape)
            {
                // 줄 모양이 바뀔 때만 다시 만든다 (깜빡임 없음)
                SuspendLayout();
                Controls.Clear();
                slots.Clear();
                int y = 10;
                foreach (RowInfo r in rows)
                {
                    Panel p = new DBPanel();
                    p.Location = new Point(10, y); p.Width = W; p.BackColor = Theme.Back;
                    Label a = null, b = null; Panel fill = null; int barW = 0;
                    switch (r.Kind)
                    {
                        case "title":
                            p.Height = 34; a = NewLabel(Theme.Head, Theme.Brass, 0, 6, 200, ContentAlignment.MiddleLeft);
                            b = NewLabel(Theme.Small, Theme.Dim, 200, 8, W - 200, ContentAlignment.MiddleRight); break;
                        case "meter":
                            p.Height = 24; a = NewLabel(Theme.Normal, Theme.Dim, 0, 1, 70, ContentAlignment.MiddleLeft);
                            b = NewLabel(Theme.Bold, Theme.Fore, W - 60, 1, 60, ContentAlignment.MiddleRight);
                            barW = W - 140; fill = MakeBar(p, 72, 8, barW, 8); break;
                        case "task":
                            p.Height = 32; a = NewLabel(Theme.Normal, Theme.Fore, 0, 0, 250, ContentAlignment.MiddleLeft);
                            b = NewLabel(Theme.Small, Theme.Dim, 250, 0, W - 250, ContentAlignment.MiddleRight);
                            barW = W; fill = MakeBar(p, 0, 24, W, 3); break;
                        case "note":
                            p.Height = 24; a = NewLabel(Theme.Small, Theme.Dim, 0, 0, W, ContentAlignment.MiddleLeft); break;
                        default: p.Height = 12; break;
                    }
                    if (a != null) p.Controls.Add(a);
                    if (b != null) p.Controls.Add(b);
                    Hook(p); if (a != null) Hook(a); if (b != null) Hook(b);
                    Controls.Add(p);
                    Slot sl = new Slot(); sl.A = a; sl.B = b; sl.Fill = fill; sl.BarW = barW;
                    slots.Add(sl);
                    y += p.Height;
                }
                ResumeLayout();
                shape = sh;
                Hook(this);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                RowInfo r = rows[i]; Slot s = slots[i];
                if (s.A != null && s.A.Text != r.Left) s.A.Text = r.Left;
                if (s.B != null && s.B.Text != r.Right) s.B.Text = r.Right;
                Color ca = r.Kind == "title" ? Theme.Brass : (r.Kind == "task" ? Theme.Fore : Theme.Dim);
                Color cb = r.Kind == "meter" ? Theme.Fore : Theme.Dim;
                if (r.Kind == "title" && r.Red) { ca = Theme.Red; cb = Theme.Red; }
                else if (r.Red) { ca = Theme.Red; cb = Theme.Red; }
                if (s.A != null && s.A.ForeColor != ca) s.A.ForeColor = ca;
                if (s.B != null && s.B.ForeColor != cb) s.B.ForeColor = cb;
                if (s.Fill != null)
                {
                    Color cf = r.Red ? Theme.Red : Theme.Brass;
                    if (s.Fill.BackColor != cf) s.Fill.BackColor = cf;
                    int w = (int)(s.BarW * Math.Max(0, Math.Min(100, r.Pct)) / 100);
                    if (s.Fill.Width != w) s.Fill.Width = w;
                }
                total += (i < rows.Count) ? RowHeight(r.Kind) : 0;
            }
            Size = new Size(W + 20, total);
            return total;
        }

        static int RowHeight(string k)
        {
            switch (k) { case "title": return 34; case "meter": return 24; case "task": return 32; case "note": return 24; default: return 12; }
        }

        Panel MakeBar(Panel parent, int x, int y, int w, int h)
        {
            Panel bar = new DBPanel(); bar.BackColor = Theme.Track; bar.Location = new Point(x, y); bar.Size = new Size(w, h);
            Panel fill = new DBPanel(); fill.BackColor = Theme.Brass; fill.Height = h; fill.Location = new Point(0, 0);
            bar.Controls.Add(fill); parent.Controls.Add(bar);
            return fill;
        }
    }

    // 테두리 없는 창 (마우스 올림 창·떠 있는 창 공용). 눌러도 활성화되지 않는다.
    class BoardForm : Form
    {
        public BoardView View;
        public BoardForm(bool draggable, ContextMenuStrip menu)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Dim; Padding = new Padding(1);
            View = new BoardView(); View.Draggable = draggable; View.Menu = menu;
            View.Dock = DockStyle.Fill;
            Controls.Add(View);
            if (menu != null) ContextMenuStrip = menu;
            Text = "HLR 작업 현황";
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x08000000; return cp; }   // TOOLWINDOW | NOACTIVATE
        }
        public void Fit(List<RowInfo> rows)
        {
            int h = View.Render(rows);
            ClientSize = new Size(BoardView.W + 20 + 2, h + 2);
        }
    }

    // ───────────────────────── 아이콘 그리기 ─────────────────────────
    static class IconArt
    {
        public static Bitmap Draw(string text, bool blocked, int size)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float u = size / 16f;
                Color fg = blocked ? Color.FromArgb(255, 96, 82) : Color.FromArgb(239, 227, 204);
                Color ring = blocked ? Color.FromArgb(226, 88, 76) : Theme.Brass;
                using (GraphicsPath gp = Round(new RectangleF(0.5f * u, 0.5f * u, size - 1f * u, size - 1f * u), 3f * u))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(29, 24, 19))) g.FillPath(b, gp);
                    using (Pen p = new Pen(ring, Math.Max(1f, 1f * u))) g.DrawPath(p, gp);
                }
                float fs = text.Length >= 3 ? 7.2f * u : 10.5f * u;
                g.TextRenderingHint = size <= 16 ? TextRenderingHint.AntiAliasGridFit : TextRenderingHint.AntiAlias;
                using (Font f = new Font("Segoe UI", fs, FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush b = new SolidBrush(fg))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(text, f, b, new RectangleF(0, 0.5f * u, size, size), sf);
                }
            }
            return bmp;
        }
        static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
        // 비트맵에서 아이콘을 만든다 (핸들은 복사 후 바로 반납)
        public static Icon ToIcon(Bitmap bmp)
        {
            IntPtr h = bmp.GetHicon();
            Icon tmp = Icon.FromHandle(h);
            Icon copy = (Icon)tmp.Clone();
            tmp.Dispose();
            Native.DestroyIcon(h);
            return copy;
        }
    }

    // ───────────────────────── 알림 영역 아이콘 한 개 ─────────────────────────
    class TrayItem
    {
        public string Name;        // "클로드" / "코덱스"
        public NotifyIcon Ni = new NotifyIcon();
        string lastKey = "";
        Icon cur;

        public Rectangle Rect()
        {
            try
            {
                FieldInfo wf = typeof(NotifyIcon).GetField("window", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo idf = typeof(NotifyIcon).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
                NativeWindow w = (NativeWindow)wf.GetValue(Ni);
                Native.NOTIFYICONIDENTIFIER id = new Native.NOTIFYICONIDENTIFIER();
                id.cbSize = Marshal.SizeOf(typeof(Native.NOTIFYICONIDENTIFIER));
                id.hWnd = w.Handle; id.uID = (uint)(int)idf.GetValue(Ni);
                Native.RECT r;
                if (Native.Shell_NotifyIconGetRect(ref id, out r) == 0)
                    return Rectangle.FromLTRB(r.left, r.top, r.right, r.bottom);
            }
            catch { }
            return Rectangle.Empty;
        }

        public void Update(Usage u, string tip)
        {
            string txt = (u != null && u.Known) ? ((int)Math.Max(0, Math.Min(100, Math.Round(100 - u.Five)))).ToString() : "--";
            bool blocked = u != null && u.Blocked;
            string key = txt + "|" + blocked;
            if (key != lastKey)
            {
                int size = 16;
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) size = (int)Math.Round(16 * g.DpiX / 96f);
                if (size < 16) size = 16;
                Icon old = cur;
                using (Bitmap b = IconArt.Draw(txt, blocked, size)) cur = IconArt.ToIcon(b);
                Ni.Icon = cur;
                if (old != null) old.Dispose();
                lastKey = key;
            }
            if (tip.Length > 63) tip = tip.Substring(0, 63);
            Ni.Text = tip;
        }
    }

    // ───────────────────────── 본체 ─────────────────────────
    class App : ApplicationContext
    {
        Settings st;
        Collector collector;
        Form host;                          // 보이지 않는 창 (다른 스레드에서 화면 쪽으로 넘기는 용도)
        ContextMenuStrip menu = new ContextMenuStrip();
        ToolStripMenuItem miFloat, miAuto;
        TrayItem claudeIcon, codexIcon;
        BoardForm popup, floatWin;
        System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer();
        System.Windows.Forms.Timer pollTimer = new System.Windows.Forms.Timer();
        Snapshot snap;
        bool busy;
        bool pinned;
        DateTime lastHover = DateTime.MinValue;
        Point lastHoverPos;
        DateTime? outsideSince;
        EventWaitHandle showEvent;
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public App(EventWaitHandle ev)
        {
            showEvent = ev;
            st = Settings.Load(AppDomain.CurrentDomain.BaseDirectory);
            collector = new Collector(st);

            host = new Form();
            host.ShowInTaskbar = false; host.FormBorderStyle = FormBorderStyle.None; host.Opacity = 0;
            host.StartPosition = FormStartPosition.Manual; host.Location = new Point(-32000, -32000); host.Size = new Size(1, 1);
            IntPtr hh = host.Handle;

            BuildMenu();
            popup = new BoardForm(false, menu);
            floatWin = new BoardForm(true, menu);
            floatWin.ResizeEnd += delegate { st.FloatX = floatWin.Left; st.FloatY = floatWin.Top; st.Save(); };

            if (st.ShowClaude) claudeIcon = MakeIcon("클로드");
            if (st.ShowCodex) codexIcon = MakeIcon("코덱스");
            if (claudeIcon == null && codexIcon == null) codexIcon = MakeIcon("코덱스");   // 아이콘이 하나도 없으면 길이 막히므로
            Log.W("시작: 아이콘 " + ((claudeIcon != null ? 1 : 0) + (codexIcon != null ? 1 : 0)) + "개 등록");

            // 두 번째 실행이 보내는 신호를 받는다
            Thread t = new Thread(delegate()
            {
                while (true)
                {
                    try { showEvent.WaitOne(); host.BeginInvoke((MethodInvoker)delegate { ShowPopup(true); }); }
                    catch { return; }
                }
            });
            t.IsBackground = true; t.Start();

            refreshTimer.Interval = st.RefreshSeconds * 1000;
            refreshTimer.Tick += delegate { StartCollect(); };
            refreshTimer.Start();
            pollTimer.Interval = 200;
            pollTimer.Tick += delegate { Poll(); };
            pollTimer.Start();
            StartCollect();
            if (st.Floating) ShowFloat(true);
        }

        TrayItem MakeIcon(string name)
        {
            TrayItem t = new TrayItem();
            t.Name = name;
            t.Update(null, name + " 읽는 중");
            t.Ni.ContextMenuStrip = menu;
            t.Ni.MouseMove += delegate { lastHover = DateTime.Now; lastHoverPos = Cursor.Position; if (!popup.Visible) ShowPopup(false); };
            t.Ni.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                if (pinned) { pinned = false; popup.Hide(); }
                else { pinned = true; ShowPopup(false); }
            };
            t.Ni.Visible = true;
            return t;
        }

        void BuildMenu()
        {
            ToolStripMenuItem open = new ToolStripMenuItem("열기");
            open.Click += delegate { ShowPopup(true); };
            miFloat = new ToolStripMenuItem("떠 있는 창으로 보기");
            miFloat.Click += delegate { ShowFloat(!st.Floating); };
            miAuto = new ToolStripMenuItem("윈도우 시작 시 자동 실행");
            miAuto.Click += delegate { SetAutoRun(!IsAutoRun()); };
            ToolStripMenuItem cfg = new ToolStripMenuItem("설정 파일 열기");
            cfg.Click += delegate
            {
                try { if (!File.Exists(st.FilePath)) st.Save(); Process.Start("notepad.exe", "\"" + st.FilePath + "\""); }
                catch (Exception ex) { Log.W("설정 열기 실패: " + ex.Message); }
            };
            ToolStripMenuItem quit = new ToolStripMenuItem("종료");
            quit.Click += delegate { Quit(); };
            menu.Items.Add(open); menu.Items.Add(miFloat); menu.Items.Add(miAuto); menu.Items.Add(cfg);
            menu.Items.Add(new ToolStripSeparator()); menu.Items.Add(quit);
            menu.Opening += delegate { miFloat.Checked = st.Floating; miAuto.Checked = IsAutoRun(); };
        }

        bool IsAutoRun()
        {
            try { using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey)) { return k != null && k.GetValue("HlrBoard") != null; } }
            catch { return false; }
        }
        void SetAutoRun(bool on)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue("HlrBoard", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("HlrBoard", false);
                }
            }
            catch (Exception ex) { Log.W("자동 실행 설정 실패: " + ex.Message); }
        }

        void Quit()
        {
            refreshTimer.Stop(); pollTimer.Stop();
            if (claudeIcon != null) { claudeIcon.Ni.Visible = false; claudeIcon.Ni.Dispose(); }
            if (codexIcon != null) { codexIcon.Ni.Visible = false; codexIcon.Ni.Dispose(); }
            ExitThread();
            Environment.Exit(0);
        }

        // ── 자료 읽기 (화면 스레드를 막지 않는다) ──
        void StartCollect()
        {
            if (busy) return;
            busy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Snapshot s = null;
                try { s = collector.Collect(); }
                catch (Exception ex) { Log.W("갱신 실패: " + ex); }
                try { host.BeginInvoke((MethodInvoker)delegate { busy = false; if (s != null) Apply(s); }); }
                catch { busy = false; }
            });
        }

        string Tip(string name, Usage u)
        {
            if (u == null || !u.Known) return name + " 기록 없음";
            string head = u.Blocked ? name + " 사용 불가 · " : name + " ";
            return head + "5시간 남음 " + (int)Math.Max(0, 100 - u.Five) + "% · 이번 주 사용 " + (int)u.Week + "%";
        }

        void Apply(Snapshot s)
        {
            snap = s;
            if (claudeIcon != null) claudeIcon.Update(s.Claude, Tip("클로드", s.Claude));
            if (codexIcon != null) codexIcon.Update(s.Codex, Tip("코덱스", s.Codex));
            List<RowInfo> rows = RowBuilder.Build(s, st);
            int oldH = popup.Height;
            popup.Fit(rows);
            if (popup.Visible && popup.Height != oldH) popup.Top += oldH - popup.Height;   // 아래쪽 기준 유지
            floatWin.Fit(rows);
        }

        // ── 마우스 올림 창 ──
        void ShowPopup(bool pin)
        {
            if (snap == null) return;
            if (pin) pinned = true;
            Point c = Cursor.Position;
            Rectangle wa = Screen.FromPoint(c).WorkingArea;
            Rectangle bounds = Screen.FromPoint(c).Bounds;
            int w = popup.Width, h = popup.Height;
            int x = c.X - w / 2;
            int y;
            if (c.Y > bounds.Top + bounds.Height / 2) y = wa.Bottom - h - 6;   // 작업 표시줄이 아래: 바로 위에
            else y = wa.Top + 6;                                               // 위에 있으면 바로 아래에
            if (x + w > wa.Right - 6) x = wa.Right - w - 6;
            if (x < wa.Left + 6) x = wa.Left + 6;
            popup.Location = new Point(x, y);
            outsideSince = null;
            if (!popup.Visible) popup.Show();
            popup.TopMost = true;
        }

        bool OverIcon(Point p)
        {
            if ((DateTime.Now - lastHover).TotalMilliseconds < 350) return true;   // 아이콘 위에서 움직이는 동안은 계속 신호가 온다
            TrayItem[] arr = new TrayItem[] { claudeIcon, codexIcon };
            foreach (TrayItem t in arr)
            {
                if (t == null) continue;
                Rectangle r = t.Rect();
                if (!r.IsEmpty && r.Contains(p)) return true;
            }
            return false;
        }

        void Poll()
        {
            if (!popup.Visible) { outsideSince = null; return; }
            if (pinned)
            {
                if ((Native.GetAsyncKeyState(0x1B) & 0x8000) != 0) { pinned = false; popup.Hide(); }   // Esc
                return;
            }
            Point p = Cursor.Position;
            bool inside = popup.Bounds.Contains(p) || OverIcon(p);
            if (inside) { outsideSince = null; return; }
            if (!outsideSince.HasValue) { outsideSince = DateTime.Now; return; }
            if ((DateTime.Now - outsideSince.Value).TotalMilliseconds >= 1000) { popup.Hide(); outsideSince = null; }
        }

        // ── 떠 있는 창 ──
        void ShowFloat(bool on)
        {
            st.Floating = on;
            if (on)
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                int x = st.FloatX, y = st.FloatY;
                Point pt = new Point(x, y);
                bool ok = x != -1 || y != -1;
                if (ok)
                {
                    ok = false;
                    foreach (Screen s in Screen.AllScreens) if (s.WorkingArea.Contains(new Point(x + 20, y + 20))) ok = true;
                }
                if (!ok) pt = new Point(wa.Right - floatWin.Width - 20, wa.Bottom - floatWin.Height - 20);
                floatWin.Location = pt;
                if (!floatWin.Visible) floatWin.Show();
                st.FloatX = floatWin.Left; st.FloatY = floatWin.Top;
            }
            else floatWin.Hide();
            st.Save();
        }
    }

    // ───────────────────────── 시작 ─────────────────────────
    static class Program
    {
        static string ArgVal(string[] a, string key)
        {
            for (int i = 0; i < a.Length - 1; i++) if (string.Equals(a[i], key, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }

        // 지금 자료로 현황 창을 그림 파일로 저장하고 끝낸다 (검증·설명용)
        static void SnapshotPanel(Settings st, string png)
        {
            Collector c = new Collector(st);
            Snapshot s = c.Collect();
            BoardForm f = new BoardForm(false, null);
            f.Location = new Point(-20000, -20000);
            f.Fit(RowBuilder.Build(s, st));
            f.Show();
            Application.DoEvents();
            using (Bitmap bmp = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                bmp.Save(png, ImageFormat.Png);
            }
            f.Close();
        }

        static void SnapshotIcons(string png)
        {
            // 위: 실제 크기(32) 정상·사용 불가·알 수 없음·100, 아래: 4배 확대
            string[] txt = new string[] { "31", "0", "--", "100" };
            bool[] bl = new bool[] { false, true, false, false };
            using (Bitmap bmp = new Bitmap(4 * 148 + 16, 32 + 24 + 128 + 16))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(70, 70, 76));
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                for (int i = 0; i < 4; i++)
                {
                    using (Bitmap ic = IconArt.Draw(txt[i], bl[i], 32))
                    {
                        g.DrawImage(ic, 8 + i * 148, 8, 32, 32);
                        g.DrawImage(ic, 8 + i * 148, 8 + 32 + 16, 128, 128);
                    }
                }
                bmp.Save(png, ImageFormat.Png);
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            Log.Path = Path.Combine(dir, "HlrBoard.log");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string snapPng = ArgVal(args, "--snapshot"), iconPng = ArgVal(args, "--iconsnap");
            if (snapPng != null || iconPng != null)
            {
                try
                {
                    if (iconPng != null) SnapshotIcons(iconPng);
                    if (snapPng != null) SnapshotPanel(Settings.Load(dir), snapPng);
                }
                catch (Exception ex) { Log.W("스냅샷 실패: " + ex); }
                return;
            }

            bool created;
            EventWaitHandle ev = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\HlrBoard_Show");
            Mutex mutex = new Mutex(true, @"Local\HlrBoard_Singleton", out created);
            if (!created) { ev.Set(); return; }   // 이미 떠 있으면 그쪽에서 창을 보여 준다
            try
            {
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log.W("예외: " + e.Exception); };
                Application.Run(new App(ev));
            }
            catch (Exception ex) { Log.W("치명적 오류: " + ex); }
            finally { GC.KeepAlive(mutex); }
        }
    }
}
