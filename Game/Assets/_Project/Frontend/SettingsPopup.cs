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
    // 값은 GameSettings(PlayerPrefs)에 바로 저장되고 바로 적용된다. 모양은 K-047 UI 부품(양피지 판·나무 단추) —
    // (기능은 이 파일의 줄 목록 그대로)
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

            var shade = Box(root.transform, "Shade", UiTokens.Night);
            Stretch(shade.rectTransform);

            var panel = Ui.Panel(root.transform, "Settings", UiPanel.Parchment);   // K-047 양피지 판
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(820, 960);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)(UiTokens.S48 + UiTokens.S8), (int)(UiTokens.S48 + UiTokens.S8), (int)UiTokens.S32, (int)UiTokens.S32);
            layout.spacing = UiTokens.S16;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Label(panel.transform, "설정", UiTokens.H1, UiTokens.S48 + UiTokens.S24, UiTokens.Ink);
            Label(panel.transform, "누를 때마다 값이 바뀝니다 · 바로 저장", UiTokens.Small, UiTokens.S32, UiTokens.InkSoft);

            Row(panel.transform, () => $"소리 크기: {Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%",
                () => GameSettings.SetMasterVolume(GameSettings.NextStep(GameSettings.VolumeSteps, GameSettings.MasterVolume)));
            Row(panel.transform, () => $"음악 크기: {Mathf.RoundToInt(GameSettings.MusicVolume * 100f)}%",
                () => GameSettings.SetMusicVolume(GameSettings.NextStep(GameSettings.VolumeSteps, GameSettings.MusicVolume)));
            Row(panel.transform, () => $"효과음·환경음 크기: {Mathf.RoundToInt(GameSettings.SfxVolume * 100f)}%",
                () => GameSettings.SetSfxVolume(GameSettings.NextStep(GameSettings.VolumeSteps, GameSettings.SfxVolume)));
            Row(panel.transform, () => "화면 효과: " + (GameSettings.ScreenEffects ? "켬" : "끔"),
                () => GameSettings.SetScreenEffects(!GameSettings.ScreenEffects));
            Row(panel.transform, () => "미니어처 흐림: " + TiltName(GameSettings.TiltStrength),
                () => GameSettings.SetTiltStrength(GameSettings.NextStep(GameSettings.TiltSteps, GameSettings.TiltStrength)));
            Row(panel.transform, () => "카메라 흔들림: " + (GameSettings.CameraShake ? "켬" : "끔"),
                () => GameSettings.SetCameraShake(!GameSettings.CameraShake));
            Row(panel.transform, () => "움직임 줄이기: " + (GameSettings.ReduceMotion ? "켬" : "끔"),
                () => GameSettings.SetReduceMotion(!GameSettings.ReduceMotion));
            Row(panel.transform, () => "화면: " + (GameSettings.Fullscreen ? "전체 화면" : "창"),
                () => GameSettings.SetFullscreen(!GameSettings.Fullscreen));
            Row(panel.transform, () => "글자 크기: " + FontName(GameSettings.FontScale),
                () => GameSettings.SetFontScale(GameSettings.NextStep(GameSettings.FontSteps, GameSettings.FontScale)));
            Row(panel.transform, () => "기본값으로", GameSettings.ResetToDefaults);
            MakeButton(panel.transform, "닫기", Close, UiButtonKind.Primary);

            GameSettings.Changed += Refresh;
            Refresh();
            EventSystem.current?.SetSelectedGameObject(first != null ? first.gameObject : null);
        }

        public bool IsOpen => root != null;

        static string TiltName(float value) => value <= 0.001f ? "끔" : value < 0.75f ? "약하게" : "보통";
        static string FontName(float value) => value < 1.07f ? "보통" : value < 1.22f ? "크게" : "아주 크게";

        void Row(Transform parent, Func<string> text, Action click)
        {
            Button button = MakeButton(parent, text(), click);
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
            label.fontSize = UiFonts.SnapPixel(size);
            label.text = text;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            obj.GetComponent<LayoutElement>().preferredHeight = height;
            return label;
        }

        // K-047 UI 부품 단추 (나무, 주 행동만 황동). 열쇠 = 'settings:' + 첫 글자 묶음 — 시험은 열쇠로 찾는다
        static Button MakeButton(Transform parent, string text, Action action, UiButtonKind kind = UiButtonKind.Normal)
        {
            Button button = Ui.Button(parent, "settings:" + text, text, action, kind);
            var layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = UiTokens.ButtonHeight;
            return button;
        }
    }
}
