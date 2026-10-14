using System;
using System.Collections.Generic;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HumanLastRampart.Meta
{
    // Prep-only scrollable modal lives under ScreenView's canvas; no common UI edits.
    internal sealed class PreparationPanel
    {
        readonly Button source;
        readonly CanvasGroup behind;
        readonly IScreenContext context;
        readonly PreparationTransactions session;
        readonly bool recruit;
        readonly RectTransform shade, content;
        readonly ScrollRect scroll;
        readonly Font font;
        int wallIndex, postIndex, unitIndex, count = 1;
        string message;
        PreparationTransactions.Quote quote;

        public static void Show(Button source, IScreenContext context, PreparationTransactions session, bool recruit)
        {
            if (source == null || !source.IsInteractable()) return;
            new PreparationPanel(source, context, session, recruit);
        }

        PreparationPanel(Button source, IScreenContext context, PreparationTransactions session, bool recruit)
        {
            this.source = source; this.context = context; this.session = session; this.recruit = recruit;
            behind = source.transform.parent.GetComponent<CanvasGroup>(); behind.interactable = false;
            font = source.GetComponentInChildren<Text>().font;
            shade = Make(source.transform.parent.parent, "Prep " + (recruit ? "Recruit" : "Repair"), typeof(Image));
            Stretch(shade); shade.GetComponent<Image>().color = new Color(0, 0, 0, 0.96f);
            var area = Make(shade, "Prep Scroll", typeof(ScrollRect));
            area.anchorMin = new Vector2(0.12f, 0.15f); area.anchorMax = new Vector2(0.88f, 0.93f);
            area.offsetMin = area.offsetMax = Vector2.zero;
            var viewport = Make(area, "Viewport", typeof(Image), typeof(RectMask2D)); Stretch(viewport);
            viewport.GetComponent<Image>().color = new Color(0.09f, 0.12f, 0.16f);
            content = Make(viewport, "Controls", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(30, 30, 20, 20);
            layout.spacing = 10; layout.childControlHeight = true; layout.childControlWidth = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = area.GetComponent<ScrollRect>(); scroll.content = content; scroll.viewport = viewport;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 38;
            var close = ButtonAt(shade, "닫기", Close);
            close.anchorMin = new Vector2(0.36f, 0.04f); close.anchorMax = new Vector2(0.64f, 0.12f);
            close.offsetMin = close.offsetMax = Vector2.zero;
            message = "수량·성벽을 선택하고 견적을 확인하세요.";
            Render();
        }

        void Render()
        {
            foreach (Transform child in content) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
            var campaign = context.Campaign;
            string wallId = WallIds.All[wallIndex]; var wall = campaign.FindWall(wallId);
            Label((recruit ? "병력 모집·보충" : "성벽 수리") + $" · 골드 {campaign.Gold:N0} · 전체 {session.TotalCount():N0}/{session.Rules.GlobalCapacity:N0}", 70);
            AddButton($"성벽 선택: {PreparationTransactions.WallName(wallId)} · HP {wall.Hp:N0}/{wall.MaxHp:N0} · 병력 {session.WallCount(wallId)}/{session.Rules.WallCapacity}", () =>
            { wallIndex = (wallIndex+1)%4; postIndex = unitIndex = 0; Changed(); });
            if (recruit)
            {
                var posts = new List<PostDef>();
                foreach (var p in context.Definitions.All<PostDef>()) if (p.WallId == wallId) posts.Add(p);
                posts.Sort((a,b) => string.CompareOrdinal(a.Id,b.Id));
                if (posts.Count == 0) { DataError("선택 성벽의 배치 지점이 없습니다"); return; }
                postIndex %= posts.Count; var post = posts[postIndex];
                var squad = campaign.Squads.Find(s => s.PostId == post.Id);
                int cap = post.Kind == PostKind.Cannon ? session.Rules.CannonCrewPostCapacity : session.Rules.SquadPostCapacity;
                AddButton($"지점: {post.DisplayName} [{post.Id}] · {squad?.Count ?? 0}/{cap}명", () => { postIndex = (postIndex+1)%posts.Count; unitIndex=0; Changed(); });
                var units = new List<UnitDef>();
                foreach (var u in context.Definitions.All<UnitDef>())
                    if (squad != null ? u.Id == squad.UnitDefId : post.Kind == PostKind.Cannon ? u.Kind == UnitKind.CannonCrew : u.Kind == UnitKind.Archer || u.Kind == UnitKind.Gunner) units.Add(u);
                units.Sort((a,b) => string.CompareOrdinal(a.Id,b.Id));
                if (units.Count == 0) { DataError("모집 가능한 병종 정의가 없습니다"); return; }
                unitIndex %= units.Count; var unit = units[unitIndex];
                AddButton("병종: " + unit.DisplayName + (squad != null ? " (기존 분대 보충)" : " (새 분대)"), () => { unitIndex=(unitIndex+1)%units.Count; Changed(); });
                Label($"모집 인원 {count}명 · 가격은 구매 직전 전체 인원 600/1200 경계에서 변경", 55);
                AddButton("인원 −1", () => { count=Math.Max(1,count-1); Changed(); });
                AddButton("인원 +1", () => { count=Math.Min(session.Rules.SquadPostCapacity,count+1); Changed(); });
                AddButton("인원 +10", () => { count=Math.Min(session.Rules.SquadPostCapacity,count+10); Changed(); });
                AddButton("인원 1명으로", () => { count=1; Changed(); });
                AddButton("모집 견적 확인", () =>
                {
                    bool ok=session.TryRecruit(post.Id,unit.Id,count,out quote,out var reason,out bool error);
                    if (error) { DataError(reason); return; }
                    message=ok?quote.Description:reason; Render();
                });
            }
            else
            {
                Label($"벽별 {session.Rules.RepairHpPerGold}HP당 1골드 올림 · 골드 부족 시 무변경", 55);
                AddButton($"선택 성벽 {session.Rules.DefaultRepairHp}HP 수리 견적", () => Repair(wallId,session.Rules.DefaultRepairHp,false));
                AddButton("선택 성벽 최대 수리 견적", () => Repair(wallId,int.MaxValue,false));
                AddButton("전체 성벽 최대 수리 견적", () => Repair(null,int.MaxValue,true));
            }
            Label(message, Math.Max(160, (message.Split('\n').Length + 1) * 34));
            var confirm = AddButton("확정 " + (recruit ? "모집" : "수리"), () =>
            {
                var q=quote; quote=null;
                bool ok=session.TryCommit(q,out var reason,out bool error);
                if(error) { DataError(reason); return; }
                message=ok?$"완료: {q.Amount:N0}{(recruit?"명":"HP")} · {q.Cost:N0}골드 사용":reason;
                RefreshSummary(source,context,session); Render();
            });
            confirm.interactable=quote!=null;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content); scroll.verticalNormalizedPosition=quote!=null?0:1;
            if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(null);
        }

        void Repair(string wallId,int amount,bool all)
        {
            bool ok=session.TryRepair(wallId,amount,all,out quote,out var reason,out bool error);
            if(error) { DataError(reason); return; }
            message=ok?quote.Description:reason; Render();
        }
        void Changed() { quote=null;session.CancelQuote();message="선택이 변경됐습니다. 견적을 다시 확인하세요.";Render(); }
        void DataError(string reason)
        { session.Close(); context.ReportDataError(reason); foreach(var b in shade.GetComponentsInChildren<Button>()) b.interactable=false; }
        void Close()
        {
            session.CancelQuote();quote=null;behind.interactable=true;
            shade.gameObject.SetActive(false);UnityEngine.Object.Destroy(shade.gameObject);
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(source.gameObject);
        }
        internal static void RefreshSummary(Button source,IScreenContext context,PreparationTransactions session)
        {
            var texts=source.transform.parent.GetComponentsInChildren<Text>();
            foreach(var text in texts) if(text.gameObject.name=="Label" && text.fontSize==27)
                text.text=$"다음 전투: {context.Campaign.BattleNumber}\n보유 골드: {context.Campaign.Gold:N0} · 병력 {session.TotalCount():N0}/{session.Rules.GlobalCapacity:N0}\n성벽을 선택해 모집·수리하세요. 함정 기능은 후속 작업입니다.";
        }
        Button AddButton(string text,Action action)
        {
            var rect=ButtonAt(content,text,action);rect.gameObject.AddComponent<LayoutElement>().preferredHeight=62;
            return rect.GetComponent<Button>();
        }
        RectTransform ButtonAt(Transform parent,string text,Action action)
        {
            var rect=Make(parent,text,typeof(Image),typeof(Button));rect.GetComponent<Image>().color=new Color(0.19f,0.28f,0.38f);
            var button=rect.GetComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
            var label=Make(rect,"Button Label",typeof(Text));Stretch(label);SetupText(label,text,24);
            button.onClick.AddListener(()=>action());return rect;
        }
        void Label(string text,float height)
        { var rect=Make(content,"Info",typeof(Text),typeof(LayoutElement));SetupText(rect,text,24);rect.GetComponent<LayoutElement>().preferredHeight=height; }
        void SetupText(RectTransform rect,string value,int size)
        { var t=rect.GetComponent<Text>();t.font=font;t.fontSize=size;t.text=value;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.supportRichText=false;t.raycastTarget=false; }
        static RectTransform Make(Transform parent,string name,params Type[] components)
        { var obj=new GameObject(name,typeof(RectTransform));obj.transform.SetParent(parent,false);foreach(var type in components)obj.AddComponent(type);return obj.GetComponent<RectTransform>(); }
        static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero; }
    }

}
