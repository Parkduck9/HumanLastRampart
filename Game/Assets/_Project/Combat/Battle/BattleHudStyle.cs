using System.Collections.Generic;
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
        public static readonly Color PanelSoft = Hex(0x26352d, 0.74f);    // 전장을 덜 가리는 창 (상태판). 뒤로 적이 비쳐 글이 묻히지 않게 조금 짙게 (K-002)
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

        // 지도·전투 준비 창 (K-021 시안): 짙은 올리브 창 + 황동 1px 테두리 + 안쪽 어두운 3px 띠, 제목 아래 가는 선
        public static readonly Color MapPanel = Hex(0x22312a, 0.96f);     // 시안의 #26352d→#19281f 기울기 가운데 색
        public static readonly Color MapInset = Hex(0x152019, 1f);
        public static readonly Color MapDivider = Hex(0x777650, 1f);
        public static readonly Color MapButton = Hex(0x314338, 0.98f);
        public static readonly Color MapButtonEdge = Hex(0x827f5d, 1f);
        public static readonly Color MapButtonActive = Hex(0x675733, 0.98f);
        public static readonly Color MapNote = Hex(0x15251d, 0.95f);       // 안내 글 바탕 (왼쪽 황동 막대)
        public static readonly Color PhaseBand = Hex(0x16251e, 0.91f);     // 위쪽 상태 띠
        public static readonly Color PrimaryText = Hex(0x1e231b, 1f);      // 황동 단추 위 글
        public static readonly Color PrimaryEdge = Hex(0xf5d99c, 1f);

        // 창·버튼에 얇은 황동 테두리 — 가장자리 선 4개 (Outline 효과는 반투명 창 뒤로 황동 복사본이 비쳐 창이 누렇게 보여서 쓰지 않는다)
        public static void Border(Graphic graphic, float alpha = 0.85f, float thickness = 2f) =>
            Border(graphic, new Color(Brass.r, Brass.g, Brass.b, alpha), thickness);

        public static void Border(Graphic graphic, Color color, float thickness, string name = "Frame", float inset = 0f)
        {
            if (graphic == null || graphic.transform.Find(name) != null) return;
            var frame = new GameObject(name, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            frame.GetComponent<LayoutElement>().ignoreLayout = true;   // 세로 목록 창 안에서도 줄 하나로 끼지 않게
            frame.SetParent(graphic.transform, false);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(inset, inset);
            frame.offsetMax = new Vector2(-inset, -inset);
            Edge(frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero, color);   // 위
            Edge(frame, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness), color);    // 아래
            Edge(frame, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f), color);    // 왼쪽
            Edge(frame, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero, color);   // 오른쪽
        }

        // 테두리 색만 바꾼다 (고른 단추는 황동 테두리)
        public static void SetBorderColor(Graphic graphic, Color color, string name = "Frame")
        {
            Transform frame = graphic != null ? graphic.transform.Find(name) : null;
            if (frame == null) return;
            foreach (Image edge in frame.GetComponentsInChildren<Image>(true)) edge.color = color;
        }

        // K-021 창 모양: 황동 1px 테두리 + 그 안쪽 어두운 3px 띠
        public static void MapFrame(Image panel)
        {
            panel.color = MapPanel;
            Border(panel, Brass, 1f);
            Border(panel, MapInset, 3f, "Inset", 1f);
        }

        // K-021 단추 모양: 보통 = 녹회색 바탕 + 올리브 테두리, 고름 = 어두운 황동 바탕 + 황동 테두리
        public static void SetMapButton(Image button, bool active)
        {
            if (button == null) return;
            button.color = active ? MapButtonActive : MapButton;
            SetBorderColor(button, active ? Brass : MapButtonEdge);
        }

        // 그림 (Art/UI/Resources/HudArt). 가져오기 설정과 상관없이 Texture2D로 읽어 스프라이트를 만든다 — 도트라 점 필터.
        // border = 9칸 늘이기 경계(왼쪽, 아래, 오른쪽, 위 px). 그림이 없으면 null (부르는 쪽이 예전 모양으로 그린다)
        static readonly Dictionary<string, Sprite> art = new Dictionary<string, Sprite>();

        public static Sprite Art(string path, Vector4 border = default)
        {
            if (art.TryGetValue(path, out Sprite cached))
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

            art[path] = sprite;
            return sprite;
        }

        // 그림 한 장을 원래 크기로 붙인 Image (그림이 없으면 null)
        public static Image ArtImage(Transform parent, string name, string path, Vector2 size)
        {
            Sprite sprite = Art(path);
            if (sprite == null) return null;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            ((RectTransform)go.transform).sizeDelta = size;
            return image;
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
