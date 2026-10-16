using System;
using HumanLastRampart.Core.Campaign;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 2번 지도 모드 표시 (BattleMap.md 2장): 위쪽 가운데 띠에 지도 상태·쉬움 시간 정지
    // 창 모양은 K-021 시안(짙은 올리브 창 + 황동 1px 테두리 + 안쪽 어두운 띠, 제목 왼쪽 정렬 + 가는 선), 표식은 K-020 그림
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
                // K-021 위쪽 상태 띠: 짙은 바탕 + 왼쪽 황동 막대
                Image band = NewImage(root.transform, "Map Banner", HudStyle.PhaseBand);
                Anchor(band.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(620f, 56f), new Vector2(0.5f, 1f));
                Image bar = NewImage(band.transform, "Bar", HudStyle.Brass);
                bar.raycastTarget = false;
                Anchor(bar.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(3f, 56f), new Vector2(0f, 0.5f));
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
                foreach (GroupMarker marker in groupMarkers) marker.Root.gameObject.SetActive(false);
                foreach (Text c in compass) if (c != null) c.gameObject.SetActive(false);
                foreach (Image back in compassBacks) if (back != null) back.gameObject.SetActive(false);
                if (heroMarker != null) heroMarker.gameObject.SetActive(false);
                if (groupPopup != null) groupPopup.SetActive(false);
            }
            if (open)
            {
                mapBanner.text = preparing ? "전투 준비 — 명령·대포 조준을 정하세요"
                    : frozen ? "지도 — 시간 멈춤 (쉬움)" : "지도 — 시간이 흐릅니다 (현실)";
            }
        }

        // 병사 명령 (D-071): 받을 곳 고르기(전체·북·동·남·서) → 명령 고르기. 전체는 4면 한 번에(MAP-2)
        // 성벽 하나를 고르면 WallCommandChosen — 전투 준비 중에는 바로, 전투 중에는 있는 자리 성벽만 바로·나머지는 전령(BattleEntry)
        public event Action<int, TargetPriority> WallCommandChosen;
        int commandTarget = -1;   // -1 = 전체
        readonly Image[] commandTabs = new Image[5];
        Text globalTitle;

        // 명령 6개 앞 아이콘 (K-026, TargetPriority 순서)
        static readonly string[] PriorityIcons = { "priority_low_hp", "priority_high_hp", "priority_nearest", "priority_climbing", "priority_special", "priority_dense" };

        void BuildGlobalPanel()
        {
            Image panel = NewImage(root.transform, "Global Command", HudStyle.MapPanel);
            HudStyle.MapFrame(panel);
            globalPanel = panel.gameObject;
            // 왼쪽 위 상태판 바로 아래 (상태판을 접고 펴면 LayoutStatus가 따라 옮긴다)
            Anchor(panel.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -30f - StatusHeight), new Vector2(330f, 550f), new Vector2(0f, 1f));
            globalTitle = NewText(panel.transform, "Title", 24, TextAnchor.MiddleLeft);
            globalTitle.color = HudStyle.Text;
            Anchor(globalTitle.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(262f, 36f), new Vector2(0f, 1f));
            MapDivider(panel.transform, -50f);

            string[] targets = { "전체", "북쪽", "동쪽", "남쪽", "서쪽" };   // 대포 패널의 북·동·남·서 탭과 구분
            for (int i = 0; i < targets.Length; i++)
            {
                int target = i - 1;
                Button tab = NewButton(panel.transform, targets[i], () => commandTarget = target, true);
                Anchor((RectTransform)tab.transform, new Vector2(0f, 1f), new Vector2(12f + i * 62f, -58f), new Vector2(58f, 44f), new Vector2(0f, 1f));
                tab.GetComponentInChildren<Text>().fontSize = 20;
                commandTabs[i] = tab.GetComponent<Image>();
            }

            for (int i = 0; i < BattleSim.PriorityNames.Length; i++)
            {
                var priority = (TargetPriority)i;
                Button button = NewButton(panel.transform, BattleSim.PriorityNames[i], () =>
                {
                    if (commandTarget < 0) GlobalCommandChosen?.Invoke(priority);
                    else WallCommandChosen?.Invoke(commandTarget, priority);
                }, true);
                Anchor((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, -112f - i * 54f), new Vector2(300f, 48f), new Vector2(0.5f, 1f));
                // 시안처럼 왼쪽 정렬 + 앞에 아이콘 (글자는 그대로 — 시험이 글자로 찾는다)
                Image icon = i < PriorityIcons.Length ? HudStyle.ArtImage(button.transform, "Icon", "Icons/" + PriorityIcons[i], new Vector2(32f, 32f)) : null;
                if (icon != null)
                {
                    Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(32f, 32f), new Vector2(0f, 0.5f));
                    Text label = button.GetComponentInChildren<Text>();
                    label.alignment = TextAnchor.MiddleLeft;
                    label.rectTransform.offsetMin = new Vector2(52f, 0f);
                }
            }

            globalStatus = NewText(panel.transform, "Status", 18, TextAnchor.UpperLeft);
            globalStatus.color = HudStyle.TextDim;
            Anchor(globalStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(300f, 96f), new Vector2(0.5f, 0f));
            MakeCollapsible(panel, globalTitle.gameObject, 550f);
            globalPanel.SetActive(false);
        }

        // K-021 제목 아래 가는 선 (창 안쪽 띠 안에서)
        void MapDivider(Transform panel, float y)
        {
            Image line = NewImage(panel, "Divider", HudStyle.MapDivider);
            line.raycastTarget = false;
            var rect = line.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(14f, y - 1f);
            rect.offsetMax = new Vector2(-14f, y);
        }

        // 창 접기 (D-073): 오른쪽 위 ▲/▼ 단추 — 접으면 제목 줄만 남는다. 지도를 넓게 보려고
        const float CollapsedHeight = 56f;

        void MakeCollapsible(Image panel, GameObject title, float fullHeight)
        {
            var content = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in panel.transform)
                if (child.gameObject != title && child.name != "Frame" && child.name != "Inset") content.Add(child.gameObject);   // 테두리는 접어도 남긴다

            bool collapsed = false;
            Button toggle = null;
            toggle = NewButton(panel.transform, "▲", () =>
            {
                collapsed = !collapsed;
                foreach (GameObject go in content) go.SetActive(!collapsed);
                var size = panel.rectTransform.sizeDelta;
                panel.rectTransform.sizeDelta = new Vector2(size.x, collapsed ? CollapsedHeight : fullHeight);
                toggle.GetComponentInChildren<Text>().text = collapsed ? "▼" : "▲";
            }, true);
            toggle.GetComponent<Image>().color = new Color(HudStyle.MapButton.r, HudStyle.MapButton.g, HudStyle.MapButton.b, 0f);   // 시안: 바탕 없이 테두리만
            toggle.GetComponentInChildren<Text>().color = HudStyle.Brass;
            Anchor((RectTransform)toggle.transform, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(40f, 40f), new Vector2(1f, 1f));
        }

        // 전투 준비 (D-071): 전투 시작 전에 지도가 열린 채 시간이 멈춰 있다. 명령·대포 조준을 정하고 [전투 시작]
        public event Action BattleStartChosen;
        GameObject startButton;
        bool preparing;

        public void SetPreparing(bool value)
        {
            preparing = value;
            if (startButton == null)
            {
                Button button = NewButton(root.transform, "전투 시작", () => BattleStartChosen?.Invoke());
                Anchor((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(300f, 56f), new Vector2(0.5f, 1f));
                // K-021: 황동 바탕 + 밝은 황동 테두리 + 어두운 글 (화면의 주된 다음 행동)
                Image face = button.GetComponent<Image>();
                face.color = HudStyle.Brass;
                HudStyle.SetBorderColor(face, HudStyle.PrimaryEdge);
                Text label = button.GetComponentInChildren<Text>();
                label.fontSize = 26;
                label.fontStyle = FontStyle.Bold;
                label.color = HudStyle.PrimaryText;
                startButton = button.gameObject;
            }

            startButton.SetActive(value);
            nextRefresh = 0f;
        }

        void BuildCannonPanel()
        {
            Image panel = NewImage(root.transform, "Cannon Panel", HudStyle.MapPanel);
            HudStyle.MapFrame(panel);
            cannonPanel = panel.gameObject;
            // 위 끝 기준(접으면 위로 접힌다). 위치는 예전(가운데 기준 +40)과 같다: 위 끝 = 가운데 + 300
            Anchor(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-20f, 300f), new Vector2(380f, 520f), new Vector2(1f, 1f));
            cannonTitle = NewText(panel.transform, "Title", 26, TextAnchor.MiddleLeft);
            Anchor(cannonTitle.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(300f, 40f), new Vector2(0f, 1f));
            MapDivider(panel.transform, -54f);

            string[] sides = { "북", "동", "남", "서" };
            for (int i = 0; i < 4; i++)
            {
                int side = i;
                Button tab = NewButton(panel.transform, sides[i], () => CannonSideChosen?.Invoke(side), true);
                Anchor((RectTransform)tab.transform, new Vector2(0f, 1f), new Vector2(14f + i * 89f, -62f), new Vector2(82f, 48f), new Vector2(0f, 1f));
                sideTabs[i] = tab.GetComponent<Image>();
            }

            for (int i = 0; i < 4; i++)
            {
                int number = i + 1;
                Button button = NewButton(panel.transform, "", () => CannonNumberChosen?.Invoke(number), true);
                Anchor((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, -122f - i * 62f), new Vector2(350f, 54f), new Vector2(0.5f, 1f));
                cannonLabels[i] = button.GetComponentInChildren<Text>();
                cannonLabels[i].alignment = TextAnchor.MiddleLeft;
                cannonLabels[i].rectTransform.offsetMin = new Vector2(14f, 0f);
                cannonLabels[i].fontSize = 22;
            }

            Button clear = NewButton(panel.transform, "조준 해제", () => CannonClearChosen?.Invoke(), true);
            Anchor((RectTransform)clear.transform, new Vector2(0.5f, 1f), new Vector2(0f, -378f), new Vector2(350f, 50f), new Vector2(0.5f, 1f));
            clear.GetComponent<Image>().color = new Color(HudStyle.MapButton.r, HudStyle.MapButton.g, HudStyle.MapButton.b, 0.25f);   // 시안: 옅은 바탕
            // 안내 글: 어두운 받침 + 왼쪽 황동 막대 (K-021 cannon-guide)
            Image note = NewImage(panel.transform, "Guide Backing", HudStyle.MapNote);
            note.raycastTarget = false;
            Anchor(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(350f, 84f), new Vector2(0.5f, 0f));
            Image noteBar = NewImage(note.transform, "Bar", HudStyle.Brass);
            noteBar.raycastTarget = false;
            Anchor(noteBar.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(2f, 84f), new Vector2(0f, 0.5f));
            cannonGuide = NewText(panel.transform, "Guide", 18, TextAnchor.UpperLeft);
            Anchor(cannonGuide.rectTransform, new Vector2(0.5f, 0f), new Vector2(4f, 12f), new Vector2(330f, 78f), new Vector2(0.5f, 0f));
            cannonGuide.horizontalOverflow = HorizontalWrapMode.Wrap;
            MakeCollapsible(panel, cannonTitle.gameObject, 520f);
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
                HudStyle.SetMapButton(sideTabs[i], i == side);
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
                Image panel = NewImage(root.transform, "Spell Panel", HudStyle.MapPanel);
                HudStyle.MapFrame(panel);
                spellPanel = panel.gameObject;
                Anchor(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(660f, 120f), new Vector2(0.5f, 0f));
                spellTitle = NewText(panel.transform, "Title", 20, TextAnchor.MiddleLeft);
                Anchor(spellTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(624f, 30f), new Vector2(0.5f, 1f));
                for (int i = 0; i < sim.SpellCount; i++)
                {
                    int index = i;
                    Button button = NewButton(panel.transform, "", () => SpellChosen?.Invoke(index), true);
                    Anchor((RectTransform)button.transform, new Vector2(0f, 0f), new Vector2(16f + i * 318f, 12f), new Vector2(306f, 60f), new Vector2(0f, 0f));
                    Text label = button.GetComponentInChildren<Text>();
                    label.alignment = TextAnchor.MiddleLeft;
                    label.rectTransform.offsetMin = new Vector2(14f, 0f);
                    spellLabels.Add(label);
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
                string key = i == 0 ? "[Q] " : i == 1 ? "[W] " : "";   // D-073 단축키
                spellLabels[i].text = (i == selected ? "▶ " : "") + $"{key}{info.Name} — {state}";
                spellButtons[i].color = i == selected ? HudStyle.MagicActive : info.Ready && sim.WizardPresent ? HudStyle.Magic : HudStyle.ButtonDisabled;
                HudStyle.SetBorderColor(spellButtons[i], i == selected ? HudStyle.Brass : HudStyle.MapButtonEdge);
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

        // ── 병력 묶음 (D-072): 지도 위 10명 묶음 표시. 누르면 갈 성벽이 바로 뜨고, 끌어다 다른 성벽에 놓아도 된다 ──
        // K-020 그림: 궁수(청록)·총병(자주) 깃발 + 숫자, 고르면 모서리 테두리, 옮기는 중이면 옆에 화살표. 그림이 없으면 예전 글자(궁10)
        public event Action<int> GroupDestinationChosen;   // 고른 성벽 0~3

        sealed class GroupMarker
        {
            public RectTransform Root;
            public Image Selected;     // 고른 묶음 테두리
            public Image Flag;         // null이면 글자 표시
            public Image Plate;        // 깃발에 그려진 '10'을 덮는 숫자 판 (10명이 아닐 때)
            public Image PlateInner;
            public Image Arrow;        // 옮기는 중 화살표
            public Text Count;         // 그림이면 숫자만, 없으면 예전 글자(궁10→북)
        }

        // 깃발 그림의 숫자 판 자리 (K-020 build.ps1: 바깥 13×9 at 17,21 / 안쪽 11×7, 좌상단 원점)
        static readonly Color PlateEdge = new Color32(0x10, 0x1b, 0x20, 0xff);
        static readonly Color ArcherShade = new Color32(0x24, 0x53, 0x60, 0xff);
        static readonly Color GunnerShade = new Color32(0x59, 0x36, 0x4f, 0xff);

        readonly System.Collections.Generic.List<GroupMarker> groupMarkers = new System.Collections.Generic.List<GroupMarker>();
        readonly System.Collections.Generic.List<Vector2> groupMarkerScreen = new System.Collections.Generic.List<Vector2>();
        readonly System.Collections.Generic.List<int> groupMarkerIndex = new System.Collections.Generic.List<int>();
        GameObject groupPopup;
        Text groupPopupTitle;
        readonly Button[] groupPopupButtons = new Button[4];

        GroupMarker NewGroupMarker()
        {
            var go = new GameObject("Group Marker", typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var marker = new GroupMarker { Root = (RectTransform)go.transform };
            marker.Root.anchorMin = marker.Root.anchorMax = Vector2.zero;
            marker.Root.sizeDelta = new Vector2(64f, 32f);
            marker.Selected = HudStyle.ArtImage(go.transform, "Selected", "Map/group_selected", new Vector2(40f, 40f));
            marker.Flag = HudStyle.ArtImage(go.transform, "Flag", "Map/flag_archer", new Vector2(32f, 32f));
            if (marker.Flag == null)
            {
                if (marker.Selected != null) UnityEngine.Object.Destroy(marker.Selected.gameObject);
                marker.Selected = null;
                Text t = NewText(go.transform, "Label", 17, TextAnchor.MiddleCenter);
                Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 24f), new Vector2(0.5f, 0.5f));
                var outline = t.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                marker.Count = t;
                return marker;
            }

            marker.Plate = NewImage(marker.Flag.transform, "Count Plate", PlateEdge);
            marker.Plate.raycastTarget = false;
            Anchor(marker.Plate.rectTransform, new Vector2(0f, 1f), new Vector2(17f, -21f), new Vector2(13f, 9f));
            marker.PlateInner = NewImage(marker.Plate.transform, "Inner", ArcherShade);
            marker.PlateInner.raycastTarget = false;
            Anchor(marker.PlateInner.rectTransform, new Vector2(0f, 1f), new Vector2(1f, -1f), new Vector2(11f, 7f));
            marker.Count = NewText(marker.Plate.transform, "Count", 10, TextAnchor.MiddleCenter);
            marker.Count.fontStyle = FontStyle.Bold;
            marker.Count.verticalOverflow = VerticalWrapMode.Overflow;
            Anchor(marker.Count.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(13f, 9f), new Vector2(0.5f, 0.5f));
            // 화살표: 깃발 오른쪽 4화소 띄움 (K-020 README 배치안)
            marker.Arrow = HudStyle.ArtImage(go.transform, "Arrow", "Map/move_east", new Vector2(24f, 24f));
            if (marker.Arrow != null) Anchor(marker.Arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(32f, 0f), new Vector2(24f, 24f), new Vector2(0.5f, 0.5f));
            return marker;
        }

        public void RefreshGroupMarkers(BattleSim sim, Camera camera, int selected, Vector2? dragScreen)
        {
            groupMarkerScreen.Clear();
            groupMarkerIndex.Clear();
            int used = 0;
            for (int g = 0; g < sim.GroupCount; g++)
            {
                BattleSim.GroupInfo info = sim.GetGroup(g);
                if (!info.Movable || info.Alive <= 0) continue;
                if (groupMarkers.Count <= used) groupMarkers.Add(NewGroupMarker());

                // 성벽 바깥쪽으로 4m 띄워 성을 덮지 않게. 글자는 짧게(궁10) — 묶음끼리 4~5m 간격이라 길면 겹친다
                // 같은 성벽 묶음은 성벽 따라 순서대로 바깥 4m·안쪽 마당 5m 번갈아 — 북·남쪽은 글자가 가로로 붙어 겹쳤고,
                // 바깥 8m로 두 줄을 쌓으면 남쪽 줄이 아래 마법 창에 가렸다
                Vector3 world = new Vector3(info.Center.x, info.Center.y, info.Center.z);
                if (info.Side >= 0)
                {
                    Unity.Mathematics.float3 n = WallGeometry.Outward(info.Side);
                    float lift = MarkerRow(sim, g, info) % 2 == 0 ? 4f : -5f;
                    world += new Vector3(n.x, 0f, n.z) * lift;
                }

                Vector3 screen = camera.WorldToScreenPoint(world);
                if (g == selected && dragScreen.HasValue) screen = dragScreen.Value;   // 끄는 중이면 마우스를 따라온다
                GroupMarker marker = groupMarkers[used++];
                marker.Root.gameObject.SetActive(mapMode);
                // 화면 비율 자리에 기준점 (화면 크기와 상관없이 지도 위 그 자리)
                marker.Root.anchorMin = marker.Root.anchorMax = new Vector2(screen.x / Mathf.Max(1, Screen.width), screen.y / Mathf.Max(1, Screen.height));
                marker.Root.anchoredPosition = Vector2.zero;
                bool chosen = g == selected;
                if (marker.Flag != null)
                {
                    bool gunner = !string.IsNullOrEmpty(info.UnitName) && info.UnitName.Contains("총");
                    Sprite flag = HudStyle.Art(gunner ? "Map/flag_gunner" : "Map/flag_archer");
                    if (flag != null) marker.Flag.sprite = flag;
                    marker.Flag.color = info.Moving && !chosen ? new Color(0.78f, 0.78f, 0.78f, 1f) : Color.white;   // 옮기는 중은 조금 어둡게
                    bool full = info.Alive == BattleSim.GroupSize;   // 10명이면 그림의 '10' 그대로
                    marker.Plate.gameObject.SetActive(!full);
                    marker.PlateInner.color = gunner ? GunnerShade : ArcherShade;
                    marker.Count.text = info.Alive.ToString();
                    if (marker.Selected != null) marker.Selected.gameObject.SetActive(chosen);
                    if (marker.Arrow != null)
                    {
                        marker.Arrow.gameObject.SetActive(info.Moving);
                        if (info.Moving)
                        {
                            Sprite arrow = HudStyle.Art(MoveArrow(camera, world, screen, info.TargetSide));
                            if (arrow != null) marker.Arrow.sprite = arrow;
                        }
                    }
                }
                else
                {
                    string shortName = string.IsNullOrEmpty(info.UnitName) ? "병" : info.UnitName.Substring(0, 1);
                    marker.Count.text = info.Moving ? $"{shortName}{info.Alive}→{SideShort[info.TargetSide]}" : $"{shortName}{info.Alive}";
                    marker.Count.color = chosen ? HudStyle.Brass : info.Moving ? HudStyle.TextDim : new Color(0.75f, 0.9f, 1f);
                }

                groupMarkerScreen.Add(new Vector2(screen.x, screen.y));
                groupMarkerIndex.Add(g);
            }

            for (int i = used; i < groupMarkers.Count; i++) groupMarkers[i].Root.gameObject.SetActive(false);
            ShowGroupPopup(sim, selected, dragScreen.HasValue);
        }

        // 옮기는 중 화살표: 갈 성벽 가운데가 화면에서 어느 쪽인지 (위·아래·왼쪽·오른쪽 중 가까운 것)
        static string MoveArrow(Camera camera, Vector3 world, Vector3 screen, int targetSide)
        {
            if (targetSide < 0 || targetSide > 3) return "Map/move_east";
            Unity.Mathematics.float3 n = WallGeometry.Outward(targetSide);
            float reach = Mathf.Max(5f, new Vector2(world.x, world.z).magnitude);   // 성 가운데에서 지금 거리 ≈ 성벽까지 거리
            Vector3 goal = camera.WorldToScreenPoint(new Vector3(n.x, world.y, n.z) * reach);
            float dx = goal.x - screen.x, dy = goal.y - screen.y;
            if (Mathf.Abs(dx) >= Mathf.Abs(dy)) return dx >= 0f ? "Map/move_east" : "Map/move_west";
            return dy >= 0f ? "Map/move_north" : "Map/move_south";
        }

        // 주인공 위치 (밤사이 계획 4): 지도에서 어느 성벽 명령이 바로 가고 어느 쪽이 전령인지 알 수 있게
        Text heroMarker;

        public void RefreshHeroMarker(Camera camera, Vector3 heroFeet, int heroSide, bool preparing)
        {
            if (heroMarker == null)
            {
                heroMarker = NewText(root.transform, "Hero Marker", 18, TextAnchor.MiddleCenter);
                heroMarker.rectTransform.sizeDelta = new Vector2(220f, 26f);
                heroMarker.color = HudStyle.Brass;
                var outline = heroMarker.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            }

            Vector3 screen = camera.WorldToScreenPoint(heroFeet);
            heroMarker.rectTransform.anchorMin = heroMarker.rectTransform.anchorMax = new Vector2(screen.x / Mathf.Max(1, Screen.width), screen.y / Mathf.Max(1, Screen.height));
            heroMarker.rectTransform.anchoredPosition = new Vector2(0f, 18f);
            // 준비 중에는 모든 성벽이 바로라 '바로 전달' 안내를 빼고 위치만
            heroMarker.text = preparing ? "◆ 주인공" : $"◆ 주인공 ({SideShort[heroSide]}쪽만 바로, 다른 성벽은 전령)";
            heroMarker.gameObject.SetActive(mapMode);
        }

        // 동서남북 표시 (D-073): 성벽 바깥 14m에 큰 글자 — 지도를 옮기거나 넓혀도 어느 쪽인지 바로 보이게
        // K-020 나침반 받침(64px 그림)을 글자 뒤에 2배로 깐다 — 받침 가운데 어두운 칸(약 20px → 40px)에 40 글자가 들어간다
        const float CompassBackSize = 128f;
        readonly Text[] compass = new Text[4];
        readonly Image[] compassBacks = new Image[4];

        public void RefreshCompass(Camera camera, float castleHalf)
        {
            string[] names = { "북", "동", "남", "서" };
            for (int side = 0; side < 4; side++)
            {
                if (compass[side] == null)
                {
                    Text t = NewText(root.transform, "Compass " + names[side], 40, TextAnchor.MiddleCenter);
                    t.rectTransform.sizeDelta = new Vector2(80f, 56f);
                    t.color = HudStyle.Brass;
                    t.fontStyle = FontStyle.Bold;
                    var outline = t.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                    outline.effectDistance = new Vector2(2f, -2f);
                    t.text = names[side];
                    t.transform.SetAsFirstSibling();   // 창들보다 뒤
                    compass[side] = t;
                    Image back = HudStyle.ArtImage(root.transform, "Compass Back " + names[side], "Map/compass_back", new Vector2(CompassBackSize, CompassBackSize));
                    if (back != null) back.transform.SetAsFirstSibling();   // 글자보다도 뒤
                    compassBacks[side] = back;
                }

                // 가까이 볼 때(성 가까이, 전투 준비)는 성 마당 가운데에서 8m — 바깥 14m는 화면 끝 창들에 가렸다
                Unity.Mathematics.float3 n = WallGeometry.Outward(side);
                float distance = camera.orthographicSize <= 45f ? 8f : castleHalf + 14f;
                Vector3 screen = camera.WorldToScreenPoint(new Vector3(n.x, 0f, n.z) * distance);
                // 화면 밖이면 가장자리에 붙여 둔다 (방향은 그대로 알 수 있게)
                var viewport = new Vector2(Mathf.Clamp(screen.x / Mathf.Max(1, Screen.width), 0.04f, 0.96f), Mathf.Clamp(screen.y / Mathf.Max(1, Screen.height), 0.06f, 0.94f));
                compass[side].rectTransform.anchorMin = compass[side].rectTransform.anchorMax = viewport;
                compass[side].rectTransform.anchoredPosition = Vector2.zero;
                compass[side].gameObject.SetActive(mapMode);
                Image backImage = compassBacks[side];
                if (backImage != null)
                {
                    backImage.rectTransform.anchorMin = backImage.rectTransform.anchorMax = viewport;
                    backImage.rectTransform.anchoredPosition = Vector2.zero;
                    backImage.gameObject.SetActive(mapMode);
                }
            }
        }

        // 같은 성벽에 선 옮길 수 있는 묶음 중 성벽 따라(오른쪽 방향) 몇 번째인지 — 번갈아 띄우기용
        static int MarkerRow(BattleSim sim, int group, BattleSim.GroupInfo info)
        {
            Unity.Mathematics.float3 r = WallGeometry.Right(info.Side);
            float mine = Unity.Mathematics.math.dot(info.Center, r);
            int order = 0;
            for (int g = 0; g < sim.GroupCount; g++)
            {
                if (g == group) continue;
                BattleSim.GroupInfo other = sim.GetGroup(g);
                if (!other.Movable || other.Alive <= 0 || other.Side != info.Side || other.Moving) continue;
                if (Unity.Mathematics.math.dot(other.Center, r) < mine) order++;
            }

            return order;
        }

        // 화면 화소 위치 가까이(28화소)의 묶음 번호, 없으면 -1
        public int GroupAtScreen(Vector2 screen)
        {
            int best = -1;
            float bestSq = 28f * 28f;
            for (int i = 0; i < groupMarkerScreen.Count; i++)
            {
                float d = (groupMarkerScreen[i] - screen).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = groupMarkerIndex[i]; }
            }

            return best;
        }

        void ShowGroupPopup(BattleSim sim, int selected, bool dragging)
        {
            if (groupPopup == null)
            {
                Image panel = NewImage(root.transform, "Group Popup", HudStyle.MapPanel);
                HudStyle.MapFrame(panel);
                groupPopup = panel.gameObject;
                Anchor(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(560f, 110f), new Vector2(0.5f, 0f));
                groupPopupTitle = NewText(panel.transform, "Title", 20, TextAnchor.MiddleCenter);
                Anchor(groupPopupTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(540f, 30f), new Vector2(0.5f, 1f));
                for (int i = 0; i < 4; i++)
                {
                    int side = i;
                    Button button = NewButton(panel.transform, $"{SideShort[i]}쪽으로", () => GroupDestinationChosen?.Invoke(side), true);
                    Anchor((RectTransform)button.transform, new Vector2(0f, 0f), new Vector2(14f + i * 134f, 12f), new Vector2(126f, 52f), new Vector2(0f, 0f));
                    groupPopupButtons[i] = button;
                }
            }

            bool show = mapMode && selected >= 0 && selected < sim.GroupCount;
            groupPopup.SetActive(show);
            if (!show) return;
            BattleSim.GroupInfo info = sim.GetGroup(selected);
            groupPopupTitle.text = dragging ? $"{info.UnitName} {info.Alive}명 — 갈 성벽 위에 놓으세요"
                : $"{info.UnitName} {info.Alive}명 ({(info.Side >= 0 ? SideShort[info.Side] + "쪽" : "이동 중")}) — 어디로 보낼까요";
            for (int i = 0; i < 4; i++) groupPopupButtons[i].gameObject.SetActive(info.Side >= 0 && i != info.Side);
        }

        // 지금 성벽별 명령 (패널 아래)
        void RefreshGlobalStatus(BattleSim sim)
        {
            if (globalStatus == null || !mapMode) return;
            string[] names = { "북", "동", "남", "서" };
            for (int i = 0; i < commandTabs.Length; i++)
                if (commandTabs[i] != null) HudStyle.SetMapButton(commandTabs[i], i - 1 == commandTarget);
            globalTitle.text = commandTarget < 0 ? "병사 명령 — 4면 전체" : $"병사 명령 — {names[commandTarget]}쪽 성벽";
            var lines = new string[4];
            for (int side = 0; side < 4; side++)
            {
                lines[side] = $"{names[side]}: {BattleSim.PriorityNames[(int)sim.PriorityOf(side)]}";
            }

            globalStatus.text = string.Join("\n", lines);
        }
    }
}
