using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Upgrades;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HumanLastRampart.Meta
{
    // 상점·병종 훈련·용병 고용 장부 (K-058, D-079) — K-047 부품(UI/Kit)으로: 어둠막 위 양피지 한 장.
    // 위: 이름 + 골드 + 닫기 / 왼쪽: 고르기 칸(2열 탭) / 오른쪽: 고른 것의 이름·설명·값 + 황동 단추 하나.
    // 규칙은 모두 Core.Upgrades.UpgradeRules. 열쇠(btn:…)는 시험이 찾는 이름
    internal sealed class UpgradePanel
    {
        public enum Mode { Shop, Training, Mercenary }

        static readonly string[] TrainedUnits = { "unit_archer", "unit_gunner", "unit_cannon_crew" };

        readonly Button source;
        readonly CanvasGroup behind;
        readonly IScreenContext context;
        readonly PreparationTransactions session;
        readonly Mode mode;
        readonly GameObject shade;
        readonly RectTransform body;
        readonly Text gold;
        string pickedItem, pickedUnit, pickedMerc;
        int wallIndex, hireCount = 10;
        string done;   // 방금 한 일 (다시 고르기 전까지 도장)

        public static void Show(Button source, IScreenContext context, PreparationTransactions session, Mode mode)
        {
            if (source == null || !source.IsInteractable()) return;
            new UpgradePanel(source, context, session, mode);
        }

        UpgradePanel(Button source, IScreenContext context, PreparationTransactions session, Mode mode)
        {
            this.source = source; this.context = context; this.session = session; this.mode = mode;
            behind = source.transform.parent.GetComponentInParent<CanvasGroup>();
            if (behind != null) behind.interactable = false;
            Transform layer = source.GetComponentInParent<Canvas>().transform;
            Image dim = Ui.Shade(layer, "Upgrade " + mode);
            shade = dim.gameObject;
            PrepLedger.Opened(shade);   // 장부는 한 번에 하나

            Image sheet = Ui.Panel(dim.transform, "Ledger", UiPanel.Parchment);
            Ui.Place(sheet.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120f, 0f));
            UiAppear.Show(sheet.gameObject, new Vector2(0f, -UiTokens.S24));
            var column = sheet.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(48, 48, 32, 48);
            column.spacing = UiTokens.S24;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            sheet.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            HorizontalLayoutGroup head = Ui.Row(sheet.transform, "Head", UiTokens.S16);
            Ui.Size(head, -1f, 48f);
            // 훈련·용병은 병영 안 탭 (K-047): 제목은 "병영", 머리에 모집·훈련·용병 탭 — 병영 모집 장부와 같은 모양
            string name = mode == Mode.Shop ? "상점" : "병영";
            Ui.Size(Ui.Label(head.transform, "Title", name, UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true), 0f, 48f, true);
            if (mode != Mode.Shop) PreparationPanel.BarracksTabs(head.transform, mode == Mode.Training ? 1 : 2, () => Close(true));
            if (Ui.HudIcon(head.transform, "Gold Icon", "Icons/stat_gold", 32f) == null) Ui.Size(Ui.Label(head.transform, "Gold Word", "골드", UiTokens.Small, UiTokens.InkSoft), 44f, 48f);
            gold = Ui.Label(head.transform, "Gold", "", UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            Ui.Size(gold, 136f, 48f);
            Button close = Ui.IconButton(head.transform, "닫기", UiArt.IconClose, Close, 48f);

            body = (RectTransform)Ui.Column(sheet.transform, "Body", UiTokens.S16).transform;
            Render();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(close.gameObject);
        }

        CampaignState Campaign => context.Campaign;
        IDefinitionLookup Defs => context.Definitions;

        void Render()
        {
            foreach (Transform child in body) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
            gold.text = Campaign.Gold.ToString("N0");
            if (mode == Mode.Shop) RenderShop();
            else if (mode == Mode.Training) RenderTraining();
            else RenderMercenary();
        }

        // 왼쪽 고르기 칸(2열) + 오른쪽 내용
        (GridLayoutGroup grid, VerticalLayoutGroup right) Split()
        {
            HorizontalLayoutGroup split = Ui.Row(body, "Split", UiTokens.S48, TextAnchor.UpperLeft);
            split.childAlignment = TextAnchor.UpperLeft;
            var grid = Ui.Rect(split.transform, "Picks").gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(248f, 56f);
            grid.spacing = new Vector2(UiTokens.S8, UiTokens.S8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            Ui.Size(grid, 504f, -1f);
            VerticalLayoutGroup right = Ui.Column(split.transform, "Detail", UiTokens.S16);
            Ui.Size(right, 472f, -1f);
            return (grid, right);
        }

        // ── 상점: 영웅 장비 칸 줄 + 품목 칸 + 고른 품목 ──
        void RenderShop()
        {
            HorizontalLayoutGroup slots = Ui.Row(body, "Slots", UiTokens.S24);
            Ui.Size(slots, -1f, 32f);
            SlotText(slots.transform, UnitKind.Hero, "주인공", ItemSlot.HeroTool, ItemSlot.HeroBadge);
            SlotText(slots.transform, UnitKind.Wizard, "마법사", ItemSlot.WizardStaff, ItemSlot.WizardCharm);

            var items = new List<ItemDef>(Defs.All<ItemDef>());
            items.Sort((a, b) => a.ShopOrder != b.ShopOrder ? a.ShopOrder.CompareTo(b.ShopOrder) : string.CompareOrdinal(a.Id, b.Id));
            if (items.Count == 0) return;
            if (pickedItem == null || items.Find(i => i.Id == pickedItem) == null) pickedItem = items[0].Id;
            var (grid, right) = Split();
            foreach (ItemDef item in items)
            {
                string id = item.Id;
                Button cell = Ui.Button(grid.transform, "물건:" + id, item.DisplayName, () => { if (pickedItem == id) return; pickedItem = id; Changed(); }, UiButtonKind.Tab);
                Ui.LabelOf(cell).alignment = TextAnchor.MiddleLeft;
                CellIcon(cell, id);
                Ui.SetOn(cell, id == pickedItem);
                if (OwnedMark(item))
                {
                    Image mark = Ui.Icon(cell.transform.Find("Content"), "Owned", UiArt.IconCheck, 1f, UiTokens.Good);   // 가진 것 표시
                    Ui.Place(mark.rectTransform, new Vector2(1f, 0.5f), new Vector2(-UiTokens.S16, 0f), mark.rectTransform.sizeDelta, new Vector2(1f, 0.5f));
                }
            }

            ItemDef picked = items.Find(i => i.Id == pickedItem);
            Heading(right.transform, picked.Id, picked.DisplayName);
            Ui.Size(Ui.Label(right.transform, "About", picked.Description, UiTokens.Text, UiTokens.InkSoft), -1f, 64f);

            string action = null, why = null;
            Action act = null;
            int price = picked.Price;
            if (picked.PerWall)
            {
                HorizontalLayoutGroup walls = Ui.Row(right.transform, "Walls", UiTokens.S8);
                for (int i = 0; i < 4; i++)
                {
                    int index = i;
                    string wall = WallIds.All[i];
                    Button tab = Ui.Button(walls.transform, "성벽:" + wall, PreparationTransactions.WallName(wall), () => { if (wallIndex == index) return; wallIndex = index; Changed(); }, UiButtonKind.Tab);
                    Ui.Size(tab, 104f, 48f);
                    Ui.SetOn(tab, i == wallIndex);
                }

                string wallId = WallIds.All[wallIndex];
                if (UpgradeRules.CanBuy(Campaign, Defs, picked.Id, wallId, out _, out why)) { action = $"{price:N0}골드에 사기"; act = () => Buy(picked, wallId); }
            }
            else if (picked.IsHeroItem && UpgradeRules.OwnedCount(Campaign, picked.Id) > 0)
            {
                OwnedItem line = Campaign.Items.Find(i => i.ItemId == picked.Id);
                bool on = line != null && !string.IsNullOrEmpty(line.EquippedBy);
                action = on ? "빼기" : "끼우기";
                act = () => ToggleEquip(picked, on);
                price = -1;
            }
            else if (UpgradeRules.CanBuy(Campaign, Defs, picked.Id, null, out _, out why))
            {
                action = $"{price:N0}골드에 사기";
                act = () => Buy(picked, null);
            }

            if (picked.Consumable) Ui.Size(Ui.Label(right.transform, "Have", $"가진 수 {UpgradeRules.OwnedCount(Campaign, picked.Id)}", UiTokens.Text, UiTokens.Ink), -1f, 32f);
            PriceLine(right.transform, act != null && price >= 0 ? price : -1, act == null ? why : null);
            Button commit = Ui.Button(right.transform, "확정 구매", action ?? "사기", () => { act?.Invoke(); }, UiButtonKind.Primary);
            Ui.Size(commit, -1f, 64f);
            commit.interactable = act != null;
        }

        bool OwnedMark(ItemDef item) =>
            item.PerWall ? UpgradeRules.OwnsForWall(Campaign, item.Id, WallIds.All[wallIndex]) : UpgradeRules.OwnedCount(Campaign, item.Id) > 0;

        // 장비 칸: "주인공  망원경 렌즈 · 지휘 휘장" (빈 칸은 감춘다 — S-21)
        void SlotText(Transform parent, UnitKind kind, string heroName, ItemSlot a, ItemSlot b)
        {
            HeroState hero = UpgradeRules.HeroFor(Campaign, Defs, kind);
            if (hero == null) return;
            var names = new List<string>();
            foreach (ItemSlot slot in new[] { a, b })
            {
                OwnedItem owned = UpgradeRules.Equipped(Campaign, Defs, hero.InstanceId, slot);
                if (owned != null) names.Add(Defs.Get<ItemDef>(owned.ItemId)?.DisplayName ?? owned.ItemId);
            }

            Text label = Ui.Label(parent, "Slots " + heroName, names.Count > 0 ? $"{heroName}  {string.Join(" · ", names)}" : heroName, UiTokens.Text, names.Count > 0 ? UiTokens.Ink : UiTokens.InkSoft, TextAnchor.MiddleLeft, true);
            Ui.Size(label, 488f, 32f);
        }

        void Buy(ItemDef item, string wallId)
        {
            if (UpgradeRules.TryBuy(Campaign, Defs, item.Id, wallId, out string why)) done = "장부에 적었습니다";
            Changed(keepDone: true);
        }

        void ToggleEquip(ItemDef item, bool equipped)
        {
            bool ok = equipped ? UpgradeRules.TryUnequip(Campaign, item.Id, out _) : UpgradeRules.TryEquip(Campaign, Defs, item.Id, out _);
            if (ok) done = equipped ? "가방에 넣었습니다" : "끼웠습니다";
            Changed(keepDone: true);
        }

        // ── 병영: 병종 훈련 ──
        void RenderTraining()
        {
            var units = new List<UnitDef>();
            foreach (string id in TrainedUnits)
            {
                UnitDef unit = Defs.Get<UnitDef>(id);
                if (unit != null && UpgradeRules.MaxTrainingLevel(Defs, id) > 0) units.Add(unit);
            }

            if (units.Count == 0) return;
            if (pickedUnit == null || units.Find(u => u.Id == pickedUnit) == null) pickedUnit = units[0].Id;
            var (grid, right) = Split();
            foreach (UnitDef unit in units)
            {
                string id = unit.Id;
                Button cell = Ui.Button(grid.transform, "병종:" + id, unit.DisplayName, () => { if (pickedUnit == id) return; pickedUnit = id; Changed(); }, UiButtonKind.Tab);
                Text label = Ui.LabelOf(cell);
                label.alignment = TextAnchor.MiddleLeft;
                CellIcon(cell, TrainingIconId(id));
                Text level = Ui.Label(cell.transform.Find("Content"), "Level", $"{UpgradeRules.TrainingLevel(Campaign, id)}/{UpgradeRules.MaxTrainingLevel(Defs, id)}", UiTokens.Small, UiTokens.BodyDim, TextAnchor.MiddleRight);
                Ui.Stretch(level.rectTransform);
                level.rectTransform.offsetMax = new Vector2(-UiTokens.S16, 0f);
                Ui.SetOn(cell, id == pickedUnit);
            }

            UnitDef picked = units.Find(u => u.Id == pickedUnit);
            int now = UpgradeRules.TrainingLevel(Campaign, picked.Id);
            Heading(right.transform, TrainingIconId(picked.Id), picked.DisplayName);
            UiBar bar = Ui.Bar(right.transform, "Level", 400f, 15f, false);
            bar.Set((float)now / Math.Max(1, UpgradeRules.MaxTrainingLevel(Defs, picked.Id)));
            Ui.Size(bar, 400f, 15f);
            bool can = UpgradeRules.CanTrain(Campaign, Defs, picked.Id, out TrainingDef next, out string why);
            if (next != null) Ui.Size(Ui.Label(right.transform, "Next", $"{next.Level}단계  {next.Description}", UiTokens.Text, UiTokens.InkSoft), -1f, 32f);
            PriceLine(right.transform, can ? next.Price : -1, can ? null : why);
            Button commit = Ui.Button(right.transform, "확정 훈련", next != null ? $"{next.Level}단계 훈련" : "훈련", () =>
            {
                if (UpgradeRules.TryTrain(Campaign, Defs, picked.Id, out _)) done = "훈련을 마쳤습니다";
                Changed(keepDone: true);
            }, UiButtonKind.Primary);
            Ui.Size(commit, -1f, 64f);
            commit.interactable = can;
        }

        // ── 병영: 용병 고용 ──
        void RenderMercenary()
        {
            var mercs = new List<MercenaryDef>(Defs.All<MercenaryDef>());
            if (mercs.Count == 0) return;
            if (pickedMerc == null || mercs.Find(m => m.Id == pickedMerc) == null) pickedMerc = mercs[0].Id;
            string wallId = WallIds.All[wallIndex];

            HorizontalLayoutGroup walls = Ui.Row(body, "Walls", UiTokens.S8);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                Button tab = Ui.Button(walls.transform, "성벽:" + WallIds.All[i], PreparationTransactions.WallName(WallIds.All[i]), () => { if (wallIndex == index) return; wallIndex = index; Changed(); }, UiButtonKind.Tab);
                Ui.Size(tab, 248f, 56f);
                Ui.SetOn(tab, i == wallIndex);
            }

            var (grid, right) = Split();
            foreach (MercenaryDef merc in mercs)
            {
                string id = merc.Id;
                Button cell = Ui.Button(grid.transform, "용병:" + id, merc.DisplayName, () => { if (pickedMerc == id) return; pickedMerc = id; Changed(); }, UiButtonKind.Tab);
                Ui.LabelOf(cell).alignment = TextAnchor.MiddleLeft;
                CellIcon(cell, id);
                Text there = Ui.Label(cell.transform.Find("Content"), "Count", $"{UpgradeRules.MercenaryCount(Campaign, id, wallId)}/{merc.PerWallCap}", UiTokens.Small, UiTokens.BodyDim, TextAnchor.MiddleRight);
                Ui.Stretch(there.rectTransform);
                there.rectTransform.offsetMax = new Vector2(-UiTokens.S16, 0f);
                Ui.SetOn(cell, id == pickedMerc);
            }

            // 지금 계약: 성벽마다 한 줄 + 해고 (조용한 단추)
            foreach (MercenaryContract contract in Campaign.Mercenaries)
            {
                string cid = contract.ContractId;
                MercenaryDef def = Defs.Get<MercenaryDef>(contract.MercenaryDefId);
                Button fire = Ui.Button(grid.transform, "해고:" + cid, $"{PreparationTransactions.WallName(contract.WallId)} {def?.DisplayName} {contract.Count}", () =>
                {
                    UpgradeRules.Dismiss(Campaign, cid);
                    done = "계약을 끝냈습니다";
                    Changed(keepDone: true);
                }, UiButtonKind.Quiet);
                Ui.LabelOf(fire).alignment = TextAnchor.MiddleLeft;
            }

            MercenaryDef picked = mercs.Find(m => m.Id == pickedMerc);
            Heading(right.transform, picked.Id, picked.DisplayName);
            HorizontalLayoutGroup counter = Ui.Row(right.transform, "Count", UiTokens.S8);
            Ui.Size(counter, -1f, 56f);
            Ui.IconButton(counter.transform, "인원 −5", UiArt.IconMinus, () => { hireCount = Math.Max(5, hireCount - 5); Changed(); }, 56f);
            Ui.Size(Ui.Label(counter.transform, "Number", $"{hireCount}명", UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleCenter, true), 112f, 56f);
            Ui.IconButton(counter.transform, "인원 +5", UiArt.IconPlus, () => { hireCount = Math.Min(picked.PerWallCap, hireCount + 5); Changed(); }, 56f);
            Ui.Size(Ui.Label(right.transform, "Wage", $"전투마다 급료 {picked.Wage * hireCount:N0}", UiTokens.Text, UiTokens.InkSoft), -1f, 32f);
            bool can = UpgradeRules.CanHire(Campaign, Defs, picked.Id, wallId, hireCount, out int cost, out string why);
            PriceLine(right.transform, can ? cost : -1, can ? null : why);
            Button commit = Ui.Button(right.transform, "확정 고용", $"{hireCount}명 고용", () =>
            {
                if (UpgradeRules.TryHire(Campaign, Defs, picked.Id, wallId, hireCount, out _)) done = "계약했습니다";
                Changed(keepDone: true);
            }, UiButtonKind.Primary);
            Ui.Size(commit, -1f, 64f);
            commit.interactable = can;
        }

        // 값 줄: [골드] 600 · 남는 골드 / 안 되면 이유 한 줄(붉게) / 방금 한 일이면 도장
        // 아이콘 (Codex K-067, HudArt/Upgrades/<정의 이름>) — 그림이 없으면 예전처럼 글만
        const string IconFolder = "Upgrades/";

        static void CellIcon(Button cell, string id)
        {
            Image icon = Ui.HudIcon(cell.transform.Find("Content"), "Icon", IconFolder + id, 32f);
            if (icon == null) return;
            Ui.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(UiTokens.S8, 0f), icon.rectTransform.sizeDelta, new Vector2(0f, 0.5f));
            Text label = Ui.LabelOf(cell);
            if (label != null) label.rectTransform.offsetMin = new Vector2(UiTokens.S8 + icon.rectTransform.sizeDelta.x + UiTokens.S8, 0f);
        }

        static void Heading(Transform parent, string iconId, string name)
        {
            HorizontalLayoutGroup row = Ui.Row(parent, "Heading", UiTokens.S16);
            Ui.HudIcon(row.transform, "Icon", IconFolder + iconId, 64f);
            Ui.Size(Ui.Label(row.transform, "Name", name, UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true), -1f, 64f, true);
            Ui.Size(row, -1f, 64f);
        }

        // 훈련 아이콘: 다음에 할 단계 (다 했으면 마지막 단계)
        string TrainingIconId(string unitId)
        {
            int max = UpgradeRules.MaxTrainingLevel(Defs, unitId);
            int level = Math.Max(1, Math.Min(UpgradeRules.TrainingLevel(Campaign, unitId) + 1, max));
            return UpgradeRules.TrainingAt(Defs, unitId, level)?.Id ?? "";
        }

        void PriceLine(Transform parent, int cost, string why)
        {
            HorizontalLayoutGroup line = Ui.Row(parent, "Price", UiTokens.S8);
            Ui.Size(line, -1f, 40f);
            // 방금 한 일은 알림으로만 (사용자 2026-10-20 #15: 도장이 단추를 잠가 다음 단계를 못 샀다) — 다음 값은 이어서 보인다
            if (done != null)
            {
                Ui.Icon(line.transform, "Done", UiArt.IconCheck, 1f, UiTokens.Good);
                Ui.Size(Ui.Label(line.transform, "Text", done, UiTokens.Text, UiTokens.Ink), 200f, 40f);
            }

            if (cost < 0)
            {
                if (!string.IsNullOrEmpty(why)) Ui.Size(Ui.Label(line.transform, "Reason", why, UiTokens.Text, UiTokens.DangerInk), 440f, 40f);
                return;
            }

            if (Ui.HudIcon(line.transform, "Gold Icon", "Icons/stat_gold", 32f) == null) Ui.Size(Ui.Label(line.transform, "Gold Word", "골드", UiTokens.Small, UiTokens.InkSoft), 44f, 40f);
            Text value = Ui.Label(line.transform, "Cost", cost.ToString("N0"), UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            Ui.Size(value, value.preferredWidth + 8f, 40f);
            Ui.Size(Ui.Label(line.transform, "Left", $"남는 골드 {Campaign.Gold - cost:N0}", UiTokens.Text, UiTokens.InkSoft), 320f, 40f);
        }

        void Changed(bool keepDone = false)
        {
            if (!keepDone) done = null;
            session.CancelQuote();   // 골드가 바뀌었으니 모집·수리 견적은 새로
            PreparationPanel.RefreshSummary(source, context, session);
            Render();
        }

        void Close() => Close(false);

        // instant = 병영 탭을 바꿀 때 — 닫히는 장부가 새 장부 뒤로 비쳐 겹쳐 보이지 않게 바로 치운다
        void Close(bool instant)
        {
            if (behind != null) behind.interactable = true;
            if (instant) { shade.SetActive(false); UnityEngine.Object.Destroy(shade); }
            else Ui.Close(shade);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(source.gameObject);
        }
    }
}
