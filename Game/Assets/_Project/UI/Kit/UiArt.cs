using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.UI
{
    // K-047 화면 그림 이름 한 곳. Codex 그림(Resources/UiKit/<이름>.png)이 있으면 그 그림을, 없으면 코드로 그린 임시 도트를 쓴다.
    // 이름·크기·9칸 경계는 Docs/GDD/UI_NoSlop_에셋요청.md 와 같다 — 그림을 넣기만 하면 바뀐다.
    // 모든 판·단추·아이콘은 한 점 = 화면 3px(UiTokens.Dot)로 보이게 만든다 (S-30)
    public static class UiArt
    {
        // ── 판 (9칸 늘이기, 가운데는 무늬가 늘어나지 않게 바둑판 반복) ──
        public const string PanelParchment = "panel_parchment";   // 양피지: 지도판·보고서·장부·확인 창
        public const string PanelWood = "panel_wood";             // 짙은 나무 + 쇠 리벳: 명령판·공방 판
        public const string PanelBand = "panel_band";             // 황동 계기 띠: 전투 위 상태 띠·정비 위 띠
        public const string Slip = "slip";                       // 전령 쪽지 (보통 알림)
        public const string SlipDanger = "slip_danger";          // 붉은 녹 띠 쪽지 (위급 알림)
        public const string Bubble = "bubble";                   // 손그림 말풍선 틀 (스토리 대사)
        public const string BubbleTail = "bubble_tail";
        public const string Plate = "plate";                     // 마당 간판 나무판
        // ── 단추 ──
        public const string ButtonWood = "button_wood";
        public const string ButtonWoodDown = "button_wood_down";
        public const string ButtonBrass = "button_brass";
        public const string ButtonBrassDown = "button_brass_down";
        public const string Glow = "frame_glow";                 // 올림·고름 빛 테두리 (가운데 투명)
        public const string KeyCap = "keycap";                   // 키 표시 (E, Tab …)
        public const string BarTrack = "bar_track";              // 막대 홈
        public const string Seal = "seal";                       // 결과 도장
        public const string Vignette = "vignette";               // 화면 가장자리 빛 (위급 붉은 빛·판 뒤 어둠) — 부드러운 것은 빛만
        // ── 아이콘 (안쪽 9점 + 바깥 1점 외곽선) ──
        public const string IconClose = "icon_close", IconUp = "icon_up", IconDown = "icon_down", IconPlay = "icon_play",
            IconPause = "icon_pause", IconSpeed1 = "icon_speed1", IconSpeed2 = "icon_speed2", IconSpeed4 = "icon_speed4",
            IconHero = "icon_hero", IconNorth = "icon_north", IconCheck = "icon_check", IconFlag = "icon_flag",
            IconAuto = "icon_auto", IconIdle = "icon_idle", IconSpecial = "icon_special", IconMinus = "icon_minus",
            IconPlus = "icon_plus", IconRight = "icon_right", IconWarn = "icon_warn";
        // ── 글꼴 (UiFonts) ──
        public const string FontDisplay = "UiKit/Fonts/display", FontBody = "UiKit/Fonts/body";

        // Codex 그림의 9칸 경계 (왼쪽, 아래, 오른쪽, 위 — 그림 점 단위). 코드 임시 그림도 같은 값으로 그린다
        static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { PanelParchment, new Vector4(6, 6, 6, 6) }, { PanelWood, new Vector4(6, 6, 6, 6) }, { PanelBand, new Vector4(6, 6, 6, 6) },
            { Slip, new Vector4(4, 4, 6, 6) }, { SlipDanger, new Vector4(6, 4, 6, 6) }, { Bubble, new Vector4(8, 8, 8, 8) },
            { Plate, new Vector4(5, 5, 5, 5) },
            { ButtonWood, new Vector4(4, 4, 4, 4) }, { ButtonWoodDown, new Vector4(4, 4, 4, 4) },
            { ButtonBrass, new Vector4(4, 4, 4, 4) }, { ButtonBrassDown, new Vector4(4, 4, 4, 4) },
            { Glow, new Vector4(4, 4, 4, 4) }, { KeyCap, new Vector4(3, 4, 3, 3) }, { BarTrack, new Vector4(3, 3, 3, 3) },
        };

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out Sprite cached) && cached != null) return cached;
            Borders.TryGetValue(name, out Vector4 border);
            var texture = Resources.Load<Texture2D>("UiKit/" + name);
            if (texture != null) texture.filterMode = FilterMode.Point;
            else texture = Draw(name);
            if (texture == null) return null;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                100f / UiTokens.Dot, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            cache[name] = sprite;
            return sprite;
        }

        public static bool IsSliced(string name) => Borders.ContainsKey(name);

        // Codex HudArt 그림 (Resources/HudArt/<경로>) — 전투 HUD·정비가 같이 쓴다. 없으면 null (부르는 쪽이 대신 모양을 그린다)
        static readonly Dictionary<string, Sprite> hud = new Dictionary<string, Sprite>();

        public static Sprite Hud(string path, Vector4 border = default)
        {
            if (hud.TryGetValue(path, out Sprite cached))
            {
                if (ReferenceEquals(cached, null)) return null;   // 없는 그림 — 다시 찾지 않는다
                if (cached != null) return cached;               // 장면이 바뀌며 지워졌으면 다시 만든다
            }

            var texture = Resources.Load<Texture2D>("HudArt/" + path);
            Sprite sprite = null;
            if (texture != null)
            {
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                sprite.name = path;
            }

            hud[path] = sprite;
            return sprite;
        }

        // 화면에 놓을 크기 (그림 점 수 × 3px × 배율)
        public static Vector2 Size(Sprite sprite, float scale = 1f) =>
            sprite == null ? Vector2.zero : new Vector2(sprite.rect.width, sprite.rect.height) * UiTokens.Dot * scale;

        // ── 코드로 그린 임시 그림 (Codex 그림이 오기 전) ──
        static Texture2D Draw(string name)
        {
            switch (name)
            {
                case PanelParchment: return Frame(32, FrameStyle.Parchment);
                case PanelWood: return Frame(32, FrameStyle.Wood);
                case PanelBand: return Frame(32, FrameStyle.Band);
                case Plate: return Frame(16, FrameStyle.Plate);
                case Slip: return SlipShape(false);
                case SlipDanger: return SlipShape(true);
                case Bubble: return BubbleShape();
                case BubbleTail: return Tail();
                case ButtonWood: return Button(UiTokens.Wood, UiTokens.WoodLight, UiTokens.WoodDark, false);
                case ButtonWoodDown: return Button(Shade(UiTokens.Wood, 0.85f), UiTokens.WoodDark, UiTokens.WoodDark, true);
                case ButtonBrass: return Button(UiTokens.Brass, UiTokens.BrassLight, UiTokens.BrassDark, false);
                case ButtonBrassDown: return Button(Shade(UiTokens.Brass, 0.88f), UiTokens.BrassDark, UiTokens.BrassDark, true);
                case Glow: return GlowFrame();
                case KeyCap: return KeyCapShape();
                case BarTrack: return Track();
                case Seal: return SealShape();
                case Vignette: return VignetteShape();
            }

            return Icon(name);
        }

        enum FrameStyle { Parchment, Wood, Band, Plate }

        // 판: 계단 모서리(2점) · 가장 바깥 짙은 1점 · 볼록한 테두리(위·왼쪽 밝음, 아래·오른쪽 어두움) · 안쪽 띠 · 무늬 바탕 · 모서리 리벳
        static Texture2D Frame(int size, FrameStyle style)
        {
            Color face, hi, lo, band, outline = UiTokens.Outline, rivet = UiTokens.Brass;
            bool rivets = true, wobble = false, grain = false;
            switch (style)
            {
                case FrameStyle.Parchment:
                    face = UiTokens.Paper; hi = UiTokens.PaperShade; lo = Shade(UiTokens.PaperShade, 0.86f); band = Shade(UiTokens.Paper, 0.93f);
                    outline = UiTokens.Ink; rivets = false; wobble = true; break;
                case FrameStyle.Band:
                    face = UiTokens.Wood; hi = UiTokens.Brass; lo = UiTokens.BrassDark; band = UiTokens.WoodDark; grain = true; break;
                case FrameStyle.Plate:
                    face = UiTokens.WoodLight; hi = Shade(UiTokens.WoodLight, 1.25f); lo = UiTokens.WoodDark; band = UiTokens.Wood; grain = true; break;
                default:
                    face = UiTokens.Wood; hi = UiTokens.WoodLight; lo = UiTokens.WoodDark; band = UiTokens.WoodDark; grain = true; rivet = Shade(UiTokens.Brass, 0.8f); break;
            }

            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Min(x, size - 1 - x), dy = Mathf.Min(y, size - 1 - y);
                    Color c;
                    if (dx + dy < 2) c = Color.clear;
                    else if (dx == 0 || dy == 0 || dx + dy == 2) c = outline;
                    else if (dx == 1 || dy == 1)
                    {
                        bool topOrLeft = dy <= dx ? y > size / 2 : x < size / 2;
                        c = topOrLeft ? hi : lo;
                        if (wobble && Hash(x, y) % 5 == 0) c = outline;   // 손그림 잉크 선처럼 군데군데 굵게
                    }
                    else if (dx == 2 || dy == 2) c = band;
                    else
                    {
                        uint h = Hash(x, y);
                        float k = grain ? 1f + (Noise(Hash(0, y / 2), 7) - 3) * 0.018f + ((h % 9) == 0 ? -0.05f : 0f)
                            : 1f + (Noise(h, 7) - 3) * 0.012f + ((h % 23) == 0 ? -0.06f : 0f);
                        c = Shade(face, k);
                    }

                    if (rivets && (dx == 3 && dy == 3)) c = rivet;
                    if (rivets && (dx == 3 && dy == 4 && y > size / 2 || dx == 4 && dy == 3 && x < size / 2)) c = Shade(rivet, 0.6f);
                    px[y * size + x] = c;
                }

            return Texture(size, size, px, "UiKit " + style);
        }

        // 쪽지: 종이 + 오른쪽 위 접힌 귀 (위급은 왼쪽에 붉은 녹 띠)
        static Texture2D SlipShape(bool danger)
        {
            const int w = 20, h = 14;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int dx = Mathf.Min(x, w - 1 - x), dy = Mathf.Min(y, h - 1 - y);
                    int fold = (w - 1 - x) + (h - 1 - y);
                    Color c;
                    if (dx + dy < 1 || fold < 3) c = Color.clear;
                    else if (fold == 3 || dx == 0 || dy == 0) c = UiTokens.Ink;
                    else if (fold == 4 || fold == 5) c = UiTokens.PaperShade;
                    else if (danger && x >= 1 && x <= 3) c = x == 3 ? Shade(UiTokens.Danger, 0.75f) : UiTokens.Danger;
                    else if (dy == 1 && y < h / 2) c = UiTokens.PaperShade;
                    else c = Shade(UiTokens.Paper, 1f + (Noise(Hash(x, y), 7) - 3) * 0.012f);
                    px[y * w + x] = c;
                }

            return Texture(w, h, px, "UiKit slip");
        }

        // 말풍선: 두 점 굵기의 고르지 않은 잉크 선 + 종이
        static Texture2D BubbleShape()
        {
            const int size = 32;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Min(x, size - 1 - x), dy = Mathf.Min(y, size - 1 - y);
                    Color c;
                    if (dx + dy < 3) c = Color.clear;
                    else if (dx == 0 || dy == 0 || dx + dy == 3) c = UiTokens.Ink;
                    else if ((dx == 1 || dy == 1 || dx + dy == 4) && Hash(x, y) % 3 != 0) c = UiTokens.Ink;
                    else if (dx == 2 || dy == 2) c = UiTokens.PaperShade;
                    else c = Shade(UiTokens.Paper, 1f + (Noise(Hash(x, y), 7) - 3) * 0.012f);
                    px[y * size + x] = c;
                }

            return Texture(size, size, px, "UiKit bubble");
        }

        static Texture2D Tail()
        {
            const int w = 12, h = 9;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // 위쪽(말풍선에 붙는 쪽)이 넓고 아래로 왼쪽 끝이 뾰족한 꼬리
                    int left = (h - 1 - y) / 2, right = w - 1 - (h - 1 - y);
                    Color c = Color.clear;
                    if (x >= left && x <= right) c = x == left || x == right || x == right - 1 ? UiTokens.Ink : UiTokens.Paper;
                    if (y == h - 1 && x > left + 1 && x < right - 1) c = UiTokens.Paper;   // 말풍선과 이어지는 윗줄은 종이
                    px[y * w + x] = c;
                }

            return Texture(w, h, px, "UiKit bubble tail");
        }

        static Texture2D Button(Color face, Color hi, Color lo, bool down)
        {
            const int size = 12;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Min(x, size - 1 - x), dy = Mathf.Min(y, size - 1 - y);
                    Color c;
                    if (dx + dy < 2) c = Color.clear;
                    else if (dx == 0 || dy == 0 || dx + dy == 2) c = UiTokens.Outline;
                    else if (!down && y == size - 2) c = hi;                    // 위 밝은 줄 (볼록)
                    else if (!down && (y == 1 || y == 2)) c = lo;                // 아래 두 줄 그림자
                    else if (down && y == size - 2) c = lo;                      // 눌림: 위가 어두움
                    else c = Shade(face, 1f + (Noise(Hash(0, y), 5) - 2) * 0.02f);
                    px[y * size + x] = c;
                }

            return Texture(size, size, px, "UiKit button");
        }

        static Texture2D GlowFrame()
        {
            const int size = 12;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Min(x, size - 1 - x), dy = Mathf.Min(y, size - 1 - y);
                    Color c = Color.clear;
                    if (dx + dy >= 2 && (dx == 0 || dy == 0 || dx + dy == 2)) c = UiTokens.BrassLight;
                    else if (dx + dy >= 3 && (dx == 1 || dy == 1)) c = UiTokens.Alpha(UiTokens.Brass, 0.55f);
                    px[y * size + x] = c;
                }

            return Texture(size, size, px, "UiKit glow");
        }

        static Texture2D KeyCapShape()
        {
            const int w = 10, h = 11;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int dx = Mathf.Min(x, w - 1 - x), dy = Mathf.Min(y, h - 1 - y);
                    Color c;
                    if (dx + dy < 1) c = Color.clear;
                    else if (dx == 0 || dy == 0) c = UiTokens.Outline;
                    else if (y <= 2) c = UiTokens.PaperShade;                    // 아래 두께
                    else c = UiTokens.Paper;
                    px[y * w + x] = c;
                }

            return Texture(w, h, px, "UiKit keycap");
        }

        static Texture2D Track()
        {
            const int size = 8;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Min(x, size - 1 - x), dy = Mathf.Min(y, size - 1 - y);
                    px[y * size + x] = dx + dy < 1 ? Color.clear : dx == 0 || dy == 0 ? UiTokens.Outline : y == size - 2 ? Color.black : UiTokens.WoodDark;
                }

            return Texture(size, size, px, "UiKit track");
        }

        // 도장: 흰 원 고리 + 옅은 안쪽 (색은 Image.color로 — 승리 풀색, 패배 붉은 녹)
        static Texture2D SealShape()
        {
            const int size = 32;
            var px = new Color32[size * size];
            float r = size / 2f - 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x - r, y - r).magnitude;
                    Color c = Color.clear;
                    if (d <= r) c = d > r - 2.2f ? Color.white : d > r - 3.4f ? new Color(1f, 1f, 1f, 0.15f) : d > r - 5f ? new Color(1f, 1f, 1f, 0.85f) : new Color(1f, 1f, 1f, 0.12f);
                    if (c.a > 0.5f && Hash(x, y) % 9 == 0) c.a = 0.35f;   // 찍힌 자국처럼 군데군데 옅게
                    px[y * size + x] = c;
                }

            return Texture(size, size, px, "UiKit seal");
        }

        static Texture2D VignetteShape()
        {
            const int w = 64, h = 36;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float ex = Mathf.Min(x + 0.5f, w - x - 0.5f) / (w * 0.5f), ey = Mathf.Min(y + 0.5f, h - y - 0.5f) / (h * 0.5f);
                    float d = Mathf.Min(ex, ey);
                    float a = Mathf.Clamp01(1f - d / 0.32f);
                    px[y * w + x] = new Color(1f, 1f, 1f, a * a);
                }

            var texture = Texture(w, h, px, "UiKit vignette");
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        // ── 아이콘: '#' = 흰 점(Image.color로 칠함), 바깥 한 점 짙은 외곽선은 자동 ──
        static readonly Dictionary<string, string[]> IconShapes = new Dictionary<string, string[]>
        {
            { IconClose, new[] { "##.....##", "###...###", ".###.###.", "..#####..", "...###...", "..#####..", ".###.###.", "###...###", "##.....##" } },
            { IconUp, new[] { "....#....", "...###...", "..#####..", ".###.###.", "###...###", "##.....##" } },
            { IconPlay, new[] { "#........", "###......", "#####....", "#######..", "#########", "#######..", "#####....", "###......", "#........" } },
            { IconPause, new[] { "###..###", "###..###", "###..###", "###..###", "###..###", "###..###", "###..###", "###..###" } },
            { IconSpeed1, new[] { "#.....", "###...", "#####.", "######", "#####.", "###...", "#....." } },
            { IconSpeed2, new[] { "#.....#.....", "###...###...", "#####.#####.", "############", "#####.#####.", "###...###...", "#.....#....." } },
            { IconSpeed4, new[] { "#....#....#....", "###..###..###..", "####.####.####.", "###############", "####.####.####.", "###..###..###..", "#....#....#...." } },
            { IconHero, new[] { "....#....", "...###...", "..#####..", ".#######.", "#########", ".#######.", "..#####..", "...###...", "....#...." } },
            { IconNorth, new[] { "....#....", "...###...", "..#####..", ".#######.", "#########", "...###...", "...###...", "...###..." } },
            { IconCheck, new[] { "........##", ".......###", "......###.", "##...###..", "###.###...", ".#####....", "..###....." } },
            { IconFlag, new[] { "#########", "##########", "#########.", "##........", "##........", "##........", "##........", "##........", "####......" } },
            { IconAuto, new[] { "...###...", "..#...#..", ".#..#..#.", "#..###..#", ".#..#..#.", "..#...#..", "...###..." } },
            { IconIdle, new[] { "#########", "#########" } },
            { IconSpecial, new[] { "....#....", "...###...", "#########", ".#######.", "..#####..", ".###.###.", "##.....##" } },
            { IconMinus, new[] { "#########", "#########", "#########" } },
            { IconPlus, new[] { "...###...", "...###...", "...###...", "#########", "#########", "#########", "...###...", "...###...", "...###..." } },
            { IconRight, new[] { ".....#...", ".....##..", "#########", "##########", "#########", ".....##..", ".....#..." } },
            { IconWarn, new[] { "###", "###", "###", "###", "###", "...", "###", "###" } },
        };

        static Texture2D Icon(string name)
        {
            string[] rows;
            bool flip = false;
            if (name == IconDown) { rows = IconShapes[IconUp]; flip = true; }
            else if (!IconShapes.TryGetValue(name, out rows)) return null;
            int ih = rows.Length, iw = 0;
            foreach (string row in rows) iw = Mathf.Max(iw, row.Length);
            int w = iw + 2, h = ih + 2;
            var solid = new bool[w * h];
            for (int r = 0; r < ih; r++)
                for (int c = 0; c < rows[r].Length; c++)
                    if (rows[r][c] == '#') solid[(flip ? r + 1 : h - 2 - r) * w + c + 1] = true;   // 글 줄은 위에서 아래, 그림은 아래에서 위
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (solid[y * w + x]) { px[y * w + x] = Color.white; continue; }
                    bool edge = false;
                    for (int oy = -1; oy <= 1 && !edge; oy++)
                        for (int ox = -1; ox <= 1 && !edge; ox++)
                        {
                            int nx = x + ox, ny = y + oy;
                            edge = nx >= 0 && ny >= 0 && nx < w && ny < h && solid[ny * w + nx];
                        }

                    px[y * w + x] = edge ? (Color32)UiTokens.Outline : new Color32(0, 0, 0, 0);
                }

            return Texture(w, h, px, "UiKit " + name);
        }

        static Texture2D Texture(int w, int h, Color32[] px, string name)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(px);
            texture.Apply(false, true);
            return texture;
        }

        static Color Shade(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

        // 0 ~ n-1 정수 (uint에서 바로 빼면 음수가 아니라 아주 큰 수가 되어 색이 하얗게 날아갔다 — K-047 사진 r10)
        static int Noise(uint h, int n) => (int)(h % (uint)n);

        static uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ 0x9e3779b9u;
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return h;
            }
        }
    }
}
