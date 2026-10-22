using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 명령 메뉴 (Battle.md 3.1-1, D-028·D-072): 휠 버튼으로 열고, 마우스나 숫자 키로 고르고, 휠·오른쪽 버튼으로 닫는다.
    // 열려 있어도 시간은 흐른다 (D-029 R2). 순서: ① 명령하기 → 명령어 / ② 전령 부르기·③ 소리 지르기 → 명령어 → 받을 성벽
    // 알림 (K-011 → K-047 → W17): 모든 알림은 오른쪽 위 목록(킬피드)에 한 줄씩. 새 줄이 맨 아래, 최대 5줄, 몇 초 뒤 흐려지며 사라진다.
    // 위급 알림은 같은 목록에 붉은 줄 + 화면 가장자리 붉은 빛 (S-23·S-27). 가운데에는 아무것도 쌓지 않는다.
    public sealed partial class BattleHud
    {
        enum MenuStep { Closed, Root, Words, Target }

        static readonly string[] SideShort = { "북", "동", "남", "서" };

        // 알림 종류. 숫자가 작을수록 중요 — 쪽지가 넘치면 덜 중요한 것부터 밀어낸다
        enum NoticeKind { Danger, MagicTrap, Command, Weather, Other }

        const int SlipMax = 5;
        const float FeedWidth = 480f;          // 목록 한 줄의 최대 너비 (가운데 단계 띠와 겹치지 않는 폭)
        const float FeedLineHeight = 40f;
        const float FeedGap = UiTokens.S8;      // 목록 줄 간격 (BuildNotices의 Column 간격과 같은 값)
        const int MapFeedMax = 3;              // 지도에서는 3줄만 (대포 판이 낮은 화면에서도 들어가도록)
        const float FeedReserve = Edge + (MapFeedMax + 1) * (FeedLineHeight + FeedGap) + UiTokens.S16;   // 지도에서 목록이 쓰는 위쪽 높이(사라지는 한 줄 여유 포함): 대포 판은 이 밑에서 시작
        int feedMax = SlipMax;
        const float NoticeRepeatWindow = 3f;   // 이 안에 같은 문장이 다시 오면 새 줄 대신 ×2, ×3
        static readonly float[] NoticeSeconds = { 6f, 5f, 4f, 4f, 3f };

        // 전투 코드의 알림 문장은 그대로 받고 낱말로 종류를 가른다. 위험 → 마법·함정 → 날씨 → 명령 순으로 먼저 맞는 것
        static readonly string[] DangerWords = { "잃었습니다", "돌아옵니다", "병사가 없습니다", "듣지 못했습니다", "남았습니다", "무너", "뚫", "위험", "전멸" };
        static readonly string[] MagicTrapWords = { "함정", "폭발", "불바다", "점화", "불발", "재사용 대기", "마법" };
        static readonly string[] WeatherWords = { "날씨", "비가", "소나기", "안개" };
        static readonly string[] CommandWords = { "전령", "명령", "모든 성벽", "대포", "망원경", "외침", "잘못 듣고", "직접" };

        sealed class NoticeLine
        {
            public string Text;
            public NoticeKind Kind;
            public int Count;
            public float Last;    // 마지막으로 들어온 때
            public float Until;   // 사라질 때
            public GameObject Slip;
            public Text Label;
        }

        GameObject menuPanel;
        Text menuTitle;
        readonly List<GameObject> menuButtons = new List<GameObject>();
        readonly List<Action> menuChoices = new List<Action>();
        MenuStep step;
        BattleSim.CommandVia chosenVia;
        TargetPriority chosenPriority;
        int currentSide;

        RectTransform slipColumn;
        Text notices;  // 시험 기록용 (이름 "Notices", 화면에는 그리지 않는다)
        readonly List<NoticeLine> noticeLines = new List<NoticeLine>();
        readonly System.Text.StringBuilder noticeText = new System.Text.StringBuilder(512);
        bool noticesDirty;

        // 명령을 내릴 때 (방법, 명령어, 받을 성벽)
        public event Action<BattleSim.CommandVia, TargetPriority, int> CommandChosen;

        public bool MenuOpen => step != MenuStep.Closed;

        void BuildCommandMenu()
        {
            // D-072 사용자 그림: 주인공 오른쪽에 세로선 + 비스듬한 붓 띠 (K-019 그림). 마우스 가까운 항목이 커진다
            var container = new GameObject("Command Menu", typeof(RectTransform));
            container.transform.SetParent(root.transform, false);
            menuPanel = container;
            var rect = (RectTransform)container.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;   // 주인공 화면 위치로 옮긴다
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(MenuWidth, 400f);
            menuLine = NewImage(container.transform, "Line", UiTokens.Alpha(UiTokens.Body, 0.55f));
            Sprite line = UiArt.Hud("Command/command_line");   // K-019: 위아래로 흐려지는 세로선 그림
            if (line != null)
            {
                menuLine.sprite = line;
                menuLine.color = Color.white;
            }

            menuTitle = Ui.Outlined(NewText(container.transform, "Title", UiTokens.Small, TextAnchor.LowerLeft));
            menuTitle.color = UiTokens.BodyDim;
            menuPanel.SetActive(false);

            BuildNotices();
        }

        public void ToggleMenu(int side)
        {
            if (MenuOpen)
            {
                CloseMenu();
                return;
            }

            currentSide = side;
            ShowRoot();
        }

        public void CloseMenu()
        {
            if (step == MenuStep.Closed) return;
            step = MenuStep.Closed;
            UiAppear.Hide(menuPanel);
        }

        // 숫자 키 1~6
        public void Choose(int index)
        {
            if (MenuOpen && index >= 0 && index < menuChoices.Count) menuChoices[index]();
        }

        void ShowRoot()
        {
            step = MenuStep.Root;
            SetOptions($"{SideFull[currentSide]} 성벽에서", new[] { "명령하기", "전령 부르기", "소리 지르기" },
                new[] { "이 성벽", "다른 성벽 · 느림", "다른 성벽 · 빠름" }, new Action[]
                {
                    () => ShowWords(BattleSim.CommandVia.Direct),
                    () => ShowWords(BattleSim.CommandVia.Messenger),
                    () => ShowWords(BattleSim.CommandVia.Shout),
                });
        }

        void ShowWords(BattleSim.CommandVia via)
        {
            chosenVia = via;
            step = MenuStep.Words;
            var labels = new string[BattleSim.PriorityNames.Length];
            var actions = new Action[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var priority = (TargetPriority)i;
                labels[i] = BattleSim.PriorityNames[i];
                actions[i] = () => ChooseWord(priority);
            }

            SetOptions(via == BattleSim.CommandVia.Direct ? "무엇을 먼저 쏠까" : via == BattleSim.CommandVia.Messenger ? "전령에게 맡길 명령" : "외칠 명령", labels, null, actions);
        }

        void ChooseWord(TargetPriority priority)
        {
            chosenPriority = priority;
            if (chosenVia == BattleSim.CommandVia.Direct)
            {
                Issue(currentSide);
                return;
            }

            // 받을 성벽: 왼쪽·오른쪽·맞은편 (방향 이름을 같이 적어 헷갈리지 않게, Battle.md 3.1-1)
            int left = (currentSide + 3) % 4;
            int right = (currentSide + 1) % 4;
            int opposite = (currentSide + 2) % 4;
            step = MenuStep.Target;
            SetOptions("어느 성벽으로", new[] { SideFull[left], SideFull[right], SideFull[opposite] }, new[] { "왼쪽", "오른쪽", "맞은편" },
                new Action[] { () => Issue(left), () => Issue(right), () => Issue(opposite) });
        }

        void Issue(int targetSide)
        {
            CloseMenu();
            CommandChosen?.Invoke(chosenVia, chosenPriority, targetSide);
        }

        const float MenuWidth = 420f;
        const float MenuSpacing = 56f;      // 항목 사이 (화면 1080 기준, 8의 배수)
        const float MenuLineGap = 32f;      // 세로선과 글자 사이
        const float MenuGrow = 0.45f;       // 가장 가까운 항목이 커지는 정도 (1.45배)
        const float MenuGrowReach = 70f;    // 이 거리(위아래) 안에서만 커진다
        Image menuLine;
        readonly List<RectTransform> menuItems = new List<RectTransform>();

        void SetOptions(string title, string[] labels, string[] tags, Action[] actions)
        {
            foreach (GameObject button in menuButtons) UnityEngine.Object.Destroy(button);
            menuButtons.Clear();
            menuChoices.Clear();
            menuItems.Clear();
            menuBands.Clear();
            menuBandHovers.Clear();
            menuTitle.text = title;

            float height = labels.Length * MenuSpacing;
            float top = height * 0.5f;
            Anchor(menuLine.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(UiTokens.Dot, height + 24f), new Vector2(0.5f, 0.5f));
            Anchor(menuTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(MenuLineGap, top + 16f), new Vector2(MenuWidth, 32f), new Vector2(0f, 0f));
            // 사용자 참고 그림(D-072): 항목이 주인공 오른쪽으로 부채꼴처럼 휜다 — 가운데 항목이 가장 바깥
            float mid = (labels.Length - 1) * 0.5f;
            for (int i = 0; i < labels.Length; i++)
            {
                Action action = actions[i];
                float k = mid > 0f ? (i - mid) / mid : 0f;
                float bulge = MenuArc * (1f - k * k);
                menuButtons.Add(NewMenuItem(i, labels[i], tags != null && i < tags.Length ? tags[i] : null, () => action(), top - (i + 0.5f) * MenuSpacing, bulge));
                menuChoices.Add(action);
            }

            UiAppear.Show(menuPanel);
        }

        const float MenuArc = 48f;          // 부채꼴로 가운데 항목이 바깥으로 나가는 거리
        const float MenuSlant = 6f;         // 띠가 비스듬히 기운 각도(도)
        static readonly Color MenuBand = new Color(UiTokens.WoodDark.r, UiTokens.WoodDark.g, UiTokens.WoodDark.b, 0.78f);
        static readonly Color MenuBandHover = new Color(UiTokens.Wood.r, UiTokens.Wood.g, UiTokens.Wood.b, 0.92f);
        readonly List<Image> menuBands = new List<Image>();
        readonly List<Image> menuBandHovers = new List<Image>();   // 그림 띠일 때 마우스 올림 띠 (커질수록 진해진다), 없으면 null
        static readonly Vector4 MenuBandBorder = new Vector4(64f, 8f, 64f, 8f);   // K-019 README의 9칸 경계 (왼쪽·아래·오른쪽·위)

        // 비스듬한 어두운 띠 + 번호 표식 + 글자 + 금색 붓 밑줄 (+ 오른쪽 작은 꼬리표). 크기·밝기는 UpdateMenuHover가 바꾼다
        GameObject NewMenuItem(int index, string label, string tag, Action onClick, float y, float bulge)
        {
            var go = new GameObject(Ui.KeyPrefix + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(menuPanel.transform, false);
            var rect = (RectTransform)go.transform;
            float width = MenuWidth - MenuLineGap - MenuArc;
            Anchor(rect, new Vector2(0f, 0.5f), new Vector2(MenuLineGap + bulge, y), new Vector2(width, MenuSpacing - 8f), new Vector2(0f, 0.5f));
            rect.localRotation = Quaternion.Euler(0f, 0f, MenuSlant);   // 오른쪽이 조금 올라간 비스듬한 띠
            var band = go.GetComponent<Image>();
            band.color = MenuBand;
            band.raycastTarget = true;
            Image bandHover = null;
            Sprite bandArt = UiArt.Hud("Command/command_band_normal", MenuBandBorder);
            Sprite hoverArt = UiArt.Hud("Command/command_band_hover", MenuBandBorder);
            if (bandArt != null)
            {
                band.sprite = bandArt;
                band.type = Image.Type.Sliced;
                band.color = Color.white;
                if (hoverArt != null)
                {
                    bandHover = NewImage(go.transform, "Band Hover", new Color(1f, 1f, 1f, 0f));
                    bandHover.sprite = hoverArt;
                    bandHover.type = Image.Type.Sliced;
                    Ui.Stretch(bandHover.rectTransform);
                }
            }

            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());

            // 번호 표식: K-019 그림(1~6). 그림이 없으면 황동 숫자
            Image numberArt = index < 6 ? Ui.HudIcon(go.transform, "Number", $"Command/command_number_{index + 1}", 34f) : null;
            if (numberArt != null) Anchor(numberArt.rectTransform, new Vector2(0f, 0.5f), new Vector2(UiTokens.S8, 0f), numberArt.rectTransform.sizeDelta, new Vector2(0f, 0.5f));
            else
            {
                Text number = NewText(go.transform, "Number", UiTokens.Text, TextAnchor.MiddleCenter);
                Anchor(number.rectTransform, new Vector2(0f, 0.5f), new Vector2(UiTokens.S8, 0f), new Vector2(33f, 33f), new Vector2(0f, 0.5f));
                number.text = (index + 1).ToString();
                number.color = UiTokens.Brass;
                number.font = UiFonts.Display;
            }

            Text text = Ui.Outlined(NewText(go.transform, "Label", UiTokens.Text, TextAnchor.MiddleLeft));
            Anchor(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(52f, 2f), new Vector2(width - 60f, 33f), new Vector2(0f, 0.5f));
            text.text = label;
            if (!string.IsNullOrEmpty(tag))
            {
                Text small = Ui.Outlined(NewText(go.transform, "Tag", UiTokens.Small, TextAnchor.MiddleRight));
                Anchor(small.rectTransform, new Vector2(1f, 0.5f), new Vector2(-UiTokens.S16, 2f), new Vector2(160f, 33f), new Vector2(1f, 0.5f));
                small.text = tag;
                small.color = UiTokens.BodyDim;
            }

            // 금색 붓 자국 밑줄: 그림이면 끝이 가늘어지는 붓 한 장, 없으면 황동 한 점 선
            float underlineWidth = Mathf.Min(width - 48f, text.preferredWidth + 40f);
            Sprite brushArt = UiArt.Hud("Command/command_underline");
            Image brush = NewImage(go.transform, "Underline", brushArt != null ? Color.white : UiTokens.Brass);
            if (brushArt != null) brush.sprite = brushArt;
            Anchor(brush.rectTransform, new Vector2(0f, 0f), new Vector2(44f, brushArt != null ? 0f : UiTokens.Dot), new Vector2(underlineWidth, brushArt != null ? 8f : UiTokens.Dot), new Vector2(0f, 0f));

            menuItems.Add(rect);
            menuBands.Add(band);
            menuBandHovers.Add(bandHover);
            return go;
        }

        // 메뉴를 주인공 화면 위치 오른쪽에 둔다 (BattleEntry가 열려 있는 동안 매 프레임). screen = 화면 화소 좌표
        public void PlaceMenu(Vector2 screen)
        {
            if (!MenuOpen || menuPanel == null) return;
            var rect = (RectTransform)menuPanel.transform;
            rect.anchorMin = rect.anchorMax = Viewport(screen);
            rect.anchoredPosition = new Vector2(64f, 0f);
            UpdateMenuHover();
        }

        // 마우스와 위아래로 가까운 항목일수록 크게 (손을 뻗으면 고를 것처럼 커지는 느낌)
        void UpdateMenuHover()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            var parent = (RectTransform)menuPanel.transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, mouse.position.ReadValue(), null, out Vector2 local)) return;
            bool near = local.x > -40f && local.x < MenuWidth + 40f;
            for (int i = 0; i < menuItems.Count; i++)
            {
                RectTransform item = menuItems[i];
                float d = Mathf.Abs(local.y - item.anchoredPosition.y);
                float t = near ? Mathf.Clamp01(1f - d / MenuGrowReach) : 0f;
                float target = 1f + MenuGrow * t * t;
                float current = item.localScale.x;
                float next = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 4f);   // 부드럽게
                item.localScale = new Vector3(next, next, 1f);
                float glow = (next - 1f) / MenuGrow;
                Image hover = i < menuBandHovers.Count ? menuBandHovers[i] : null;
                if (hover != null) hover.color = new Color(1f, 1f, 1f, glow);
                else if (i < menuBands.Count && menuBands[i].sprite == null) menuBands[i].color = Color.Lerp(MenuBand, MenuBandHover, glow);
            }
        }

        // ── 알림 ──
        void BuildNotices()
        {
            VerticalLayoutGroup column = Ui.Column(root.transform, "Notice Slips", FeedGap);
            column.childForceExpandWidth = false;
            column.childAlignment = TextAnchor.UpperRight;
            slipColumn = (RectTransform)column.transform;
            slipColumn.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            PlaceNotices(false);

            notices = Ui.Label(root.transform, "Notices", "", UiTokens.Small, UiTokens.Body);
            notices.enabled = false;
        }

        // 전투·지도 모두 오른쪽 위 한 곳. 지도의 대포 판은 FeedReserve 아래에서 시작해 겹치지 않는다
        void PlaceNotices(bool map)
        {
            Ui.Place(slipColumn, new Vector2(1f, 1f), new Vector2(-Edge, -Edge), new Vector2(FeedWidth, 0f), new Vector2(1f, 1f));
            feedMax = map ? MapFeedMax : SlipMax;
            TrimNotices();
        }

        // 사라지는 줄은 배치에서 바로 빼서 (제자리에서 흐려짐) 남은 줄과 아래 판이 밀리거나 겹치지 않게 한다
        static void FoldSlip(GameObject slip)
        {
            if (!slip.TryGetComponent(out LayoutElement element)) element = slip.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
            slip.transform.SetAsFirstSibling();
            UiAppear.Hide(slip, true);
        }

        // 줄이 넘치면 덜 중요한 것부터 접는다
        void TrimNotices()
        {
            while (noticeLines.Count > feedMax)
            {
                int drop = LeastImportantSlip();
                if (drop < 0) break;
                FoldSlip(noticeLines[drop].Slip);
                noticeLines.RemoveAt(drop);
            }
        }

        public void PushNotice(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            float now = Time.unscaledTime;
            NoticeKind kind = ClassifyNotice(text);

            // 같은 문장이 짧은 시간 안에 다시 오면 기존 쪽지에 ×N을 붙이고 시간을 늘린다
            for (int i = 0; i < noticeLines.Count; i++)
            {
                NoticeLine line = noticeLines[i];
                if (line.Until >= now && now - line.Last <= NoticeRepeatWindow && line.Text == text)
                {
                    line.Count++;
                    line.Last = now;
                    line.Until = now + NoticeSeconds[(int)kind];
                    if (line.Label != null)
                    {
                        FitSlip(line.Slip, line.Label, text + " ×" + line.Count);
                    }

                    if (kind == NoticeKind.Danger) ShowDanger();
                    noticesDirty = true;
                    RefreshNotices();
                    return;
                }
            }

            var added = new NoticeLine { Text = text, Kind = kind, Count = 1, Last = now, Until = now + NoticeSeconds[(int)kind] };
            noticeLines.Add(added);
            added.Slip = NewSlip(text, kind == NoticeKind.Danger, out added.Label);
            if (kind == NoticeKind.Danger) ShowDanger();
            TrimNotices();

            noticesDirty = true;
            RefreshNotices();
        }

        // 한 줄: 짧은 쪽지. 새 줄이 맨 아래 (킬피드)
        GameObject NewSlip(string text, bool danger, out Text label)
        {
            Image slip = Ui.Panel(slipColumn, "Slip", danger ? UiPanel.SlipDanger : UiPanel.Slip);
            slip.raycastTarget = false;
            slip.transform.SetAsLastSibling();
            label = Ui.Label(slip.transform, "Notice Line", text, UiTokens.Small, UiTokens.Ink, TextAnchor.MiddleLeft);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Stretch(label.rectTransform);
            float left = UiTokens.S16;
            if (danger)
            {
                // 색을 못 가려도 구분되도록 경고 그림을 붙인다
                Image warn = Ui.Icon(slip.transform, "Warn", UiArt.IconWarn, 1f, UiTokens.Danger);
                Anchor(warn.rectTransform, new Vector2(0f, 0.5f), new Vector2(UiTokens.S8, 0f), warn.rectTransform.sizeDelta, new Vector2(0f, 0.5f));
                left = UiTokens.S8 + warn.rectTransform.sizeDelta.x + UiTokens.S8;
            }

            label.rectTransform.offsetMin = new Vector2(left, 0f);
            label.rectTransform.offsetMax = new Vector2(-UiTokens.S16, 0f);
            FitSlip(slip.gameObject, label, text);
            UiAppear.Show(slip.gameObject);
            return slip.gameObject;
        }

        // 글이 길면 말줄임표로 자른다 (전체 문장은 시험용 숨은 기록 "Notices"에 남는다)
        static void FitSlip(GameObject slip, Text label, string full)
        {
            float pad = label.rectTransform.offsetMin.x - label.rectTransform.offsetMax.x;
            float room = FeedWidth - pad;
            label.text = full;
            string shown = full;
            while (label.preferredWidth > room && shown.Length > 1)
            {
                shown = shown.Substring(0, shown.Length - 1);
                label.text = shown.TrimEnd() + "…";
            }

            Ui.Size((RectTransform)slip.transform, label.preferredWidth + pad, FeedLineHeight);
        }

        void ShowDanger()
        {
            vignette.gameObject.SetActive(true);
            UiPulse.Play(vignette, 2.4f, 0.55f);
        }

        // 넘칠 때 밀어낼 쪽지: 가장 덜 중요한 종류 중 가장 오래된 것
        int LeastImportantSlip()
        {
            int pick = -1;
            for (int i = 0; i < noticeLines.Count; i++)
            {
                if (noticeLines[i].Slip == null) continue;
                if (pick < 0 || noticeLines[i].Kind > noticeLines[pick].Kind) pick = i;
            }

            return pick;
        }

        static NoticeKind ClassifyNotice(string text)
        {
            if (ContainsAny(text, DangerWords)) return NoticeKind.Danger;
            if (ContainsAny(text, MagicTrapWords)) return NoticeKind.MagicTrap;
            if (ContainsAny(text, WeatherWords)) return NoticeKind.Weather;
            if (ContainsAny(text, CommandWords)) return NoticeKind.Command;
            return NoticeKind.Other;
        }

        static bool ContainsAny(string text, string[] words)
        {
            foreach (string word in words)
                if (text.IndexOf(word, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        // 사라질 쪽지를 접고, 시험 기록용 글을 맞춘다
        void RefreshNotices()
        {
            float now = Time.unscaledTime;
            for (int i = noticeLines.Count - 1; i >= 0; i--)
            {
                if (noticeLines[i].Until >= now) continue;
                if (noticeLines[i].Slip != null) FoldSlip(noticeLines[i].Slip);
                noticeLines.RemoveAt(i);
                noticesDirty = true;
            }

            if (!noticesDirty) return;
            noticesDirty = false;
            noticeText.Clear();
            for (int i = 0; i < noticeLines.Count; i++)
            {
                if (i > 0) noticeText.Append('\n');
                noticeText.Append(noticeLines[i].Text);
                if (noticeLines[i].Count > 1) noticeText.Append(" ×").Append(noticeLines[i].Count);
            }

            notices.text = noticeText.ToString();
        }
    }
}
