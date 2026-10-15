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
            Image panel = NewImage(root.transform, "Command Menu", HudStyle.Panel);
            HudStyle.Border(panel);
            menuPanel = panel.gameObject;
            Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(560f, 520f), new Vector2(0.5f, 0.5f));
            menuTitle = NewText(panel.transform, "Title", 26, TextAnchor.MiddleCenter);
            Anchor(menuTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(540f, 50f), new Vector2(0.5f, 1f));
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

        void SetOptions(string title, string[] labels, Action[] actions)
        {
            foreach (GameObject button in menuButtons)
            {
                UnityEngine.Object.Destroy(button);
            }

            menuButtons.Clear();
            menuChoices.Clear();
            menuTitle.text = title;
            for (int i = 0; i < labels.Length; i++)
            {
                Action action = actions[i];
                Button button = NewButton(menuPanel.transform, $"{i + 1}. {labels[i]}", () => action());
                Anchor((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, -80f - i * 70f), new Vector2(500f, 60f), new Vector2(0.5f, 1f));
                menuButtons.Add(button.gameObject);
                menuChoices.Add(action);
            }

            menuPanel.SetActive(true);
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
