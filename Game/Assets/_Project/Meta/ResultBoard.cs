using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.CombatContracts;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanLastRampart.Meta
{
    // 4번 결과 화면 그림판 (D-076): 글자 덩어리 대신 한 장에 — 왼쪽 성벽 도식(4면 피해), 가운데 병종별 병력 손실 막대,
    // 오른쪽 영웅 초상과 체력. 그림(성 도식·흉상)은 Codex K-028 확정 전이라 지금은 도형·병종 시트 첫 칸으로 그린다
    public static class ResultBoard
    {
        const float ColumnGap = 28f;
        static readonly Color Good = new Color(0.45f, 0.72f, 0.42f);
        static readonly Color Warn = new Color(0.86f, 0.70f, 0.34f);
        static readonly Color Bad = new Color(0.80f, 0.36f, 0.30f);
        static readonly Color Track = new Color(1f, 1f, 1f, 0.08f);

        public static void Build(RectTransform area, BattleSetup setup, BattleResult result, Func<string, UnitDef> unit)
        {
            float width = area.rect.width > 1f ? area.rect.width : 1488f;
            float column = (width - ColumnGap * 2f) / 3f;
            RectTransform walls = Column(area, "Walls", 0f, column, "성벽 피해");
            RectTransform squads = Column(area, "Squads", column + ColumnGap, column, "병력 손실");
            RectTransform heroes = Column(area, "Heroes", (column + ColumnGap) * 2f, column, "영웅 상태");
            DrawWalls(walls, setup, result);
            DrawSquads(squads, setup, result, unit);
            DrawHeroes(heroes, setup, result, unit);
        }

        // ── 성벽: 성을 위에서 본 네모, 4면 막대 색 = 남은 체력 비율, 옆에 이번 피해 ──
        static void DrawWalls(RectTransform box, BattleSetup setup, BattleResult result)
        {
            var after = new Dictionary<string, int>();
            foreach (WallOutcome w in result.WallOutcomes) after[w.SegmentId] = w.HpAfter;
            const float size = 220f, thick = 22f;
            var center = new Vector2(0f, -40f);
            Rect(box, "Yard", center, new Vector2(size - thick * 2f, size - thick * 2f), new Color(0.16f, 0.21f, 0.18f));
            Text(box, "성", center, 30, UiStyle.TextDim, new Vector2(80f, 40f));
            string[] ids = { WallIds.North, WallIds.East, WallIds.South, WallIds.West };
            string[] names = { "북", "동", "남", "서" };
            Vector2[] offsets = { new Vector2(0f, (size - thick) / 2f), new Vector2((size - thick) / 2f, 0f), new Vector2(0f, -(size - thick) / 2f), new Vector2(-(size - thick) / 2f, 0f) };
            Vector2[] sizes = { new Vector2(size, thick), new Vector2(thick, size), new Vector2(size, thick), new Vector2(thick, size) };
            Vector2[] labelOffsets = { new Vector2(0f, size / 2f + 34f), new Vector2(size / 2f + 70f, 0f), new Vector2(0f, -size / 2f - 34f), new Vector2(-size / 2f - 70f, 0f) };
            for (int i = 0; i < 4; i++)
            {
                WallSegmentState start = setup.Wall.Find(w => w.SegmentId == ids[i]);
                if (start == null) continue;
                int hpAfter = after.TryGetValue(ids[i], out int a) ? a : start.Hp;
                float ratio = start.MaxHp > 0 ? Mathf.Clamp01((float)hpAfter / start.MaxHp) : 1f;
                Rect(box, "Wall " + names[i], center + offsets[i], sizes[i], RatioColor(ratio));
                int damage = start.Hp - hpAfter;
                string text = damage > 0 ? $"{names[i]} −{damage:N0}\n{ratio:P0}" : $"{names[i]}\n멀쩡";
                Text(box, text, center + labelOffsets[i], 20, damage > 0 ? UiStyle.Body : UiStyle.TextDim, new Vector2(130f, 52f));
            }
        }

        // ── 병력: 병종마다 한 줄 — 그림 + 남은/잃은 막대 + 숫자 ──
        static void DrawSquads(RectTransform box, BattleSetup setup, BattleResult result, Func<string, UnitDef> unit)
        {
            var lostBySquad = new Dictionary<string, int>();
            foreach (SquadLoss l in result.SquadLosses) lostBySquad[l.SquadId] = l.LostCount;
            var order = new List<string>();
            var start = new Dictionary<string, int>();
            var lost = new Dictionary<string, int>();
            foreach (SquadState s in setup.Squads)
            {
                if (s.Count <= 0) continue;
                if (!start.ContainsKey(s.UnitDefId)) { order.Add(s.UnitDefId); start[s.UnitDefId] = 0; lost[s.UnitDefId] = 0; }
                start[s.UnitDefId] += s.Count;
                lost[s.UnitDefId] += lostBySquad.TryGetValue(s.SquadId, out int n) ? n : 0;
            }

            if (order.Count == 0) { Text(box, "참가 분대 없음", new Vector2(0f, -40f), 22, UiStyle.TextDim, new Vector2(300f, 40f)); return; }
            float rowHeight = Mathf.Min(96f, 300f / order.Count);
            float barWidth = box.rect.width > 1f ? box.rect.width - 150f : 300f;
            for (int i = 0; i < order.Count; i++)
            {
                UnitDef def = unit(order[i]);
                float y = 70f - i * rowHeight - rowHeight * 0.5f;
                Portrait(box, def, new Vector2(-box.rect.width / 2f + 48f, y), rowHeight * 0.95f);
                int total = start[order[i]], gone = Mathf.Min(lost[order[i]], total);
                float left = -box.rect.width / 2f + 104f + barWidth / 2f;
                Rect(box, "Track", new Vector2(left, y - 10f), new Vector2(barWidth, 16f), Track);
                float keep = total > 0 ? (float)(total - gone) / total : 1f;
                Rect(box, "Kept", new Vector2(left - barWidth / 2f + barWidth * keep / 2f, y - 10f), new Vector2(barWidth * keep, 16f), Good);
                if (gone > 0) Rect(box, "Lost", new Vector2(left - barWidth / 2f + barWidth * keep + barWidth * (1f - keep) / 2f, y - 10f), new Vector2(barWidth * (1f - keep), 16f), Bad);
                string name = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : order[i];
                Text(box, gone > 0 ? $"{name}  {total:N0} → {total - gone:N0}  (−{gone:N0})" : $"{name}  {total:N0}  손실 없음",
                    new Vector2(left, y + 14f), 20, gone > 0 ? UiStyle.Body : UiStyle.TextDim, new Vector2(barWidth, 28f), TextAnchor.MiddleLeft);
            }
        }

        // ── 영웅: 초상 + 체력 막대 + 생존/사망 ──
        static void DrawHeroes(RectTransform box, BattleSetup setup, BattleResult result, Func<string, UnitDef> unit)
        {
            var outcomes = new Dictionary<string, HeroOutcome>();
            foreach (HeroOutcome h in result.HeroOutcomes) outcomes[h.InstanceId] = h;
            if (setup.Heroes.Count == 0) { Text(box, "영웅 없음", new Vector2(0f, -40f), 22, UiStyle.TextDim, new Vector2(300f, 40f)); return; }
            float rowHeight = Mathf.Min(120f, 300f / setup.Heroes.Count);
            float barWidth = box.rect.width > 1f ? box.rect.width - 160f : 280f;
            for (int i = 0; i < setup.Heroes.Count; i++)
            {
                HeroState start = setup.Heroes[i];
                UnitDef def = unit(start.UnitDefId);
                bool took = outcomes.TryGetValue(start.InstanceId, out HeroOutcome o);
                int hp = took ? o.HpAfter : start.Hp;
                bool alive = took ? o.IsAliveAfter : start.IsAlive;
                int max = def != null && def.MaxHp > 0 ? def.MaxHp : Mathf.Max(1, start.Hp);
                float y = 70f - i * rowHeight - rowHeight * 0.5f;
                Portrait(box, def, new Vector2(-box.rect.width / 2f + 52f, y), rowHeight * 0.95f, alive ? Color.white : new Color(0.4f, 0.4f, 0.4f));
                float left = -box.rect.width / 2f + 112f + barWidth / 2f;
                float ratio = Mathf.Clamp01((float)hp / max);
                Rect(box, "Track", new Vector2(left, y - 10f), new Vector2(barWidth, 16f), Track);
                Rect(box, "Hp", new Vector2(left - barWidth / 2f + barWidth * ratio / 2f, y - 10f), new Vector2(barWidth * ratio, 16f), alive ? RatioColor(ratio) : Bad);
                string name = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : start.UnitDefId;
                Text(box, alive ? $"{name}  체력 {hp:N0}/{max:N0}{(took ? "" : " · 미참전")}" : $"{name}  쓰러짐",
                    new Vector2(left, y + 14f), 20, alive ? UiStyle.Body : UiStyle.Danger, new Vector2(barWidth, 28f), TextAnchor.MiddleLeft);
            }
        }

        // 병종 그림 시트의 첫 칸(256×384 격자, 위 왼쪽)을 초상으로. 서 있기 그림이 있으면 그것
        static void Portrait(RectTransform parent, UnitDef def, Vector2 at, float height, Color? tint = null)
        {
            Texture2D sheet = def != null ? (def.IdleSheet != null ? def.IdleSheet : def.Sheet) : null;
            var obj = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = at;
            rect.sizeDelta = new Vector2(height * 2f / 3f, height);
            var image = obj.GetComponent<RawImage>();
            image.raycastTarget = false;
            if (sheet == null) { image.color = new Color(1f, 1f, 1f, 0.1f); return; }
            image.texture = sheet;
            int cols = Mathf.Max(1, Mathf.RoundToInt(sheet.width / 256f)), rows = Mathf.Max(1, Mathf.RoundToInt(sheet.height / 384f));
            image.uvRect = new UnityEngine.Rect(0f, 1f - 1f / rows, 1f / cols, 1f / rows);
            image.color = tint ?? Color.white;
        }

        static RectTransform Column(RectTransform area, string name, float x, float width, string title)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(area, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, 0f);
            obj.GetComponent<Image>().color = UiStyle.PanelDeep;
            obj.GetComponent<Image>().raycastTarget = false;
            UiStyle.Border(obj.GetComponent<Image>(), 0.5f, 1f);
            var titleText = Text(rect, title, Vector2.zero, 26, UiStyle.Brass, new Vector2(width, 40f));
            titleText.rectTransform.anchorMin = titleText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, -28f);
            // 그리기 기준: 가운데 (아래 그림들은 가운데 기준 좌표)
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(rect, false);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(width, 360f);
            return content;
        }

        static void Rect(RectTransform parent, string name, Vector2 at, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
            var image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static Text Text(RectTransform parent, string text, Vector2 at, int size, Color color, Vector2 box, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var obj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = at;
            rect.sizeDelta = box;
            var t = obj.GetComponent<Text>();
            t.font = ScreenView.UiFont;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        static Color RatioColor(float ratio) => ratio >= 0.7f ? Good : ratio >= 0.35f ? Warn : Bad;
    }
}
