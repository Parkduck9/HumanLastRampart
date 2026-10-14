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
        const float NoticeSeconds = 6f;

        GameObject menuPanel;
        Text menuTitle;
        readonly List<GameObject> menuButtons = new List<GameObject>();
        readonly List<Action> menuChoices = new List<Action>();
        MenuStep step;
        BattleSim.CommandVia chosenVia;
        TargetPriority chosenPriority;
        int currentSide;

        Text notices;
        readonly List<(string text, float until)> noticeLines = new List<(string, float)>();

        // 명령을 내릴 때 (방법, 명령어, 받을 성벽)
        public event Action<BattleSim.CommandVia, TargetPriority, int> CommandChosen;

        public bool MenuOpen => step != MenuStep.Closed;

        void BuildCommandMenu()
        {
            Image panel = NewImage(root.transform, "Command Menu", new Color(0.05f, 0.07f, 0.1f, 0.85f));
            menuPanel = panel.gameObject;
            Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(560f, 520f), new Vector2(0.5f, 0.5f));
            menuTitle = NewText(panel.transform, "Title", 26, TextAnchor.MiddleCenter);
            Anchor(menuTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(540f, 50f), new Vector2(0.5f, 1f));
            menuPanel.SetActive(false);

            notices = NewText(root.transform, "Notices", 22, TextAnchor.LowerLeft);
            Anchor(notices.rectTransform, new Vector2(0f, 0f), new Vector2(20f, 80f), new Vector2(1100f, 200f));
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
            noticeLines.Add((text, Time.unscaledTime + NoticeSeconds));
            if (noticeLines.Count > 6)
            {
                noticeLines.RemoveAt(0);
            }

            RefreshNotices();
        }

        void RefreshNotices()
        {
            noticeLines.RemoveAll(n => n.until < Time.unscaledTime);
            var lines = new string[noticeLines.Count];
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = noticeLines[i].text;
            }

            notices.text = string.Join("\n", lines);
        }
    }
}
