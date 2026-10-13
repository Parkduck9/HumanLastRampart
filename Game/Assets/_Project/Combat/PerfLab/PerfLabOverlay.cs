using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.PerfLab
{
    // F8 토글 성능 표시 (C-014 최소안). 기본은 숨김.
    // 글자는 0.5초마다만 바꾼다. 이때 문자열이 만들어지므로 GC 측정은 표시를 숨긴 상태로 한다(자동 측정은 숨긴 채 진행).
    // 자동 측정 상태·결과는 왼쪽 아래 별도 패널에 보인다 (내용이 바뀔 때만 갱신).
    public sealed class PerfLabOverlay : MonoBehaviour
    {
        const float RefreshInterval = 0.5f;

        Text label;
        GameObject panel;
        Text benchLabel;
        GameObject benchPanel;
        string shownBenchText;
        float elapsed;
        int frames;
        int deathsAtLastRefresh;

        public int EnemyCount { get; set; }
        public int AllyCount { get; set; }
        public int ActiveArrows { get; set; }
        public int DeathsTotal { get; set; }
        public int PoolOverflowTotal { get; set; }
        public string StageName { get; set; } = "";
        public string ResolutionName { get; set; } = "";
        public string CameraName { get; set; } = "";
        public string BenchmarkText { get; set; } = "";

        void Awake()
        {
            var canvasObject = new GameObject("PerfLab Overlay", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            panel = CreatePanel(canvasObject.transform, "Stats", new Vector2(0f, 1f), new Vector2(8f, -8f), new Vector2(440f, 215f), out label);
            panel.SetActive(false);

            benchPanel = CreatePanel(canvasObject.transform, "Benchmark", new Vector2(0f, 0f), new Vector2(8f, 8f), new Vector2(1100f, 230f), out benchLabel);
            benchLabel.fontSize = 15;
            benchPanel.SetActive(false);
        }

        static GameObject CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 offset, Vector2 size, out Text text)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            var image = panelObject.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.65f);
            image.raycastTarget = false;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(panelObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 6f);
            labelRect.offsetMax = new Vector2(-8f, -6f);
            text = labelObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.raycastTarget = false;
            return panelObject;
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

            // 문자열 비교는 참조만 본다: 같은 문자열 객체면 다시 그리지 않는다.
            if (!ReferenceEquals(shownBenchText, BenchmarkText))
            {
                shownBenchText = BenchmarkText;
                bool hasText = !string.IsNullOrEmpty(shownBenchText);
                benchPanel.SetActive(hasText);
                if (hasText)
                {
                    benchLabel.text = shownBenchText;
                }
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
                float deathsPerSecond = (DeathsTotal - deathsAtLastRefresh) / elapsed;
                label.text = $"{StageName}\n{CameraName} (C 전환, A/D 이동)\n{ResolutionName} (R 전환, B 자동 측정)\n적 {EnemyCount:N0} / 아군 {AllyCount:N0}\n화살 {ActiveArrows:N0} / 풀 부족 {PoolOverflowTotal:N0}\n처치 {DeathsTotal:N0} (초당 {deathsPerSecond:F0})\nFPS {fps:F1} / {ms:F2} ms";
            }

            deathsAtLastRefresh = DeathsTotal;
            elapsed = 0f;
            frames = 0;
        }
    }
}
