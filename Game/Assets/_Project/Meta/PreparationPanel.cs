using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HumanLastRampart.UI;

namespace HumanLastRampart.Meta
{
    // 병영·성벽 수리 장부 (K-047): 어둠막 위 양피지 한 장.
    // 위: 이름 + 골드 + 닫기 / 성벽 탭 4개(남은 체력 막대) / 병영 = 지점 칸(2열) + 병종·인원·값 / 수리 = 세 가지 수리 + 값.
    // 값은 고를 때마다 바로 보여 준다(견적 단추 없음 — 규칙 설명 대신 숫자, S-17). 확정은 황동 단추 하나.
    // 거래는 PreparationTransactions의 견적(TryRecruit·TryRepair) → 확정(TryCommit) 그대로
    internal sealed class PreparationPanel
    {
        enum RepairChoice { Hundred, Wall, All }

        readonly Button source;
        readonly CanvasGroup behind;
        readonly IScreenContext context;
        readonly PreparationTransactions session;
        readonly bool recruit;
        readonly GameObject shade;
        readonly RectTransform body;
        readonly Text gold;
        readonly Button[] wallTabs = new Button[4];
        readonly UiBar[] wallBars = new UiBar[4];
        int wallIndex, postIndex, unitIndex, count = 1;
        RepairChoice repairChoice = RepairChoice.Hundred;
        PreparationTransactions.Quote quote;
        string reason;
        string done;   // 방금 확정한 결과 (다시 고르기 전까지 같은 견적은 확정할 수 없다)

        // 병영 안 탭 (K-058): PrepEntry가 [병종 훈련]·[용병 고용] 여는 길을 넣어 둔다
        internal static Action OpenRecruit, OpenTraining, OpenMercenary;

        public static void Show(Button source, IScreenContext context, PreparationTransactions session, bool recruit)
        {
            if (source == null || !source.IsInteractable()) return;
            new PreparationPanel(source, context, session, recruit);
        }

        PreparationPanel(Button source, IScreenContext context, PreparationTransactions session, bool recruit)
        {
            this.source = source; this.context = context; this.session = session; this.recruit = recruit;
            behind = source.transform.parent.GetComponentInParent<CanvasGroup>();   // 정비 띠 판 (단추 자신의 숨김 묶음이 아니라)
            if (behind != null) behind.interactable = false;
            Transform layer = source.GetComponentInParent<Canvas>().transform;
            Image dim = Ui.Shade(layer, "Prep " + (recruit ? "Recruit" : "Repair"));
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

            // 머리 줄: 병영 / 성벽 수리 · · · [골드] 1,240 [닫기]
            HorizontalLayoutGroup head = Ui.Row(sheet.transform, "Head", UiTokens.S16);
            Ui.Size(head, -1f, 48f);
            Text title = Ui.Label(head.transform, "Title", recruit ? "병영" : "성벽 수리", UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            Ui.Size(title, 0f, 48f, true);
            if (recruit)   // 병영 안 다른 일 (K-058): 훈련·용병 — 이 장부를 닫고 그 장부를 연다
            {
                BarracksTabs(head.transform, 0, () => Close(true));
            }
            if (Ui.HudIcon(head.transform, "Gold Icon", "Icons/stat_gold", 32f) == null) Ui.Size(Ui.Label(head.transform, "Gold Word", "골드", UiTokens.Small, UiTokens.InkSoft), 44f, 48f);
            gold = Ui.Label(head.transform, "Gold", "", UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            Ui.Size(gold, 136f, 48f);
            Button close = Ui.IconButton(head.transform, "닫기", UiArt.IconClose, Close, 48f);

            // 성벽 탭: 이름 + 남은 체력 막대
            // (수리 장부는 아래 수리 지도에서 성벽을 직접 누른다 — 탭은 병영 장부에만)
            HorizontalLayoutGroup tabs = recruit ? Ui.Row(sheet.transform, "Walls", UiTokens.S8) : null;
            for (int i = 0; i < 4 && recruit; i++)
            {
                int index = i;
                Button tab = Ui.Button(tabs.transform, "성벽:" + WallIds.All[i], PreparationTransactions.WallName(WallIds.All[i]), () =>
                {
                    if (wallIndex == index) return;
                    wallIndex = index; postIndex = unitIndex = 0; count = 1; Changed();
                }, UiButtonKind.Tab);
                Ui.Size(tab, 248f, 72f);
                Text label = Ui.LabelOf(tab);
                label.alignment = TextAnchor.UpperCenter;
                label.rectTransform.offsetMax = new Vector2(-UiTokens.S16, -UiTokens.S8);
                UiBar bar = Ui.Bar(tab.transform.Find("Content"), "Hp", 200f, 15f);
                Ui.Place((RectTransform)bar.transform, new Vector2(0.5f, 0f), new Vector2(0f, UiTokens.S16), new Vector2(200f, 15f), new Vector2(0.5f, 0f));
                wallTabs[i] = tab;
                wallBars[i] = bar;
            }

            body = (RectTransform)Ui.Column(sheet.transform, "Body", UiTokens.S16).transform;
            Render();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(close.gameObject);
        }

        void Render()
        {
            foreach (Transform child in body) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
            var campaign = context.Campaign;
            gold.text = campaign.Gold.ToString("N0");
            for (int i = 0; i < 4; i++)
            {
                if (wallTabs[i] == null) continue;   // 수리 장부는 탭이 없다 (수리 지도)
                var w = campaign.FindWall(WallIds.All[i]);
                if (w != null) wallBars[i].Set(w.MaxHp > 0 ? (float)w.Hp / w.MaxHp : 1f);
                Ui.SetOn(wallTabs[i], i == wallIndex);
            }

            string wallId = WallIds.All[wallIndex];
            if (recruit) RenderRecruit(wallId);
            else RenderRepair(wallId);
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null) EventSystem.current.SetSelectedGameObject(null);
        }

        // ── 병영: 왼쪽 지점 칸(2열) / 오른쪽 병종·인원·값·모집 ──
        void RenderRecruit(string wallId)
        {
            var campaign = context.Campaign;
            var posts = new List<PostDef>();
            foreach (var p in context.Definitions.All<PostDef>()) if (p.WallId == wallId) posts.Add(p);
            posts.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            if (posts.Count == 0) { DataError("선택 성벽의 배치 지점이 없습니다"); return; }
            postIndex %= posts.Count;
            var post = posts[postIndex];

            HorizontalLayoutGroup split = Ui.Row(body, "Split", UiTokens.S48, TextAnchor.UpperLeft);
            split.childAlignment = TextAnchor.UpperLeft;
            var grid = Ui.Rect(split.transform, "Posts").gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(248f, 56f);
            grid.spacing = new Vector2(UiTokens.S8, UiTokens.S8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            Ui.Size(grid, 504f, -1f);
            for (int i = 0; i < posts.Count; i++)
            {
                int index = i;
                var p = posts[i];
                var squadAt = campaign.Squads.Find(s => s.PostId == p.Id);
                int capAt = p.Kind == PostKind.Cannon ? session.Rules.CannonCrewPostCapacity : session.Rules.SquadPostCapacity;
                // 분대 종류를 이름 옆에 (사용자 2026-10-20 #14: 총 분대인지 활 분대인지 안 보였다)
                string kindName = squadAt != null ? context.Definitions.Get<UnitDef>(squadAt.UnitDefId)?.DisplayName : null;
                string cellText = kindName != null ? $"{p.DisplayName} · {kindName}" : p.DisplayName;
                Button cell = Ui.Button(grid.transform, "지점:" + p.Id, cellText, () => { if (postIndex == index) return; postIndex = index; unitIndex = 0; count = 1; Changed(); }, UiButtonKind.Tab);
                Text label = Ui.LabelOf(cell);
                label.alignment = TextAnchor.MiddleLeft;
                label.fontSize = UiTokens.Small;
                Text fill = Ui.Label(cell.transform.Find("Content"), "Count", $"{squadAt?.Count ?? 0}/{capAt}", UiTokens.Small, UiTokens.BodyDim, TextAnchor.MiddleRight);
                Ui.Stretch(fill.rectTransform);
                fill.rectTransform.offsetMax = new Vector2(-UiTokens.S16, 0f);
                Ui.SetOn(cell, i == postIndex);
            }

            VerticalLayoutGroup right = Ui.Column(split.transform, "Order", UiTokens.S16);
            Ui.Size(right, 472f, -1f);
            var squad = campaign.Squads.Find(s => s.PostId == post.Id);
            var units = new List<UnitDef>();
            foreach (var u in context.Definitions.All<UnitDef>())
                if (squad != null ? u.Id == squad.UnitDefId : post.Kind == PostKind.Cannon ? u.Kind == UnitKind.CannonCrew : u.Kind == UnitKind.Archer || u.Kind == UnitKind.Gunner) units.Add(u);
            units.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            if (units.Count == 0) { DataError("모집 가능한 병종 정의가 없습니다"); return; }
            unitIndex %= units.Count;
            var unit = units[unitIndex];

            string squadLine = squad != null ? $"{post.DisplayName} — {unit.DisplayName} 분대 {squad.Count}명" : $"{post.DisplayName} — 빈 지점 (병종을 고르세요)";
            Text where = Ui.Label(right.transform, "Post", squadLine, UiTokens.Text, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            Ui.Size(where, -1f, 32f);
            HorizontalLayoutGroup kinds = Ui.Row(right.transform, "Units", UiTokens.S8);
            for (int i = 0; i < units.Count; i++)
            {
                int index = i;
                Button chip = Ui.Button(kinds.transform, "병종:" + units[i].Id, units[i].DisplayName, () => { if (unitIndex == index) return; unitIndex = index; count = 1; Changed(); }, UiButtonKind.Tab);
                Ui.Size(chip, 152f, 48f);
                Ui.SetOn(chip, i == unitIndex);
                chip.interactable = squad == null;   // 기존 분대는 같은 병종만 보충
            }

            // 인원: [−10] [−] 11 [+] [+10] — 최대는 그 지점에 남은 자리 (대포 지점은 대포 정원)
            int room = Math.Max(1, (post.Kind == PostKind.Cannon ? session.Rules.CannonCrewPostCapacity : session.Rules.SquadPostCapacity) - (squad?.Count ?? 0));
            count = Math.Min(count, room);
            HorizontalLayoutGroup counter = Ui.Row(right.transform, "Count", UiTokens.S8);
            Ui.Size(counter, -1f, 56f);
            Button tenDown = Ui.Button(counter.transform, "인원 −10", "−10", () => { count = Math.Max(1, count - 10); Changed(); });
            Ui.Size(tenDown, 80f, 56f);
            Ui.IconButton(counter.transform, "인원 −1", UiArt.IconMinus, () => { count = Math.Max(1, count - 1); Changed(); }, 56f);
            Text number = Ui.Label(counter.transform, "Number", $"{count}명", UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleCenter, true);
            Ui.Size(number, 112f, 56f);
            Ui.IconButton(counter.transform, "인원 +1", UiArt.IconPlus, () => { count = Math.Min(room, count + 1); Changed(); }, 56f);
            Button ten = Ui.Button(counter.transform, "인원 +10", "+10", () => { count = Math.Min(room, count + 10); Changed(); });
            Ui.Size(ten, 80f, 56f);

            bool can = session.TryRecruit(post.Id, unit.Id, count, out quote, out reason, out bool error);
            if (error) { DataError(reason); return; }
            if (!can) quote = null;

            Price(right.transform);
            Button commit = Ui.Button(right.transform, "확정 모집", $"{count}명 모집", Commit, UiButtonKind.Primary);
            Ui.Size(commit, -1f, 64f);
            commit.interactable = quote != null;
        }

        // ── 수리: 세 가지 (100 · 이 성벽 모두 · 네 성벽 모두), 칸마다 값 ──
        void RenderRepair(string wallId)
        {
            var campaign = context.Campaign;
            var wall = campaign.FindWall(wallId);
            string[] keys = { "수리:100", "수리:최대", "수리:전체" };
            string[] names = { $"+{session.Rules.DefaultRepairHp}", "이 성벽 전부", "모든 성벽 전부" };
            int[] costs = new int[3];
            string[] whyNot = new string[3];
            for (int i = 0; i < 3; i++)
            {
                bool ok = TryRepair((RepairChoice)i, wallId, out var q, out whyNot[i], out bool error);
                if (error) { DataError(whyNot[i]); return; }
                costs[i] = ok ? q.Cost : -1;
            }

            // 마지막에 고른 쪽으로 다시 견적 — 확정은 이 견적 하나 (견적은 하나만 살아 있다)
            bool can = TryRepair(repairChoice, wallId, out quote, out reason, out bool failed);
            if (failed) { DataError(reason); return; }
            if (!can) quote = null;

            // 왼쪽 성 지도 / 오른쪽 이름·체력 막대(수리 뒤 옅은 그림자)·세 가지 수리·값·확정
            HorizontalLayoutGroup split = Ui.Row(body, "Split", UiTokens.S24, TextAnchor.UpperLeft);
            RepairMapView.Build(split.transform, wallIndex, i =>
            {
                var w = campaign.FindWall(WallIds.All[i]);
                return w != null ? (w.Hp, w.MaxHp) : (0, 1);
            }, index => { if (wallIndex == index) return; wallIndex = index; postIndex = unitIndex = 0; count = 1; Changed(); });

            VerticalLayoutGroup right = Ui.Column(split.transform, "Order", UiTokens.S8);
            Ui.Size(right, 500f, -1f);
            RepairWallState wallState = RepairWallLook.StateOf(wall.Hp, wall.MaxHp);
            Text title = Ui.Label(right.transform, "Wall", $"{PreparationTransactions.WallName(wallId)} 성벽 · {RepairWallLook.StateName(wallState)}", UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            Ui.Size(title, -1f, 44f);
            int after = quote == null ? wall.Hp : repairChoice == RepairChoice.Hundred ? (int)Math.Min((long)wall.MaxHp, (long)wall.Hp + session.Rules.DefaultRepairHp) : wall.MaxHp;
            RepairMapView.HpBar(right.transform, 500f, 24f, wall.Hp, wall.MaxHp, after);
            string hpLine = after > wall.Hp ? $"{wall.Hp:N0} / {wall.MaxHp:N0}  →  수리 뒤 {after:N0}" : $"{wall.Hp:N0} / {wall.MaxHp:N0}";
            Text hpText = Ui.Label(right.transform, "Hp", hpLine, UiTokens.Text, UiTokens.InkSoft);
            Ui.Size(hpText, -1f, 32f);

            for (int i = 0; i < 3; i++)
            {
                var choice = (RepairChoice)i;
                Button card = Ui.Button(right.transform, keys[i], names[i], () => { if (repairChoice == choice) return; repairChoice = choice; Changed(); }, UiButtonKind.Tab);
                Ui.Size(card, 500f, 56f);
                card.interactable = costs[i] >= 0;   // 골드가 모자라거나 이미 가득이면 누를 수 없다
                Text label = Ui.LabelOf(card);
                label.alignment = TextAnchor.MiddleLeft;
                string costText = costs[i] >= 0 ? $"{costs[i]:N0}골드" : Short(whyNot[i]);
                Text cost = Ui.Label(card.transform.Find("Content"), "Cost", costText, UiTokens.Small, costs[i] >= 0 ? UiTokens.BrassLight : UiTokens.BodyDim, TextAnchor.MiddleRight);
                Ui.Stretch(cost.rectTransform, UiTokens.S16);
                Ui.SetOn(card, choice == repairChoice);
            }

            Price(right.transform, 440f);
            Button commit = Ui.Button(right.transform, "확정 수리", "수리", Commit, UiButtonKind.Primary);
            Ui.Size(commit, -1f, 64f);
            commit.interactable = quote != null;
        }

        bool TryRepair(RepairChoice choice, string wallId, out PreparationTransactions.Quote q, out string why, out bool error)
        {
            int amount = choice == RepairChoice.Hundred ? session.Rules.DefaultRepairHp : int.MaxValue;
            return session.TryRepair(choice == RepairChoice.All ? null : wallId, amount, choice == RepairChoice.All, out q, out why, out error);
        }

        // 값 줄: [골드] 55 → 남는 골드 1,185 / 안 되면 이유 한 줄 (붉게) / 방금 확정했으면 도장 글
        void Price(Transform parent, float reasonWidth = 900f)
        {
            HorizontalLayoutGroup line = Ui.Row(parent, "Price", UiTokens.S8);
            Ui.Size(line, -1f, 40f);
            // 방금 확정한 것은 알림으로만 — 다음 견적은 이어서 보이고 바로 또 할 수 있다 (사용자 2026-10-20 #15)
            if (done != null)
            {
                Ui.Icon(line.transform, "Done", UiArt.IconCheck, 1f, UiTokens.Good);
                Text text = Ui.Label(line.transform, "Text", done, UiTokens.Text, UiTokens.Ink);
                Ui.Size(text, reasonWidth < 900f ? 110f : 160f, 40f);
            }

            if (quote == null)
            {
                Text why = Ui.Label(line.transform, "Reason", Short(reason), UiTokens.Text, UiTokens.DangerInk);
                // 확정 도장 글이 앞에 있으면 좁은 칸을 넘지 않게 그만큼 줄인다
                Ui.Size(why, done != null && reasonWidth < 900f ? reasonWidth - 150f : reasonWidth, 40f);
                return;
            }

            if (Ui.HudIcon(line.transform, "Gold Icon", "Icons/stat_gold", 32f) == null) Ui.Size(Ui.Label(line.transform, "Gold Word", "골드", UiTokens.Small, UiTokens.InkSoft), 44f, 40f);
            Text cost = Ui.Label(line.transform, "Cost", quote.Cost.ToString("N0"), UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true);
            Ui.Size(cost, cost.preferredWidth + 8f, 40f);
            Text left = Ui.Label(line.transform, "Left", $"남는 골드 {context.Campaign.Gold - quote.Cost:N0}", UiTokens.Text, UiTokens.InkSoft);
            Ui.Size(left, reasonWidth < 900f ? 230f : 320f, 40f);
        }

        // 거래 쪽 이유 문장을 한 줄로 (콜론 뒤 숫자 덩어리는 값 줄이 이미 보여 준다)
        static string Short(string why)
        {
            if (string.IsNullOrEmpty(why)) return "";
            int colon = why.IndexOf(':');
            return colon > 0 ? why.Substring(0, colon) : why;
        }

        void Commit()
        {
            var q = quote; quote = null;
            bool ok = session.TryCommit(q, out var why, out bool error);
            if (error) { DataError(why); return; }
            if (ok) { done = recruit ? "모집 완료" : "수리 완료"; count = 1; }
            else reason = why;
            RefreshSummary(source, context, session);
            Render();
        }

        void Changed() { quote = null; done = null; session.CancelQuote(); Render(); }

        void DataError(string why)
        {
            session.Close(); context.ReportDataError(why);
            foreach (var b in shade.GetComponentsInChildren<Button>()) b.interactable = false;
            Transform close = shade.transform.Find("Ledger/Head/" + Ui.KeyPrefix + "닫기");
            if (close != null) close.GetComponent<Button>().interactable = true;   // 닫기는 남긴다
        }

        // 병영 안 탭 세 개 (모집·훈련·용병) — 세 장부가 같은 머리 탭을 쓴다. 바꿀 때는 지금 장부를 바로 치우고(겹쳐 보이지 않게) 다른 장부를 연다
        internal static void BarracksTabs(Transform head, int current, Action closeNow)
        {
            string[] keys = { "병영:모집", "병영:훈련", "병영:용병" };
            string[] names = { "모집", "병종 훈련", "용병 고용" };
            Action[] opens = { OpenRecruit, OpenTraining, OpenMercenary };
            for (int i = 0; i < 3; i++)
            {
                if (opens[i] == null) continue;
                Action open = opens[i];
                bool here = i == current;
                Button tab = Ui.Button(head, keys[i], names[i], () => { if (here) return; closeNow(); open(); }, UiButtonKind.Tab);
                Ui.Size(tab, i == 0 ? 112f : 160f, 48f);
                Ui.SetOn(tab, here);
            }
        }

        void Close() => Close(false);

        void Close(bool instant)
        {
            session.CancelQuote(); quote = null;
            if (behind != null) behind.interactable = true;
            if (instant) { shade.SetActive(false); UnityEngine.Object.Destroy(shade); }
            else Ui.Close(shade);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(source.gameObject);
        }

        // 정비 띠의 골드·병력 숫자 (PrepEntry가 만든 "Gold Value"·"Troops Value"). 함정 공방·지형 손질도 부른다
        internal static void RefreshSummary(Button source, IScreenContext context, PreparationTransactions session)
        {
            if (source == null) return;
            CanvasGroup band = source.transform.parent.GetComponentInParent<CanvasGroup>();
            if (band == null) return;
            foreach (var text in band.GetComponentsInChildren<Text>(true))
            {
                if (text.gameObject.name == "Gold Value") text.text = context.Campaign.Gold.ToString("N0");
                else if (text.gameObject.name == "Troops Value") text.text = $"{session.TotalCount():N0}/{session.Rules.GlobalCapacity:N0}";
            }
        }
    }
}
