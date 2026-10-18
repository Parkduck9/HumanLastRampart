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
        public static readonly Color InkSoft = Hex(0x6e5842);     // 종이 위 보조 글
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
        public static readonly Color Good = Hex(0x7fa35a);        // 남은 체력·승리
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

    // 글꼴 두 벌 (S-06): 제목·이름·큰 숫자 = 세계 글꼴(명조 계열), 본문 = 읽기 글꼴.
    // Codex 글꼴 파일이 오면 Resources/UiKit/Fonts/display·body 에 넣는 것만으로 바뀐다 (UiArt.FontDisplay·FontBody)
    public static class UiFonts
    {
        static Font display, body;

        public static Font Display => display != null ? display : (display = Load(UiArt.FontDisplay,
            new[] { "Batang", "바탕", "BatangChe", "Gungsuh", "궁서", "Noto Serif CJK KR", "AppleMyungjo", "Malgun Gothic" }));

        public static Font Body => body != null ? body : (body = Load(UiArt.FontBody,
            new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }));

        static Font Load(string resource, string[] system)
        {
            var font = Resources.Load<Font>(resource);
            if (font != null) return font;
            // D-033 F1: 설치된 시스템 글꼴을 임시 사용. 글꼴 파일을 복사·배포하지 않는다
            font = Font.CreateDynamicFontFromOSFont(system, 32);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
