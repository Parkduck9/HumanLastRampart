using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.PerfLab
{
    // F8 토글 성능 표시 (C-014 최소안). 기본은 숨김.
    // 글자는 0.5초마다만 바꾼다. 이때 문자열이 만들어지므로 GC 측정은 표시를 숨긴 상태로도 한다.
    public sealed class PerfLabOverlay : MonoBehaviour
    {
        const float RefreshInterval = 0.5f;

        Text label;
        GameObject panel;
        float elapsed;
        int frames;

        public int EnemyCount { get; set; }
        public int AllyCount { get; set; }
        public string StageName { get; set; } = "";

        void Awake()
        {
            var canvasObject = new GameObject("PerfLab Overlay", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(8f, -8f);
            panelRect.sizeDelta = new Vector2(360f, 110f);
            var image = panel.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.6f);
            image.raycastTarget = false;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(panel.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 6f);
            labelRect.offsetMax = new Vector2(-8f, -6f);
            label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.color = Color.white;
            label.raycastTarget = false;

            panel.SetActive(false);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
            {
                panel.SetActive(!panel.activeSelf);
                elapsed = 0f;
                frames = 0;
            }

            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < RefreshInterval)
            {
                return;
            }

            if (panel.activeSelf)
            {
                float fps = frames / elapsed;
                float ms = elapsed * 1000f / frames;
                label.text = $"{StageName}\n적 {EnemyCount:N0} / 아군 {AllyCount:N0}\nFPS {fps:F1} / {ms:F2} ms";
            }

            elapsed = 0f;
            frames = 0;
        }
    }
}
