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
    // 4번 결과 화면 (D-076 → K-047): 양피지 전투 보고서 한 장. 머리 = 승패 도장 + 처치·골드, 몸 = 성벽 도식 · 병종별 손실 막대 · 영웅 초상.
    // 칸은 상자 대신 잉크 세로줄로 나눈다. 그림(성 도식·흉상)은 Codex 확정 전이라 도형·병종 시트 첫 칸으로 그린다
    public static class ResultBoard
    {
        const float ColumnGap = UiTokens.S48;

        // 머리 줄: [도장 승/패] 승리 · [처치 아이콘] 1,234 · [골드 아이콘] +120
        public static void BuildHead(RectTransform area, BattleResult result)
        {
            bool victory = result.Outcome == BattleOutcome.Victory;
            HorizontalLayoutGroup row = Ui.Row(area, "Head Row", UiTokens.S24);
            Ui.Stretch((RectTransform)row.transform);
            Image seal = Ui.Icon(row.transform, "Seal", UiArt.Seal, 1f, Color.white);   // K-123 도장: 황동 고리 그림 (승패 색은 글자에)
            Text sealText = Ui.Label(seal.transform, "Mark", victory ? "승" : "패", UiTokens.H2, victory ? UiTokens.GoodInk : UiTokens.DangerInk, TextAnchor.MiddleCenter, true);
            Ui.Stretch(sealText.rectTransform);
            Text outcome = Ui.Label(row.transform, "Outcome", victory ? "승리" : "패배", UiTokens.H1, victory ? UiTokens.GoodInk : UiTokens.DangerInk, TextAnchor.MiddleLeft, true);
            Ui.Size(outcome, outcome.preferredWidth + 8f, 96f);
            Stat(row.transform, "Icons/stat_kills", "처치", result.DefeatedEnemyCount.ToString("N0"));
            Stat(row.transform, "Icons/stat_gold", "골드", "+" + result.EarnedGold.ToString("N0"));
        }

        static void Stat(Transform row, string icon, string word, string value)
        {
            RectTransform gap = Ui.Rect(row, "Gap");
            Ui.Size(gap, UiTokens.S24, 96f);
            Image picture = Ui.HudIcon(row, word + " Icon", icon, 32f);
            if (picture == null)
            {
                Text label = Ui.Label(row, word, word, UiTokens.Text, UiTokens.InkSoft);
                Ui.Size(label, label.preferredWidth + 4f, 96f);
            }

            Text number = Ui.Label(row, word + " Value", value, UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            Ui.Size(number, number.preferredWidth + 8f, 96f);
        }

        public static void Build(RectTransform area, BattleSetup setup, BattleResult result, Func<string, UnitDef> unit)
        {
            float width = area.rect.width > 1f ? area.rect.width : area.sizeDelta.x > 1f ? area.sizeDelta.x : 1504f;
            float column = (width - ColumnGap * 2f) / 3f;
            RectTransform walls = Column(area, "Walls", 0f, column, "성벽");
            RectTransform squads = Column(area, "Squads", column + ColumnGap, column, "병력");
            RectTransform heroes = Column(area, "Heroes", (column + ColumnGap) * 2f, column, "주인공과 마법사");
            for (int i = 1; i < 3; i++)
            {
                // 칸 사이 잉크 세로줄 (한 점)
                Image line = Ui.Image(area, "Divider", UiTokens.Alpha(UiTokens.Ink, 0.35f));
                line.rectTransform.anchorMin = new Vector2(0f, 0f);
                line.rectTransform.anchorMax = new Vector2(0f, 1f);
                line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                line.rectTransform.anchoredPosition = new Vector2(column * i + ColumnGap * (i - 0.5f), 0f);
                line.rectTransform.sizeDelta = new Vector2(UiTokens.Dot, -UiTokens.S24);
            }

            DrawWalls(walls, setup, result);
            DrawSquads(squads, setup, result, unit);
            DrawHeroes(heroes, setup, result, unit);
        }

        // ── 성벽: 성을 위에서 본 도식 (K-123 그림: 마당 + 4면 × 체력 4단계 + 등대 탑), 옆에 이번 피해 (멀쩡한 면은 글 없이 흐리게) ──
        static void DrawWalls(RectTransform box, BattleSetup setup, BattleResult result)
        {
            var after = new Dictionary<string, int>();
            foreach (WallOutcome w in result.WallOutcomes) after[w.SegmentId] = w.HpAfter;
            Sprite courtyard = UiArt.Get(UiArt.FortressCourtyard);
            bool art = courtyard != null;
            float size = art ? 192f : 216f, thick = 24f;
            var center = new Vector2(0f, -24f);
            if (art) Picture(box, "Yard", courtyard, center);
            else Rect(box, "Yard", center, new Vector2(size - thick * 2f, size - thick * 2f), UiTokens.Alpha(UiTokens.PaperShade, 0.6f));
            string[] ids = { WallIds.North, WallIds.East, WallIds.South, WallIds.West };
            string[] names = { "북", "동", "남", "서" };
            string[] artSides = { "north", "east", "south", "west" };
            Vector2[] offsets = { new Vector2(0f, (size - thick) / 2f), new Vector2((size - thick) / 2f, 0f), new Vector2(0f, -(size - thick) / 2f), new Vector2(-(size - thick) / 2f, 0f) };
            Vector2[] sizes = { new Vector2(size, thick), new Vector2(thick, size), new Vector2(size, thick), new Vector2(thick, size) };
            Vector2[] labelOffsets = { new Vector2(0f, size / 2f + 32f), new Vector2(size / 2f + 72f, 0f), new Vector2(0f, -size / 2f - 32f), new Vector2(-size / 2f - 72f, 0f) };
            for (int i = 0; i < 4; i++)
            {
                WallSegmentState start = setup.Wall.Find(w => w.SegmentId == ids[i]);
                if (start == null) continue;
                int hpAfter = after.TryGetValue(ids[i], out int a) ? a : start.Hp;
                float ratio = start.MaxHp > 0 ? Mathf.Clamp01((float)hpAfter / start.MaxHp) : 1f;
                Sprite wall = art ? UiArt.Get(UiArt.FortressWall(artSides[i], ratio)) : null;
                if (wall != null) Picture(box, "Wall " + names[i], wall, center);   // 면마다 192×192 투명 판 — 같은 자리에 겹친다
                else
                {
                    Rect(box, "Wall " + names[i], center + offsets[i], sizes[i], RatioColor(ratio));
                    Rect(box, "Wall Edge " + names[i], center + offsets[i], sizes[i] + new Vector2(UiTokens.Dot * 2f, UiTokens.Dot * 2f), UiTokens.Ink).SetAsFirstSibling();
                }

                int damage = start.Hp - hpAfter;
                Text label = Text(box, damage > 0 ? $"{names[i]} −{damage:N0}" : names[i], center + labelOffsets[i], UiTokens.Text,
                    UiTokens.Ink, new Vector2(136f, 32f), TextAnchor.MiddleCenter, true);
                if (damage > 0 && ratio < 0.35f) label.color = UiTokens.DangerInk;
            }

            Sprite tower = art ? UiArt.Get(UiArt.FortressTower) : null;
            if (tower != null) Picture(box, "Tower", tower, center + new Vector2(0f, 6f));
        }

        // K-123 그림 한 장 (원래 크기, 가운데 기준)
        static void Picture(RectTransform parent, string name, Sprite sprite, Vector2 at)
        {
            Image image = Ui.Image(parent, name, Color.white);
            image.sprite = sprite;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = at;
            rect.sizeDelta = UiArt.Size(sprite);
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

            if (order.Count == 0) return;   // 빈 상태 글을 쓰지 않는다 (S-21)
            float rowHeight = Mathf.Min(96f, 312f / order.Count);
            float barWidth = box.rect.width > 1f ? box.rect.width - 152f : box.sizeDelta.x - 152f;
            for (int i = 0; i < order.Count; i++)
            {
                UnitDef def = unit(order[i]);
                float y = 120f - i * rowHeight - rowHeight * 0.5f;
                float half = box.sizeDelta.x / 2f;
                Portrait(box, def, new Vector2(-half + 48f, y), rowHeight * 0.95f);
                int total = start[order[i]], gone = Mathf.Min(lost[order[i]], total);
                float left = -half + 104f + barWidth / 2f;
                float keep = total > 0 ? (float)(total - gone) / total : 1f;
                if (!BarArt(box, new Vector2(left, y - 12f), barWidth, 0f, 1f, UiArt.BarTrackSmall, null))
                {
                    Rect(box, "Track", new Vector2(left, y - 12f), new Vector2(barWidth, 18f), UiTokens.Ink);
                    float inner = barWidth - UiTokens.Dot * 2f;
                    Rect(box, "Kept", new Vector2(left - inner / 2f + inner * keep / 2f, y - 12f), new Vector2(inner * keep, 12f), UiTokens.Good);
                    if (gone > 0) Rect(box, "Lost", new Vector2(left - inner / 2f + inner * keep + inner * (1f - keep) / 2f, y - 12f), new Vector2(inner * (1f - keep), 12f), UiTokens.Danger);
                }
                else
                {
                    // K-123 막대: 남은 부분 = 좋음(초록), 잃은 부분 = 위험(붉음)
                    BarArt(box, new Vector2(left, y - 12f), barWidth, 0f, keep, UiArt.BarFill("good", false), "Kept");
                    if (gone > 0) BarArt(box, new Vector2(left, y - 12f), barWidth, keep, 1f, UiArt.BarFill("danger", false), "Lost");
                }
                string name = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : order[i];
                Text(box, gone > 0 ? $"{name}  {total - gone:N0}/{total:N0}  −{gone:N0}" : $"{name}  {total:N0}",
                    new Vector2(left, y + 16f), UiTokens.Text, UiTokens.Ink, new Vector2(barWidth, 32f), TextAnchor.MiddleLeft);
            }
        }

        // ── 주인공과 마법사: 초상 + 체력 막대 (쓰러지면 초상이 어둡고 막대가 붉다) ──
        static void DrawHeroes(RectTransform box, BattleSetup setup, BattleResult result, Func<string, UnitDef> unit)
        {
            var outcomes = new Dictionary<string, HeroOutcome>();
            foreach (HeroOutcome h in result.HeroOutcomes) outcomes[h.InstanceId] = h;
            if (setup.Heroes.Count == 0) return;
            float rowHeight = Mathf.Min(128f, 312f / setup.Heroes.Count);
            float barWidth = box.sizeDelta.x - 168f;
            for (int i = 0; i < setup.Heroes.Count; i++)
            {
                HeroState start = setup.Heroes[i];
                UnitDef def = unit(start.UnitDefId);
                bool took = outcomes.TryGetValue(start.InstanceId, out HeroOutcome o);
                int hp = took ? o.HpAfter : start.Hp;
                bool alive = took ? o.IsAliveAfter : start.IsAlive;
                int max = def != null && def.MaxHp > 0 ? def.MaxHp : Mathf.Max(1, start.Hp);
                float y = 120f - i * rowHeight - rowHeight * 0.5f;
                float half = box.sizeDelta.x / 2f;
                Portrait(box, def, new Vector2(-half + 52f, y), rowHeight * 0.95f, alive ? Color.white : new Color(0.35f, 0.3f, 0.28f));
                float left = -half + 112f + barWidth / 2f;
                float ratio = Mathf.Clamp01((float)hp / max);
                if (!BarArt(box, new Vector2(left, y - 12f), barWidth, 0f, 1f, UiArt.BarTrackSmall, null))
                {
                    float inner = barWidth - UiTokens.Dot * 2f;
                    Rect(box, "Track", new Vector2(left, y - 12f), new Vector2(barWidth, 18f), UiTokens.Ink);
                    Rect(box, "Hp", new Vector2(left - inner / 2f + inner * ratio / 2f, y - 12f), new Vector2(inner * ratio, 12f), alive ? RatioColor(ratio) : UiTokens.Danger);
                }
                else
                {
                    string kind = !alive || ratio < 0.35f ? "danger" : ratio < 0.7f ? "brass" : "good";
                    BarArt(box, new Vector2(left, y - 12f), barWidth, 0f, ratio, UiArt.BarFill(kind, false), "Hp");
                }
                string name = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : start.UnitDefId;
                Text(box, alive ? $"{name}  {hp:N0}/{max:N0}" : $"{name}  쓰러짐", new Vector2(left, y + 16f), UiTokens.Text,
                    alive ? UiTokens.Ink : UiTokens.DangerInk, new Vector2(barWidth, 32f), TextAnchor.MiddleLeft);
            }
        }

        // 병종 그림 시트의 첫 칸을 초상으로. 서 있기 그림 우선, 6판은 288×384·발360 / 기존은256×384·발352.
        static void Portrait(RectTransform parent, UnitDef def, Vector2 at, float height, Color? tint = null)
        {
            Texture2D sheet = def != null ? (def.IdleSheet != null ? def.IdleSheet : def.Sheet) : null;
            if (sheet == null) return;
            bool six = sheet.width % 288 == 0 && sheet.width % 256 != 0;
            float cellWidth = six ? 288f : 256f;
            float footFromBottom = (384f - (six ? 360f : 352f)) / 384f;
            var obj = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, footFromBottom);
            // 기존 초상의 발선을 유지한다. 새 그림의 하단 투명 여백8px 차이를 위치에 반영한다.
            rect.anchoredPosition = at - new Vector2(0f, height * (0.5f - 32f / 384f));
            rect.sizeDelta = new Vector2(height * cellWidth / 384f, height);
            var image = obj.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.texture = sheet;
            int cols = Mathf.Max(1, Mathf.RoundToInt(sheet.width / cellWidth)), rows = Mathf.Max(1, Mathf.RoundToInt(sheet.height / 384f));
            image.uvRect = new UnityEngine.Rect(0f, 1f - 1f / rows, 1f / cols, 1f / rows);
            image.color = tint ?? Color.white;
        }

        // 칸: 제목(세계 글꼴) + 가운데 기준 그리기 판. 이름(Walls·Squads·Heroes)은 시험이 찾는다
        static RectTransform Column(RectTransform area, string name, float x, float width, string title)
        {
            var rect = Ui.Rect(area, name);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, 0f);
            Text heading = Ui.Label(rect, "Title", title, UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = heading.rectTransform.pivot = new Vector2(0f, 1f);
            heading.rectTransform.anchoredPosition = Vector2.zero;
            heading.rectTransform.sizeDelta = new Vector2(width, 48f);
            var content = Ui.Rect(rect, "Content");
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(width, 360f);
            return content;
        }

        // K-123 막대 조각 한 장: 가운데 center, 전체 너비 width 중 [from, to] 구간을 9칸 늘이기로 채운다 (name이 null이면 홈 그림). 그림이 없으면 false
        static bool BarArt(RectTransform parent, Vector2 center, float width, float from, float to, string art, string name)
        {
            Sprite sprite = UiArt.Get(art);
            if (sprite == null || to <= from) return sprite != null;
            Image image = Ui.Image(parent, name ?? "Track", Color.white);
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(center.x - width / 2f + width * (from + to) / 2f, center.y);
            rect.sizeDelta = new Vector2(width * (to - from), UiArt.Size(sprite).y);
            return true;
        }
        static RectTransform Rect(RectTransform parent, string name, Vector2 at, Vector2 size, Color color)
        {
            Image image = Ui.Image(parent, name, color);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
            return rect;
        }

        static Text Text(RectTransform parent, string text, Vector2 at, int size, Color color, Vector2 box, TextAnchor anchor = TextAnchor.MiddleCenter, bool display = false)
        {
            Text t = Ui.Label(parent, "Text", text, size, color, anchor, display);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.rectTransform.anchoredPosition = at;
            t.rectTransform.sizeDelta = box;
            return t;
        }

        static Color RatioColor(float ratio) => ratio >= 0.7f ? UiTokens.Good : ratio >= 0.35f ? UiTokens.Brass : UiTokens.Danger;
    }
}
