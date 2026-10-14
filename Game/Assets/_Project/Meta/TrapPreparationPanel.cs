using System;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Traps;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace HumanLastRampart.Meta
{
    internal sealed class TrapPreparationPanel
    {
        readonly Button source;
        readonly CanvasGroup behind;
        readonly IScreenContext context;
        readonly TrapPreparationTransactions session;
        readonly PreparationTransactions preparation;
        readonly RectTransform shade, content;
        readonly ScrollRect scroll;
        readonly Font font;
        int side, kind;
        string cell, message = "성벽과 빈 칸을 골라 견적을 확인하세요. 환불은 불가능합니다.";
        bool storage, marked = true;
        TrapPreparationTransactions.Quote quote;
        public static void Show(Button source, IScreenContext context, TrapPreparationTransactions session, PreparationTransactions preparation)
        { if (source != null && source.IsInteractable()) new TrapPreparationPanel(source, context, session, preparation); }
        TrapPreparationPanel(Button source, IScreenContext context, TrapPreparationTransactions session, PreparationTransactions preparation)
        {
            this.source=source; this.context=context; this.session=session; this.preparation=preparation;
            behind=source.transform.parent.GetComponent<CanvasGroup>(); behind.interactable=false;
            font=source.GetComponentInChildren<Text>().font;
            shade=Make(source.transform.parent.parent,"Trap preparation",typeof(Image)); Stretch(shade);
            shade.GetComponent<Image>().color=new Color(0,0,0,.97f);
            var area=Make(shade,"Scroll",typeof(ScrollRect)); area.anchorMin=new Vector2(.06f,.14f);area.anchorMax=new Vector2(.94f,.95f);area.offsetMin=area.offsetMax=Vector2.zero;
            var viewport=Make(area,"Viewport",typeof(Image),typeof(RectMask2D));Stretch(viewport);viewport.GetComponent<Image>().color=new Color(.08f,.12f,.17f);
            content=Make(viewport,"Content",typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.offsetMin=content.offsetMax=Vector2.zero;
            var layout=content.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(20,20,20,20);layout.spacing=8;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;
            content.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll=area.GetComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=38;
            var close=ButtonAt(shade,"닫기",Close);close.anchorMin=new Vector2(.35f,.03f);close.anchorMax=new Vector2(.65f,.11f);close.offsetMin=close.offsetMax=Vector2.zero;
            Render();
        }
        void Render()
        {
            foreach(Transform child in content){child.gameObject.SetActive(false);UnityEngine.Object.Destroy(child.gameObject);}
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
            if(!session.HasNextBattle){Label("회차 종료: 다음 전투가 없어 함정을 설치할 수 없습니다.",90);return;}
            var terrain=session.Terrain;var defs=context.Definitions.All<TrapDef>();kind%=defs.Count;var def=defs[kind];
            Label($"함정 구매·설치 · 다음 지형 {terrain.DisplayName} · 골드 {context.Campaign.Gold:N0}",64);
            Label("중앙 성  ▣   북 / 동 / 남 / 서 성벽 밖에 설치\n선택 성벽을 바깥쪽으로 바라본 격자입니다.",80);
            AddButton("성벽: "+PreparationTransactions.WallName(WallIds.All[side]),()=>{side=(side+1)%4;cell=null;Changed();});
            AddButton($"함정: {def.DisplayName} · 상점 {session.ShopCount(def.Id)}개 · 보관 {session.StoredCount(def.Id)}개",()=>{kind=(kind+1)%defs.Count;Changed();});
            AddButton(storage?"보관품 재설치 · 골드 없음":"상점 구매·설치 · "+def.Price+"골드",()=>{storage=!storage;Changed();});
            AddButton(marked?"지도 표시: 켜짐 (기본)":"지도 표시: 없음 — 지도에서 점화 불가",()=>{marked=!marked;Changed();});
            Label($"칸 {terrain.TrapCellSize}m · 성벽에서 {terrain.TrapNear}~{terrain.TrapFar}m\n□ 빈 칸 · ■ 설치됨 · × 언덕/설치 불가 · ● 선택",72);
            int cols=TrapGrid.Columns(terrain),rows=TrapGrid.Rows(terrain);
            var grid=Make(content,"Placement grid",typeof(GridLayoutGroup),typeof(LayoutElement));
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);Canvas.ForceUpdateCanvases();
            var gl=grid.GetComponent<GridLayoutGroup>();gl.constraint=GridLayoutGroup.Constraint.FixedColumnCount;gl.constraintCount=cols;gl.spacing=new Vector2(2,2);
            float available=Mathf.Max(240,content.rect.width-40);gl.cellSize=new Vector2(Mathf.Max(8,(available-(cols-1)*2)/cols),34);
            grid.GetComponent<LayoutElement>().preferredHeight=rows*36;
            // 먼 줄을 위에, 성벽에 가까운 줄을 아래에 표시한다.
            for(int row=rows-1;row>=0;row--)for(int col=0;col<cols;col++)
            {
                string id=TrapGrid.CellId(side,col,row);bool allowed=TrapGrid.IsPlaceable(terrain,id);bool occupied=session.Occupied(id);
                var tile=ButtonAt(grid,!allowed?"×":occupied?"■":cell==id?"●":"□",()=>{cell=id;Changed(false);});
                tile.name=id;tile.GetComponent<Button>().interactable=allowed&&!occupied;
                tile.GetComponent<Image>().color=!allowed?new Color(.3f,.22f,.18f):occupied?new Color(.5f,.25f,.2f):cell==id?new Color(.15f,.55f,.4f):new Color(.2f,.3f,.4f);
                tile.GetComponentInChildren<Text>().fontSize=17;
            }
            Label("선택: "+(cell??"선택 안 함"),48);
            AddButton("설치 견적 확인",()=>{bool ok=session.TryQuote(def.Id,cell,storage,marked,out quote,out var reason,out bool error);if(error){DataError(reason);return;}message=ok?quote.Description:reason;Render();});
            Label(message,Math.Max(100,(message.Split('\n').Length+1)*31));
            var confirm=AddButton("확정 설치",()=>{var q=quote;quote=null;bool ok=session.TryCommit(q,out var reason,out bool error);if(error){DataError(reason);return;}message=ok?"설치 완료 · "+q.Cost+"골드 사용 · 환불 불가":reason;preparation.CancelQuote();PreparationPanel.RefreshSummary(source,context,preparation);Render();});confirm.interactable=quote!=null;
            Label("회수 기능은 판정 약속 보완 후 연결합니다.",52);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);scroll.verticalNormalizedPosition=quote!=null?0:1;
        }
        void Changed(bool top=true){quote=null;session.CancelQuote();message="선택이 변경됐습니다. 견적을 확인하세요.";float saved=scroll.verticalNormalizedPosition;Render();if(!top)scroll.verticalNormalizedPosition=saved;}
        void DataError(string reason){session.Close();context.ReportDataError(reason);foreach(var b in shade.GetComponentsInChildren<Button>())b.interactable=false;}
        void Close(){session.CancelQuote();behind.interactable=true;shade.gameObject.SetActive(false);UnityEngine.Object.Destroy(shade.gameObject);if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(source.gameObject);}
        Button AddButton(string text,Action action){var r=ButtonAt(content,text,action);r.gameObject.AddComponent<LayoutElement>().preferredHeight=56;return r.GetComponent<Button>();}
        RectTransform ButtonAt(Transform parent,string value,Action action){var r=Make(parent,value,typeof(Image),typeof(Button));r.GetComponent<Image>().color=new Color(.18f,.28f,.38f);var b=r.GetComponent<Button>();b.targetGraphic=r.GetComponent<Image>();var l=Make(r,"Text",typeof(Text));Stretch(l);Text(l,value,22);b.onClick.AddListener(()=>action());return r;}
        void Label(string value,float height){var r=Make(content,"Info",typeof(Text),typeof(LayoutElement));Text(r,value,22);r.GetComponent<LayoutElement>().preferredHeight=height;}
        void Text(RectTransform r,string value,int size){var t=r.GetComponent<Text>();t.font=font;t.fontSize=size;t.text=value;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.supportRichText=false;t.raycastTarget=false;}
        static RectTransform Make(Transform parent,string name,params Type[] components){var o=new GameObject(name,typeof(RectTransform));o.transform.SetParent(parent,false);foreach(var type in components)o.AddComponent(type);return o.GetComponent<RectTransform>();}
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
