using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 전투 HUD (임시 모양 — 정식 UI는 전투 화면 설계 CY1-C3 이후). 전투 HUD는 Combat 안에 둔다 (C-002).
    public sealed partial class BattleHud : IDisposable
    {
        static readonly string[] SideNames = { "북", "동", "남", "서" };

        readonly GameObject root;
        readonly Font font;
        readonly bool ownsFont;
        // 상태판 크기 (기준 해상도 1920×1080). 간단히 = 머리 줄 + 지금 성벽 한 줄, 자세히 = 날씨·처치·4면
        const float StatusCompactWidth = 640f;
        const float StatusCompactHeight = 76f;
        const float StatusDetailWidth = 820f;
        const float StatusDetailHeight = 196f;

        readonly RectTransform statusPanel;
        readonly Text header;
        readonly Text toggleHint;
        readonly Text walls;
        readonly Text help;
        bool statusDetail;
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

            // 상태판: 먼 들판의 적을 덜 가리게 작게. H로 자세히/간단히 접는다 (처음은 간단히)
            Image panel = NewImage(root.transform, "Status Panel", HudStyle.PanelSoft);
            HudStyle.Border(panel, 0.6f);
            statusPanel = panel.rectTransform;
            Anchor(statusPanel, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(StatusCompactWidth, StatusCompactHeight));

            header = NewText(panel.transform, "Header", 18, TextAnchor.UpperLeft);
            header.color = HudStyle.Brass;
            Anchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -8f), new Vector2(StatusCompactWidth - 150f, 26f));
            header.text = battleLine;

            toggleHint = NewText(panel.transform, "Toggle Hint", 16, TextAnchor.UpperRight);
            toggleHint.color = HudStyle.TextDim;
            Anchor(toggleHint.rectTransform, new Vector2(1f, 1f), new Vector2(-12f, -9f), new Vector2(130f, 24f), new Vector2(1f, 1f));

            walls = NewText(panel.transform, "Walls", 18, TextAnchor.UpperLeft);
            Anchor(walls.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -36f), new Vector2(StatusCompactWidth - 28f, StatusCompactHeight - 42f));

            // 조작 안내: 오른쪽 아래 [시험] 버튼(가로 1520부터) 앞에서 줄을 바꾼다
            help = NewText(root.transform, "Help", 18, TextAnchor.LowerLeft);
            help.color = HudStyle.TextDim;
            help.horizontalOverflow = HorizontalWrapMode.Wrap;
            Anchor(help.rectTransform, new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(1460f, 60f));

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
                dev.GetComponent<Image>().color = HudStyle.ButtonDev;
                Anchor((RectTransform)dev.transform, new Vector2(1f, 0f), new Vector2(-20f, 20f + i * 62f), new Vector2(380f, 54f), new Vector2(1f, 0f));
            }

            endPanel = NewImage(root.transform, "End Panel", HudStyle.Background).gameObject;
            HudStyle.Border(endPanel.GetComponent<Image>());
            Anchor((RectTransform)endPanel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 360f), new Vector2(0.5f, 0.5f));
            endText = NewText(endPanel.transform, "End Text", 48, TextAnchor.MiddleCenter);
            Anchor(endText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(860f, 200f), new Vector2(0.5f, 1f));
            Button finish = NewButton(endPanel.transform, "결과로", () => onFinish());
            Anchor((RectTransform)finish.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(320f, 80f), new Vector2(0.5f, 0f));
            endPanel.SetActive(false);

            BuildCommandMenu();
            LayoutStatus();
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
            if (!statusDetail)
            {
                // 간단히: 지금 보는 성벽 한 줄 + 처치 수·경과·속도
                int watched = rig != null && !rig.Overview ? rig.Side : -1;
                if (watched >= 0)
                {
                    BattleSim.SideStatus w = sim.Sides[watched];
                    text.Append("▶ ").Append(SideNames[watched]).Append(" 성벽 ").Append(w.WallHp).Append('/').Append(w.WallMaxHp)
                        .Append(" · 병력 ").Append(w.GarrisonAlive).Append('/').Append(w.GarrisonStart);
                }
                else
                {
                    text.Append("전체 보기");
                }

                text.Append(" · 처치 ").Append(sim.Kills.ToString("N0")).Append('/').Append(sim.TotalEnemies.ToString("N0"))
                    .Append(" · ").Append(minutes).Append(':').Append(seconds.ToString("00"))
                    .Append(" ×").Append(speed).Append(paused ? " (멈춤)" : "");
            }
            else
            {
                // 자세히: 날씨 / 경과·처치·골드 / 4면 (6줄)
                text.Append("날씨 ").Append(BattleSim.WeatherName(sim.CurrentWeather)).Append(sim.TimeOfDay == HumanLastRampart.Core.CombatContracts.TimeOfDay.Night ? " · 밤" : " · 낮")
                    .Append(sim.LowVisibility ? $" (시야 {sim.VisibleDistance:0}m)" : "").Append(sim.IsRaining ? " (공격 속도 −20%)" : "").Append(sim.HidesDirections ? " (흐림: 방향별 규모 모름)" : "")
                    .Append('\n');
                text.Append("경과 ").Append(minutes).Append(':').Append(seconds.ToString("00"))
                    .Append(" ×").Append(speed).Append(paused ? " (멈춤)" : "")
                    .Append(" · 적 처치 ").Append(sim.Kills.ToString("N0")).Append('/').Append(sim.TotalEnemies.ToString("N0"))
                    .Append(" (등장 ").Append(sim.Spawned.ToString("N0")).Append(") · 얻은 골드 ").Append(sim.Gold.ToString("N0"))
                    .Append('\n');
                for (int side = 0; side < 4; side++)
                {
                    BattleSim.SideStatus s = sim.Sides[side];
                    bool watching = rig != null && !rig.Overview && rig.Side == side;
                    text.Append(watching ? "▶ " : "   ").Append(SideNames[side]).Append(" 성벽 ")
                        .Append(s.WallHp).Append('/').Append(s.WallMaxHp)
                        .Append(" 병력 ").Append(s.GarrisonAlive).Append('/').Append(s.GarrisonStart)
                        .Append(" 들판 ").Append(sim.HidesDirections ? "?" : s.InField.ToString()).Append(" · 벽 앞 ").Append(s.AtWall)
                        .Append(" · 오름 ").Append(s.Climbing).Append(" · 위 ").Append(s.OnWall)
                        .Append(" 더미 ").Append(s.MaxPile.ToString("0.0")).Append("m")
                        .Append(" [").Append(BattleSim.PriorityNames[(int)sim.PriorityOf(side)]).Append("]\n");
                }
            }

            walls.text = text.ToString();
            RefreshNotices();
            RefreshGlobalStatus(sim);
            help.text = mapMode ? "지도: 왼쪽 전체 명령 · 오른쪽 대포(벽→대포→들판 클릭) · 아래 마법(고르고 들판 클릭) · 함정 표시 클릭 = 점화 · 오른쪽 버튼 누르고 있기 = 망원경 · 휠 확대 · 빈 곳 왼쪽 끌기/화살표 이동 · Q/W 마법 · H 상태판 접기 · Tab/M/Esc 닫기" : "휠 버튼 명령 · A/D 달리기(모서리 돌기) · W/S 통로 앞뒤 · Tab 지도(명령·대포 조준·마법) · C 카메라 [" + (rig != null ? rig.PresetName : "") + "] · 1/2/3 속도 · P 멈춤 · G 화면 효과 · H 상태판 자세히/간단히   (임시 조작, 투사체 " + sim.ActiveShots + ")";
        }

        // H 키: 상태판 자세히/간단히 (BattleEntry가 부름)
        public void ToggleStatusDetail()
        {
            statusDetail = !statusDetail;
            LayoutStatus();
        }

        float StatusHeight => statusDetail ? StatusDetailHeight : StatusCompactHeight;

        void LayoutStatus()
        {
            float width = statusDetail ? StatusDetailWidth : StatusCompactWidth;
            float height = StatusHeight;
            statusPanel.sizeDelta = new Vector2(width, height);
            header.rectTransform.sizeDelta = new Vector2(width - 150f, 26f);
            walls.fontSize = statusDetail ? 17 : 18;
            walls.rectTransform.sizeDelta = new Vector2(width - 28f, height - 42f);
            toggleHint.text = statusDetail ? "H 간단히" : "H 자세히";

            // 지도의 전체 명령 창은 상태판 바로 아래에 붙인다
            if (globalPanel != null)
            {
                ((RectTransform)globalPanel.transform).anchoredPosition = new Vector2(20f, -30f - height);
            }

            nextRefresh = 0f;   // 글자를 바로 새 모양으로
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
            t.color = HudStyle.Text;
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
            Image image = NewImage(parent, "Button " + label, HudStyle.Button);
            HudStyle.Border(image, 0.55f);
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
