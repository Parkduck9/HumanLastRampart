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
    // 상점·병종 훈련·용병 고용 창 (K-058, D-079). 마당의 '상점'과 병영 안 [병종 훈련]·[용병 고용]에서 연다.
    // 모양은 K-047(슬롭 배제 UI)이 다시 만들 것이라 기능 위주 최소 화면 — 규칙은 모두 Core.Upgrades.UpgradeRules
    internal sealed class UpgradePanel
    {
        public enum Mode { Shop, Training, Mercenary }

        static readonly string[] TrainedUnits = { "unit_archer", "unit_gunner", "unit_cannon_crew" };

        readonly Button source;
        readonly CanvasGroup behind;
        readonly IScreenContext context;
        readonly PreparationTransactions session;
        readonly Mode mode;
        readonly RectTransform shade, content;
        readonly ScrollRect scroll;
        readonly Font font;
        int wallIndex, hireCount = 10;
        string message = "";

        public static void Show(Button source, IScreenContext context, PreparationTransactions session, Mode mode)
        {
            if (source == null || !source.IsInteractable()) return;
            new UpgradePanel(source, context, session, mode);
        }

        UpgradePanel(Button source, IScreenContext context, PreparationTransactions session, Mode mode)
        {
            this.source = source; this.context = context; this.session = session; this.mode = mode;
            behind = source.transform.parent.GetComponent<CanvasGroup>(); behind.interactable = false;
            font = source.GetComponentInChildren<Text>().font;
            shade = Make(source.transform.parent.parent, "Upgrade " + mode, typeof(Image));
            Stretch(shade); shade.GetComponent<Image>().color = UiStyle.Shade;
            var area = Make(shade, "Upgrade Scroll", typeof(ScrollRect));
            area.anchorMin = new Vector2(0.12f, 0.15f); area.anchorMax = new Vector2(0.88f, 0.93f);
            area.offsetMin = area.offsetMax = Vector2.zero;
            var viewport = Make(area, "Viewport", typeof(Image), typeof(RectMask2D)); Stretch(viewport);
            viewport.GetComponent<Image>().color = UiStyle.PanelSolid; UiStyle.Border(viewport.GetComponent<Image>());
            content = Make(viewport, "Controls", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(32, 32, 24, 24);
            layout.spacing = 8; layout.childControlHeight = true; layout.childControlWidth = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = area.GetComponent<ScrollRect>(); scroll.content = content; scroll.viewport = viewport;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 38;
            var close = ButtonAt(shade, "닫기", Close);
            close.anchorMin = new Vector2(0.36f, 0.04f); close.anchorMax = new Vector2(0.64f, 0.12f);
            close.offsetMin = close.offsetMax = Vector2.zero;
            Render();
        }

        CampaignState Campaign => context.Campaign;
        IDefinitionLookup Defs => context.Definitions;

        void Render()
        {
            foreach (Transform child in content) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
            switch (mode)
            {
                case Mode.Shop: RenderShop(); break;
                case Mode.Training: RenderTraining(); break;
                default: RenderMercenary(); break;
            }

            if (!string.IsNullOrEmpty(message)) Label(message, 56, UiStyle.Brass);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        // ── 상점: 영웅 장비 칸 + 품목 장부 ──
        void RenderShop()
        {
            Label($"상점 · 골드 {Campaign.Gold:N0}", 64, UiStyle.Brass, 32);
            SlotLine(UnitKind.Hero, "주인공", ItemSlot.HeroTool, "소지품", ItemSlot.HeroBadge, "휘장");
            SlotLine(UnitKind.Wizard, "마법사", ItemSlot.WizardStaff, "지팡이", ItemSlot.WizardCharm, "부적");

            var items = new List<ItemDef>(Defs.All<ItemDef>());
            items.Sort((a, b) => a.ShopOrder != b.ShopOrder ? a.ShopOrder.CompareTo(b.ShopOrder) : string.CompareOrdinal(a.Id, b.Id));
            foreach (ItemDef item in items)
            {
                if (item.PerWall)
                {
                    foreach (string wallId in WallIds.All)
                    {
                        string wall = wallId;
                        if (UpgradeRules.OwnsForWall(Campaign, item.Id, wall)) Label($"{item.DisplayName} · {PreparationTransactions.WallName(wall)} 성벽에 있음", 44, UiStyle.TextDim);
                        else AddButton($"구매: {item.DisplayName} · {PreparationTransactions.WallName(wall)} 성벽 · {item.Price:N0}골드", () => Buy(item, wall));
                    }

                    continue;
                }

                int owned = UpgradeRules.OwnedCount(Campaign, item.Id);
                if (item.IsHeroItem && owned > 0)
                {
                    OwnedItem line = Campaign.Items.Find(i => i.ItemId == item.Id);
                    bool on = line != null && !string.IsNullOrEmpty(line.EquippedBy);
                    AddButton(on ? $"빼기: {item.DisplayName}" : $"끼우기: {item.DisplayName}", () => ToggleEquip(item, on));
                    continue;
                }

                string have = item.Consumable ? $" · 보유 {owned}" : "";
                if (!item.Consumable && owned > 0) { Label($"{item.DisplayName} · 가짐 — {item.Description}", 44, UiStyle.TextDim); continue; }
                AddButton($"구매: {item.DisplayName} · {item.Price:N0}골드{have}", () => Buy(item, null));
                Label(item.Description, 32, UiStyle.TextDim, 20);
            }
        }

        void SlotLine(UnitKind kind, string heroName, ItemSlot a, string aName, ItemSlot b, string bName)
        {
            HeroState hero = UpgradeRules.HeroFor(Campaign, Defs, kind);
            if (hero == null) return;
            Label($"{heroName}  {aName}: {SlotText(hero, a)}  ·  {bName}: {SlotText(hero, b)}", 48, UiStyle.Body);
        }

        string SlotText(HeroState hero, ItemSlot slot)
        {
            OwnedItem owned = UpgradeRules.Equipped(Campaign, Defs, hero.InstanceId, slot);
            return owned != null ? Defs.Get<ItemDef>(owned.ItemId)?.DisplayName ?? owned.ItemId : "빈 칸";
        }

        void Buy(ItemDef item, string wallId)
        {
            message = UpgradeRules.TryBuy(Campaign, Defs, item.Id, wallId, out string reason) ? $"{item.DisplayName} 구매" : reason;
            Changed();
        }

        void ToggleEquip(ItemDef item, bool equipped)
        {
            string reason;
            bool ok = equipped ? UpgradeRules.TryUnequip(Campaign, item.Id, out reason) : UpgradeRules.TryEquip(Campaign, Defs, item.Id, out reason);
            message = ok ? (equipped ? $"{item.DisplayName}을 가방에 넣었습니다" : $"{item.DisplayName}을 끼웠습니다") : reason;
            Changed();
        }

        // ── 병영: 병종 훈련 ──
        void RenderTraining()
        {
            Label($"병종 훈련 · 골드 {Campaign.Gold:N0}", 64, UiStyle.Brass, 32);
            foreach (string unitId in TrainedUnits)
            {
                UnitDef unit = Defs.Get<UnitDef>(unitId);
                int max = UpgradeRules.MaxTrainingLevel(Defs, unitId);
                if (unit == null || max == 0) continue;
                int level = UpgradeRules.TrainingLevel(Campaign, unitId);
                Label($"{unit.DisplayName} · {level}/{max}단계", 44, UiStyle.Body);
                TrainingDef next = UpgradeRules.TrainingAt(Defs, unitId, level + 1);
                if (next == null) continue;
                string id = unitId;
                AddButton($"훈련: {unit.DisplayName} {next.Level}단계 · {next.Price:N0}골드 · {next.Description}", () =>
                {
                    message = UpgradeRules.TryTrain(Campaign, Defs, id, out string reason) ? $"{unit.DisplayName} {next.Level}단계 훈련 완료" : reason;
                    Changed();
                });
            }
        }

        // ── 병영: 용병 고용 ──
        void RenderMercenary()
        {
            string wallId = WallIds.All[wallIndex];
            Label($"용병 고용 · 골드 {Campaign.Gold:N0} · 전투마다 급료 {UpgradeRules.WagesDue(Campaign, Defs):N0}", 64, UiStyle.Brass, 32);
            AddButton($"성벽: {PreparationTransactions.WallName(wallId)}", () => { wallIndex = (wallIndex + 1) % 4; Render(); });
            AddButton($"인원: {hireCount}명 (누르면 +5, 50 넘으면 5)", () => { hireCount = hireCount >= 50 ? 5 : hireCount + 5; Render(); });
            foreach (MercenaryDef merc in Defs.All<MercenaryDef>())
            {
                MercenaryDef m = merc;
                int there = UpgradeRules.MercenaryCount(Campaign, m.Id, wallId);
                AddButton($"고용: {m.DisplayName} {hireCount}명 · {m.ContractCost * hireCount:N0}골드 · 급료 {m.Wage * hireCount:N0} · 이 성벽 {there}/{m.PerWallCap}", () =>
                {
                    message = UpgradeRules.TryHire(Campaign, Defs, m.Id, wallId, hireCount, out string reason) ? $"{m.DisplayName} {hireCount}명 고용" : reason;
                    Changed();
                });
            }

            foreach (MercenaryContract contract in Campaign.Mercenaries)
            {
                string id = contract.ContractId;
                MercenaryDef def = Defs.Get<MercenaryDef>(contract.MercenaryDefId);
                AddButton($"해고: {PreparationTransactions.WallName(contract.WallId)} {def?.DisplayName ?? contract.MercenaryDefId} {contract.Count}명", () =>
                {
                    UpgradeRules.Dismiss(Campaign, id);
                    message = "계약을 끝냈습니다";
                    Changed();
                });
            }
        }

        void Changed()
        {
            session.CancelQuote();   // 골드가 바뀌었으니 모집·수리 견적은 새로
            PreparationPanel.RefreshSummary(source, context, session);
            Render();
        }

        void Close()
        {
            behind.interactable = true;
            shade.gameObject.SetActive(false); UnityEngine.Object.Destroy(shade.gameObject);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(source.gameObject);
        }

        Button AddButton(string text, Action action)
        {
            var rect = ButtonAt(content, text, action); rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;
            return rect.GetComponent<Button>();
        }

        RectTransform ButtonAt(Transform parent, string text, Action action)
        {
            var rect = Make(parent, text, typeof(Image), typeof(Button));
            var button = rect.GetComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); UiStyle.StyleButton(button);
            var label = Make(rect, "Button Label", typeof(Text)); Stretch(label); SetupText(label, text, 24, UiStyle.Body, TextAnchor.MiddleLeft);
            label.offsetMin = new Vector2(24f, 0f);
            button.onClick.AddListener(() => action());
            return rect;
        }

        void Label(string text, float height, Color color, int size = 24)
        {
            var rect = Make(content, "Info", typeof(Text), typeof(LayoutElement)); SetupText(rect, text, size, color, TextAnchor.MiddleLeft);
            rect.GetComponent<LayoutElement>().preferredHeight = height;
        }

        void SetupText(RectTransform rect, string value, int size, Color color, TextAnchor anchor)
        {
            var t = rect.GetComponent<Text>(); t.font = font; t.fontSize = size; t.text = value; t.color = color; t.alignment = anchor;
            t.supportRichText = false; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        static RectTransform Make(Transform parent, string name, params Type[] components)
        { var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false); foreach (var type in components) obj.AddComponent(type); return obj.GetComponent<RectTransform>(); }

        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    }
}
