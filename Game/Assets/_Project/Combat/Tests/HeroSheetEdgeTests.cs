using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 주인공 걷기·달리기 시트 칸 점검 (W49, 사용자 1.0 시험 "플레이어 움직일때 짤리는거같은데"):
    // 모양 옆 끝 줄이 세로로 길게 곧게 끊긴 칸 = 그림이 옆으로 싹둑 잘린 칸. 자연스러운 옆 끝(머리카락·주먹·발끝)은 한 줄에 27화소를 안 넘는다.
    // 지금 달리기 행(행 2) 1·2번 칸은 뒤로 뻗은 다리·장화가 원본부터 옆으로 잘려 있다 (42~45화소 곧은 단면) — 그림 다시 그리기는 Codex 몫.
    // 새 잘림이 생기면 이 시험이 실패하고, 위 두 칸을 고치면 알려진 목록에서 지우라고 알려 준다
    public sealed class HeroSheetEdgeTests
    {
        const string SheetPath = "_Project/Art/Characters/Hero/hero_walk_run_grid.png";
        const int Columns = 4, Rows = 4, SliceRun = 36;   // 곧은 단면 판정: 맨 끝 줄에서 이어진 불투명 화소 수
        static readonly HashSet<string> KnownSliced = new HashSet<string> { "행2 칸1 오른쪽", "행2 칸2 오른쪽" };

        // 한 칸의 왼쪽·오른쪽 맨 끝 불투명 줄에서 세로로 이어진 최대 길이
        static void EdgeRuns(Color32[] pixels, int width, int cellX, int cellY, int cellW, int cellH, out int left, out int right)
        {
            int minX = int.MaxValue, maxX = -1;
            for (int y = 0; y < cellH; y++)
                for (int x = 0; x < cellW; x++)
                    if (pixels[(cellY + y) * width + cellX + x].a > 8) { if (x < minX) minX = x; if (x > maxX) maxX = x; }
            left = maxX < 0 ? 0 : Run(pixels, width, cellX + minX, cellY, cellH);
            right = maxX < 0 ? 0 : Run(pixels, width, cellX + maxX, cellY, cellH);
        }

        static int Run(Color32[] pixels, int width, int x, int cellY, int cellH)
        {
            int best = 0, run = 0;
            for (int y = 0; y < cellH; y++)
            {
                if (pixels[(cellY + y) * width + x].a > 8) { run++; if (run > best) best = run; }
                else run = 0;
            }

            return best;
        }

        [Test]
        public void WalkRunSheetSlicedFramesAreOnlyTheKnownOnes()
        {
            string path = Path.Combine(Application.dataPath, SheetPath);
            Assert.IsTrue(File.Exists(path), "주인공 시트가 있다: " + path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(path)));
                int cw = tex.width / Columns, ch = tex.height / Rows;
                Color32[] pixels = tex.GetPixels32();   // 아래가 y = 0 (시트를 위에서부터 센 행 r 은 아래에서 Rows-1-r)
                var found = new HashSet<string>();
                for (int r = 0; r < Rows; r++)
                    for (int c = 0; c < Columns; c++)
                    {
                        EdgeRuns(pixels, tex.width, c * cw, (Rows - 1 - r) * ch, cw, ch, out int left, out int right);
                        if (left > SliceRun) found.Add($"행{r} 칸{c} 왼쪽");
                        if (right > SliceRun) found.Add($"행{r} 칸{c} 오른쪽");
                    }

                var fresh = new List<string>(found); fresh.RemoveAll(KnownSliced.Contains);
                var fixedNow = new List<string>(KnownSliced); fixedNow.RemoveAll(found.Contains);
                Assert.IsEmpty(fresh, "새로 옆이 잘린 주인공 칸이 생겼다: " + string.Join(", ", fresh));
                Assert.IsEmpty(fixedNow, "잘렸던 칸이 고쳐졌다 — KnownSliced에서 지운다: " + string.Join(", ", fixedNow));
            }
            finally { Object.DestroyImmediate(tex); }
        }
    }
}
