using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 전투 화면 글자판·버튼 색 (D-066: Codex 공통 화면 스타일 v1 — 어두운 올리브 창 + 황동 테두리 + 종이색 글)
    // 원본: Docs/References/codex_ui_direction_v1/PROPOSAL.md. 글꼴 파일·크기 기준은 스타일 확정 뒤 Codex와 따로 맞춘다
    public static class HudStyle
    {
        public static readonly Color Background = Hex(0x101613, 0.82f);   // 큰 판(끝 화면 등)
        public static readonly Color Panel = Hex(0x26352d, 0.86f);        // 창
        public static readonly Color PanelSoft = Hex(0x26352d, 0.62f);    // 전장을 덜 가리는 창 (상태판)
        public static readonly Color Brass = Hex(0xd5b16b, 1f);           // 강조·테두리
        public static readonly Color Text = Hex(0xeee4cf, 1f);            // 본문
        public static readonly Color TextDim = Hex(0xb9c2b0, 1f);         // 보조 글
        public static readonly Color Danger = Hex(0xc37e70, 1f);          // 위험
        public static readonly Color Button = Hex(0x33473b, 0.95f);       // 보통 버튼
        public static readonly Color ButtonActive = Hex(0x8a7440, 0.97f); // 고른 버튼 (황동을 어둡게)
        public static readonly Color ButtonDisabled = Hex(0x2a2f2b, 0.9f);
        public static readonly Color ButtonDev = Hex(0x4a3430, 0.85f);    // 개발용 [시험] 버튼
        public static readonly Color Magic = Hex(0x4a3a5c, 0.95f);        // 마법 버튼
        public static readonly Color MagicActive = Hex(0x7a5a96, 0.97f);

        // 창·버튼에 얇은 황동 테두리 — 가장자리 선 4개 (Outline 효과는 반투명 창 뒤로 황동 복사본이 비쳐 창이 누렇게 보여서 쓰지 않는다)
        public static void Border(Graphic graphic, float alpha = 0.85f, float thickness = 2f)
        {
            if (graphic == null || graphic.transform.Find("Frame") != null) return;
            var frame = new GameObject("Frame", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            frame.GetComponent<LayoutElement>().ignoreLayout = true;   // 세로 목록 창 안에서도 줄 하나로 끼지 않게
            frame.SetParent(graphic.transform, false);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = frame.offsetMax = Vector2.zero;
            var color = new Color(Brass.r, Brass.g, Brass.b, alpha);
            Edge(frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero, color);   // 위
            Edge(frame, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness), color);    // 아래
            Edge(frame, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f), color);    // 왼쪽
            Edge(frame, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero, color);   // 오른쪽
        }

        static void Edge(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var line = new GameObject("Edge", typeof(RectTransform), typeof(Image));
            var rect = line.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var image = line.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static Color Hex(int rgb, float alpha) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, alpha);
    }
}
