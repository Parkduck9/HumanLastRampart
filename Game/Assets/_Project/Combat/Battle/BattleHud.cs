using System;
using System.Text;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 전투 HUD (K-047, Docs/GDD/UI_NoSlop.md). 전투 HUD는 Combat 안에 둔다 (C-002). 부품은 HumanLastRampart.UI.Ui 한 벌만 쓴다.
    // 화면 구역 (W17): 아래 왼쪽 황동 계기 띠(지금 성벽 체력 막대가 1순위) · 계기 띠 위 키 안내 3개 · 오른쪽 위 알림 목록(킬피드).
    // 위쪽은 들판을 보는 자리라 비워 둔다.
    // 나머지 조작은 F1 조작 판, 시험 단추·내부 정보는 F12 개발 표시에서만 보인다 (S-15·S-16)
    public sealed partial class BattleHud : IDisposable
    {
        static readonly string[] SideNames = { "북", "동", "남", "서" };
        static readonly string[] SideFull = { "북쪽", "동쪽", "남쪽", "서쪽" };

        const float Edge = UiTokens.S32;      // 판과 화면 끝 (S-04)
        const float BandHeight = 80f;
        const float BottomClear = Edge + BandHeight + UiTokens.S16;   // 아래 계기 띠 위쪽: 그 위로 판·안내를 올린다

        readonly GameObject root;
        readonly string battleLine;

        // 위 계기 띠
        RectTransform band;
        Text bandSide, bandWallNumber, bandTroops, bandKills, bandGold, bandTime, bandState;
        UiBar bandWall;
        Image weatherIcon, timeIcon;
        readonly Button[] speedButtons = new Button[3];
        static readonly int[] SpeedValues = { 1, 2, 4 };
        Image pauseIcon;

        // 자세히 (H): 4면 막대 + 날씨 한 줄
        bool statusDetail;
        GameObject detailBoard;
        readonly UiBar[] detailBars = new UiBar[4];
        readonly Text[] detailRows = new Text[4];
        readonly Image[] detailPriority = new Image[4];
        Text detailWeather;

        // 시험 기록용 글 (PlayProbe HudText가 이름 "Walls"로 읽는다 — 화면에는 그리지 않는다)
        readonly Text walls;
        readonly StringBuilder text = new StringBuilder(512);
        float nextRefresh;

        // 키 안내 · F1 조작 판 · F12 개발 표시
        RectTransform hints;
        int hintsMode = -1;
        GameObject helpBoard;
        CanvasGroup devGroup;
        Text devLine;

        // 전투 끝 보고
        GameObject endPanel;
        Text endTitle, endLine, endSealText;
        Image endSeal;

        public BattleHud(Transform parent, string battleLine, Action onFinish, Action<int> onSpeed, Action[] devActions, string[] devLabels)
        {
            this.battleLine = battleLine;
            root = new GameObject("Battle HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;   // 모든 화면 같은 기준 (ScreenView와 같음)

            BuildVignette();
            BuildBand(onSpeed);
            BuildDetail();
            walls = Ui.Label(root.transform, "Walls", "", UiTokens.Small, UiTokens.Body);
            walls.enabled = false;
            BuildHints();
            BuildHelp();
            BuildDev(devActions, devLabels);
            BuildCommandMenu();
            BuildEnd(onFinish);
            LayoutStatus();
        }

        // ── 아래 계기 띠: [북] [성벽 막대·숫자] [병사] [처치] [골드] [시간] [날씨·밤낮] [상태] [속도 ▶ ▶▶ ▶▶▶] ──
        void BuildBand(Action<int> onSpeed)
        {
            Image panel = Ui.Panel(root.transform, "Status Band", UiPanel.Band);
            band = panel.rectTransform;
            Ui.Place(band, Vector2.zero, new Vector2(Edge, Edge), new Vector2(900f, BandHeight), Vector2.zero);
            var row = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(24, 24, 16, 16);
            row.spacing = UiTokens.S16;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            bandSide = Ui.Label(band, "Side", "북", UiTokens.H2, UiTokens.Brass, TextAnchor.MiddleCenter, true);
            Ui.Size(bandSide, 40f, 48f);

            VerticalLayoutGroup wall = Ui.Column(band, "Wall", UiTokens.S4);
            wall.childAlignment = TextAnchor.MiddleLeft;
            Ui.Size(wall, 280f, 48f);
            bandWall = Ui.Bar(wall.transform, "Wall Bar", 280f, 24f);
            bandWallNumber = Ui.Label(wall.transform, "Wall Number", "", UiTokens.Small, UiTokens.BodyDim);
            Ui.Size(bandWallNumber, 280f, 20f);

            bandTroops = Stat("Troops", "Icons/stat_troops", 72f);
            bandKills = Stat("Kills", "Icons/stat_kills", 128f);
            bandGold = Stat("Gold", "Icons/stat_gold", 64f);
            bandTime = Ui.Label(band, "Time", "0:00", UiTokens.Text, UiTokens.BodyDim);
            Ui.Size(bandTime, 64f, 48f);

            weatherIcon = Ui.HudIcon(band, "Weather Icon", "Icons/weather_clear", 24f);
            timeIcon = Ui.HudIcon(band, "Time Icon", "Icons/time_day", 24f);
            bandState = Ui.Label(band, "State", "", UiTokens.Small, UiTokens.Brass);
            bandState.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Size(bandState, 0f, 48f);

            pauseIcon = Ui.Icon(band, "Paused", UiArt.IconPause, 1f, UiTokens.Brass);
            string[] icons = { UiArt.IconSpeed1, UiArt.IconSpeed2, UiArt.IconSpeed4 };
            string[] keys = { "×1", "×2", "×4" };   // 시험이 이 열쇠로 누른다
            for (int i = 0; i < 3; i++)
            {
                int value = SpeedValues[i];
                speedButtons[i] = Ui.IconButton(band, keys[i], icons[i], () => onSpeed(value), 48f);
                Ui.Size(speedButtons[i], i == 0 ? 48f : i == 1 ? 64f : 72f, 48f);
            }
        }

        // 아이콘 + 숫자 칸 (그림이 없으면 낱말)
        Text Stat(string name, string icon, float width)
        {
            Image picture = Ui.HudIcon(band, name + " Icon", icon, 24f);
            if (picture == null)
            {
                Text word = Ui.Label(band, name + " Word", name == "Troops" ? "병사" : name == "Kills" ? "처치" : "골드", UiTokens.Small, UiTokens.BodyDim);
                Ui.Size(word, 44f, 48f);
            }

            Text value = Ui.Label(band, name, "", UiTokens.Text, UiTokens.Body);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Size(value, width, 48f);
            return value;
        }

        // ── 자세히 (H): 띠 아래 나무 판 — 4면 막대 + 병사·벽 앞·오름 + 지금 명령 아이콘, 날씨 한 줄 ──
        void BuildDetail()
        {
            Image panel = Ui.Panel(root.transform, "Status Detail", UiPanel.Wood);
            detailBoard = panel.gameObject;
            Ui.Place(panel.rectTransform, Vector2.zero, new Vector2(Edge, BottomClear + 33f + UiTokens.S8), new Vector2(640f, 0f), Vector2.zero);   // 키 안내 줄 위
            var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(24, 24, 24, 24);
            column.spacing = UiTokens.S8;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int side = 0; side < 4; side++)
            {
                HorizontalLayoutGroup line = Ui.Row(panel.transform, "Side " + SideNames[side], UiTokens.S16);
                Ui.Size(line, -1f, 32f);
                Text name = Ui.Label(line.transform, "Name", SideNames[side], UiTokens.Text, UiTokens.Brass, TextAnchor.MiddleCenter, true);
                Ui.Size(name, 32f, 32f);
                detailBars[side] = Ui.Bar(line.transform, "Bar", 180f, 18f);
                detailRows[side] = Ui.Label(line.transform, "Numbers", "", UiTokens.Small, UiTokens.Body);
                Ui.Size(detailRows[side], 300f, 32f);
                detailPriority[side] = Ui.HudIcon(line.transform, "Order", "Icons/" + PriorityIcons[0], 24f);
            }

            HorizontalLayoutGroup weather = Ui.Row(panel.transform, "Weather", UiTokens.S8);
            Ui.Size(weather, -1f, 28f);
            detailWeather = Ui.Label(weather.transform, "Text", "", UiTokens.Small, UiTokens.BodyDim);
            Ui.Size(detailWeather, 560f, 28f);
            detailBoard.SetActive(false);
        }

        // ── 계기 띠 바로 위 왼쪽: 지금 할 수 있는 핵심 행동 3개만 (S-15) ──
        void BuildHints()
        {
            HorizontalLayoutGroup row = Ui.Row(root.transform, "Key Hints", UiTokens.S24);
            hints = (RectTransform)row.transform;
            row.childAlignment = TextAnchor.MiddleRight;
            Ui.Place(hints, Vector2.zero, new Vector2(Edge, BottomClear), new Vector2(800f, 33f), Vector2.zero);   // 띠 바로 위 왼쪽 (어떤 화면 비율에서도 띠와 겹치지 않는다)
            row.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        void RefreshHints()
        {
            int mode = !mapMode ? 0 : preparing ? 2 : 1;
            if (mode == hintsMode || hints == null) return;
            hintsMode = mode;
            foreach (Transform child in hints) UnityEngine.Object.Destroy(child.gameObject);
            if (mode == 0)
            {
                Ui.KeyHint(hints, "휠", "명령");
                Ui.KeyHint(hints, "Tab", "지도");
            }
            else
            {
                Ui.KeyHint(hints, "Tab", mode == 2 ? "전투 시작" : "닫기");
                Ui.KeyHint(hints, "우클릭", "망원경");
            }

            Ui.KeyHint(hints, "F1", "조작");
        }

        // ── F1 조작 판: 전체 키 목록은 여기에만 ──
        void BuildHelp()
        {
            Image panel = Ui.Panel(root.transform, "Controls", UiPanel.Parchment);
            helpBoard = panel.gameObject;
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 0f));
            var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(48, 48, 32, 32);
            column.spacing = UiTokens.S16;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text title = Ui.Label(panel.transform, "Title", "조작", UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            Ui.Size(title, -1f, 48f);
            HorizontalLayoutGroup sections = Ui.Row(panel.transform, "Sections", UiTokens.S48, TextAnchor.UpperLeft);
            sections.childAlignment = TextAnchor.UpperLeft;
            HelpColumn(sections.transform, "성벽 위", new[]
            {
                "휠 버튼", "명령 메뉴", "A · D", "성벽 따라 달리기", "W · S", "통로 앞뒤로", "Tab", "지도 펼치기",
                "C", "카메라 바꾸기", "1 · 2 · 3", "빠르기", "P", "멈춤", "H", "4면 자세히", "G", "화면 효과",
                "E", "계단 내려가기 · 탑 빛줄기 명령", "W A S D", "탑에서 북·서·남·동 성벽으로",
            });
            HelpColumn(sections.transform, "지도", new[]
            {
                "왼쪽 클릭", "고르기 · 조준 · 점화", "왼쪽 끌기", "지도 옮기기 · 병사 보내기", "우클릭 누름", "망원경",
                "휠", "가까이 · 멀리", "Q · W", "마법 고르기", "Tab · Esc", "지도 접기",
            });
            Text close = Ui.Label(panel.transform, "Close", "F1을 다시 누르면 닫힙니다", UiTokens.Small, UiTokens.InkSoft);
            Ui.Size(close, -1f, 28f);
            helpBoard.SetActive(false);
        }

        static void HelpColumn(Transform parent, string heading, string[] pairs)
        {
            VerticalLayoutGroup column = Ui.Column(parent, heading, UiTokens.S8);
            Ui.Size(column, 440f, -1f);
            Text title = Ui.Label(column.transform, "Heading", heading, UiTokens.Text, UiTokens.InkSoft, TextAnchor.MiddleLeft, true);
            Ui.Size(title, -1f, 32f);
            for (int i = 0; i + 1 < pairs.Length; i += 2) Ui.KeyHint(column.transform, pairs[i], pairs[i + 1], false);
        }

        public void ToggleHelp() => UiAppear.Toggle(helpBoard, !helpBoard.activeSelf, new Vector2(0f, -UiTokens.S24));

        // ── F12 개발 표시: [시험] 단추·내부 정보. 기본은 안 보이지만 켜져 있고 눌린다 (시험 자동화가 쓴다) ──
        void BuildDev(Action[] devActions, string[] devLabels)
        {
            VerticalLayoutGroup column = Ui.Column(root.transform, "Dev Tools", UiTokens.S8);
            column.childAlignment = TextAnchor.LowerRight;
            var rect = (RectTransform)column.transform;
            Ui.Place(rect, new Vector2(1f, 0f), new Vector2(-Edge, Edge + 48f), new Vector2(420f, 0f), new Vector2(1f, 0f));
            column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            devGroup = column.gameObject.AddComponent<CanvasGroup>();
            devLine = Ui.Outlined(Ui.Label(column.transform, "Dev Line", battleLine, UiTokens.Small, UiTokens.BodyDim, TextAnchor.LowerRight));
            Ui.Size(devLine, 420f, 52f);
            for (int i = 0; i < devActions.Length; i++)
            {
                Action action = devActions[i];
                Button dev = Ui.Button(column.transform, devLabels[i], devLabels[i], () => action());
                Ui.Size(dev, 420f, 48f);
            }

            SetDevVisible(false);
        }

        bool devVisible;

        public void ToggleDev() => SetDevVisible(!devVisible);

        void SetDevVisible(bool visible)
        {
            devVisible = visible;
            devGroup.alpha = visible ? 1f : 0f;
            devGroup.blocksRaycasts = visible;   // 숨겨도 단추는 켜진 채(interactable) — 시험이 같은 길로 누른다
        }

        // ── 전투 끝: 도장 찍힌 전투 보고 한 장 ──
        void BuildEnd(Action onFinish)
        {
            Image shade = Ui.Shade(root.transform, "End");
            endPanel = shade.gameObject;
            Image box = Ui.Panel(shade.transform, "Report", UiPanel.Parchment);
            Ui.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 360f));
            endTitle = Ui.Label(box.transform, "Title", "", UiTokens.H1, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            Ui.Place(endTitle.rectTransform, new Vector2(0f, 1f), new Vector2(UiTokens.S48, -UiTokens.S48), new Vector2(480f, 64f), new Vector2(0f, 1f));
            endLine = Ui.Label(box.transform, "Line", "", UiTokens.Text, UiTokens.InkSoft, TextAnchor.UpperLeft);
            Ui.Place(endLine.rectTransform, new Vector2(0f, 1f), new Vector2(UiTokens.S48, -128f), new Vector2(480f, 72f), new Vector2(0f, 1f));
            endSeal = Ui.Icon(box.transform, "Seal", UiArt.Seal, 1.5f, UiTokens.Good);
            Ui.Place(endSeal.rectTransform, new Vector2(1f, 1f), new Vector2(-UiTokens.S48, -UiTokens.S32), endSeal.rectTransform.sizeDelta, new Vector2(1f, 1f));
            endSeal.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            endSealText = Ui.Label(endSeal.transform, "Mark", "", UiTokens.H1, UiTokens.Good, TextAnchor.MiddleCenter, true);
            Ui.Stretch(endSealText.rectTransform);
            Button finish = Ui.Button(box.transform, "결과로", "보고서 보기", () => onFinish(), UiButtonKind.Primary);
            Ui.Place((RectTransform)finish.transform, new Vector2(0.5f, 0f), new Vector2(0f, UiTokens.S48), new Vector2(320f, 64f), new Vector2(0.5f, 0f));
            endPanel.SetActive(false);
        }

        public void ShowEnd(bool victory, string line)
        {
            endTitle.text = victory ? "승리" : "패배";
            endLine.text = line;
            Color mark = victory ? UiTokens.Good : UiTokens.Danger;
            endSeal.color = UiArt.IsNative(endSeal.sprite) ? Color.white : mark;   // K-123 도장은 황동 고리 그림이라 흰색 (임시 그림이면 승패 색)
            endSealText.color = mark;
            endSealText.text = victory ? "승" : "패";
            CloseMenu();
            UiAppear.Show(endPanel);
            Transform box = endPanel.transform.Find("Report");
            if (box != null) UiAppear.Show(box.gameObject, new Vector2(0f, -UiTokens.S24));
        }

        public void SetHeader(string line) { if (devLine != null) devLine.text = line; }

        // ── 위급 가장자리 빛 (S-27) ──
        Image vignette;

        void BuildVignette()
        {
            vignette = Ui.Image(root.transform, "Danger Edge", UiTokens.Alpha(UiTokens.Danger, 0f));
            vignette.sprite = UiArt.Get(UiArt.Vignette);
            Ui.Stretch(vignette.rectTransform);
            vignette.gameObject.SetActive(false);
        }

        public void Refresh(BattleSim sim, BattleCameraRig rig, int speed, bool paused)
        {
            if (sim == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.2f;   // 글자 갱신은 초당 5번이면 충분 (GC 줄이기). 막대는 매 프레임 스스로 움직인다
            int minutes = (int)(sim.Elapsed / 60f);
            int seconds = (int)(sim.Elapsed % 60f);
            int watched = rig != null && !rig.Overview ? rig.Side : -1;

            // 1순위: 지금 보는 성벽 (전체 보기면 가장 약한 성벽)
            int shown = watched;
            if (shown < 0)
            {
                shown = 0;
                for (int s = 1; s < 4; s++)
                    if (Ratio(sim.Sides[s]) < Ratio(sim.Sides[shown])) shown = s;
            }

            BattleSim.SideStatus w = sim.Sides[shown];
            bandSide.text = SideNames[shown];
            bandWall.Set(Ratio(w));
            bandWallNumber.text = $"{w.WallHp:N0} / {w.WallMaxHp:N0}";
            int alive = 0, start = 0;
            for (int s = 0; s < 4; s++) { alive += sim.Sides[s].GarrisonAlive; start += sim.Sides[s].GarrisonStart; }
            bandTroops.text = watched >= 0 ? $"{w.GarrisonAlive}/{w.GarrisonStart}" : $"{alive}/{start}";
            bandKills.text = $"{sim.Kills:N0}/{sim.TotalEnemies:N0}";
            bandGold.text = sim.Gold.ToString("N0");
            bandTime.text = $"{minutes}:{seconds:00}";
            bandState.text = preparing ? "전투 준비" : mapMode ? (mapFrozen ? "지도 · 시간 멈춤" : "지도 · 시간 흐름") : paused ? "멈춤" : "";
            Ui.Size(bandState, bandState.text.Length == 0 ? 0f : bandState.preferredWidth + 2f, 48f);
            pauseIcon.gameObject.SetActive(paused);
            for (int i = 0; i < 3; i++) Ui.SetOn(speedButtons[i], !paused && speed == SpeedValues[i]);
            RefreshStatusIcons(sim);

            bool detail = statusDetail && !mapMode;
            if (detailBoard.activeSelf != detail) UiAppear.Toggle(detailBoard, detail, new Vector2(0f, -UiTokens.S16));
            if (detail) RefreshDetail(sim, watched);

            WriteProbeText(sim, watched, minutes, seconds, speed, paused);
            RefreshNotices();
            RefreshPhaseBanner(sim);   // 단계 예고 띠 (BattleHud.Phase.cs)
            RefreshBossBar(sim);       // 우두머리 체력 막대 (BattleHud.Boss.cs)
            RefreshGlobalStatus(sim);
            RefreshHints();
            devLine.text = battleLine + "\n투사체 " + sim.ActiveShots;
        }

        static float Ratio(BattleSim.SideStatus s) => s.WallMaxHp > 0 ? (float)s.WallHp / s.WallMaxHp : 1f;

        void RefreshDetail(BattleSim sim, int watched)
        {
            for (int side = 0; side < 4; side++)
            {
                BattleSim.SideStatus s = sim.Sides[side];
                detailBars[side].Set(Ratio(s));
                string field = sim.HidesDirections ? "?" : s.InField.ToString("N0");
                detailRows[side].text = $"병사 {s.GarrisonAlive}/{s.GarrisonStart} · 들판 {field} · 벽 앞 {s.AtWall + s.Climbing + s.OnWall}";
                detailRows[side].color = side == watched ? UiTokens.Brass : UiTokens.Body;
                if (detailPriority[side] != null)
                {
                    Sprite icon = UiArt.Hud("Icons/" + PriorityIcons[(int)sim.PriorityOf(side)]);
                    if (icon != null) detailPriority[side].sprite = icon;
                }
            }

            string weather = BattleSim.WeatherName(sim.CurrentWeather) + (sim.TimeOfDay == HumanLastRampart.Core.CombatContracts.TimeOfDay.Night ? " 밤" : " 낮");
            if (sim.LowVisibility) weather += $" · 시야 {sim.VisibleDistance:0}m";
            if (sim.IsRaining) weather += " · 활·총이 느려짐";
            if (sim.HidesDirections) weather += " · 적 규모를 모름";
            detailWeather.text = weather;
        }

        // 예전 상태판 글 그대로 (시험 기록 HudText가 읽는다)
        void WriteProbeText(BattleSim sim, int watched, int minutes, int seconds, int speed, bool paused)
        {
            text.Clear();
            if (watched >= 0)
            {
                BattleSim.SideStatus w = sim.Sides[watched];
                text.Append(SideNames[watched]).Append(" 성벽 ").Append(w.WallHp).Append('/').Append(w.WallMaxHp)
                    .Append(" · 병력 ").Append(w.GarrisonAlive).Append('/').Append(w.GarrisonStart);
            }
            else text.Append("전체 보기");

            text.Append(" · 처치 ").Append(sim.Kills.ToString("N0")).Append('/').Append(sim.TotalEnemies.ToString("N0"))
                .Append(" · ").Append(minutes).Append(':').Append(seconds.ToString("00")).Append(" ×").Append(speed).Append(paused ? " 멈춤" : "");
            for (int side = 0; side < 4; side++)
            {
                BattleSim.SideStatus s = sim.Sides[side];
                text.Append('\n').Append(SideNames[side]).Append(" 성벽 ").Append(s.WallHp).Append('/').Append(s.WallMaxHp)
                    .Append(" 병력 ").Append(s.GarrisonAlive).Append('/').Append(s.GarrisonStart)
                    .Append(" 들판 ").Append(s.InField).Append(" 벽 앞 ").Append(s.AtWall).Append(" 오름 ").Append(s.Climbing).Append(" 위 ").Append(s.OnWall)
                    .Append(" 더미 ").Append(s.MaxPile.ToString("0.0")).Append("m ").Append(BattleSim.PriorityNames[(int)sim.PriorityOf(side)]);
            }

            walls.text = text.ToString();
        }

        // H 키: 4면 자세히 (BattleEntry가 부름)
        public void ToggleStatusDetail()
        {
            statusDetail = !statusDetail;
            LayoutStatus();
        }

        void LayoutStatus() => nextRefresh = 0f;   // 바로 새 모양으로

        // ── 날씨·밤낮 아이콘 (K-026 그림) ──
        static readonly string[] WeatherIconNames = { "weather_clear", "weather_cloudy", "weather_rain", "weather_fog" };   // Weather 순서

        void RefreshStatusIcons(BattleSim sim)
        {
            if (weatherIcon != null)
            {
                int w = (int)sim.CurrentWeather;
                Sprite sprite = w >= 0 && w < WeatherIconNames.Length ? UiArt.Hud("Icons/" + WeatherIconNames[w]) : null;
                if (sprite != null) weatherIcon.sprite = sprite;
            }

            if (timeIcon != null)
            {
                bool night = sim.TimeOfDay == HumanLastRampart.Core.CombatContracts.TimeOfDay.Night;
                Sprite sprite = UiArt.Hud(night ? "Icons/time_night" : "Icons/time_day");
                if (sprite != null) timeIcon.sprite = sprite;
            }
        }

        // ── 이 HUD 안에서 쓰는 짧은 도우미 (모두 Ui 부품으로) ──
        Text NewText(Transform parent, string name, int size, TextAnchor alignment)
        {
            Text t = Ui.Label(parent, name, "", Mathf.Max(UiTokens.Small, size), UiTokens.Body, alignment);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        static Image NewImage(Transform parent, string name, Color color) => Ui.Image(parent, name, color);

        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size) => Ui.Place(rect, anchor, position, size, anchor);

        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot) => Ui.Place(rect, anchor, position, size, pivot);

        // 화면 화소 좌표 → 이 캔버스의 화면 비율 자리 (화면 크기·배율과 상관없이 그 자리)
        static Vector2 Viewport(Vector3 screen) => new Vector2(screen.x / Mathf.Max(1, Screen.width), screen.y / Mathf.Max(1, Screen.height));

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}
