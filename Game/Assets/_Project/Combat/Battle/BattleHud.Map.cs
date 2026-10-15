using System;
using HumanLastRampart.Core.Campaign;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 2번 지도 모드 표시 (BattleMap.md 2장): 위쪽 가운데 띠에 지도 상태·쉬움 시간 정지
    public sealed partial class BattleHud
    {
        Text mapBanner;
        bool mapMode;
        GameObject globalPanel;
        Text globalStatus;

        // 지도 전체 명령 (MAP-2, Battle.md 5.1-1): 4면에 한 번에. 성벽 명령은 지워진다 (D-031 K3, D-039)
        public event Action<TargetPriority> GlobalCommandChosen;

        // 대포 조준 (MAP-3, Battle.md 5.2): 벽 고르기 → 대포 고르기 → 들판 찍기
        public event Action<int> CannonSideChosen;     // 0~3
        public event Action<int> CannonNumberChosen;   // 1~4
        public event Action CannonClearChosen;
        GameObject cannonPanel;
        Text cannonTitle;
        Text cannonGuide;
        readonly Text[] cannonLabels = new Text[4];
        readonly Image[] sideTabs = new Image[4];

        // 마법 (MAP-4, Battle.md 5.3): 고르고 들판 클릭
        public event Action<int> SpellChosen;
        GameObject spellPanel;
        Text spellTitle;
        readonly System.Collections.Generic.List<Text> spellLabels = new System.Collections.Generic.List<Text>();
        readonly System.Collections.Generic.List<Image> spellButtons = new System.Collections.Generic.List<Image>();

        public void SetMapMode(bool open, bool frozen)
        {
            if (mapBanner == null)
            {
                Image band = NewImage(root.transform, "Map Banner", HudStyle.Background);
                Anchor(band.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(620f, 56f), new Vector2(0.5f, 1f));
                mapBanner = NewText(band.transform, "Text", 26, TextAnchor.MiddleCenter);
                Anchor(mapBanner.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 50f), new Vector2(0.5f, 0.5f));
            }

            if (open != mapMode)
            {
                nextRefresh = 0f;   // 안내 문구 바로 바꾸기
            }

            mapMode = open;
            mapBanner.transform.parent.gameObject.SetActive(open);
            if (globalPanel == null) BuildGlobalPanel();
            globalPanel.SetActive(open);
            if (cannonPanel == null) BuildCannonPanel();
            cannonPanel.SetActive(open);
            if (spellPanel != null) spellPanel.SetActive(open);
            if (!open)
            {
                foreach (Text label in groupLabels) label.gameObject.SetActive(false);
            }
            if (open)
            {
                mapBanner.text = frozen ? "지도 — 시간 멈춤 (쉬움)" : "지도 — 시간이 흐릅니다 (현실)";
            }
        }

        void BuildGlobalPanel()
        {
            Image panel = NewImage(root.transform, "Global Command", HudStyle.Panel);
            HudStyle.Border(panel);
            globalPanel = panel.gameObject;
            // 왼쪽 위 상태판 바로 아래 (상태판을 접고 펴면 LayoutStatus가 따라 옮긴다)
            Anchor(panel.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -30f - StatusHeight), new Vector2(330f, 490f), new Vector2(0f, 1f));
            Text title = NewText(panel.transform, "Title", 26, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(310f, 40f), new Vector2(0.5f, 1f));
            title.text = "전체 명령 (4면)";
            for (int i = 0; i < BattleSim.PriorityNames.Length; i++)
            {
                var priority = (TargetPriority)i;
                Button button = NewButton(panel.transform, BattleSim.PriorityNames[i], () => GlobalCommandChosen?.Invoke(priority));
                Anchor((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, -56f - i * 54f), new Vector2(300f, 48f), new Vector2(0.5f, 1f));
            }

            globalStatus = NewText(panel.transform, "Status", 18, TextAnchor.UpperLeft);
            Anchor(globalStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(300f, 96f), new Vector2(0.5f, 0f));
            globalPanel.SetActive(false);
        }

        void BuildCannonPanel()
        {
            Image panel = NewImage(root.transform, "Cannon Panel", HudStyle.Panel);
            HudStyle.Border(panel);
            cannonPanel = panel.gameObject;
            Anchor(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-20f, 40f), new Vector2(380f, 520f), new Vector2(1f, 0.5f));
            cannonTitle = NewText(panel.transform, "Title", 26, TextAnchor.MiddleCenter);
            Anchor(cannonTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(360f, 40f), new Vector2(0.5f, 1f));

            string[] sides = { "북", "동", "남", "서" };
            for (int i = 0; i < 4; i++)
            {
                int side = i;
                Button tab = NewButton(panel.transform, sides[i], () => CannonSideChosen?.Invoke(side));
                Anchor((RectTransform)tab.transform, new Vector2(0f, 1f), new Vector2(14f + i * 89f, -60f), new Vector2(82f, 48f), new Vector2(0f, 1f));
                sideTabs[i] = tab.GetComponent<Image>();
            }

            for (int i = 0; i < 4; i++)
            {
                int number = i + 1;
                Button button = NewButton(panel.transform, "", () => CannonNumberChosen?.Invoke(number));
                Anchor((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, -122f - i * 62f), new Vector2(350f, 54f), new Vector2(0.5f, 1f));
                cannonLabels[i] = button.GetComponentInChildren<Text>();
            }

            Button clear = NewButton(panel.transform, "조준 해제", () => CannonClearChosen?.Invoke());
            Anchor((RectTransform)clear.transform, new Vector2(0.5f, 1f), new Vector2(0f, -378f), new Vector2(350f, 50f), new Vector2(0.5f, 1f));
            cannonGuide = NewText(panel.transform, "Guide", 18, TextAnchor.UpperLeft);
            Anchor(cannonGuide.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(350f, 80f), new Vector2(0.5f, 0f));
            cannonGuide.horizontalOverflow = HorizontalWrapMode.Wrap;
            cannonPanel.SetActive(false);
        }

        // 고른 벽의 대포 4문 상태 (BattleEntry가 그리기 뒤 부름)
        public void RefreshCannons(BattleSim sim, int side, int selected)
        {
            if (cannonPanel == null || !mapMode) return;
            string[] names = { "북", "동", "남", "서" };
            cannonTitle.text = $"대포 — {names[side]}쪽";
            for (int i = 0; i < 4; i++)
            {
                sideTabs[i].color = i == side ? HudStyle.ButtonActive : HudStyle.Button;
                cannonLabels[i].text = $"{i + 1}번: 없음";
            }

            string guide = "대포를 고른 뒤 들판을 왼쪽 클릭 (사거리 원 안)";
            for (int c = 0; c < sim.CannonCount; c++)
            {
                BattleSim.CannonInfo info = sim.GetCannon(c);
                if (info.Side != side || info.Number < 1 || info.Number > 4) continue;
                string state = !info.Alive ? "운용병 없음" : !info.HasAim ? "자동(밀집한 곳)" : info.Idle ? "조준 — 대기(근처에 적 없음)" : "조준 — 포격 중";
                cannonLabels[info.Number - 1].text = (c == selected ? "▶ " : "") + $"{info.Number}번: {state}";
                if (c == selected) guide = $"{info.Number}번 선택됨 — 들판을 왼쪽 클릭해 조준점 지정 (사거리 {info.Range:0}m)";
            }

            cannonGuide.text = guide;
        }

        // 마법 패널 (지도 아래 가운데). 정의 수만큼 버튼
        public void RefreshSpells(BattleSim sim, int selected)
        {
            if (!mapMode) return;
            if (spellPanel == null)
            {
                Image panel = NewImage(root.transform, "Spell Panel", HudStyle.Panel);
                HudStyle.Border(panel);
                spellPanel = panel.gameObject;
                Anchor(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(660f, 120f), new Vector2(0.5f, 0f));
                spellTitle = NewText(panel.transform, "Title", 20, TextAnchor.MiddleCenter);
                Anchor(spellTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(640f, 30f), new Vector2(0.5f, 1f));
                for (int i = 0; i < sim.SpellCount; i++)
                {
                    int index = i;
                    Button button = NewButton(panel.transform, "", () => SpellChosen?.Invoke(index));
                    Anchor((RectTransform)button.transform, new Vector2(0f, 0f), new Vector2(16f + i * 318f, 12f), new Vector2(306f, 60f), new Vector2(0f, 0f));
                    spellLabels.Add(button.GetComponentInChildren<Text>());
                    spellButtons.Add(button.GetComponent<Image>());
                }
            }

            spellPanel.SetActive(true);
            spellTitle.text = !sim.WizardPresent ? "마법사 — 이번 전투에 없음"
                : selected >= 0 ? "들판을 왼쪽 클릭 (보라 점선 = 성벽에서 150m 안)" : "마법사 — 마법을 고른 뒤 들판 클릭";
            for (int i = 0; i < spellLabels.Count; i++)
            {
                BattleSim.SpellInfo info = sim.GetSpell(i);
                string state = info.Ready ? "준비" : $"{info.CooldownLeft:0}초";
                spellLabels[i].text = (i == selected ? "▶ " : "") + $"{info.Name} — {state}";
                spellButtons[i].color = i == selected ? HudStyle.MagicActive : info.Ready && sim.WizardPresent ? HudStyle.Magic : HudStyle.ButtonDisabled;
            }
        }

        // 망원경으로 확인한 무리 표시 (MAP-5): 지도 위 무리 가운데에 "북쪽 약 320 · 오우거 1"
        readonly System.Collections.Generic.List<Text> groupLabels = new System.Collections.Generic.List<Text>();

        public void RefreshGroupLabels(BattleSim sim, Camera camera)
        {
            string[] names = { "북쪽", "동쪽", "남쪽", "서쪽" };
            var groups = sim.SpottedGroups;
            float scale = 1080f / Mathf.Max(1, Screen.height);
            for (int i = 0; i < groups.Count; i++)
            {
                if (groupLabels.Count <= i)
                {
                    Text t = NewText(root.transform, "Group Label", 20, TextAnchor.MiddleCenter);
                    t.color = new Color(0.6f, 0.95f, 1f);
                    t.rectTransform.anchorMin = t.rectTransform.anchorMax = Vector2.zero;
                    t.rectTransform.sizeDelta = new Vector2(320f, 30f);
                    groupLabels.Add(t);
                }

                BattleSim.GroupSummary g = groups[i];
                Vector3 screen = camera.WorldToScreenPoint(g.Center);
                Text label = groupLabels[i];
                label.gameObject.SetActive(mapMode);
                label.rectTransform.anchoredPosition = new Vector2(screen.x, screen.y + 26f) * scale;
                label.text = $"{names[g.Side]} 약 {g.Alive}{(g.Special > 0 ? $" · 특수 {g.Special}" : "")}";
            }

            for (int i = groups.Count; i < groupLabels.Count; i++) groupLabels[i].gameObject.SetActive(false);
        }

        // 지금 성벽별 명령 (패널 아래)
        void RefreshGlobalStatus(BattleSim sim)
        {
            if (globalStatus == null || !mapMode) return;
            string[] names = { "북", "동", "남", "서" };
            var lines = new string[4];
            for (int side = 0; side < 4; side++)
            {
                lines[side] = $"{names[side]}: {BattleSim.PriorityNames[(int)sim.PriorityOf(side)]}";
            }

            globalStatus.text = string.Join("\n", lines);
        }
    }
}