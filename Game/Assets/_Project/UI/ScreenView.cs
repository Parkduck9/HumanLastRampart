using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HumanLastRampart.UI
{
    // 공통 화면 스타일 (D-066, Codex 공통 UI v1). 전투 HudStyle과 같은 색을 쓴다.
    // 원본: Docs/References/codex_ui_direction_v1/PROPOSAL.md
    public static class UiStyle
    {
        public static readonly Color Background = Hex(0x101613, 1f);      // 화면 바탕
        public static readonly Color Shade = Hex(0x101613, 0.94f);        // 팝업 뒤 가림막
        public static readonly Color Panel = Hex(0x26352d, 0.86f);        // 창
        // 테두리(Outline)를 붙이는 창은 불투명하게: 반투명이면 뒤에 그려진 테두리 복사본이 비쳐 보인다
        public static readonly Color PanelSolid = Hex(0x23302a, 1f);     // Panel을 Background 위에 겹친 색
        public static readonly Color PanelDeep = Hex(0x18211c, 1f);       // 창 안쪽 구역 (지도 바탕 등)
        public static readonly Color Brass = Hex(0xd5b16b, 1f);           // 강조·테두리·제목
        public static readonly Color Body = Hex(0xeee4cf, 1f);            // 본문
        public static readonly Color TextDim = Hex(0xb9c2b0, 1f);         // 보조 글
        public static readonly Color Danger = Hex(0xc37e70, 1f);          // 위험
        public static readonly Color ButtonNormal = Hex(0x33473b, 1f);          // 보통 버튼
        public static readonly Color ButtonHover = Hex(0x46604f, 1f);
        public static readonly Color Primary = Hex(0x8a7440, 1f);         // 주된 다음 행동·선택
        public static readonly Color PrimaryHover = Hex(0xa38a50, 1f);
        public static readonly Color Disabled = Hex(0x2a2f2b, 1f);        // 비활성
        public const float ButtonHeight = 60f;                            // 1920x1080 기준 최소 44~48 이상

        // 얇은 황동 테두리 — 가장자리 선 4개 (Outline 효과는 창 뒤로 황동 복사본이 비쳐 색이 바뀌어 쓰지 않는다)
        public static void Border(Graphic graphic, float alpha = 0.85f, float thickness = 2f)
        {
            if (graphic == null || graphic.transform.Find("Frame") != null) return;
            var frame = new GameObject("Frame", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            frame.GetComponent<LayoutElement>().ignoreLayout = true;   // 세로 목록 창 안에서도 줄 하나로 끼지 않게
            frame.SetParent(graphic.transform, false);
            frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one; frame.offsetMin = frame.offsetMax = Vector2.zero;
            var color = new Color(Brass.r, Brass.g, Brass.b, alpha);
            Edge(frame, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -thickness), Vector2.zero, color);
            Edge(frame, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, thickness), color);
            Edge(frame, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(thickness, 0), color);
            Edge(frame, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-thickness, 0), Vector2.zero, color);
        }

        static void Edge(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var line = new GameObject("Edge", typeof(RectTransform), typeof(Image));
            var rect = line.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            var image = line.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        // 버튼 색: 바탕 그림에 색을 직접 넣고 상태는 밝기 배율로 구분한다 (보통/호버·포커스/눌림/비활성)
        // 색을 ColorBlock에만 넣으면 이미 켜진 버튼에는 처음 색이 적용되지 않아 하얗게 보인다
        public static void StyleButton(Button button, bool primary = false)
        {
            if (button == null) return;
            var graphic = button.targetGraphic;
            if (graphic != null) graphic.color = primary ? Primary : ButtonNormal;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            Border(graphic, primary ? 1f : 0.55f);
        }

        public static void SetPrimary(Button button) { StyleButton(button, true); }

        // 그림 색을 직접 쓰는 버튼(격자 칸 등): 상태는 밝기만 바꾼다
        public static void StyleTintButton(Button button)
        {
            if (button == null) return;
            var colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.85f);
            button.colors = colors;
        }

        static Color Hex(int rgb, float alpha) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, alpha);
    }

    // Phase 2용 공용 버튼/팝업. 화면 씬에만 속하며 EventSystem은 Boot가 제공한다.
    public sealed class ScreenView : IDisposable
    {
        static Font temporaryFont;
        readonly GameObject root;
        readonly CanvasGroup input;
        readonly RectTransform panel;
        GameObject popup;
        Button firstButton;
        bool disposed;
        readonly bool storyPresentation;

        public ScreenView(Transform owner, string title, string description)
            : this(owner, title, description, false, null, null) { }

        // 스토리 전용 배치. 기존 3인자 메인·정비·결과 화면은 중앙 패널을 그대로 쓴다.
        public ScreenView(Transform owner, string title, string description, Texture2D character, string speaker)
            : this(owner, title, description, true, character, speaker) { }

        ScreenView(Transform owner, string title, string description, bool story, Texture2D character, string speaker)
        {
            storyPresentation = story;
            root = new GameObject("Screen UI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(owner, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            input = root.GetComponent<CanvasGroup>();
            SetInput(false);

            var background = Image(root.transform, "Background", UiStyle.Background);
            Stretch(background.rectTransform);
            if (storyPresentation)
                panel = MakeStory(root.transform, title, description, character, speaker);
            else
            {
                panel = MakePanel(root.transform, "Content");
                Label(panel, title, 46, 80, UiStyle.Brass);
                Divider(panel);
                // 설명 글 크기 27은 정비 요약 갱신(PreparationPanel.RefreshSummary)이 찾는 기준이라 유지한다
                Label(panel, description, 27, 150, UiStyle.Body);
            }
        }

        // 화면의 주된 다음 행동 하나를 황동색으로 표시
        public void MarkPrimary(Button button) { UiStyle.SetPrimary(button); }

        public Button AddButton(string text, UnityAction action)
        {
            var button = MakeButton(panel, text, action);
            if (storyPresentation)
            {
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
                rect.sizeDelta = new Vector2(220, 64);
                rect.anchoredPosition = new Vector2(-32, 24);
            }
            if (firstButton == null) firstButton = button;
            return button;
        }

        public void SetInput(bool active)
        {
            input.interactable = active;
            input.blocksRaycasts = active;
        }

        public void FocusFirst() { Focus(firstButton); }

        public void ShowDifficulty(UnityAction easy, UnityAction realistic)
        {
            if (popup != null || disposed) return;
            panel.GetComponent<CanvasGroup>().interactable = false;
            var shade = Image(root.transform, "Difficulty Popup", UiStyle.Shade);
            Stretch(shade.rectTransform);
            popup = shade.gameObject;
            var content = MakePanel(popup.transform, "Difficulty");
            Label(content, "난이도 선택", 46, 80, UiStyle.Brass);
            Divider(content);
            Label(content, "쉬움: 적 규모 절반 · 전투 지도에서 시간 정지\n현실: 전투 지도에서도 시간 진행", 27, 150,
                UiStyle.Body);
            var easyButton = MakeButton(content, "쉬움", easy);
            MakeButton(content, "현실", realistic);
            MakeButton(content, "돌아가기", ClosePopup);
            Focus(easyButton);
        }

        void ClosePopup()
        {
            if (popup == null || disposed || !input.interactable) return;
            ClearSelection(popup.transform);
            foreach (var button in popup.GetComponentsInChildren<Button>())
                button.onClick.RemoveAllListeners();
            popup.SetActive(false);
            UnityEngine.Object.Destroy(popup);
            popup = null;
            panel.GetComponent<CanvasGroup>().interactable = true;
            FocusFirst();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SetInput(false);
            ClearSelection(root.transform);
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                button.onClick.RemoveAllListeners();
            root.SetActive(false);
            UnityEngine.Object.Destroy(root);
        }

        // 승인된 공통 시안의 배경 공간·오른쪽 인물·하단 대사창. 새 배경 아트 대신 단순 색면을 쓴다.
        static RectTransform MakeStory(Transform parent, string title, string description, Texture2D character, string speaker)
        {
            var sky = Image(parent, "Story Backdrop", new Color(0.145f, 0.212f, 0.180f));
            Stretch(sky.rectTransform);
            sky.raycastTarget = false;
            StoryBand(parent, "Distant Landscape", new Vector2(0, 0.45f), new Vector2(1, 0.66f),
                new Color(0.25f, 0.29f, 0.22f));
            StoryBand(parent, "Wall Silhouette", new Vector2(0, 0.33f), new Vector2(1, 0.49f),
                new Color(0.31f, 0.34f, 0.28f));
            StoryBand(parent, "Walkway Silhouette", new Vector2(0, 0), new Vector2(1, 0.33f),
                UiStyle.PanelDeep);

            var heading = Label(parent, title, 34, 60, UiStyle.Brass);
            StoryRect(heading.rectTransform, new Vector2(0.055f, 0.87f), new Vector2(0.62f, 0.95f));
            heading.alignment = TextAnchor.MiddleLeft;
            if (character != null)
            {
                var holder = new GameObject("Story Character", typeof(RectTransform)).GetComponent<RectTransform>();
                holder.SetParent(parent, false);
                StoryRect(holder, new Vector2(0.60f, 0.335f), new Vector2(0.88f, 0.90f));
                var art = new GameObject("Wizard Front", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(RawImage), typeof(AspectRatioFitter));
                art.transform.SetParent(holder, false);
                var image = art.GetComponent<RawImage>();
                image.texture = character;
                image.color = Color.white;
                image.raycastTarget = false;
                var fit = art.GetComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = (float)character.width / Mathf.Max(1, character.height);
            }

            var dialogue = Image(parent, "Story Dialogue", UiStyle.PanelSolid);
            var rect = dialogue.rectTransform;
            StoryRect(rect, new Vector2(0.055f, 0.055f), new Vector2(0.945f, 0.335f));
            dialogue.gameObject.AddComponent<CanvasGroup>();
            UiStyle.Border(dialogue, 1f);

            var name = Label(rect, speaker ?? "", 28, 48, UiStyle.Brass);
            name.gameObject.name = "Story Speaker";
            StoryRect(name.rectTransform, new Vector2(0, 0.75f), new Vector2(1, 1));
            name.rectTransform.offsetMin = new Vector2(32, 0);
            name.rectTransform.offsetMax = new Vector2(-32, -12);
            name.alignment = TextAnchor.MiddleLeft;

            var body = Label(rect, description, 30, 120, UiStyle.Body);
            body.gameObject.name = "Story Line";
            StoryRect(body.rectTransform, new Vector2(0, 0.32f), new Vector2(1, 0.76f));
            body.rectTransform.offsetMin = new Vector2(32, 0);
            body.rectTransform.offsetMax = new Vector2(-32, 0);
            body.alignment = TextAnchor.UpperLeft;
            return rect;
        }

        static void StoryBand(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var image = Image(parent, name, color);
            StoryRect(image.rectTransform, min, max);
            image.raycastTarget = false;
        }

        static void StoryRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static RectTransform MakePanel(Transform parent, string name)
        {
            var image = Image(parent, name, UiStyle.PanelSolid);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(960, 740);
            UiStyle.Border(image);
            image.gameObject.AddComponent<CanvasGroup>();
            var layout = image.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(56, 56, 40, 40);
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        // 제목 아래 얇은 황동 선
        static void Divider(Transform parent)
        {
            var line = Image(parent, "Divider", new Color(UiStyle.Brass.r, UiStyle.Brass.g, UiStyle.Brass.b, 0.6f));
            line.raycastTarget = false;
            line.gameObject.AddComponent<LayoutElement>().preferredHeight = 2;
        }

        static Image Image(Transform parent, string name, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = color;
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static Text Label(Transform parent, string text, int size, float height, Color color)
        {
            var obj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            var label = obj.GetComponent<Text>();
            label.font = GetFont();
            label.fontSize = size;
            label.text = text;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.supportRichText = false;
            label.raycastTarget = false;
            obj.GetComponent<LayoutElement>().preferredHeight = height;
            return label;
        }

        static Button MakeButton(Transform parent, string text, UnityAction action)
        {
            var image = Image(parent, text, Color.white);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            UiStyle.StyleButton(button);
            image.gameObject.AddComponent<LayoutElement>().preferredHeight = UiStyle.ButtonHeight;
            var label = Label(image.transform, text, 28, UiStyle.ButtonHeight, UiStyle.Body);
            Stretch(label.rectTransform);
            button.onClick.AddListener(action);
            return button;
        }

        static Font GetFont()
        {
            if (temporaryFont != null) return temporaryFont;
            // D-033 F1: 설치된 시스템 글꼴을 임시 사용. 글꼴 파일을 복사/배포하지 않는다.
            temporaryFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
            if (temporaryFont == null)
                temporaryFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return temporaryFont;
        }

        static void Focus(Button button)
        {
            if (button != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        static void ClearSelection(Transform owner)
        {
            var system = EventSystem.current;
            if (system != null && system.currentSelectedGameObject != null &&
                system.currentSelectedGameObject.transform.IsChildOf(owner))
                system.SetSelectedGameObject(null);
        }
    }
}
