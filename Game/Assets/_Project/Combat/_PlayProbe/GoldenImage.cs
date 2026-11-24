using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// W30 화면 회귀 시험의 비교기: 사진을 240×135로 줄여 기준 사진과 화소별로 견준다.
// 기준 사진은 Combat/Tests/GoldenBaselines/*.png (240×135). HLR_UPDATE_BASELINES=1이면 기준을 새로 쓴다.
// 실패하면 HLR_VISUAL_DIFF(기본 C:\Unity\1\HLR-work\visual_diff)에 actual/baseline/diff 사진을 남긴다 (Assets 밖).
public enum GoldenKind { Day, Night, Ui }

public static class GoldenImage
{
    public const int W = 240, H = 135;
    public const int PixelTolerance = 24;        // 화소 차이(채널 최댓값, 0~255)가 이보다 크면 "다른 화소"
    public const double MaxDiffPercent = 3.0;    // 다른 화소가 전체의 3%를 넘으면 실패
    public const double MaxMeanError = 0.02;     // 평균 오차(0~1)가 이를 넘으면 실패
    public const double MaxOneColourShare = 0.92; // 한 가지 색(상위 4비트 양자화)이 이만큼 넘으면 "거의 한 색"
    public const double MaxMagentaShare = 0.02;  // 마젠타(셰이더 없음)가 이만큼 넘으면 실패

    public struct Diff { public double DiffPercent, MeanError; }

    public static string BaselineFolder => Path.Combine(Application.dataPath, "_Project", "Combat", "Tests", "GoldenBaselines");
    public static string DiffFolder => Environment.GetEnvironmentVariable("HLR_VISUAL_DIFF") ?? @"C:\Unity\1\HLR-work\visual_diff";
    public static bool UpdateMode => Environment.GetEnvironmentVariable("HLR_UPDATE_BASELINES") == "1";

    // 상자 평균으로 줄인다 (GPU를 쓰지 않아 실행마다 같다)
    public static Color32[] Downscale(Color32[] src, int sw, int sh, int dw = W, int dh = H)
    {
        var dst = new Color32[dw * dh];
        for (int y = 0; y < dh; y++)
        {
            int y0 = y * sh / dh, y1 = Math.Max(y0 + 1, (y + 1) * sh / dh);
            for (int x = 0; x < dw; x++)
            {
                int x0 = x * sw / dw, x1 = Math.Max(x0 + 1, (x + 1) * sw / dw);
                int r = 0, g = 0, b = 0, n = 0;
                for (int yy = y0; yy < y1; yy++)
                    for (int xx = x0; xx < x1; xx++) { Color32 p = src[yy * sw + xx]; r += p.r; g += p.g; b += p.b; n++; }
                dst[y * dw + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
            }
        }
        return dst;
    }

    public static Diff Compare(Color32[] a, Color32[] b, int tolerance = PixelTolerance)
    {
        if (a.Length != b.Length) return new Diff { DiffPercent = 100, MeanError = 1 };
        int differing = 0; double error = 0;
        for (int i = 0; i < a.Length; i++)
        {
            int dr = Math.Abs(a[i].r - b[i].r), dg = Math.Abs(a[i].g - b[i].g), db = Math.Abs(a[i].b - b[i].b);
            if (Math.Max(dr, Math.Max(dg, db)) > tolerance) differing++;
            error += (dr + dg + db) / (3.0 * 255.0);
        }
        return new Diff { DiffPercent = differing * 100.0 / a.Length, MeanError = error / a.Length };
    }

    // 기준 없이 보는 "고장 아님" 검사: 거의 한 색 / 마젠타 / 평균 밝기 범위. 문제가 있으면 설명을 돌려준다 (없으면 null)
    public static string SanityProblem(Color32[] px, GoldenKind kind, out double mean, out double oneColour, out double magenta)
    {
        var bins = new Dictionary<int, int>();
        double sum = 0; int mag = 0;
        foreach (Color32 p in px)
        {
            sum += (0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b) / 255.0;
            int key = (p.r >> 4) << 8 | (p.g >> 4) << 4 | (p.b >> 4);
            bins.TryGetValue(key, out int c); bins[key] = c + 1;
            if (p.r > 200 && p.b > 200 && p.g < 80) mag++;
        }
        int top = 0; foreach (int c in bins.Values) if (c > top) top = c;
        mean = sum / px.Length; oneColour = top / (double)px.Length; magenta = mag / (double)px.Length;
        double lo, hi;
        switch (kind) { case GoldenKind.Night: lo = 0.02; hi = 0.45; break; case GoldenKind.Day: lo = 0.15; hi = 0.85; break; default: lo = 0.03; hi = 0.90; break; }
        if (magenta > MaxMagentaShare) return $"마젠타 {magenta * 100:0.0}% (셰이더 없음?)";
        if (oneColour > MaxOneColourShare) return $"거의 한 색 {oneColour * 100:0.0}%";
        if (mean < lo || mean > hi) return $"평균 밝기 {mean:0.000}가 {kind} 범위 {lo:0.00}~{hi:0.90}를 벗어남";
        return null;
    }

    public static Color32[] LoadPixels(string path, out int w, out int h)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        try { tex.LoadImage(File.ReadAllBytes(path)); w = tex.width; h = tex.height; return tex.GetPixels32(); }
        finally { UnityEngine.Object.DestroyImmediate(tex); }
    }

    public static void SavePixels(string path, Color32[] px, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        try { tex.SetPixels32(px); tex.Apply(); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, tex.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(tex); }
    }

    // 한 장 검사: 문제 설명 목록에 덧붙인다. 설명이 없으면 통과. line에는 수치 한 줄을 돌려준다
    public static void Check(string name, string actualPng, GoldenKind kind, List<string> problems, out string line)
    {
        Color32[] full = LoadPixels(actualPng, out int w, out int h);
        Color32[] small = Downscale(full, w, h);
        string sanity = SanityProblem(small, kind, out double mean, out double one, out double mag);
        line = $"{name}: 밝기 {mean:0.000}, 한색 {one * 100:0.0}%, 마젠타 {mag * 100:0.00}%";
        if (sanity != null) problems.Add($"{name}: {sanity}");

        string baseline = Path.Combine(BaselineFolder, name + ".png");
        if (UpdateMode)
        {
            SavePixels(baseline, small, W, H);
            line += " [기준 갱신]";
            return;
        }

        if (!File.Exists(baseline)) { problems.Add($"{name}: 기준 사진 없음 ({baseline}) — HLR_UPDATE_BASELINES=1로 만든다"); return; }
        Color32[] basePx = LoadPixels(baseline, out int bw, out int bh);
        if (bw != W || bh != H) { problems.Add($"{name}: 기준 크기 {bw}x{bh} (240x135여야 함)"); return; }
        Diff d = Compare(small, basePx);
        line += $", 다른 화소 {d.DiffPercent:0.00}%, 평균 오차 {d.MeanError:0.0000}";
        if (d.DiffPercent <= MaxDiffPercent && d.MeanError <= MaxMeanError) return;

        problems.Add($"{name}: 기준과 다름 (다른 화소 {d.DiffPercent:0.00}% > {MaxDiffPercent}%, 또는 평균 오차 {d.MeanError:0.0000} > {MaxMeanError}) — {DiffFolder}");
        try
        {
            SavePixels(Path.Combine(DiffFolder, name + "_actual.png"), small, W, H);
            File.Copy(baseline, Path.Combine(DiffFolder, name + "_baseline.png"), true);
            var diff = new Color32[small.Length];
            for (int i = 0; i < diff.Length; i++)
            {
                int m = Math.Max(Math.Abs(small[i].r - basePx[i].r), Math.Max(Math.Abs(small[i].g - basePx[i].g), Math.Abs(small[i].b - basePx[i].b)));
                byte v = (byte)Math.Min(255, m * 4);   // 차이를 4배로 키워 보이게
                diff[i] = new Color32(v, 0, 0, 255);
            }
            SavePixels(Path.Combine(DiffFolder, name + "_diff.png"), diff, W, H);
        }
        catch (Exception e) { Debug.LogWarning("[Golden] 차이 사진 저장 실패: " + e.Message); }
    }
}
