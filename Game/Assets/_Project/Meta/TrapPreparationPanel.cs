using System;
using HumanLastRampart.Core.Campaign;
using HumanLastRampart.Core.Definitions;
using HumanLastRampart.Core.Flow;
using HumanLastRampart.Core.Traps;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using HumanLastRampart.UI;

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
            shade.GetComponent<Image>().color=UiStyle.Shade;
            var area=Make(shade,"Scroll",typeof(ScrollRect)); area.anchorMin=new Vector2(.06f,.14f);area.anchorMax=new Vector2(.94f,.95f);area.offsetMin=area.offsetMax=Vector2.zero;
            var viewport=Make(area,"Viewport",typeof(Image),typeof(RectMask2D));Stretch(viewport);viewport.GetComponent<Image>().color=UiStyle.PanelSolid;UiStyle.Border(viewport.GetComponent<Image>());
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
            Title($"함정 구매·설치 · 다음 지형 {terrain.DisplayName} · 골드 {context.Campaign.Gold:N0}",64);
            // 지도 시안 2판 (D-066): 북쪽이 늘 위. 성·언덕·주 공격 방향·고른 성벽과 설치 영역·고른 칸
            Overview(terrain);
            SideButtons();
            AddButton($"함정: {def.DisplayName} · 상점 {session.ShopCount(def.Id)}개 · 보관 {session.StoredCount(def.Id)}개",()=>{kind=(kind+1)%defs.Count;Changed();});
            AddButton(storage?"보관품 재설치 · 골드 없음":"상점 구매·설치 · "+def.Price+"골드",()=>{storage=!storage;Changed();});
            AddButton(marked?"지도 표시: 켜짐 (기본)":"지도 표시: 없음 — 지도에서 점화 불가",()=>{marked=!marked;Changed();});
            Label($"{PreparationTransactions.WallName(WallIds.All[side])} 성벽에서 바깥을 본 격자 · 위 = 먼 쪽 · 아래 = 성벽 쪽 · {SideHint[side]}\n칸 {terrain.TrapCellSize}m · 성벽에서 {terrain.TrapNear}~{terrain.TrapFar}m · □ 빈 칸 · ■ 설치됨 · × 언덕/설치 불가 · ● 선택",76);
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
                tile.GetComponent<Image>().color=!allowed?new Color(.30f,.24f,.18f):occupied?new Color(.55f,.32f,.26f):cell==id?new Color(.24f,.45f,.70f):UiStyle.ButtonNormal;UiStyle.StyleTintButton(tile.GetComponent<Button>());
                tile.GetComponentInChildren<Text>().fontSize=17;
            }
            Label("선택: "+CellText(terrain),48);
            AddButton("설치 견적 확인",()=>{bool ok=session.TryQuote(def.Id,cell,storage,marked,out quote,out var reason,out bool error);if(error){DataError(reason);return;}message=ok?quote.Description:reason;Render();});
            Label(message,Math.Max(100,(message.Split('\n').Length+1)*31));
            var confirm=AddButton("확정 설치",()=>{var q=quote;quote=null;bool ok=session.TryCommit(q,out var reason,out bool error);if(error){DataError(reason);return;}message=ok?"설치 완료 · "+q.Cost+"골드 사용 · 환불 불가":reason;preparation.CancelQuote();PreparationPanel.RefreshSummary(source,context,preparation);Render();});confirm.interactable=quote!=null;
            Label("회수 기능은 판정 약속 보완 후 연결합니다.",52);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);scroll.verticalNormalizedPosition=quote!=null?0:1;
        }
        // ── 지도 시안 2판 (D-066, Docs/References/codex_trap_map_proposal_v2) ──
        const float MapSize=340f;
        // 성벽에서 바깥을 볼 때 격자의 왼쪽·오른쪽 (북 0 · 동 1 · 남 2 · 서 3)
        static readonly string[] SideHint={"왼쪽 서 · 오른쪽 동","왼쪽 북 · 오른쪽 남","왼쪽 동 · 오른쪽 서","왼쪽 남 · 오른쪽 북"};
        static Vector2 Out(int s)=>s==0?new Vector2(0,1):s==1?new Vector2(1,0):s==2?new Vector2(0,-1):new Vector2(-1,0);
        void Title(string value,float height){var r=Make(content,"Title",typeof(Text),typeof(LayoutElement));Text(r,value,26);r.GetComponent<Text>().color=UiStyle.Brass;r.GetComponent<LayoutElement>().preferredHeight=height;}
        void Overview(TerrainDef terrain)
        {
            var holder=Make(content,"Overview map",typeof(LayoutElement));holder.GetComponent<LayoutElement>().preferredHeight=MapSize+76;
            var north=Make(holder,"North",typeof(Text));north.anchorMin=north.anchorMax=new Vector2(.5f,1);north.pivot=new Vector2(.5f,1);north.sizeDelta=new Vector2(MapSize+260,32);north.anchoredPosition=Vector2.zero;
            Text(north,"▼ 북 · 정면 — 주 공격 방향 (적 대부분)",22);north.GetComponent<Text>().color=UiStyle.Danger;
            var map=Make(holder,"Map",typeof(Image));map.anchorMin=map.anchorMax=new Vector2(.5f,1);map.pivot=new Vector2(.5f,1);map.sizeDelta=new Vector2(MapSize,MapSize);map.anchoredPosition=new Vector2(0,-34);
            map.GetComponent<Image>().color=UiStyle.PanelDeep;map.GetComponent<Image>().raycastTarget=false;UiStyle.Border(map.GetComponent<Image>(),.5f);
            float half=terrain.CastleHalfSize,extent=half+terrain.TrapFar+30f,scale=MapSize*.5f/extent;
            foreach(var h in terrain.Hills)Box(map,"Hill",new Vector2(h.x,h.z)*scale,Vector2.one*h.y*2*scale,new Color(.33f,.43f,.25f),"언덕");
            Vector2 o=Out(side);float depth=terrain.TrapFar-terrain.TrapNear;
            Vector2 area=Mathf.Abs(o.y)>0?new Vector2(half*2,depth):new Vector2(depth,half*2);
            Box(map,"Placement area",o*(half+(terrain.TrapNear+terrain.TrapFar)*.5f)*scale,area*scale,new Color(UiStyle.Brass.r,UiStyle.Brass.g,UiStyle.Brass.b,.26f),"설치 영역");
            Box(map,"Castle",Vector2.zero,Vector2.one*half*2*scale,new Color(.46f,.43f,.37f),"성");
            for(int s=0;s<4;s++)
            {
                Vector2 os=Out(s);bool selected=s==side;float t=selected?6f:3f;
                Vector2 size=Mathf.Abs(os.y)>0?new Vector2(half*2*scale+t,t):new Vector2(t,half*2*scale+t);
                Box(map,"Wall",os*half*scale,size,selected?UiStyle.Danger:new Color(.78f,.74f,.66f),null);
                var letter=Make(map,"Side",typeof(Text));letter.anchorMin=letter.anchorMax=new Vector2(.5f,.5f);letter.sizeDelta=new Vector2(40,28);letter.anchoredPosition=os*(MapSize*.5f-16f);
                Text(letter,s==0?"북":s==1?"동":s==2?"남":"서",20);letter.GetComponent<Text>().color=selected?UiStyle.Danger:UiStyle.TextDim;
            }
            if(cell!=null&&TrapGrid.TryParse(cell,out int cs,out int cc,out int cr)&&cs==side)
                Box(map,"Selected cell",TrapGrid.Center(terrain,cs,cc,cr)*scale,Vector2.one*Mathf.Max(5f,terrain.TrapCellSize*scale),new Color(.35f,.62f,1f),null);
            var note=Make(holder,"Note",typeof(Text));note.anchorMin=note.anchorMax=new Vector2(.5f,1);note.pivot=new Vector2(.5f,1);note.sizeDelta=new Vector2(MapSize+260,30);note.anchoredPosition=new Vector2(0,-(MapSize+40));
            Text(note,"지도 위쪽이 북 · 일부 적은 옆·뒤 성벽으로 돌아올 수 있습니다",18);note.GetComponent<Text>().color=UiStyle.TextDim;
        }
        void Box(RectTransform map,string name,Vector2 center,Vector2 size,Color color,string label)
        {
            var b=Make(map,name,typeof(Image));b.anchorMin=b.anchorMax=new Vector2(.5f,.5f);b.sizeDelta=size;b.anchoredPosition=center;
            var image=b.GetComponent<Image>();image.color=color;image.raycastTarget=false;
            if(label==null||size.x<24f)return;
            var l=Make(b,"Label",typeof(Text));Stretch(l);Text(l,label,14);
        }
        void SideButtons()
        {
            var row=Make(content,"Wall sides",typeof(HorizontalLayoutGroup),typeof(LayoutElement));row.GetComponent<LayoutElement>().preferredHeight=56;
            var h=row.GetComponent<HorizontalLayoutGroup>();h.spacing=8;h.childControlWidth=h.childControlHeight=true;h.childForceExpandWidth=true;h.childForceExpandHeight=true;
            for(int s=0;s<4;s++)
            {
                int pick=s;
                var b=ButtonAt(row,(s==side?"● ":"")+PreparationTransactions.WallName(WallIds.All[s])+" 성벽",()=>{if(side==pick)return;side=pick;cell=null;Changed();});
                UiStyle.StyleButton(b.GetComponent<Button>(),s==side);
            }
        }
        string CellText(TerrainDef terrain)
        {
            if(cell==null||!TrapGrid.TryParse(cell,out int s,out int c,out int r))return "선택 안 함";
            Vector2 p=TrapGrid.Center(terrain,s,c,r);float away=terrain.TrapNear+(r+.5f)*terrain.TrapCellSize;
            return $"{PreparationTransactions.WallName(WallIds.All[s])} 성벽 {c+1}열 · 성벽에서 {away:0.#}m · 지도 X {p.x:0.#} / Z {p.y:0.#}";
        }
        void Changed(bool top=true){quote=null;session.CancelQuote();message="선택이 변경됐습니다. 견적을 확인하세요.";float saved=scroll.verticalNormalizedPosition;Render();if(!top)scroll.verticalNormalizedPosition=saved;}
        void DataError(string reason){session.Close();context.ReportDataError(reason);foreach(var b in shade.GetComponentsInChildren<Button>())b.interactable=false;}
        void Close(){session.CancelQuote();behind.interactable=true;shade.gameObject.SetActive(false);UnityEngine.Object.Destroy(shade.gameObject);if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(source.gameObject);}
        Button AddButton(string text,Action action){var r=ButtonAt(content,text,action);r.gameObject.AddComponent<LayoutElement>().preferredHeight=56;return r.GetComponent<Button>();}
        RectTransform ButtonAt(Transform parent,string value,Action action){var r=Make(parent,value,typeof(Image),typeof(Button));var b=r.GetComponent<Button>();b.targetGraphic=r.GetComponent<Image>();UiStyle.StyleButton(b);var l=Make(r,"Text",typeof(Text));Stretch(l);Text(l,value,22);b.onClick.AddListener(()=>action());return r;}
        void Label(string value,float height){var r=Make(content,"Info",typeof(Text),typeof(LayoutElement));Text(r,value,22);r.GetComponent<LayoutElement>().preferredHeight=height;}
        void Text(RectTransform r,string value,int size){var t=r.GetComponent<Text>();t.font=font;t.fontSize=size;t.text=value;t.alignment=TextAnchor.MiddleCenter;t.color=UiStyle.Body;t.supportRichText=false;t.raycastTarget=false;}
        static RectTransform Make(Transform parent,string name,params Type[] components){var o=new GameObject(name,typeof(RectTransform));o.transform.SetParent(parent,false);foreach(var type in components)o.AddComponent(type);return o.GetComponent<RectTransform>();}
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
