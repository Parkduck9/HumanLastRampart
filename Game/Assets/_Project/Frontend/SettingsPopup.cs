using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Settings;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HumanLastRampart.Frontend
{
    // 메인 설정 창 (K-062, P3-X02): 줄마다 버튼 하나 — 누를 때마다 다음 값으로 돌아간다(마우스·키보드 모두).
    // 값은 GameSettings(PlayerPrefs)에 바로 저장되고 바로 적용된다. 모양은 지금 공용 색(UiStyle)만 쓴 임시 —
    // K-047 UI 부품(UI/Kit)이 들어오면 그 창·버튼으로 바꾼다 (기능은 이 파일의 줄 목록 그대로)
    public sealed class SettingsPopup
    {
        readonly GameObject root;
        readonly List<(Text label, Func<string> text)> rows = new List<(Text, Func<string>)>();
        readonly Action onClose;
        Button first;

        public static SettingsPopup Show(Transform owner, Action onClose) => new SettingsPopup(owner, onClose);

        SettingsPopup(Transform owner, Action onClose)
        {
            this.onClose = onClose;
            GameSettings.EnsureLoaded();
            root = new GameObject("Settings Popup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(owner, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;   // 메인 화면(10) 위
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var shade = Box(root.transform, "Shade", UiStyle.Shade);
            Stretch(shade.rectTransform);

            var panel = Box(root.transform, "Settings", UiStyle.PanelSolid);
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(820, 860);
            UiStyle.Border(panel);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(56, 56, 36, 36);
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Label(panel.transform, "설정", 44, 72, UiStyle.Brass);
            Label(panel.transform, "누를 때마다 값이 바뀝니다 · 바로 저장", 24, 36, UiStyle.TextDim);

            Row(panel.transform, () => $"소리 크기: {Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%  (소리는 아직 없음)",
                () => GameSettings.SetMasterVolume(GameSettings.NextStep(GameSettings.VolumeSteps, GameSettings.MasterVolume)));
            Row(panel.transform, () => "화면 효과: " + (GameSettings.ScreenEffects ? "켬" : "끔"),
                () => GameSettings.SetScreenEffects(!GameSettings.ScreenEffects));
            Row(panel.transform, () => "미니어처 흐림: " + TiltName(GameSettings.TiltStrength),
                () => GameSettings.SetTiltStrength(GameSettings.NextStep(GameSettings.TiltSteps, GameSettings.TiltStrength)));
            Row(panel.transform, () => "카메라 흔들림: " + (GameSettings.CameraShake ? "켬" : "끔"),
                () => GameSettings.SetCameraShake(!GameSettings.CameraShake));
            Row(panel.transform, () => "화면: " + (GameSettings.Fullscreen ? "전체 화면" : "창"),
                () => GameSettings.SetFullscreen(!GameSettings.Fullscreen));
            Row(panel.transform, () => "글자 크기: " + FontName(GameSettings.FontScale),
                () => GameSettings.SetFontScale(GameSettings.NextStep(GameSettings.FontSteps, GameSettings.FontScale)));
            Row(panel.transform, () => "기본값으로", GameSettings.ResetToDefaults);
            var close = MakeButton(panel.transform, "닫기", Close);
            UiStyle.SetPrimary(close);

            GameSettings.Changed += Refresh;
            Refresh();
            EventSystem.current?.SetSelectedGameObject(first != null ? first.gameObject : null);
        }

        public bool IsOpen => root != null;

        static string TiltName(float value) => value <= 0.001f ? "끔" : value < 0.75f ? "약하게" : "보통";
        static string FontName(float value) => value < 1.07f ? "보통" : value < 1.22f ? "크게" : "아주 크게";

        void Row(Transform parent, Func<string> text, Action click)
        {
            Button button = MakeButton(parent, text(), () => click());
            if (first == null) first = button;
            rows.Add((button.GetComponentInChildren<Text>(), text));
        }

        void Refresh()
        {
            foreach (var (label, text) in rows) if (label != null) label.text = text();
        }

        public void Close()
        {
            if (root == null) return;
            GameSettings.Changed -= Refresh;
            EventSystem.current?.SetSelectedGameObject(null);
            foreach (Button button in root.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            UnityEngine.Object.Destroy(root);
            onClose?.Invoke();
        }

        static Image Box(Transform parent, string name, Color color)
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
            label.font = ScreenView.UiFont;
            label.fontSize = size;
            label.text = text;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            obj.GetComponent<LayoutElement>().preferredHeight = height;
            return label;
        }

        static Button MakeButton(Transform parent, string text, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject("Button " + text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<LayoutElement>().preferredHeight = 62;
            var button = obj.GetComponent<Button>();
            button.targetGraphic = obj.GetComponent<Image>();
            UiStyle.StyleButton(button);
            button.onClick.AddListener(action);
            var label = Label(obj.transform, text, 27, 62, UiStyle.Body);
            Stretch(label.rectTransform);
            return button;
        }
    }
}
