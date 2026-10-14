using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HumanLastRampart.Meta
{
    // Result 전용 UI. ScreenView가 소유한 Canvas 아래라 화면 종료 때 함께 정리된다.
    internal static class ResultDetailPopup
    {
        public static void Show(Button source, string title, string content)
        {
            if (source == null || !source.IsInteractable()) return;
            var panel = source.transform.parent;
            var group = panel.GetComponent<CanvasGroup>();
            var shade = Make(panel.parent, "Result Details", typeof(Image));
            Stretch(shade);
            shade.GetComponent<Image>().color = new Color(0, 0, 0, 0.94f);
            group.interactable = false;
            var font = source.GetComponentInChildren<Text>().font;
            var heading = Label(shade, "Detail Title", title, font, 36);
            heading.anchorMin = new Vector2(0.18f, 0.82f);
            heading.anchorMax = new Vector2(0.82f, 0.93f);
            heading.offsetMin = heading.offsetMax = Vector2.zero;
            var area = Make(shade, "Detail Scroll", typeof(ScrollRect));
            area.anchorMin = new Vector2(0.18f, 0.23f);
            area.anchorMax = new Vector2(0.82f, 0.79f);
            area.offsetMin = area.offsetMax = Vector2.zero;
            var viewport = Make(area, "Viewport", typeof(Image), typeof(RectMask2D));
            Stretch(viewport);
            viewport.GetComponent<Image>().color = new Color(0.1f, 0.13f, 0.17f);
            var text = Label(viewport, "Detail Content", content, font, 26);
            text.anchorMin = new Vector2(0, 1);
            text.anchorMax = new Vector2(1, 1);
            text.pivot = new Vector2(0.5f, 1);
            text.offsetMin = new Vector2(22, 0);
            text.offsetMax = new Vector2(-22, 0);
            text.GetComponent<Text>().alignment = TextAnchor.UpperLeft;
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = area.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = text;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 38;
            var closeRect = Make(shade, "닫기", typeof(Image), typeof(Button));
            closeRect.anchorMin = new Vector2(0.35f, 0.1f);
            closeRect.anchorMax = new Vector2(0.65f, 0.18f);
            closeRect.offsetMin = closeRect.offsetMax = Vector2.zero;
            closeRect.GetComponent<Image>().color = new Color(0.2f, 0.3f, 0.4f);
            var close = closeRect.GetComponent<Button>();
            close.targetGraphic = closeRect.GetComponent<Image>();
            close.navigation = new Navigation { mode = Navigation.Mode.None };
            Stretch(Label(closeRect, "Close Label", "닫기 · 긴 목록은 휠로 이동", font, 24));
            close.onClick.AddListener(() =>
            {
                group.interactable = true;
                shade.gameObject.SetActive(false);
                Object.Destroy(shade.gameObject);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(source.gameObject);
            });
            LayoutRebuilder.ForceRebuildLayoutImmediate(text);
            scroll.verticalNormalizedPosition = 1;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(close.gameObject);
        }

        static RectTransform Make(Transform parent, string name, params System.Type[] components)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            foreach (var component in components) obj.AddComponent(component);
            return obj.GetComponent<RectTransform>();
        }
        static RectTransform Label(Transform parent, string name, string value, Font font, int size)
        {
            var rect = Make(parent, name, typeof(Text));
            var label = rect.GetComponent<Text>();
            label.text = value;
            label.font = font;
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.supportRichText = false;
            label.raycastTarget = false;
            return rect;
        }
        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
