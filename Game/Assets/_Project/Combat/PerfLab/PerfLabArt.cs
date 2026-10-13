using UnityEngine;

namespace HumanLastRampart.Combat.PerfLab
{
    // 시제품용 그림 설정과 임시 그림.
    // 실제 그림(고블린 시트)이 연결되지 않았을 때도 같은 4열×2행 규격으로 돌아가도록 임시 시트를 만든다.
    public static class PerfLabArt
    {
        public const int FramesPerAction = 4;   // 걷기 4 + 공격 4
        const int CellSize = 16;

        // 셰이더에 넘길 종류별 시트 규격
        public struct SheetLayout
        {
            public Vector4 Grid;    // 열, 행
            public Vector4 Pivot;   // 칸 안 발 위치 (왼쪽 아래 기준 0~1)
            public float Aspect;    // 칸 가로/세로

            public static SheetLayout Make(int columns, int rows, float pivotX, float pivotY, float aspect) =>
                new SheetLayout
                {
                    Grid = new Vector4(columns, rows, 0f, 0f),
                    Pivot = new Vector4(pivotX, pivotY, 0f, 0f),
                    Aspect = aspect,
                };
        }

        // 고블린 시트 (Docs/References/codex_goblin_reference_v1.md): 4열×2행, 칸 256×384, 발 (128, 352) — 위에서 352px
        public static readonly SheetLayout GoblinLayout = SheetLayout.Make(4, 2, 0.5f, (384f - 352f) / 384f, 256f / 384f);
        public static readonly SheetLayout PlaceholderLayout = SheetLayout.Make(4, 2, 0.5f, 0f, 1f);
        public static readonly SheetLayout ArrowLayout = SheetLayout.Make(1, 1, 0.5f, 0.5f, 0.35f);

        // 발이 원점, 높이 1인 사각형. 가로·발 위치·크기는 셰이더가 정한다.
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

        // 흰색 사람 모양 4열×2행 (위: 걷기, 아래: 공격). 색은 개체별로 곱한다.
        public static Texture2D CreatePlaceholderSheet()
        {
            int width = CellSize * FramesPerAction;
            int height = CellSize * 2;
            var texture = NewTexture("PerfLabPlaceholderSheet", width, height);
            var pixels = ClearPixels(width * height);

            for (int row = 0; row < 2; row++)
            {
                // 텍스처 y는 아래가 0이므로 위쪽 행(걷기)은 y0 = CellSize
                int y0 = (1 - row) * CellSize;
                for (int frame = 0; frame < FramesPerAction; frame++)
                {
                    int x0 = frame * CellSize;
                    FillRect(pixels, width, x0 + 6, y0 + 12, 4, 3);
                    FillRect(pixels, width, x0 + 5, y0 + 6, 6, 6);
                    int stride = row == 0 ? frame % 3 : 0;
                    FillRect(pixels, width, x0 + 5 - stride, y0, 2, 6);
                    FillRect(pixels, width, x0 + 9 + stride, y0, 2, 6);
                    if (row == 1)
                    {
                        // 공격: 팔을 들었다 내린다
                        FillRect(pixels, width, x0 + 11, y0 + 8 + (frame == 1 ? 4 : 0), 4, 2);
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // 화살: 가는 세로 막대 (세로 빌보드로 세워 보인다)
        public static Texture2D CreateArrowTexture()
        {
            const int size = 8;
            var texture = NewTexture("PerfLabArrow", size, size);
            var pixels = ClearPixels(size * size);
            FillRect(pixels, size, 3, 0, 2, 8);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Texture2D NewTexture(string name, int width, int height) =>
            new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

        static Color32[] ClearPixels(int count)
        {
            var pixels = new Color32[count];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            return pixels;
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
