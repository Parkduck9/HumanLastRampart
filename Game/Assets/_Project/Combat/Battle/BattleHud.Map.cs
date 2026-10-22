using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Combat.Battle
{
    // 2번 지도 화면 (BattleMap.md 2장, K-047): 구역은 셋 — 위 계기 띠(지도 상태는 띠 안에) · 왼쪽 명령판 · 오른쪽 포병·마법판.
    // 성과 적 무리가 있는 가운데는 비워 둔다 (S-01·S-02). 병사 묶음 창은 고를 때만 띠 아래에 잠깐
    public sealed partial class BattleHud
    {
        bool mapMode, mapFrozen;

        // ── 왼쪽 명령판: 받을 곳(전체·북·동·남·서) → 명령 6개 (D-071) ──
        public event Action<TargetPriority> GlobalCommandChosen;     // 4면 한 번에 (MAP-2). 성벽 명령은 지워진다 (D-031 K3, D-039)
        public event Action<int, TargetPriority> WallCommandChosen;  // 성벽 하나 — 준비 중에는 바로, 전투 중에는 있는 자리 성벽만 바로·나머지는 전령
        GameObject commandBoard, commandBody;
        int commandTarget = -1;   // -1 = 전체
        readonly Button[] commandTabs = new Button[5];
        readonly Button[] orderButtons = new Button[6];
        readonly Image[] wallOrderIcons = new Image[4];
        Text heroRule;

        // 명령 6개 아이콘 (K-026, TargetPriority 순서)과 판에 보이는 짧은 이름 (열쇠는 BattleSim.PriorityNames — 시험이 그것으로 누른다)
        static readonly string[] PriorityIcons = { "priority_low_hp", "priority_high_hp", "priority_nearest", "priority_climbing", "priority_special", "priority_dense" };
        static readonly string[] PriorityShort = { "약한 적", "튼튼한 적", "가까운 적", "오르는 적", "특수 적", "밀집한 곳" };

        // ── 오른쪽 포병·마법판 (MAP-3·4) ──
        public event Action<int> CannonPicked;         // 대포 번호 (sim 안 순서 0~)
        public event Action CannonClearChosen;
        public event Action<int> SpellChosen;
        GameObject fireBoard, fireBody, spellSection;
        Text cannonTitle, fireGuide;
        Button cannonClear;
        readonly Button[] cannonRows = new Button[4];
        readonly Text[] cannonStates = new Text[4];
        readonly Image[] cannonStateIcons = new Image[4];
        readonly int[] rowCannon = { -1, -1, -1, -1 };
        readonly List<Button> cannonMarkers = new List<Button>();
        readonly List<Button> spellButtons = new List<Button>();
        readonly List<Image> spellCooldowns = new List<Image>();
        readonly List<Text> spellKeys = new List<Text>();
        int spellSelected = -1;

        public void SetMapMode(bool open, bool frozen)
        {
            mapFrozen = frozen;
            if (commandBoard == null) BuildCommandBoard();
            if (fireBoard == null) BuildFireBoard();
            if (open != mapMode)
            {
                nextRefresh = 0f;   // 띠 상태·키 안내 바로 바꾸기
                mapMode = open;
                PlaceNotices(open);
                if (!open)
                {
                    foreach (Text label in groupLabels) label.gameObject.SetActive(false);
                    foreach (GroupMarker marker in groupMarkers) marker.Root.gameObject.SetActive(false);
                    foreach (Button marker in cannonMarkers) marker.gameObject.SetActive(false);
                    foreach (Text c in compass) if (c != null) c.gameObject.SetActive(false);
                    foreach (Image back in compassBacks) if (back != null) back.gameObject.SetActive(false);
                    if (heroMarker != null) heroMarker.gameObject.SetActive(false);
                    if (groupPopup != null) groupPopup.SetActive(false);
                }
            }

            UiAppear.Toggle(commandBoard, open, new Vector2(-UiTokens.S24, 0f));
            UiAppear.Toggle(fireBoard, open, new Vector2(UiTokens.S24, 0f));
        }

        // 나무 판 하나: 세로 목록 + 내용에 맞춘 높이. 머리 줄(제목 + 접기)과 몸통을 돌려준다
        Image Board(string name, Vector2 anchor, Vector2 position, float width, string title, out Text titleText, out GameObject body)
        {
            Image panel = Ui.Panel(root.transform, name, UiPanel.Wood);
            Ui.Place(panel.rectTransform, anchor, position, new Vector2(width, 0f), anchor);
            var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(24, 24, 16, 24);
            column.spacing = UiTokens.S8;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            HorizontalLayoutGroup head = Ui.Row(panel.transform, "Head", UiTokens.S8);
            Ui.Size(head, -1f, 48f);
            titleText = Ui.Label(head.transform, "Title", title, UiTokens.H2, UiTokens.Brass, TextAnchor.MiddleLeft, true);
            Ui.Size(titleText, width - 48f - 48f - UiTokens.S8, 48f);
            VerticalLayoutGroup inner = Ui.Column(panel.transform, "Body", UiTokens.S8);
            GameObject bodyObject = inner.gameObject;
            body = bodyObject;
            Button fold = null;
            fold = Ui.IconButton(head.transform, "접기:" + name, UiArt.IconUp, () =>
            {
                bool open = !bodyObject.activeSelf;
                bodyObject.SetActive(open);
                Ui.SetIcon(fold, open ? UiArt.IconUp : UiArt.IconDown);
            }, 48f);
            return panel;
        }

        void BuildCommandBoard()
        {
            Image panel = Board("Command Board", new Vector2(0f, 1f), new Vector2(Edge, -(Edge + BandHeight + UiTokens.S16)), 416f, "병사 명령", out _, out commandBody);
            commandBoard = panel.gameObject;

            HorizontalLayoutGroup tabs = Ui.Row(commandBody.transform, "Targets", UiTokens.S8);
            string[] keys = { "전체", "북쪽", "동쪽", "남쪽", "서쪽" };   // 시험이 이 열쇠로 누른다
            string[] labels = { "전체", "북", "동", "남", "서" };
            for (int i = 0; i < keys.Length; i++)
            {
                int target = i - 1;
                commandTabs[i] = Ui.Button(tabs.transform, keys[i], labels[i], () => { commandTarget = target; nextRefresh = 0f; }, UiButtonKind.Tab);
                Ui.Size(commandTabs[i], 64f, 48f);
            }

            var grid = Ui.Rect(commandBody.transform, "Orders").gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(180f, 52f);
            grid.spacing = new Vector2(UiTokens.S8, UiTokens.S8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            for (int i = 0; i < BattleSim.PriorityNames.Length && i < orderButtons.Length; i++)
            {
                var priority = (TargetPriority)i;
                Button button = Ui.Button(grid.transform, BattleSim.PriorityNames[i], PriorityShort[i], () =>
                {
                    if (commandTarget < 0) GlobalCommandChosen?.Invoke(priority);
                    else WallCommandChosen?.Invoke(commandTarget, priority);
                    nextRefresh = 0f;
                });
                Image icon = Ui.HudIcon(button.transform.Find("Content"), "Icon", "Icons/" + PriorityIcons[i], 24f);
                if (icon != null)
                {
                    Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(UiTokens.S16, 0f), icon.rectTransform.sizeDelta, new Vector2(0f, 0.5f));
                    Text label = Ui.LabelOf(button);
                    label.alignment = TextAnchor.MiddleLeft;
                    label.rectTransform.offsetMin = new Vector2(UiTokens.S16 + icon.rectTransform.sizeDelta.x + UiTokens.S8, 0f);
                }

                orderButtons[i] = button;
            }

            // 지금 4면 명령: 북 [아이콘]  동 [아이콘] …
            HorizontalLayoutGroup now = Ui.Row(commandBody.transform, "Now", UiTokens.S8);
            Ui.Size(now, -1f, 32f);
            for (int side = 0; side < 4; side++)
            {
                Text letter = Ui.Label(now.transform, "Side", SideNames[side], UiTokens.Small, UiTokens.BodyDim, TextAnchor.MiddleCenter, true);
                Ui.Size(letter, 24f, 32f);
                wallOrderIcons[side] = Ui.HudIcon(now.transform, "Order " + SideNames[side], "Icons/" + PriorityIcons[0], 24f);
                if (side < 3)
                {
                    RectTransform gap = Ui.Rect(now.transform, "Gap");
                    Ui.Size(gap, UiTokens.S16, 32f);
                }
            }

            heroRule = Ui.Label(commandBody.transform, "Hero Rule", "", UiTokens.Small, UiTokens.BodyDim);
            Ui.Size(heroRule, -1f, 28f);
            commandBoard.SetActive(false);
        }

        void BuildFireBoard()
        {
            Image panel = Board("Fire Board", new Vector2(1f, 1f), new Vector2(-Edge, -(Edge + BandHeight + UiTokens.S16)), 400f, "대포", out cannonTitle, out fireBody);
            fireBoard = panel.gameObject;

            for (int i = 0; i < 4; i++)
            {
                int row = i;
                Button button = Ui.Button(fireBody.transform, "대포 " + (i + 1), "", () => { if (rowCannon[row] >= 0) CannonPicked?.Invoke(rowCannon[row]); }, UiButtonKind.Tab);
                Ui.Size(button, -1f, 48f);
                Transform content = button.transform.Find("Content");
                Text number = Ui.Label(content, "Number", (i + 1).ToString(), UiTokens.Text, UiTokens.Brass, TextAnchor.MiddleCenter, true);
                Anchor(number.rectTransform, new Vector2(0f, 0.5f), new Vector2(UiTokens.S16, 0f), new Vector2(24f, 40f), new Vector2(0f, 0.5f));
                Image cannon = Ui.HudIcon(content, "Cannon", "Icons/unit_cannon", 24f);
                if (cannon != null) Anchor(cannon.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 0f), cannon.rectTransform.sizeDelta, new Vector2(0f, 0.5f));
                Image state = Ui.Icon(content, "State Icon", UiArt.IconAuto, 1f, UiTokens.BodyDim);
                Anchor(state.rectTransform, new Vector2(0f, 0.5f), new Vector2(96f, 0f), state.rectTransform.sizeDelta, new Vector2(0f, 0.5f));
                cannonStateIcons[i] = state;
                cannonStates[i] = Ui.Label(content, "Cannon " + (i + 1) + " State", "", UiTokens.Small, UiTokens.Body);
                Anchor(cannonStates[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(144f, 0f), new Vector2(180f, 40f), new Vector2(0f, 0.5f));
                cannonRows[i] = button;
            }

            cannonClear = Ui.Button(fireBody.transform, "조준 해제", "조준 풀기", () => CannonClearChosen?.Invoke());
            Ui.Size(cannonClear, -1f, 48f);
            fireGuide = Ui.Label(fireBody.transform, "Guide", "", UiTokens.Small, UiTokens.BodyDim);
            Ui.Size(fireGuide, -1f, 28f);

            // 마법사 칸 (마법사가 없으면 칸째 숨긴다 — S-21)
            VerticalLayoutGroup spells = Ui.Column(fireBody.transform, "Wizard", UiTokens.S8);
            spellSection = spells.gameObject;
            Image line = Ui.Image(spells.transform, "Rule", UiTokens.BrassDark);
            Ui.Size(line, -1f, UiTokens.Dot);
            Text heading = Ui.Label(spells.transform, "Heading", "마법사", UiTokens.Text, UiTokens.Brass, TextAnchor.MiddleLeft, true);
            Ui.Size(heading, -1f, 32f);
            Ui.Row(spells.transform, "Spells", UiTokens.S8);
            BuildLampSection(fireBody.transform);   // 등불 밝기·빛줄기 방향 (BattleHud.Lamp.cs)
            fireBoard.SetActive(false);
        }

        // 전투 준비 (D-071): 지도가 열린 채 시간이 멈춰 있다. 명령·대포 조준을 정하고 [전투 시작]
        public event Action BattleStartChosen;
        Button startButton;
        bool preparing;

        public void SetPreparing(bool value)
        {
            preparing = value;
            if (startButton == null)
            {
                startButton = Ui.Button(root.transform, "전투 시작", "전투 시작", () => BattleStartChosen?.Invoke(), UiButtonKind.Primary);
                Anchor((RectTransform)startButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -Edge), new Vector2(320f, 64f), new Vector2(0.5f, 1f));
                startButton.gameObject.SetActive(false);
            }

            UiAppear.Toggle(startButton.gameObject, value, new Vector2(0f, UiTokens.S16));
            nextRefresh = 0f;
            if (slipColumn != null) PlaceNotices(mapMode);
        }

        // 칠 수 있는 들판 면 비트 → "북·동" (K-042)
        static string ReachText(int reach)
        {
            var parts = new List<string>(3);
            for (int side = 0; side < 4; side++) if ((reach & (1 << side)) != 0) parts.Add(SideNames[side]);
            return string.Join("·", parts);
        }

        // 고른 벽의 대포 4문 상태 (BattleEntry가 그리기 뒤 부름). 상태는 아이콘으로, 낱말은 작게 (S-26)
        public void RefreshCannons(BattleSim sim, int side, int selected)
        {
            if (fireBoard == null || !mapMode) return;
            cannonTitle.text = $"{SideFull[side]} 대포";
            for (int i = 0; i < 4; i++) rowCannon[i] = -1;
            bool aimed = false;
            string guide = "지도의 대포를 눌러 고르기";
            for (int c = 0; c < sim.CannonCount; c++)
            {
                BattleSim.CannonInfo info = sim.GetCannon(c);
                if (info.Side != side || info.Number < 1 || info.Number > 4) continue;
                int row = info.Number - 1;
                rowCannon[row] = c;
                string icon = !info.Alive ? UiArt.IconClose : !info.HasAim ? UiArt.IconAuto : info.Idle ? UiArt.IconIdle : UiArt.IconFlag;
                Color tint = !info.Alive ? UiTokens.Danger : !info.HasAim ? UiTokens.BodyDim : info.Idle ? UiTokens.BodyDim : UiTokens.Brass;
                if (info.Igniting && Mathf.Repeat(Time.unscaledTime, 0.3f) < 0.18f) tint = UiTokens.Fire;   // 불붙이는 중 — 곧 발사 (K-046)
                cannonStateIcons[row].sprite = UiArt.Get(icon);
                cannonStateIcons[row].rectTransform.sizeDelta = UiArt.Size(cannonStateIcons[row].sprite);
                cannonStateIcons[row].color = tint;
                cannonStates[row].text = !info.Alive ? "병사 없음" : !info.HasAim ? "자동" : info.Idle ? "대기" : "포격";
                cannonStates[row].color = info.Alive ? UiTokens.Body : UiTokens.DangerLight;
                if (c == selected)
                {
                    aimed = info.HasAim;
                    guide = $"들판을 눌러 조준 · {ReachText(info.Reach)} 들판 {info.Range:0}m";
                }
            }

            for (int i = 0; i < 4; i++)
            {
                cannonRows[i].gameObject.SetActive(rowCannon[i] >= 0);   // 없는 대포 줄은 감춘다 (S-21)
                Ui.SetOn(cannonRows[i], rowCannon[i] >= 0 && rowCannon[i] == selected);
            }

            cannonClear.interactable = aimed;
            if (spellSelected < 0) fireGuide.text = guide;
        }

        // 지도 위 대포 표시 (K-037): 대포마다 누를 수 있는 작은 단추(대포 그림 + 번호). 누르면 그 대포를 고른다
        // 판들보다 뒤(앞 형제)에 둬서 판 위를 누를 때는 판이 먼저 받는다
        public void RefreshCannonMarkers(BattleSim sim, Camera camera, int selected)
        {
            for (int c = 0; c < sim.CannonCount; c++)
            {
                if (cannonMarkers.Count <= c)
                {
                    int index = c;
                    Button button = Ui.Button(root.transform, "대포 표시 " + c, null, () => CannonPicked?.Invoke(index), UiButtonKind.Icon, UiArt.IconFlag);
                    ((RectTransform)button.transform).sizeDelta = new Vector2(48f, 48f);
                    Transform content = button.transform.Find("Content");
                    Sprite art = UiArt.Hud("Icons/unit_cannon");
                    Transform icon = content.Find("Icon");
                    if (art != null && icon != null)
                    {
                        var image = icon.GetComponent<Image>();
                        image.sprite = art;
                        image.color = Color.white;
                        image.rectTransform.sizeDelta = Ui.IntegerSize(art, 32f);
                    }

                    Text numberLabel = Ui.Outlined(Ui.Label(content, "Number", "", UiTokens.Small, UiTokens.Body, TextAnchor.LowerRight, true));
                    HumanLastRampart.UI.UiFonts.Bold(numberLabel);
                    Ui.Stretch(numberLabel.rectTransform, UiTokens.Dot);
                    button.transform.SetAsFirstSibling();
                    cannonMarkers.Add(button);
                }

                BattleSim.CannonInfo info = sim.GetCannon(c);
                Button marker = cannonMarkers[c];
                Vector3 screen = camera.WorldToScreenPoint(info.Position);
                bool show = mapMode && screen.z > 0f;
                marker.gameObject.SetActive(show);
                if (!show) continue;
                var markerRect = (RectTransform)marker.transform;
                markerRect.anchorMin = markerRect.anchorMax = Viewport(screen);
                markerRect.anchoredPosition = Vector2.zero;
                Ui.SetOn(marker, c == selected);
                var feel = marker.GetComponent<UiButtonFeel>();
                feel.FaceColor = info.Alive ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.75f);   // 운용병이 없으면 흐리게
                Text number = marker.transform.Find("Content/Number").GetComponent<Text>();
                number.text = info.Number.ToString();
                number.color = c == selected ? UiTokens.BrassLight : UiTokens.Body;
            }

            for (int c = sim.CannonCount; c < cannonMarkers.Count; c++) cannonMarkers[c].gameObject.SetActive(false);
        }

        // 마법 (지도 오른쪽 판 아래). 재사용 대기는 단추 위로 시계 방향으로 걷히는 어둠 + 남은 초 (S-26)
        public void RefreshSpells(BattleSim sim, int selected)
        {
            if (!mapMode || fireBoard == null) return;
            spellSelected = selected;
            Transform row = spellSection.transform.Find("Spells");
            if (spellButtons.Count == 0)
            {
                for (int i = 0; i < sim.SpellCount; i++)
                {
                    int index = i;
                    BattleSim.SpellInfo info = sim.GetSpell(i);
                    Button button = Ui.Button(row, "마법 " + (i + 1), info.Name, () => SpellChosen?.Invoke(index), UiButtonKind.Tab);
                    Ui.Size(button, 172f, 56f);
                    Text label = Ui.LabelOf(button);
                    label.alignment = TextAnchor.MiddleLeft;
                    label.color = info.IsFire ? UiTokens.Fire : UiTokens.Blast;
                    Image cool = Ui.Image(button.transform, "Cooldown", UiTokens.Alpha(UiTokens.Outline, 0.72f));
                    cool.type = Image.Type.Filled;
                    cool.fillMethod = Image.FillMethod.Radial360;
                    cool.fillOrigin = (int)Image.Origin360.Top;
                    cool.fillClockwise = false;
                    Ui.Stretch(cool.rectTransform, UiTokens.Dot);
                    cool.transform.SetSiblingIndex(1);   // 바탕 위, 글 아래
                    Text key = Ui.Label(button.transform.Find("Content"), "Key", "", UiTokens.Small, UiTokens.BodyDim, TextAnchor.MiddleRight);
                    Ui.Stretch(key.rectTransform);
                    key.rectTransform.offsetMax = new Vector2(-UiTokens.S16, 0f);
                    spellButtons.Add(button);
                    spellCooldowns.Add(cool);
                    spellKeys.Add(key);
                }
            }

            UiAppear.Toggle(spellSection, sim.WizardPresent);
            for (int i = 0; i < spellButtons.Count; i++)
            {
                BattleSim.SpellInfo info = sim.GetSpell(i);
                spellCooldowns[i].fillAmount = info.Ready || info.Cooldown <= 0f ? 0f : Mathf.Clamp01(info.CooldownLeft / info.Cooldown);
                spellKeys[i].text = !info.Ready ? $"{info.CooldownLeft:0}초" : i == 0 ? "Q" : i == 1 ? "W" : "";
                spellButtons[i].interactable = info.Ready && sim.WizardPresent;
                Ui.SetOn(spellButtons[i], i == selected);
                Ui.LabelOf(spellButtons[i]).color = info.IsFire ? UiTokens.Fire : UiTokens.Blast;
            }

            if (selected >= 0) fireGuide.text = $"들판을 눌러 마법 · 성벽에서 {BattleSim.SpellCastRange:0}m 안";
        }

        // 망원경으로 확인한 무리 표시 (MAP-5): 무리 가운데에 "약 320 · 특수 1" (방향은 지도에서 보인다)
        readonly List<Text> groupLabels = new List<Text>();

        public void RefreshGroupLabels(BattleSim sim, Camera camera)
        {
            var groups = sim.SpottedGroups;
            for (int i = 0; i < groups.Count; i++)
            {
                if (groupLabels.Count <= i)
                {
                    Text t = Ui.Outlined(Ui.Label(root.transform, "Group Label", "", UiTokens.Small, UiTokens.Enemy, TextAnchor.MiddleCenter, true));
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    t.rectTransform.sizeDelta = new Vector2(320f, 28f);
                    t.transform.SetAsFirstSibling();
                    groupLabels.Add(t);
                }

                BattleSim.GroupSummary g = groups[i];
                Vector3 screen = camera.WorldToScreenPoint(g.Center);
                Text label = groupLabels[i];
                label.gameObject.SetActive(mapMode);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = Viewport(screen);
                label.rectTransform.anchoredPosition = new Vector2(0f, 28f);
                label.text = g.Special > 0 ? $"약 {g.Alive} · 특수 {g.Special}" : $"약 {g.Alive}";
            }

            for (int i = groups.Count; i < groupLabels.Count; i++) groupLabels[i].gameObject.SetActive(false);
        }

        // ── 병력 묶음 (D-072): 지도 위 10명 묶음 표시. 누르면 갈 성벽이 바로 뜨고, 끌어다 다른 성벽에 놓아도 된다 ──
        // K-020 그림: 궁수(청록)·총병(자주) 깃발 + 숫자, 고르면 모서리 테두리, 옮기는 중이면 옆에 화살표
        public event Action<int> GroupDestinationChosen;        // 고른 성벽 0~3
        public event Action<int> WallGroupsDestinationChosen;   // 한 번에 옮기기 — 고른 묶음이 있는 성벽의 묶음 전부(20명 남김)
        readonly Button[] wallMoveButtons = new Button[4];

        sealed class GroupMarker
        {
            public RectTransform Root;
            public Image Selected;     // 고른 묶음 테두리
            public Image Flag;         // null이면 글자 표시
            public Image Plate;        // 깃발에 그려진 '10'을 덮는 숫자 판 (10명이 아닐 때)
            public Image PlateInner;
            public Image Arrow;        // 옮기는 중 화살표
            public Text Count;
        }

        // 깃발 그림의 숫자 판 자리 (K-020 build.ps1: 바깥 13×9 at 17,21 / 안쪽 11×7, 좌상단 원점)
        static readonly Color PlateEdge = new Color32(0x10, 0x1b, 0x20, 0xff);
        static readonly Color ArcherShade = new Color32(0x24, 0x53, 0x60, 0xff);
        static readonly Color GunnerShade = new Color32(0x59, 0x36, 0x4f, 0xff);

        readonly List<GroupMarker> groupMarkers = new List<GroupMarker>();
        readonly List<Vector2> groupMarkerScreen = new List<Vector2>();
        readonly List<Vector2> groupCenterScreen = new List<Vector2>();   // 깃발이 아닌 병사 무리 가운데
        readonly List<int> groupMarkerIndex = new List<int>();
        GameObject groupPopup;
        Text groupPopupTitle;
        readonly Button[] groupPopupButtons = new Button[4];

        GroupMarker NewGroupMarker()
        {
            var go = new GameObject("Group Marker", typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            go.transform.SetAsFirstSibling();   // 판들보다 뒤
            var marker = new GroupMarker { Root = (RectTransform)go.transform };
            marker.Root.anchorMin = marker.Root.anchorMax = Vector2.zero;
            marker.Root.sizeDelta = new Vector2(64f, 32f);
            marker.Selected = Ui.HudIcon(go.transform, "Selected", "Map/group_selected", 40f);
            marker.Flag = Ui.HudIcon(go.transform, "Flag", "Map/flag_archer", 32f);
            if (marker.Flag == null)
            {
                if (marker.Selected != null) UnityEngine.Object.Destroy(marker.Selected.gameObject);
                marker.Selected = null;
                Text t = Ui.Outlined(NewText(go.transform, "Label", UiTokens.Small, TextAnchor.MiddleCenter));
                Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 24f), new Vector2(0.5f, 0.5f));
                marker.Count = t;
                return marker;
            }

            marker.Plate = NewImage(marker.Flag.transform, "Count Plate", PlateEdge);
            Anchor(marker.Plate.rectTransform, new Vector2(0f, 1f), new Vector2(17f, -21f), new Vector2(13f, 9f));
            marker.PlateInner = NewImage(marker.Plate.transform, "Inner", ArcherShade);
            Anchor(marker.PlateInner.rectTransform, new Vector2(0f, 1f), new Vector2(1f, -1f), new Vector2(11f, 7f));
            // 그림 속 숫자 판(13×9 점)에 맞춘 작은 숫자 — 글자가 아니라 그림의 일부라 20 규칙 밖
            var count = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            count.transform.SetParent(marker.Plate.transform, false);
            marker.Count = count.GetComponent<Text>();
            marker.Count.font = UiFonts.Body;
            marker.Count.fontSize = 10;
            HumanLastRampart.UI.UiFonts.Bold(marker.Count);
            marker.Count.alignment = TextAnchor.MiddleCenter;
            marker.Count.color = UiTokens.Body;
            marker.Count.raycastTarget = false;
            marker.Count.horizontalOverflow = HorizontalWrapMode.Overflow;
            marker.Count.verticalOverflow = VerticalWrapMode.Overflow;
            Anchor(marker.Count.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(13f, 9f), new Vector2(0.5f, 0.5f));
            // 화살표: 깃발 오른쪽 4화소 띄움 (K-020 README 배치안)
            marker.Arrow = Ui.HudIcon(go.transform, "Arrow", "Map/move_east", 24f);
            if (marker.Arrow != null) Anchor(marker.Arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(32f, 0f), new Vector2(24f, 24f), new Vector2(0.5f, 0.5f));
            return marker;
        }

        public void RefreshGroupMarkers(BattleSim sim, Camera camera, int selected, Vector2? dragScreen)
        {
            groupMarkerScreen.Clear();
            groupCenterScreen.Clear();
            groupMarkerIndex.Clear();
            int used = 0;
            for (int g = 0; g < sim.GroupCount; g++)
            {
                BattleSim.GroupInfo info = sim.GetGroup(g);
                if (!info.Movable || info.Alive <= 0) continue;
                if (groupMarkers.Count <= used) groupMarkers.Add(NewGroupMarker());

                // 성벽 바깥쪽으로 4m 띄워 성을 덮지 않게. 같은 성벽 묶음은 성벽 따라 순서대로 바깥 4m·안쪽 마당 5m 번갈아
                Vector3 world = new Vector3(info.Center.x, info.Center.y, info.Center.z);
                if (info.Side >= 0)
                {
                    Unity.Mathematics.float3 n = WallGeometry.Outward(info.Side);
                    float lift = MarkerRow(sim, g, info) % 2 == 0 ? 4f : -5f;
                    world += new Vector3(n.x, 0f, n.z) * lift;
                }

                Vector3 center = camera.WorldToScreenPoint(new Vector3(info.Center.x, info.Center.y, info.Center.z));
                Vector3 screen = camera.WorldToScreenPoint(world);
                if (g == selected && dragScreen.HasValue) screen = dragScreen.Value;   // 끄는 중이면 마우스를 따라온다
                GroupMarker marker = groupMarkers[used++];
                marker.Root.gameObject.SetActive(mapMode);
                marker.Root.anchorMin = marker.Root.anchorMax = Viewport(screen);
                marker.Root.anchoredPosition = Vector2.zero;
                bool chosen = g == selected || boxGroups.Contains(g);
                if (marker.Flag != null)
                {
                    bool gunner = !string.IsNullOrEmpty(info.UnitName) && info.UnitName.Contains("총");
                    Sprite flag = UiArt.Hud(gunner ? "Map/flag_gunner" : "Map/flag_archer");
                    if (flag != null) marker.Flag.sprite = flag;
                    marker.Flag.color = chosen ? UiTokens.Brass : info.Moving ? new Color(0.78f, 0.78f, 0.78f, 1f) : Color.white;   // 선택 테두리 그림이 없어도 황동색으로 구별
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
                            Sprite arrow = UiArt.Hud(MoveArrow(camera, world, screen, info.TargetSide));
                            if (arrow != null) marker.Arrow.sprite = arrow;
                        }
                    }
                }
                else
                {
                    string shortName = string.IsNullOrEmpty(info.UnitName) ? "병" : info.UnitName.Substring(0, 1);
                    marker.Count.text = $"{shortName}{info.Alive}";
                    marker.Count.color = chosen ? UiTokens.Brass : info.Moving ? UiTokens.BodyDim : UiTokens.Body;
                }

                groupMarkerScreen.Add(new Vector2(screen.x, screen.y));
                groupCenterScreen.Add(new Vector2(center.x, center.y));
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
            float reach = Mathf.Max(5f, new Vector2(world.x, world.z).magnitude);
            Vector3 goal = camera.WorldToScreenPoint(new Vector3(n.x, world.y, n.z) * reach);
            float dx = goal.x - screen.x, dy = goal.y - screen.y;
            if (Mathf.Abs(dx) >= Mathf.Abs(dy)) return dx >= 0f ? "Map/move_east" : "Map/move_west";
            return dy >= 0f ? "Map/move_north" : "Map/move_south";
        }

        // 주인공 위치 (밤사이 계획 4): 다이아몬드 표 + "주인공". 어느 성벽이 바로 가는지는 명령판 아래 한 줄에
        RectTransform heroMarker;

        public void RefreshHeroMarker(Camera camera, Vector3 heroFeet, int heroSide, bool preparing)
        {
            if (heroMarker == null)
            {
                HorizontalLayoutGroup row = Ui.Row(root.transform, "Hero Marker", UiTokens.S4, TextAnchor.MiddleCenter);
                heroMarker = (RectTransform)row.transform;
                heroMarker.pivot = new Vector2(0.5f, 0f);
                row.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                heroMarker.sizeDelta = new Vector2(120f, 30f);
                Ui.Icon(heroMarker, "Diamond", UiArt.IconHero, 1f, UiTokens.Brass);
                Text name = Ui.Outlined(Ui.Label(heroMarker, "Name", "주인공", UiTokens.Small, UiTokens.Brass, TextAnchor.MiddleLeft, true));
                Ui.Size(name, name.preferredWidth + 4f, 30f);
                heroMarker.SetAsFirstSibling();
            }

            Vector3 screen = camera.WorldToScreenPoint(heroFeet);
            heroMarker.anchorMin = heroMarker.anchorMax = Viewport(screen);
            heroMarker.anchoredPosition = new Vector2(0f, UiTokens.S16);
            heroMarker.gameObject.SetActive(mapMode);
            if (heroRule != null)
            {
                heroRule.text = preparing ? "준비 중에는 모든 성벽에 바로 닿습니다" : $"{SideFull[heroSide]}만 바로 · 나머지는 전령";
            }
        }

        // 동서남북 표시 (D-073): 성벽 바깥 14m에 큰 글자 — 지도를 옮기거나 넓혀도 어느 쪽인지 바로 보이게. K-020 나침반 받침 그림
        const float CompassBackSize = 128f;
        readonly Text[] compass = new Text[4];
        readonly Image[] compassBacks = new Image[4];

        public void RefreshCompass(Camera camera, float castleHalf)
        {
            for (int side = 0; side < 4; side++)
            {
                if (compass[side] == null)
                {
                    Text t = Ui.Outlined(Ui.Label(root.transform, "Compass " + SideNames[side], SideNames[side], UiTokens.H2, UiTokens.Brass, TextAnchor.MiddleCenter, true));
                    HumanLastRampart.UI.UiFonts.Bold(t);
                    t.rectTransform.sizeDelta = new Vector2(80f, 56f);
                    t.transform.SetAsFirstSibling();   // 판들보다 뒤
                    compass[side] = t;
                    Image back = Ui.HudIcon(root.transform, "Compass Back " + SideNames[side], "Map/compass_back", CompassBackSize);
                    if (back != null) back.transform.SetAsFirstSibling();   // 글자보다도 뒤
                    compassBacks[side] = back;
                }

                // 가까이 볼 때(성 가까이, 전투 준비)는 성 마당 가운데에서 8m — 바깥 14m는 화면 끝 판들에 가렸다
                Unity.Mathematics.float3 n = WallGeometry.Outward(side);
                float distance = camera.orthographicSize <= 45f ? 8f : castleHalf + 14f;
                Vector3 screen = camera.WorldToScreenPoint(new Vector3(n.x, 0f, n.z) * distance);
                // 화면 밖이면 가장자리에 붙여 둔다 (방향은 그대로 알 수 있게)
                Vector2 view = Viewport(screen);
                view = new Vector2(Mathf.Clamp(view.x, 0.04f, 0.96f), Mathf.Clamp(view.y, 0.06f, 0.94f));
                compass[side].rectTransform.anchorMin = compass[side].rectTransform.anchorMax = view;
                compass[side].rectTransform.anchoredPosition = Vector2.zero;
                compass[side].gameObject.SetActive(mapMode);
                Image backImage = compassBacks[side];
                if (backImage != null)
                {
                    backImage.rectTransform.anchorMin = backImage.rectTransform.anchorMax = view;
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

        // 묶음을 고르면 띠 아래 가운데에 작은 나무 판: [동쪽] [남쪽] [서쪽] / [모두 동쪽] … (성과 적 무리를 가리지 않는 자리)
        void ShowGroupPopup(BattleSim sim, int selected, bool dragging)
        {
            if (groupPopup == null)
            {
                Image panel = Ui.Panel(root.transform, "Group Popup", UiPanel.Wood);
                groupPopup = panel.gameObject;
                Anchor(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -(Edge + BandHeight + UiTokens.S16)), new Vector2(560f, 0f), new Vector2(0.5f, 1f));
                var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
                column.padding = new RectOffset(24, 24, 16, 24);
                column.spacing = UiTokens.S8;
                column.childControlWidth = column.childControlHeight = true;
                column.childForceExpandWidth = true;
                column.childForceExpandHeight = false;
                panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                groupPopupTitle = Ui.Label(panel.transform, "Title", "", UiTokens.Text, UiTokens.Body);
                Ui.Size(groupPopupTitle, -1f, 36f);
                HorizontalLayoutGroup one = Ui.Row(panel.transform, "One", UiTokens.S8);
                HorizontalLayoutGroup all = Ui.Row(panel.transform, "All", UiTokens.S8);
                for (int i = 0; i < 4; i++)
                {
                    int side = i;
                    groupPopupButtons[i] = Ui.Button(one.transform, $"{SideShort[i]}쪽으로", SideFull[i], () => GroupDestinationChosen?.Invoke(side));
                    Ui.Size(groupPopupButtons[i], 160f, 48f);
                    // 아랫줄: 이 성벽 묶음 한 번에 (사용자 2차 피드백 11, 성벽마다 20명 남김)
                    wallMoveButtons[i] = Ui.Button(all.transform, $"전부 {SideShort[i]}쪽으로", "모두 " + SideFull[i], () => WallGroupsDestinationChosen?.Invoke(side));
                    Ui.Size(wallMoveButtons[i], 160f, 48f);
                }

                groupPopup.SetActive(false);
            }

            bool show = mapMode && selected >= 0 && selected < sim.GroupCount;
            UiAppear.Toggle(groupPopup, show, new Vector2(0f, UiTokens.S16));
            if (!show) return;
            BattleSim.GroupInfo info = sim.GetGroup(selected);
            groupPopupTitle.text = dragging ? "보낼 성벽 위에서 놓기"
                : info.Side >= 0 ? $"{info.UnitName} {info.Alive}명 · {SideFull[info.Side]} 성벽" : $"{info.UnitName} {info.Alive}명 · 이동 중";
            for (int i = 0; i < 4; i++)
            {
                groupPopupButtons[i].gameObject.SetActive(info.Side >= 0 && i != info.Side);
                wallMoveButtons[i].gameObject.SetActive(info.Side >= 0 && i != info.Side);
            }
        }

        // 명령판 고름 표시 + 지금 4면 명령 아이콘
        void RefreshGlobalStatus(BattleSim sim)
        {
            if (commandBoard == null || !mapMode) return;
            for (int i = 0; i < commandTabs.Length; i++) Ui.SetOn(commandTabs[i], i - 1 == commandTarget);
            int current = -1;
            if (commandTarget >= 0) current = (int)sim.PriorityOf(commandTarget);
            else
            {
                current = (int)sim.PriorityOf(0);
                for (int side = 1; side < 4; side++) if ((int)sim.PriorityOf(side) != current) current = -1;
            }

            for (int i = 0; i < orderButtons.Length; i++) if (orderButtons[i] != null) Ui.SetOn(orderButtons[i], i == current);
            for (int side = 0; side < 4; side++)
            {
                if (wallOrderIcons[side] == null) continue;
                Sprite icon = UiArt.Hud("Icons/" + PriorityIcons[(int)sim.PriorityOf(side)]);
                if (icon != null) wallOrderIcons[side].sprite = icon;
            }
        }
    }
}
