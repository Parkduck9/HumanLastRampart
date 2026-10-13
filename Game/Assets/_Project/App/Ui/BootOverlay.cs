using System;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.App.Ui
{
    // Boot 씬의 공용 화면 (C-003 3-1·3-2, D-017 E2)
    // - 전환용 검은 화면: 카메라 없이 보이는 Overlay Canvas
    // - 데이터 오류 안내: '메인으로 돌아가기' 버튼만 쓸 수 있다
    public sealed class BootOverlay : MonoBehaviour
    {
        Image fade;
        GameObject errorPanel;
        Text errorText;
        Action onBackToMain;

        public bool ErrorShown => errorPanel != null && errorPanel.activeSelf;

        public static BootOverlay Create(Transform parent)
        {
            var root = new GameObject("Boot Overlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var overlay = root.AddComponent<BootOverlay>();
            overlay.Build(root.transform);
            return overlay;
        }

        void Build(Transform root)
        {
            fade = NewImage(root, "Fade", Color.black);
            Stretch(fade.rectTransform);
            fade.raycastTarget = true;   // 전환 중 클릭 막기
            fade.gameObject.SetActive(false);

            Image panelBack = NewImage(root, "Error", new Color(0f, 0f, 0f, 0.85f));
            Stretch(panelBack.rectTransform);
            panelBack.raycastTarget = true;   // 아래 화면 클릭 막기
            errorPanel = panelBack.gameObject;

            errorText = NewText(panelBack.transform, "Message", 34);
            errorText.rectTransform.anchorMin = new Vector2(0.15f, 0.45f);
            errorText.rectTransform.anchorMax = new Vector2(0.85f, 0.75f);
            errorText.rectTransform.offsetMin = Vector2.zero;
            errorText.rectTransform.offsetMax = Vector2.zero;

            Button button = NewButton(panelBack.transform, "메인으로 돌아가기", () => onBackToMain?.Invoke());
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.3f);
            rect.sizeDelta = new Vector2(420f, 90f);

            errorPanel.SetActive(false);
        }

        public void SetFade(bool visible) => fade.gameObject.SetActive(visible);

        public void ShowError(string message, Action backToMain)
        {
            if (ErrorShown)
            {
                return;   // 같은 오류가 반복돼도 안내는 하나만 (D-017 E2)
            }

            onBackToMain = backToMain;
            errorText.text = "전투 정보를 처리하는 중 오류가 발생했습니다.\n진행을 멈췄습니다.\n메인으로 돌아가면 이번 판은 종료되며 다시 시작해야 합니다.\n\n<size=22>(" + message + ")</size>";
            errorPanel.SetActive(true);
        }

        public void HideError() => errorPanel.SetActive(false);

        public static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text NewText(Transform parent, string name, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");   // 임시 글꼴 (D-033 F1)
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        public static Button NewButton(Transform parent, string label, Action onClick)
        {
            Image back = NewImage(parent, "Button " + label, new Color(0.25f, 0.3f, 0.4f, 1f));
            var button = back.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            Text text = NewText(back.transform, "Label", 28);
            text.text = label;
            Stretch(text.rectTransform);
            return button;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}