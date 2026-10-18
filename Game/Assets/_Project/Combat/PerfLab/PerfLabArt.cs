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

        // Codex 캐릭터 시트 공통 규격: 칸 256×384, 모든 칸 발 (128, 352). 그림 크기로 열·행을 센다
        // (고블린 1024×768 = 4×2 걷기·공격, 주인공 1024×1536 = 4×4, 정적 데모 256×384 = 1×1)
        public const int CellWidth = 256, CellHeight = 384;
        public static SheetLayout FromSheet(Texture sheet) => SheetLayout.Make(
            Mathf.Max(1, sheet.width / CellWidth), Mathf.Max(1, sheet.height / CellHeight),
            0.5f, (CellHeight - 352f) / CellHeight, (float)CellWidth / CellHeight);
        public static int FrameCount(Texture sheet) => Mathf.Max(1, sheet.width / CellWidth) * Mathf.Max(1, sheet.height / CellHeight);

        // 시트별 인물 크기 맞추기 (K-034): 같은 칸 384px 안이라도 시트마다 인물이 다른 크기로 그려져 있다
        // (서 있기 300px, 사격 249~265, 달리기 약 230~260 환산 — Tools/SheetMeasure/sheet-heights.md).
        // 셰이더 _SheetScale = 기준 키 ÷ 이 시트 인물 키 → 어느 시트로 그려도 인물이 같은 크기. 발 줄(352)은 모든 시트가 같다.
        // 병사·주인공·마법사 기준 = FigureReferencePx(칸 높이 384) → 개체 크기(m) = 인물 실제 키 (병사 0.35m).
        // 300으로 바꾸면 서 있는 병사가 예전(K-033까지) 화면 크기로 돌아간다 (그때는 인물 키 = 크기 × 300/384)
        public const float FigureReferencePx = CellHeight;
        public const float DefaultFigurePx = 300f;   // 표에 없는 시트: Codex 기준 서 있는 인물 키

        public static float FigurePx(Texture sheet) =>
            sheet != null && sheet.name.StartsWith(TrialSheetPrefix) ? TrialFigurePx
            : sheet != null && PerfLabSheetHeights.FigurePx.TryGetValue(sheet.name, out float px) && px > 1f ? px : DefaultFigurePx;

        // 3등신 시험 그림 (K-057, Codex K-056): 모든 칸 인물 키 192px(원본 48점 × 4). 이름이 이 앞글자로 시작하면 이 키로 맞춘다
        // → 기존 그림(서기 300px)과 같은 월드 키로 선다. 자동 표(PerfLabSheetHeights)는 손대지 않는다
        public const string TrialSheetPrefix = "trial3_";
        public const float TrialFigurePx = 192f;

        // 그 시트를 그릴 배율. referencePx = 이 개체의 기준 인물 키 (병사 FigureReferencePx, 적은 자기 기본 시트 키)
        public static float SheetScale(Texture sheet, float referencePx = FigureReferencePx) => referencePx / FigurePx(sheet);

        // 주인공 시트 (Codex M-CX-048, Docs/References/codex_hero_grid_v2): 4열×4행, 칸 256×384, 모든 칸 발 (128, 352).
        // 행: 0 뒷모습 걷기(첫 칸 = 서 있기) / 1 옆모습 걷기(왼쪽 보기) / 2 옆모습 달리기(왼쪽) / 3 앞모습 걷기.
        // 오른쪽은 왼쪽 행 좌우 뒤집기. 걷기는 칸 순서 HeroWalkFlipPhase, 달리기는 그대로 (JSON)
        public const int HeroRows = 4;
        public const int HeroRowBack = 0, HeroRowSideWalk = 1, HeroRowSideRun = 2, HeroRowFront = 3;
        public static readonly int[] HeroWalkFlipPhase = { 2, 3, 0, 1 };
        public static readonly SheetLayout HeroLayout = SheetLayout.Make(4, HeroRows, 0.5f, (384f - 352f) / 384f, 256f / 384f);
        public static readonly SheetLayout HeroPlaceholderLayout = SheetLayout.Make(4, HeroRows, 0.5f, 0f, 1f);
        public static readonly SheetLayout ArrowLayout = SheetLayout.Make(1, 1, 0.5f, 0.5f, 0.35f);

        // 동작 그림 칸 구간 (K-032): 한 개체 버퍼를 시트마다 한 번씩 그리고, 셰이더 _FrameWindow가 자기 구간 칸만 남긴다.
        // 프레임 번호 = 구간 시작 + 그 시트 안 칸 번호. 0~15는 원래 시트(걷기·공격·주인공 4×4)
        public const int PoseWindow = 16;
        public const int AttackFrame0 = 16;   // 적 공격 시트 (원래 시트에 공격 행이 없을 때)
        public const int DeployFrame0 = 32;   // 사다리 세우기
        public const int ClimbFrame0 = 48;    // 사다리 오르기
        public const int FireFrame0 = 64;     // 병사 사격
        public const int IgniteFrame0 = 80;   // 대포병 불붙이기 (K-046)
        public const int FallFrame0 = 96;     // 쓰러짐 — 지도 점은 이 앞까지만 (죽어 가는 개체는 점으로 안 그림)
        public static Vector4 Window(int start, int count = PoseWindow) => new Vector4(start, start + count, 0f, 0f);

        // 투사체 1칸 256×128, 가운데 (128, 64) 기준, 오른쪽(+X)을 본다 (Codex K-016)
        public static SheetLayout ProjectileLayout(Texture sheet) => SheetLayout.Make(1, 1, 0.5f, 0.5f, (float)sheet.width / Mathf.Max(1, sheet.height));

        // 사다리 소품 1칸 256×1536, 아래 (128, 1472). 실체 1152px = 4.3m → 칸 전체 높이(m)
        public static readonly SheetLayout LadderLayout = SheetLayout.Make(1, 1, 0.5f, (1536f - 1472f) / 1536f, 256f / 1536f);
        public const float LadderDrawSize = 1536f / 1152f * 4.3f;

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

        // 주인공 임시 시트 (HeroLayout과 같은 4행 순서). 옆모습은 얼굴이 왼쪽으로 튀어나와 뒤집기가 보인다
        public static Texture2D CreateHeroPlaceholderSheet()
        {
            int width = CellSize * FramesPerAction;
            int height = CellSize * HeroRows;
            var texture = NewTexture("HeroPlaceholderSheet", width, height);
            var pixels = ClearPixels(width * height);
            for (int row = 0; row < HeroRows; row++)
            {
                int y0 = (HeroRows - 1 - row) * CellSize;   // 텍스처 y는 아래가 0
                bool side = row == HeroRowSideWalk || row == HeroRowSideRun;
                bool run = row == HeroRowSideRun;
                for (int frame = 0; frame < FramesPerAction; frame++)
                {
                    int x0 = frame * CellSize;
                    int lean = run ? -1 : 0;   // 달리기는 앞(왼쪽)으로 기울임
                    FillRect(pixels, width, x0 + 6 + lean, y0 + 12, 4, 3);   // 머리
                    if (side) FillRect(pixels, width, x0 + 4 + lean, y0 + 13, 2, 1);   // 얼굴 방향(왼쪽)
                    if (row == HeroRowFront) FillRect(pixels, width, x0 + 7, y0 + 11, 2, 1);   // 앞: 목깃
                    FillRect(pixels, width, x0 + 5 + lean, y0 + 6, 6, 6);    // 몸 (장교 옷)
                    int stride = frame == 0 && row == HeroRowBack ? 0 : (run ? (frame % 2) * 3 : frame % 3);
                    FillRect(pixels, width, x0 + 5 - stride, y0, 2, 6);
                    FillRect(pixels, width, x0 + 9 + stride, y0, 2, 6);
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
