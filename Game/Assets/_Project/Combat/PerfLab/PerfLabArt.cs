using UnityEngine;

namespace HumanLastRampart.Combat.PerfLab
{
    // 시제품용 임시 그림. 실제 아트는 보유 에셋 확인 뒤 연결한다 (PERF_PROTOTYPE.md 2장).
    public static class PerfLabArt
    {
        public const int FrameCount = 4;
        const int FrameSize = 16;

        // 발이 원점, 높이 1인 사각형 (셰이더가 크기를 곱한다)
        public static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "PerfLabQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(0.5f, 1f, 0f),
                new Vector3(-0.5f, 1f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        // 흰색 사람 모양 4프레임 (걷기 3장 + 공격 자세 1장). 색은 개체별로 곱한다.
        public static Texture2D CreateAtlas()
        {
            int width = FrameSize * FrameCount;
            var texture = new Texture2D(width, FrameSize, TextureFormat.RGBA32, false)
            {
                name = "PerfLabAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[width * FrameSize];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            for (int frame = 0; frame < FrameCount; frame++)
            {
                int x0 = frame * FrameSize;
                // 머리와 몸통
                FillRect(pixels, width, x0 + 6, 12, 4, 3);
                FillRect(pixels, width, x0 + 5, 6, 6, 6);

                // 다리: 프레임마다 벌어진 정도를 바꾼다
                int stride = frame == 3 ? 0 : frame;
                FillRect(pixels, width, x0 + 5 - stride, 0, 2, 6);
                FillRect(pixels, width, x0 + 9 + stride, 0, 2, 6);

                // 공격 자세: 팔을 앞으로
                if (frame == 3)
                {
                    FillRect(pixels, width, x0 + 11, 9, 4, 2);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static void FillRect(Color32[] pixels, int width, int x, int y, int w, int h)
        {
            var white = new Color32(255, 255, 255, 255);
            for (int yy = y; yy < y + h; yy++)
            {
                for (int xx = x; xx < x + w; xx++)
                {
                    pixels[yy * width + xx] = white;
                }
            }
        }
    }
}
