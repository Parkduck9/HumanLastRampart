using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.Combat.Tests
{
    // 주인공 걷기·달리기 시트 칸 점검 (W49, 사용자 1.0 시험 "플레이어 움직일때 짤리는거같은데"):
    // 모양 옆 끝 줄이 세로로 길게 곧게 끊긴 칸 = 그림이 옆으로 싹둑 잘린 칸. 자연스러운 옆 끝(머리카락·주먹·발끝)은 한 줄에 27화소를 안 넘는다.
    // 예전 달리기 행(행 2) 1·2번 칸은 뒤로 뻗은 다리·장화가 옆으로 잘려 있었다 (42~45화소 곧은 단면) — Codex K-133 그림으로 고쳐졌다.
    // 걷기 행(행 1) 1·3번 칸은 앞 장화 끝이 칸 안쪽에서 잘려 있었다 (33~42화소, 맨 끝 줄이 아니라 놓쳤다) — Codex K-134로 고쳐졌다.
    // 그래서 맨 끝 줄만이 아니라 칸 안의 모든 세로 윤곽(불투명 옆이 투명)을 본다. 잘린 칸이 하나라도 생기면 실패한다
    // 앞·뒤 걷기 행(0·3)은 한쪽 다리가 외투 밑에서 끊겨 있어 서 있는 뒷모습 그림의 두 다리로 바꿨다 (2026-11-02, 사용자 허락으로 Claude가 고침)
    // 그 뒤 앞·뒤 걷기 행은 코덱스 K-139 새 시안을 Claude가 게임 격자·색에 맞춰 다듬은 그림으로 바꿨다 (D-088)
    // 달리기 행(행 2)도 코덱스 K-140 새 시안을 다듬은 그림으로 바꿨다 — 2·3번 칸 뒷발이 장화 모양 없는 덩어리였다 (D-089)
    // 그 뒤 걷기·달리기 머리가 서 있기 그림보다 커서 시안에서 머리·몸을 따로 줄여 다시 맞췄다 (D-091)
    public sealed class HeroSheetEdgeTests
    {
        const string Dir = "_Project/Art/Characters/Hero/";
        const int SliceRun = 30;   // 곧은 단면 판정: 한 세로 윤곽에서 이어진 화소 수. 자연스러운 윤곽(3배 확대 그림)은 27을 안 넘는다

        // 한 칸 안의 세로 윤곽 중 가장 긴 곧은 단면 (불투명 화소 바로 옆(dir 쪽)이 투명인 줄이 세로로 이어진 길이)
        static int LongestStraightEdge(Color32[] pixels, int width, int cellX, int cellY, int cellW, int cellH, out int atX)
        {
            int best = 0; atX = -1;
            for (int dir = -1; dir <= 1; dir += 2)
                for (int x = 1; x < cellW - 1; x++)
                {
                    int run = 0;
                    for (int y = 0; y < cellH; y++)
                    {
                        int i = (cellY + y) * width + cellX + x;
                        if (pixels[i].a > 128 && pixels[i + dir].a < 20) { run++; if (run > best) { best = run; atX = x; } }
                        else run = 0;
                    }
                }

            return best;
        }

        static void AssertNoSlicedCells(string file, int columns, int rows)
        {
            string path = Path.Combine(Application.dataPath, Dir + file);
            Assert.IsTrue(File.Exists(path), "주인공 시트가 있다: " + path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(path)));
                int cw = tex.width / columns, ch = tex.height / rows;
                Color32[] pixels = tex.GetPixels32();   // 아래가 y = 0 (시트를 위에서부터 센 행 r 은 아래에서 rows-1-r)
                var found = new List<string>();
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < columns; c++)
                    {
                        int run = LongestStraightEdge(pixels, tex.width, c * cw, (rows - 1 - r) * ch, cw, ch, out int x);
                        if (run > SliceRun) found.Add($"행{r} 칸{c} x={x} {run}화소");
                    }

                Assert.IsEmpty(found, file + "에 옆이 곧게 잘린 칸이 있다: " + string.Join(", ", found));

                // 밑이 가로로 잘린 칸: 칸 맨 아래 불투명 줄이 어두운 밑창 윤곽이 아니라 부츠 색이면 땅에서 싹둑 잘린 것이다
                // (옆 달리기·걷기 8칸이 그랬다 — 26~61%. 고친 뒤와 서 있는 그림은 100%)
                var cut = new List<string>();
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < columns; c++)
                    {
                        int cellY = (rows - 1 - r) * ch, opaque = 0, dark = 0, bottom = -1;
                        for (int y = 0; y < ch && bottom < 0; y++)   // 아래(y=0)부터 처음 불투명이 있는 줄
                            for (int x = 0; x < cw; x++)
                                if (pixels[(cellY + y) * tex.width + c * cw + x].a > 128) { bottom = y; break; }
                        if (bottom < 0) continue;
                        for (int x = 0; x < cw; x++)
                        {
                            Color32 p = pixels[(cellY + bottom) * tex.width + c * cw + x];
                            if (p.a <= 128) continue;
                            opaque++;
                            if (p.r < 55 && p.g < 55 && p.b < 55) dark++;
                        }

                        if (dark < opaque * 0.85f) cut.Add($"행{r} 칸{c} 밑줄 어두운 비율 {dark}/{opaque}");
                    }

                Assert.IsEmpty(cut, file + "에 밑이 가로로 잘린 칸이 있다: " + string.Join(", ", cut));
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void WalkRunSheetHasNoSlicedFrames() => AssertNoSlicedCells("hero_walk_run_grid.png", 4, 4);

        [Test]
        public void BackIdleHasNoSlicedFrames() => AssertNoSlicedCells("hero_back_idle.png", 1, 1);

        [Test]
        public void CommandSheetHasNoSlicedFrames() => AssertNoSlicedCells("hero_command.png", 3, 1);
    }
}
