using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 전투 HUD (임시 모양 — 정식 UI는 전투 화면 설계 CY1-C3 이후). 전투 HUD는 Combat 안에 둔다 (C-002).
    public sealed class BattleHud : IDisposable
    {
        static readonly string[] SideNames = { "북", "동", "남", "서" };

        readonly GameObject root;
        readonly Font font;
        readonly bool ownsFont;
        readonly Text header;
        readonly Text walls;
        readonly Text help;
        readonly GameObject endPanel;
        readonly Text endText;
        readonly StringBuilder text = new StringBuilder(512);
        float nextRefresh;

        public BattleHud(Transform parent, string battleLine, Action onFinish, Action<int> onSpeed, Action[] devActions, string[] devLabels)
        {
            // 임시 한글 글꼴 (D-033 F1): 시스템 맑은 고딕, 없으면 기본 글꼴
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
            ownsFont = font != null;
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            root = new GameObject("Battle HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Image panel = NewImage(root.transform, "Status Panel", new Color(0f, 0f, 0f, 0.55f));
            Anchor(panel.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(760f, 300f));

            header = NewText(panel.transform, "Header", 24, TextAnchor.UpperLeft);
            Anchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(730f, 90f));
            header.text = battleLine;

            walls = NewText(panel.transform, "Walls", 22, TextAnchor.UpperLeft);
            Anchor(walls.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -110f), new Vector2(730f, 180f));

            help = NewText(root.transform, "Help", 20, TextAnchor.LowerLeft);
            Anchor(help.rectTransform, new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(1100f, 60f));

            // 속도 버튼 (시험 플레이용)
            string[] speeds = { "×1", "×2", "×4" };
            int[] values = { 1, 2, 4 };
            for (int i = 0; i < speeds.Length; i++)
            {
                int value = values[i];
                Button speed = NewButton(root.transform, speeds[i], () => onSpeed(value));
                Anchor((RectTransform)speed.transform, new Vector2(1f, 1f), new Vector2(-20f - (2 - i) * 110f, -20f), new Vector2(100f, 56f), new Vector2(1f, 1f));
            }

            // 개발용 [시험] 버튼 — 결과 화면 시험을 위해 남긴다
            for (int i = 0; i < devActions.Length; i++)
            {
                Action action = devActions[i];
                Button dev = NewButton(root.transform, devLabels[i], () => action());
                dev.GetComponent<Image>().color = new Color(0.35f, 0.25f, 0.25f, 0.85f);
                Anchor((RectTransform)dev.transform, new Vector2(1f, 0f), new Vector2(-20f, 20f + i * 62f), new Vector2(380f, 54f), new Vector2(1f, 0f));
            }

            endPanel = NewImage(root.transform, "End Panel", new Color(0f, 0f, 0f, 0.7f)).gameObject;
            Anchor((RectTransform)endPanel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 360f), new Vector2(0.5f, 0.5f));
            endText = NewText(endPanel.transform, "End Text", 48, TextAnchor.MiddleCenter);
            Anchor(endText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(860f, 200f), new Vector2(0.5f, 1f));
            Button finish = NewButton(endPanel.transform, "결과로", () => onFinish());
            Anchor((RectTransform)finish.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(320f, 80f), new Vector2(0.5f, 0f));
            endPanel.SetActive(false);
        }

        public void Refresh(BattleSim sim, BattleCameraRig rig, int speed, bool paused)
        {
            if (sim == null || Time.unscaledTime < nextRefresh)
            {
                return;
            }

            nextRefresh = Time.unscaledTime + 0.2f;   // 글자 갱신은 초당 5번이면 충분 (GC 줄이기)
            text.Clear();
            int minutes = (int)(sim.Elapsed / 60f);
            int seconds = (int)(sim.Elapsed % 60f);
            text.Append("경과 ").Append(minutes).Append(':').Append(seconds.ToString("00"))
                .Append("   속도 ×").Append(speed).Append(paused ? " (멈춤)" : "")
                .Append("\n적 처치 ").Append(sim.Kills.ToString("N0")).Append(" / ").Append(sim.TotalEnemies.ToString("N0"))
                .Append("  (등장 ").Append(sim.Spawned.ToString("N0")).Append(")   얻은 골드 ").Append(sim.Gold.ToString("N0"))
                .Append('\n');
            for (int side = 0; side < 4; side++)
            {
                BattleSim.SideStatus s = sim.Sides[side];
                bool watching = rig != null && !rig.Overview && rig.Side == side;
                text.Append(watching ? "▶ " : "   ").Append(SideNames[side]).Append("  성벽 ")
                    .Append(s.WallHp).Append('/').Append(s.WallMaxHp)
                    .Append("  병력 ").Append(s.GarrisonAlive).Append('/').Append(s.GarrisonStart)
                    .Append("  들판 ").Append(s.InField).Append(" · 벽 앞 ").Append(s.AtWall)
                    .Append(" · 오름 ").Append(s.Climbing).Append(" · 위 ").Append(s.OnWall).Append('\n');
            }

            walls.text = text.ToString();
            help.text = "A/D 성벽 따라 이동 · Q/E 다른 성벽 · Tab 전체 보기 · 1/2/3 속도 · P 멈춤   (임시 조작, 투사체 " + sim.ActiveShots + ")";
        }

        public void SetHeader(string line) => header.text = line;

        public void ShowEnd(string message)
        {
            endText.text = message;
            endPanel.SetActive(true);
        }

        Text NewText(Transform parent, string name, int size, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = alignment;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        Button NewButton(Transform parent, string label, Action onClick)
        {
            Image image = NewImage(parent, "Button " + label, new Color(0.25f, 0.3f, 0.4f, 0.95f));
            var button = image.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            Text t = NewText(image.transform, "Label", 24, TextAnchor.MiddleCenter);
            var rect = t.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            t.text = label;
            return button;
        }

        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size) =>
            Anchor(rect, anchor, position, size, anchor);

        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (ownsFont && font != null) UnityEngine.Object.Destroy(font);
        }
    }
}
