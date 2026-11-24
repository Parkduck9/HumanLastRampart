using UnityEngine;

namespace HumanLastRampart.UI
{
    // K-047 화면 기준 값 한 곳 (Docs/GDD/UI_NoSlop.md 3장). 색·간격·글자 크기·점 크기는 여기서만 정한다 (S-28)
    public static class UiTokens
    {
        // ── 색 역할 표 (UI_NoSlop 3.3). 이 밖의 색은 쓰지 않는다 (S-10) ──
        public static readonly Color Paper = Hex(0xe8d9b5);       // 종이 바탕: 지도·보고서·쪽지·대사창
        public static readonly Color PaperShade = Hex(0xc9b48a);  // 종이 안쪽 띠·접힌 그림자
        public static readonly Color Ink = Hex(0x3a2a1e);         // 종이 위 글·선
        public static readonly Color InkSoft = Hex(0x463324);     // 종이 위 보조 글 (종이 대비 8.6:1 — Ink에 가깝게. 예전 #6e5842는 4.8:1이라 작은 글이 흐렸다)
        public static readonly Color Wood = Hex(0x3b2a1f);        // 나무 바탕: 명령판·장부·단추
        public static readonly Color WoodLight = Hex(0x5e4430);   // 나무 볼록한 위쪽
        public static readonly Color WoodDark = Hex(0x23170f);    // 나무 아래쪽 그림자
        public static readonly Color Outline = Hex(0x150e09);     // 모든 판·아이콘 가장 바깥 한 점
        public static readonly Color Body = Hex(0xeee4cf);        // 나무·어둠 위 본문
        public static readonly Color BodyDim = Hex(0xc8bba0);     // 나무·어둠 위 보조 글 (따뜻한 쪽)
        public static readonly Color Brass = Hex(0xd5b16b);       // 강조·고름·주 행동
        public static readonly Color BrassLight = Hex(0xf5d99c);  // 황동 반사
        public static readonly Color BrassDark = Hex(0x8a6a32);   // 황동 그늘
        public static readonly Color Danger = Hex(0xb4533c);      // 위험: 위급·패배·되돌릴 수 없음
        public static readonly Color DangerLight = Hex(0xe28a6c); // 어둠 위에서 읽히는 위험 글
        public static readonly Color Good = Hex(0x7fa35a);        // 남은 체력·승리 (막대·도장 — 종이 위 글에는 GoodInk)
        public static readonly Color GoodInk = Hex(0x3d6326);     // 종이 위 좋음 글 (대비 5:1)
        public static readonly Color DangerInk = Hex(0x9a3f2a);   // 종이 위 위험 글 (대비 4.8:1)
        public static readonly Color Fire = Hex(0xff9a3c);        // 불 마법·함정 불씨
        public static readonly Color Blast = Hex(0xffd75a);       // 폭발 마법
        public static readonly Color Enemy = Hex(0xb9b1e6);       // 지도 위 적 무리 글 (차가운 보라)
        public static readonly Color EnemyDeep = Hex(0x4b4370);
        public static readonly Color Night = new Color(0.05f, 0.05f, 0.07f, 0.74f);   // 판 뒤 화면을 누르는 어둠막 (단색은 여기만)

        // ── 간격 (8의 배수 단계만, S-04) ──
        public const float S4 = 4f, S8 = 8f, S16 = 16f, S24 = 24f, S32 = 32f, S48 = 48f;

        // ── 글자 크기 네 단계 (1920×1080 기준, 20 미만 금지 — S-07) ──
        public const int H1 = 48, H2 = 32, Text = 24, Small = 20;

        // ── 점 크기: UI 그림의 한 점 = 화면 3px (S-30). 테두리·아이콘은 이 정수배 ──
        public const float Dot = 3f;

        // ── 움직임 길이 (S-24): 판 열기·닫기, 화면 전환 ──
        public const float Open = 0.18f, Close = 0.12f;

        public const float ButtonHeight = 60f;   // 1920×1080 기준 (손가락·마우스 모두 44 이상)

        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);
    }

    // 글꼴 (W16, 사용자 확정 · 전부 SIL OFL 1.1, 라이선스는 StreamingAssets/Licenses 에 같이 배포):
    //  본문·숫자·단추 = 갈무리11 (도트, 11의 배수 크기에서 또렷), 대사 = 고운바탕, 제목 = 함렛 Black.
    // 전부 동적 글꼴이라 한글 전체가 나오고 큰 글자판(아틀라스)을 저장소에 두지 않는다.
    public static class UiFonts
    {
        const string GalmuriRegular = "Fonts/Galmuri/Galmuri11";
        const string GalmuriBold = "Fonts/Galmuri/Galmuri11-Bold";
        const string GowunRegular = "Fonts/GowunBatang/GowunBatang-Regular";
        const string HahmletBlack = "Fonts/Hahmlet/Hahmlet-Black";

        // 갈무리 원래 픽셀 크기 (이 배수로 그리면 점이 깨끗하다)
        public const int GalmuriPixel = 11;

        static Font display, body, bodyBold, story;

        // 제목·이름·큰 숫자 (함렛 Black)
        public static Font Display => display != null ? display : (display = Load(HahmletBlack, false));
        // 본문·숫자 (갈무리11 Regular)
        public static Font Body => body != null ? body : (body = Load(GalmuriRegular, true));
        // 굵은 본문 (갈무리11 Bold 파일 — 가짜 굵기를 쓰지 않는다. 이 글꼴은 FontStyle.Normal 로 쓴다)
        public static Font BodyBold => bodyBold != null ? bodyBold : (bodyBold = Load(GalmuriBold, true));
        // 이야기 대사 (고운바탕)
        public static Font Story => story != null ? story : (story = Load(GowunRegular, false));
        // 굵은 고운바탕 파일은 용량 때문에 뺐다 (가짜 굵기도 쓰지 않는다) — 보통 굵기로 대신
        public static Font StoryBold => Story;

        // 갈무리용 크기: 11의 가장 가까운 배수 (20→22, 24→22, 32→33, 48→44). 다른 글꼴은 그대로.
        // 본문(Text 24)과 보조(Small 20)는 둘 다 22가 되므로 위계는 굵기로 나눈다 (Ui.Label: Text 이상은 Bold 파일). 읽기 최소는 22 (직접 16 미만을 부른 곳만 11).
        // 한계: 캔버스 배율이 정수(1080p·2160p)일 때만 점이 정확히 맞는다 — CanvasScaler 배율이 소수면 조금 부드러워진다
        public static int SnapPixel(int size) => size < 16 ? GalmuriPixel : Mathf.Max(2 * GalmuriPixel, Mathf.RoundToInt(size / (float)GalmuriPixel) * GalmuriPixel);

        // 굵게: 갈무리는 Bold 파일(가짜 굵기 X), 함렛·고운바탕은 굵기 없음(이미 Black / 보통)
        public static void Bold(UnityEngine.UI.Text text)
        {
            text.fontStyle = FontStyle.Normal;
            if (text.font == Display || text.font == Story) return;
            text.font = BodyBold;
            text.fontSize = SnapPixel(text.fontSize);
        }

        static Font Load(string resource, bool pixel)
        {
            var font = Resources.Load<Font>(resource);
            if (font == null)
            {
                // 글꼴 파일이 없으면(옮기다 빠진 경우) 시스템 글꼴로 대신해 화면이 비지 않게 한다
                font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
                return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (pixel)
            {
                // 도트 글꼴은 글자판을 점 단위로 뽑아야 흐려지지 않는다
                Font own = font;
                void Point(Font f)
                {
                    if (f == own && own.material != null && own.material.mainTexture != null)
                        own.material.mainTexture.filterMode = FilterMode.Point;
                }
                Font.textureRebuilt += Point;
                Point(font);
            }
            return font;
        }
    }
}
