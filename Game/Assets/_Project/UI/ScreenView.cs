using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HumanLastRampart.UI
{
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

        public ScreenView(Transform owner, string title, string description)
        {
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

            var background = Image(root.transform, "Background", new Color(0.055f, 0.065f, 0.08f));
            Stretch(background.rectTransform);
            panel = MakePanel(root.transform, "Content");
            Label(panel, title, 42, 80, new Color(0.94f, 0.83f, 0.60f));
            Label(panel, description, 27, 150, new Color(0.82f, 0.85f, 0.88f));
        }

        public Button AddButton(string text, UnityAction action)
        {
            var button = MakeButton(panel, text, action);
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
            var shade = Image(root.transform, "Difficulty Popup", new Color(0, 0, 0, 0.82f));
            Stretch(shade.rectTransform);
            popup = shade.gameObject;
            var content = MakePanel(popup.transform, "Difficulty");
            Label(content, "난이도 선택", 42, 80, new Color(0.94f, 0.83f, 0.60f));
            Label(content, "쉬움: 적 규모 절반 · 전투 지도에서 시간 정지\n현실: 전투 지도에서도 시간 진행", 27, 150,
                new Color(0.82f, 0.85f, 0.88f));
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

        static RectTransform MakePanel(Transform parent, string name)
        {
            var image = Image(parent, name, new Color(0.10f, 0.12f, 0.15f));
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(960, 700);
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
            var image = Image(parent, text, new Color(0.20f, 0.27f, 0.32f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.85f, 0.90f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.65f, 0.70f, 0.80f);
            button.colors = colors;
            image.gameObject.AddComponent<LayoutElement>().preferredHeight = 72;
            var label = Label(image.transform, text, 30, 72, Color.white);
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
