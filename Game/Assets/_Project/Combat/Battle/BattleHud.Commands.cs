using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 명령 메뉴 (Battle.md 3.1-1, D-028) — 임시 모양. 휠 버튼으로 열고, 마우스나 숫자 키로 고르고, 휠·오른쪽 버튼으로 닫는다.
    // 열려 있어도 시간은 흐른다 (D-029 R2).
    // 순서: ① 명령하기 → 명령어 / ② 전령 부르기·③ 소리 지르기 → 명령어 → 받을 성벽
    // 지금은 전령도 명령어·성벽을 고른 뒤 출발한다 (병사가 먼저 달려와 마주 보는 연출은 그림·주인공 이동 이후)
    public sealed partial class BattleHud
    {
        enum MenuStep { Closed, Root, Words, Target }

        static readonly string[] SideShort = { "북", "동", "남", "서" };

        // 알림 종류 (K-011). 숫자가 작을수록 중요 — 줄이 넘치면 큰 것부터 밀어낸다
        enum NoticeKind { Danger, MagicTrap, Command, Weather, Other }

        const int NoticeMaxLines = 6;
        const float NoticeRepeatWindow = 3f;   // 이 안에 같은 문장이 다시 오면 새 줄 대신 ×2, ×3
        static readonly float[] NoticeSeconds = { 8f, 5f, 5f, 3f, 3f };   // 종류별 머무는 시간
        static readonly string[] NoticeColorTags =
        {
            // 날씨·기타도 본문색 그대로 — 보조색(TextDim)은 안개 바탕과 밝기가 같아 묻혔다. 중요도는 순서·머무는 시간으로 가른다
            ColorTag(HudStyle.Danger), ColorTag(HudStyle.Brass), ColorTag(HudStyle.Text), ColorTag(HudStyle.Text), ColorTag(HudStyle.Text),
        };

        // 전투 코드의 알림 문장은 그대로 두고 낱말로 종류를 가른다. 위험 → 마법·함정 → 날씨 → 명령 순으로 먼저 맞는 것
        // (예: "전령: …돌아오고 있습니다"는 위험, "북쪽 대포 1: 조준점 지정"은 명령)
        static readonly string[] DangerWords = { "잃었습니다", "돌아오고 있습니다", "병사가 없습니다", "듣지 못했습니다", "무너", "뚫", "위험", "전멸" };
        static readonly string[] MagicTrapWords = { "함정", "폭발", "불바다", "점화", "불발", "재사용 대기", "마법" };
        static readonly string[] WeatherWords = { "날씨", "비가", "소나기", "안개" };
        static readonly string[] CommandWords = { "전령", "명령", "전체:", "대포", "망원경", "외침", "잘못 듣고", "(직접)" };

        sealed class NoticeLine
        {
            public string Text;
            public NoticeKind Kind;
            public int Count;
            public float Last;    // 마지막으로 들어온 때
            public float Until;   // 사라질 때
        }

        GameObject menuPanel;
        Text menuTitle;
        readonly List<GameObject> menuButtons = new List<GameObject>();
        readonly List<Action> menuChoices = new List<Action>();
        MenuStep step;
        BattleSim.CommandVia chosenVia;
        TargetPriority chosenPriority;
        int currentSide;

        Text notices;
        Image noticeBacking;
        readonly List<NoticeLine> noticeLines = new List<NoticeLine>();
        readonly System.Text.StringBuilder noticeText = new System.Text.StringBuilder(512);
        bool noticesDirty;

        // 명령을 내릴 때 (방법, 명령어, 받을 성벽)
        public event Action<BattleSim.CommandVia, TargetPriority, int> CommandChosen;

        public bool MenuOpen => step != MenuStep.Closed;

        void BuildCommandMenu()
        {
            // D-072 사용자 그림: 주인공 오른쪽에 세로선 + 글자와 밑줄만 (상자 버튼 없음). 마우스 가까운 항목이 커진다
            var container = new GameObject("Command Menu", typeof(RectTransform));
            container.transform.SetParent(root.transform, false);
            menuPanel = container;
            var rect = (RectTransform)container.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;   // 왼쪽 아래 기준 — 주인공 화면 위치로 옮긴다
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(MenuWidth, 400f);
            menuLine = NewImage(container.transform, "Line", new Color(HudStyle.Text.r, HudStyle.Text.g, HudStyle.Text.b, 0.55f));
            menuLine.raycastTarget = false;
            Sprite line = HudStyle.Art("Command/command_line");   // K-019: 위아래로 흐려지는 세로선 그림
            if (line != null)
            {
                menuLine.sprite = line;
                menuLine.color = Color.white;
            }
            menuTitle = NewText(container.transform, "Title", 18, TextAnchor.LowerLeft);
            menuTitle.color = HudStyle.TextDim;
            AddMenuOutline(menuTitle);
            menuPanel.SetActive(false);

            // 알림: 오른쪽 위, 속도 버튼 아래. 아래 가운데·왼쪽(주인공·마법사 자리)을 피하고,
            // 지도 모드 오른쪽 대포 창(위 끝이 화면 위에서 240)에 닿지 않게 높이를 맞춘다
            // 받침: 안개(밝은 회색) 위에서 글씨가 바탕과 같은 밝기가 되어 안 보였다 — 글 높이에 맞춘 어두운 띠를 깐다
            noticeBacking = NewImage(root.transform, "Notice Backing", new Color(0.05f, 0.07f, 0.06f, 0.55f));
            noticeBacking.raycastTarget = false;
            Anchor(noticeBacking.rectTransform, new Vector2(1f, 1f), new Vector2(-12f, -80f), new Vector2(400f, 0f), new Vector2(1f, 1f));
            noticeBacking.gameObject.SetActive(false);
            notices = NewText(root.transform, "Notices", 20, TextAnchor.UpperRight);
            notices.fontStyle = FontStyle.Bold;   // 가는 획은 밝기가 다 올라오지 않아 흐려 보였다
            Anchor(notices.rectTransform, new Vector2(1f, 1f), new Vector2(-20f, -84f), new Vector2(760f, 148f), new Vector2(1f, 1f));
            notices.supportRichText = true;   // 종류별 색 (K-011)
            // 밝은 들판·안개 위에서도 읽히게: 사방 진한 테두리 + 아래 그림자 (그림자만으로는 안개에서 흐렸다)
            var outline = notices.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.04f, 0.03f, 0.02f, 0.95f);
            outline.effectDistance = new Vector2(1.6f, -1.6f);
            var shadow = notices.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2.5f, -2.5f);
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
            step = MenuStep.Closed;
            menuPanel.SetActive(false);
        }

        // 숫자 키 1~6
        public void Choose(int index)
        {
            if (MenuOpen && index >= 0 && index < menuChoices.Count)
            {
                menuChoices[index]();
            }
        }

        void ShowRoot()
        {
            step = MenuStep.Root;
            SetOptions($"명령 — {SideShort[currentSide]}쪽 성벽에서", new[] { "명령하기 (이 성벽)", "전령 부르기", "소리 지르기" }, new Action[]
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

            SetOptions(via == BattleSim.CommandVia.Direct ? "명령어" : via == BattleSim.CommandVia.Messenger ? "전령 — 명령어" : "소리 지르기 — 명령어", labels, actions);
        }

        void ChooseWord(TargetPriority priority)
        {
            chosenPriority = priority;
            if (chosenVia == BattleSim.CommandVia.Direct)
            {
                Issue(currentSide);
                return;
            }

            // 받을 성벽: 왼쪽·오른쪽·맞은편 (북·동·남·서를 함께 적어 헷갈리지 않게, Battle.md 3.1-1)
            int left = (currentSide + 3) % 4;
            int right = (currentSide + 1) % 4;
            int opposite = (currentSide + 2) % 4;
            step = MenuStep.Target;
            SetOptions(chosenVia == BattleSim.CommandVia.Messenger ? "전령 — 어느 성벽으로" : "소리 지르기 — 어느 성벽으로",
                new[] { $"{SideShort[left]}쪽 (왼쪽)", $"{SideShort[right]}쪽 (오른쪽)", $"{SideShort[opposite]}쪽 (맞은편)" },
                new Action[] { () => Issue(left), () => Issue(right), () => Issue(opposite) });
        }

        void Issue(int targetSide)
        {
            CloseMenu();
            CommandChosen?.Invoke(chosenVia, chosenPriority, targetSide);
        }

        const float MenuWidth = 420f;
        const float MenuSpacing = 58f;      // 항목 사이 (화면 1080 기준)
        const float MenuLineGap = 34f;      // 세로선과 글자 사이
        const float MenuGrow = 0.45f;       // 가장 가까운 항목이 커지는 정도 (1.45배)
        const float MenuGrowReach = 70f;    // 이 거리(위아래) 안에서만 커진다
        Image menuLine;
        readonly List<RectTransform> menuItems = new List<RectTransform>();

        void SetOptions(string title, string[] labels, Action[] actions)
        {
            foreach (GameObject button in menuButtons)
            {
                UnityEngine.Object.Destroy(button);
            }

            menuButtons.Clear();
            menuChoices.Clear();
            menuItems.Clear();
            menuBands.Clear();
            menuBandHovers.Clear();
            menuTitle.text = title;

            float height = labels.Length * MenuSpacing;
            float top = height * 0.5f;
            Anchor(menuLine.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(3f, height + 20f), new Vector2(0.5f, 0.5f));
            Anchor(menuTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(MenuLineGap, top + 14f), new Vector2(MenuWidth, 30f), new Vector2(0f, 0f));
            // 사용자 참고 그림(D-072): 항목이 주인공 오른쪽으로 부채꼴처럼 휜다 — 가운데 항목이 가장 바깥
            float mid = (labels.Length - 1) * 0.5f;
            for (int i = 0; i < labels.Length; i++)
            {
                Action action = actions[i];
                float k = mid > 0f ? (i - mid) / mid : 0f;
                float bulge = MenuArc * (1f - k * k);
                menuButtons.Add(NewMenuItem(i, labels[i], () => action(), top - (i + 0.5f) * MenuSpacing, bulge));
                menuChoices.Add(action);
            }

            menuPanel.SetActive(true);
        }

        const float MenuArc = 46f;          // 부채꼴로 가운데 항목이 바깥으로 나가는 거리
        const float MenuSlant = 6f;         // 띠가 비스듬히 기운 각도(도)
        static readonly string[] CircledNumbers = { "①", "②", "③", "④", "⑤", "⑥", "⑦", "⑧", "⑨" };
        static readonly Color MenuBand = new Color(0.06f, 0.07f, 0.07f, 0.72f);
        static readonly Color MenuBandHover = new Color(0.12f, 0.16f, 0.2f, 0.88f);
        readonly List<Image> menuBands = new List<Image>();
        readonly List<Image> menuBandHovers = new List<Image>();   // 그림 띠일 때 마우스 올림 띠 (커질수록 진해진다), 없으면 null
        static readonly Vector4 MenuBandBorder = new Vector4(64f, 8f, 64f, 8f);   // K-019 README의 9칸 경계 (왼쪽·아래·오른쪽·위)

        // 참고 그림처럼: 비스듬한 어두운 띠 + 동그라미 번호 + 글자 + 금색 밑줄. 띠는 누르는 자리, 크기·밝기는 UpdateMenuHover가 바꾼다
        // K-019 그림(붓 띠·번호 표식·붓 밑줄)이 있으면 그것으로, 없으면 예전 단색 모양
        GameObject NewMenuItem(int index, string label, Action onClick, float y, float bulge)
        {
            var go = new GameObject("Item", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(menuPanel.transform, false);
            var rect = (RectTransform)go.transform;
            float width = MenuWidth - MenuLineGap - MenuArc;
            Anchor(rect, new Vector2(0f, 0.5f), new Vector2(MenuLineGap + bulge, y), new Vector2(width, MenuSpacing - 10f), new Vector2(0f, 0.5f));
            rect.localRotation = Quaternion.Euler(0f, 0f, MenuSlant);   // 오른쪽이 조금 올라간 비스듬한 띠 (그림은 수평 원본이라 여기서만 기울인다)
            var band = go.GetComponent<Image>();
            band.color = MenuBand;
            Image bandHover = null;
            Sprite bandArt = HudStyle.Art("Command/command_band_normal", MenuBandBorder);
            Sprite hoverArt = HudStyle.Art("Command/command_band_hover", MenuBandBorder);
            if (bandArt != null)
            {
                band.sprite = bandArt;
                band.type = Image.Type.Sliced;
                band.color = Color.white;   // 그림 색 그대로 (어두운 색을 다시 곱하면 두 번 어두워진다)
                if (hoverArt != null)
                {
                    bandHover = NewImage(go.transform, "Band Hover", new Color(1f, 1f, 1f, 0f));
                    bandHover.sprite = hoverArt;
                    bandHover.type = Image.Type.Sliced;
                    bandHover.raycastTarget = false;
                    var hoverRect = bandHover.rectTransform;
                    hoverRect.anchorMin = Vector2.zero;
                    hoverRect.anchorMax = Vector2.one;
                    hoverRect.offsetMin = hoverRect.offsetMax = Vector2.zero;
                }
            }

            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());

            // 번호 표식: 그림(① ~ ⑥)이 있으면 그림, 없으면 동그라미 숫자 글자
            Image numberArt = index < 6 ? HudStyle.ArtImage(go.transform, "Number", $"Command/command_number_{index + 1}", new Vector2(34f, 34f)) : null;
            if (numberArt != null)
            {
                Anchor(numberArt.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(34f, 34f), new Vector2(0f, 0.5f));
            }
            else
            {
                Text number = NewText(go.transform, "Number", 26, TextAnchor.MiddleCenter);
                Anchor(number.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(34f, 34f), new Vector2(0f, 0.5f));
                number.text = index < CircledNumbers.Length ? CircledNumbers[index] : (index + 1).ToString();
                number.color = HudStyle.Brass;
            }

            Text text = NewText(go.transform, "Text", 24, TextAnchor.MiddleLeft);
            Anchor(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(numberArt != null ? 52f : 50f, 2f), new Vector2(width - 60f, 34f), new Vector2(0f, 0.5f));
            text.text = label;
            AddMenuOutline(text);

            // 금색 붓 자국 밑줄: 그림이면 끝이 가늘어지는 붓 한 장, 없으면 띠 아래쪽을 따라 두 겹 선
            float underlineWidth = Mathf.Min(width - 50f, text.preferredWidth + 40f);
            Image brush = HudStyle.ArtImage(go.transform, "Underline", "Command/command_underline", new Vector2(underlineWidth, 8f));
            if (brush != null)
            {
                Anchor(brush.rectTransform, new Vector2(0f, 0f), new Vector2(44f, 0f), new Vector2(underlineWidth, 8f), new Vector2(0f, 0f));
            }
            else
            {
                Image underline = NewImage(go.transform, "Underline", HudStyle.Brass);
                underline.raycastTarget = false;
                Anchor(underline.rectTransform, new Vector2(0f, 0f), new Vector2(44f, 3f), new Vector2(underlineWidth, 2f), new Vector2(0f, 0f));
                Image tail = NewImage(go.transform, "Underline Tail", new Color(HudStyle.Brass.r, HudStyle.Brass.g, HudStyle.Brass.b, 0.45f));
                tail.raycastTarget = false;
                Anchor(tail.rectTransform, new Vector2(0f, 0f), new Vector2(44f, 0f), new Vector2(width - 50f, 1f), new Vector2(0f, 0f));
            }

            menuItems.Add(rect);
            menuBands.Add(band);
            menuBandHovers.Add(bandHover);
            return go;
        }

        static void AddMenuOutline(Text text)
        {
            var outline = text.gameObject.AddComponent<Outline>();   // 들판 위에서도 읽히게 (상자 바탕이 없으므로)
            outline.effectColor = new Color(0.04f, 0.03f, 0.02f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // 메뉴를 주인공 화면 위치 오른쪽에 둔다 (BattleEntry가 열려 있는 동안 매 프레임). screen = 화면 화소 좌표
        // 화면 비율 자리(0~1)에 기준점을 두고 오른쪽으로 60만큼 — 화면 크기·화면 배율과 상관없이 주인공 옆에 붙는다
        public void PlaceMenu(Vector2 screen)
        {
            if (!MenuOpen || menuPanel == null) return;
            var rect = (RectTransform)menuPanel.transform;
            Vector2 viewport = new Vector2(screen.x / Mathf.Max(1, Screen.width), screen.y / Mathf.Max(1, Screen.height));
            rect.anchorMin = rect.anchorMax = viewport;
            rect.anchoredPosition = new Vector2(60f, 0f);
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
                if (hover != null) hover.color = new Color(1f, 1f, 1f, glow);   // 그림 띠: 마우스 올림 띠가 위에 겹쳐 밝아진다
                else if (i < menuBands.Count && menuBands[i].sprite == null) menuBands[i].color = Color.Lerp(MenuBand, MenuBandHover, glow);   // 커질수록 밝은 띠
            }
        }

        public void PushNotice(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            float now = Time.unscaledTime;
            NoticeKind kind = ClassifyNotice(text);

            // 같은 문장이 짧은 시간 안에 다시 오면 기존 줄에 ×N을 붙이고 시간을 늘린다
            for (int i = 0; i < noticeLines.Count; i++)
            {
                NoticeLine line = noticeLines[i];
                if (line.Until >= now && now - line.Last <= NoticeRepeatWindow && line.Text == text)
                {
                    line.Count++;
                    line.Last = now;
                    line.Until = now + NoticeSeconds[(int)kind];
                    noticesDirty = true;
                    RefreshNotices();
                    return;
                }
            }

            noticeLines.Add(new NoticeLine { Text = text, Kind = kind, Count = 1, Last = now, Until = now + NoticeSeconds[(int)kind] });
            while (noticeLines.Count > NoticeMaxLines)
            {
                noticeLines.RemoveAt(LeastImportantNotice());
            }

            noticesDirty = true;
            RefreshNotices();
        }

        // 넘칠 때 밀어낼 줄: 가장 덜 중요한 종류 중 가장 오래된 것
        int LeastImportantNotice()
        {
            int pick = 0;
            for (int i = 1; i < noticeLines.Count; i++)
            {
                if (noticeLines[i].Kind > noticeLines[pick].Kind) pick = i;
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
            {
                if (text.IndexOf(word, StringComparison.Ordinal) >= 0) return true;
            }

            return false;
        }

        static string ColorTag(Color color) => "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">";

        // 매 프레임 불린다 — 사라진 줄이 있거나 새 알림이 왔을 때만 글을 다시 만든다
        void RefreshNotices()
        {
            float now = Time.unscaledTime;
            for (int i = noticeLines.Count - 1; i >= 0; i--)
            {
                if (noticeLines[i].Until < now)
                {
                    noticeLines.RemoveAt(i);
                    noticesDirty = true;
                }
            }

            if (!noticesDirty) return;
            noticesDirty = false;
            noticeText.Clear();
            for (int i = 0; i < noticeLines.Count; i++)
            {
                NoticeLine line = noticeLines[i];
                if (i > 0) noticeText.Append('\n');
                noticeText.Append(NoticeColorTags[(int)line.Kind]).Append(line.Text);
                if (line.Count > 1) noticeText.Append(" ×").Append(line.Count);
                noticeText.Append("</color>");
            }

            notices.text = noticeText.ToString();

            // 받침 크기를 글에 맞춘다 (글이 없으면 숨김)
            bool any = noticeLines.Count > 0;
            noticeBacking.gameObject.SetActive(any);
            if (any)
            {
                Canvas.ForceUpdateCanvases();
                float width = Mathf.Min(notices.preferredWidth, notices.rectTransform.rect.width) + 16f;
                float height = Mathf.Min(notices.preferredHeight, notices.rectTransform.rect.height) + 8f;
                noticeBacking.rectTransform.sizeDelta = new Vector2(width, height);
            }
        }
    }
}
