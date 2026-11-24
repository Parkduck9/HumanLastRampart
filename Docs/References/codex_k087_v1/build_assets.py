from pathlib import Path
from PIL import Image, ImageOps, ImageDraw, ImageFont
import numpy as np
import json, math, hashlib, base64, io

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[2]
NN = Image.Resampling.NEAREST
STAGES = ['intact', 'cracked', 'collapsed', 'rubble']
STAGE_KO = ['온전함', '균열', '붕괴', '잔해']
DIRS = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW']
VECTORS = [(0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1)]
PARTS = ['straight','diagonal','end_left','end_right','joint']
assets = []

def dump(name, data):
    (ROOT/name).write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')

def solid(im):
    a = np.array(im.convert('RGBA'))
    a[:,:,3] = np.where(a[:,:,3] >= 128, 255, 0)
    a[a[:,:,3] == 0] = 0
    return Image.fromarray(a)

def largest(im):
    a = np.array(solid(im)); mask = a[:,:,3] > 0
    seen = np.zeros(mask.shape, dtype=bool); best = []
    for y,x in zip(*np.where(mask)):
        if seen[y,x]: continue
        comp=[]; stack=[(int(y),int(x))]; seen[y,x]=True
        while stack:
            yy,xx=stack.pop(); comp.append((yy,xx))
            for dy in [-1,0,1]:
                for dx in [-1,0,1]:
                    ny,nx=yy+dy,xx+dx
                    if 0<=ny<mask.shape[0] and 0<=nx<mask.shape[1] and mask[ny,nx] and not seen[ny,nx]:
                        seen[ny,nx]=True; stack.append((ny,nx))
        if len(comp)>len(best): best=comp
    keep=np.zeros(mask.shape,dtype=bool)
    if best:
        yy,xx=zip(*best); keep[yy,xx]=True
    a[~keep]=0
    return Image.fromarray(a)

def tight(im):
    im=largest(im)
    return im.crop(im.getbbox())

def save_asset(im, rel, **extra):
    im=solid(im); im.save(ROOT/rel)
    assets.append(dict(file=rel, size_px=list(im.size), alpha='binary_0_255', **extra))
    return im

def pack(images, cell):
    im=Image.new('RGBA',(cell[0]*len(images),cell[1]))
    for i,s in enumerate(images): im.alpha_composite(s,(cell[0]*i+(cell[0]-s.width)//2,cell[1]-s.height))
    return im

def build_battle():
    src=Image.open(ROOT/'source/battle_cutout.png').convert('RGBA')
    # 생성 원화의 네 행을 분리하고 기준선에 등록한다.
    rows=[]
    for i in range(4):
        row=tight(src.crop((0,round(src.height*i/4),src.width,round(src.height*(i+1)/4))))
        rows.append(row)
    all_parts={}
    for i,(stage,row) in enumerate(zip(STAGES,rows)):
        h=[112,112,72,36][i]
        # 원화의 가운데 부분을 사용해 양 끝의 마감선을 제외한다.
        crop_width=round(row.height*200/h)
        crop_x=(row.width-crop_width)//2
        tex=row.crop((crop_x,0,crop_x+crop_width,row.height)).resize((200,h),NN)
        tex=largest(tex)
        ar=np.array(tex)
        # 양쪽 경계는 같은 원화 열을 복사하여 반복 이음의 색과 알파를 맞춘다.
        seam=ar[:,100:101].copy()
        for x in [0,1,198,199]: ar[:,x:x+1]=seam
        # 하단은 각 열의 마지막 흙 픽셀로 맞추어 바닥 접촉을 유지한다.
        for x in range(200):
            ys=np.where(ar[:,x,3]>0)[0]
            if len(ys): ar[ys[-1]:,x]=ar[ys[-1],x]
        tex=Image.fromarray(ar)
        straight=Image.new('RGBA',(200,128)); straight.alpha_composite(tex,(0,128-h)); straight=largest(straight)
        diag=Image.new('RGBA',(283,128))
        # 대각 부품은 재질을 늘이지 않고 가운데 반복 구간을 추가한다.
        dtex=Image.new('RGBA',(283,h)); dtex.alpha_composite(tex,(0,0))
        dtex.alpha_composite(tex.crop((58,0,141,h)),(200,0))
        da=np.array(dtex); da[:,-2:]=np.array(tex)[:,:2]; dtex=Image.fromarray(da)
        diag.alpha_composite(dtex,(0,128-h)); diag=largest(diag)
        cap_l=Image.new('RGBA',(32,128)); cap_r=Image.new('RGBA',(32,128))
        cap=tight(row.crop((0,0,max(1,round(row.width*.045)),row.height))).resize((32,h),NN)
        ca=np.array(cap)
        for x in range(32):
            ys=np.where(ca[:,x,3]>0)[0]
            if len(ys): ca[ys[-1]:,x]=ca[ys[-1],x]
        cap=Image.fromarray(ca)
        cap_l.alpha_composite(cap,(0,128-h)); cap_r.alpha_composite(ImageOps.mirror(cap),(0,128-h))
        post=Image.new('RGBA',(48,128))
        post.alpha_composite(cap.resize((48,h),NN),(0,128-h))
        pieces=dict(zip(PARTS,[straight,diag,cap_l,cap_r,post])); all_parts[stage]=pieces
        for folder,scale in [('native',1),('game',3)]:
            im=straight.resize((200*scale,128*scale),NN)
            save_asset(im,f'{folder}/earthwall_{stage}.png',kind='legacy_filename_single_frame',frame_count=1,source_part=f'native/earthwall_{stage}_straight.png',scale=scale,pivot_normalized_bottom_left=[.5,0],baseline_y_top_left=128*scale,px_per_metre=40*scale)
        for name,im in pieces.items():
            native=f'native/earthwall_{stage}_{name}.png'
            visible_h=128-im.getbbox()[1]
            extra=dict(kind='battle_part',stage=stage,part=name,pivot_normalized_bottom_left=[.5,0],pivot_px_bottom_left=[im.width/2,0],baseline_y_top_left=128,px_per_metre=40,registration_height_px=h,visible_height_px=visible_h,visible_height_m=visible_h/40,nominal_board_height_m=2.8)
            if name in ['straight','diagonal']:
                extra.update(length_m=5 if name=='straight' else math.sqrt(50),anchors_px_top_left=[[0,128],[im.width,128]],raster_length_m=im.width/40)
            save_asset(im,native,**extra)
            save_asset(im.resize((im.width*3,384),NN),f'game/earthwall_{stage}_{name}.png',kind='battle_part_3x',native_file=native,scale=3,pivot_normalized_bottom_left=[.5,0],pivot_px_bottom_left=[im.width*1.5,0],baseline_y_top_left=384,px_per_metre=120)
        sheet=pack([pieces[p] for p in PARTS],(288,128))
        frame_rects=[[idx*288+(288-pieces[name].width)//2,0,pieces[name].width,128] for idx,name in enumerate(PARTS)]
        save_asset(sheet,f'native/earthwall_{stage}_sheet.png',kind='battle_sheet',cell_px=[288,128],frame_order=PARTS,frame_count=5,pivot_px_bottom_left=[144,0],part_rects_top_left=frame_rects,part_pivots_px_bottom_left=[[r[0]-idx*288+r[2]/2,0] for idx,r in enumerate(frame_rects)])
        save_asset(sheet.resize((sheet.width*3,sheet.height*3),NN),f'game/earthwall_{stage}_sheet.png',kind='battle_sheet_3x',cell_px=[864,384],frame_order=PARTS,frame_count=5,pivot_px_bottom_left=[432,0],part_rects_top_left=[[v*3 for v in r] for r in frame_rects],part_pivots_px_bottom_left=[[(r[0]-idx*288+r[2]/2)*3,0] for idx,r in enumerate(frame_rects)])
        sheet.resize((sheet.width*4,sheet.height*4),NN).save(ROOT/f'previews/earthwall_{stage}_sheet_4x.png')
    return all_parts

def build_map():
    src=Image.open(ROOT/'source/map_generated.png').convert('RGBA'); w,h=src.size
    pieces={}
    for row,kind in enumerate(['earthwall','hill']):
        hub=tight(src.crop((0,row*h//2,w//2,(row+1)*h//2)))
        ridge=tight(src.crop((w//2,row*h//2,w,(row+1)*h//2)))
        size=20 if kind=='earthwall' else 28
        hub=hub.resize((size,size),NN)
        hub_canvas=Image.new('RGBA',(32,32)); hub_canvas.alpha_composite(hub,((32-size)//2,(32-size)//2))
        pieces[kind]={'hub':save_asset(hub_canvas,f'map/{kind}_hub.png',kind='map_hub',pivot_px_top_left=[16,16],px_per_metre=6.4)}
        # 팔의 재질은 원화 띠의 중앙만 잘라 양끝 마감선을 없앤다.
        thick=10 if kind=='earthwall' else 16
        texture=ridge.crop((ridge.width//4,0,ridge.width*3//4,ridge.height)).resize((48,thick),NN)
        hub_pixels=np.array(hub_canvas)
        # 회전 후에도 셀 모서리에 닿도록 충분히 긴 띠를 원점에서 뻗는다.
        for direction,(dx,dy) in zip(DIRS,VECTORS):
            out=Image.new('RGBA',(32,32)); a=np.array(texture); o=np.array(out)
            norm=math.hypot(dx,dy); ux,uy=dx/norm,dy/norm
            for y in range(32):
                for x in range(32):
                    rx,ry=x+.5-16,y+.5-16
                    along=rx*ux+ry*uy; across=-rx*uy+ry*ux
                    if along>=-3 and abs(across)<thick/2:
                        tx=min(47,max(0,int(along+3))); ty=min(thick-1,max(0,int(across+thick/2)))
                        o[y,x]=hub_pixels[y,x] if math.hypot(rx,ry)<(5 if kind=='earthwall' else 8) else a[ty,tx]
            arm=largest(Image.fromarray(o))
            pieces[kind][direction]=save_asset(arm,f'map/{kind}_arm_{direction}.png',kind='map_arm',direction=direction,mask_bit=1<<DIRS.index(direction),px_per_metre=6.4,pivot_px_top_left=[16,16],anchor_start_px_top_left=[16,16],anchor_end_px_top_left=[16+16*dx,16+16*dy],arm_width_px=thick)
        sheet=pack([pieces[kind]['hub']]+[pieces[kind][d] for d in DIRS],(32,32))
        save_asset(sheet,f'map/{kind}_autotile_sheet.png',kind='map_sheet',cell_px=[32,32],frame_order=['hub']+DIRS,frame_count=9)
        sheet.resize((sheet.width*4,128),NN).save(ROOT/f'previews/{kind}_autotile_sheet_4x.png')
    return pieces

LAYOUTS=[
    ('straight','직선 · 두 칸 조각 3개',[(0,1),(1,1),(2,1),(3,1),(4,1),(5,1)],'earthwall'),
    ('corner','직각 · 두 칸 조각 3개',[(0,0),(1,0),(2,0),(2,1),(2,2),(2,3)],'earthwall'),
    ('diagonal','대각 · 모서리까지 막힌 연결',[(0,0),(1,1),(2,2),(3,3),(4,4),(5,5)],'earthwall'),
    ('mixed','대각 + 직선 · 두 칸 조각 3개',[(0,0),(1,1),(2,1),(3,1),(4,2),(5,3)],'earthwall'),
    ('hill','언덕 · 연속 능선',[(0,1),(1,1),(2,2),(3,2),(4,1),(5,1)],'hill')]

def map_layout(cells,kind,mp,mask_override=None):
    out=Image.new('RGBA',(224,224)); cellset=set(cells)
    # 중심 둔덕을 먼저 놓고 팔을 겹쳐 테두리가 연결을 가르지 않게 한다.
    for x,y in cells: out.alpha_composite(mp[kind]['hub'],((x+1)*32,(y+1)*32))
    for x,y in cells:
        for d,(dx,dy) in zip(DIRS,VECTORS):
            if (x+dx,y+dy) in cellset and (mask_override is None or d in mask_override):
                out.alpha_composite(mp[kind][d],((x+1)*32,(y+1)*32))
    return out

def battle_layout(cells,parts,stage='intact'):
    out=Image.new('RGBA',(880,420)); cellset=set(cells)
    def pos(p): return (int(90+p[0]*108+p[1]*21),int(164+p[1]*30))
    edges=[]
    for p in cells:
        for dx,dy in [(1,0),(0,1),(1,1),(-1,1)]:
            q=(p[0]+dx,p[1]+dy)
            if q in cellset: edges.append((p,q))
    # 길이 방향만 투영하고 기둥의 세로 방향은 고정한다.
    for p,q in sorted(edges,key=lambda e:e[0][1]):
        pa,pb=pos(p),pos(q)
        if pa[0]>pb[0]: pa,pb=pb,pa
        tex=parts[stage]['diagonal' if p[0]!=q[0] and p[1]!=q[1] else 'straight']
        width=pb[0]-pa[0]
        for col in range(width+1):
            t=col/max(1,width); sx=min(tex.width-1,round(t*(tex.width-1))); base=round(pa[1]*(1-t)+pb[1]*t)
            out.alpha_composite(tex.crop((sx,0,sx+1,128)),(pa[0]+col,base-128))
    for p in sorted(cells,key=lambda p:p[1]):
        degree=sum((p[0]+dx,p[1]+dy) in cellset for dx,dy in VECTORS)
        name='joint' if degree>1 else ('end_left' if p==cells[0] else 'end_right')
        im=parts[stage][name]; x,y=pos(p); out.alpha_composite(im,(x-im.width//2,y-128))
    return out

def build_previews(bp,mp):
    out=Image.new('RGBA',(1120,420*5))
    records=[]
    for idx,(key,label,cells,kind) in enumerate(LAYOUTS):
        mm=map_layout(cells,kind,mp)
        mm.save(ROOT/f'previews/{key}_map.png')
        if kind=='earthwall': bb=battle_layout(cells,bp)
        else:
            bb=Image.new('RGBA',(880,420)); ground=mm.resize((720,180),NN)
            bb.alpha_composite(ground,(70,135))
        bb.save(ROOT/f'previews/{key}_battle.png')
        out.alpha_composite(bb,(0,idx*420)); out.alpha_composite(mm,(880,idx*420+80))
        records.append(dict(id=key,label_ko=label,cells=cells,placement_pairs=[cells[i:i+2] for i in range(0,len(cells),2)],map=f'previews/{key}_map.png',battle=f'previews/{key}_battle.png'))
    out.save(ROOT/'previews/layout_examples.png')
    # 손상 단계는 동작 애니메이션이 아니라 같은 배치의 상태 전환이다.
    for stage in STAGES: battle_layout(LAYOUTS[0][2],bp,stage).save(ROOT/f'previews/damage_{stage}.png')
    return records

def data_uri(im):
    b=io.BytesIO(); im.save(b,format='PNG')
    return 'data:image/png;base64,'+base64.b64encode(b.getvalue()).decode()

def html_image(file,alt,cls=''):
    return f'<img class="{cls}" alt="{alt}" src="{data_uri(Image.open(file))}">'

def build_html(layouts):
    old=REPO/'Game/Assets/_Project/Art'
    sections=[]
    for r in layouts:
        sections.append(f'<article><h3>{r["label_ko"]}</h3><div class="layout"><figure>{html_image(ROOT/r["battle"],"전투 배치 예시")}<figcaption>전투 시점'+(' · 언덕은 지도 부품의 지면 투영' if r['id']=='hill' else '')+f'</figcaption></figure><figure>{html_image(ROOT/r["map"],"지도 배치 예시")}<figcaption>지도 · 32점 = 5m</figcaption></figure></div></article>')
    part_sections=[]
    for stage,ko in zip(STAGES,STAGE_KO):
        before=html_image(old/f'Battle/Resources/BattleArt/Terrain/earthwall_{stage}.png',f'현재 토벽 {ko}')
        after=html_image(ROOT/f'native/earthwall_{stage}_sheet.png',f'새 토벽 {ko} 부품 시트')
        part_sections.append(f'<article><h3>{ko}</h3><div class="comparison"><figure>{before}<figcaption>현재 게임 원본 · 256×128</figcaption></figure><figure class="wide">{after}<figcaption>새 부품 · 직선 / 대각 / 왼쪽 끝 / 오른쪽 끝 / 접합 기둥</figcaption></figure></div></article>')
    maps=[]
    for kind,name,oldname in [('earthwall','토벽','earthwall_map'),('hill','언덕','raised_hill_map')]:
        maps.append(f'<article><h3>{name} 지도 부품</h3><div class="comparison"><figure>{html_image(old/f"UI/Resources/TrapMap/{oldname}.png", "현재 지도 그림", "oldmap")}<figcaption>현재 게임 원본</figcaption></figure><figure>{html_image(ROOT/f"map/{kind}_autotile_sheet.png", "중심과 여덟 방향 팔", "mapstrip")}<figcaption>중심 / 북 / 북동 / 동 / 남동 / 남 / 남서 / 서 / 북서</figcaption></figure></div></article>')
    anim=[data_uri(Image.open(ROOT/f'previews/damage_{s}.png')) for s in STAGES]
    mask_images={k:{d:data_uri(v) for d,v in vv.items()} for k,vv in MAP_PARTS.items()}
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-087 토벽·언덕 이음</title>
<style>*{box-sizing:border-box}body{margin:0;background:#171d1b;color:#e3e5d5;font:16px/1.65 system-ui,sans-serif}main{max-width:1440px;margin:auto;padding:32px}h1{font-size:32px;line-height:1.3}h2{margin-top:44px;color:#e4c48a}h3{margin:0 0 16px}p{max-width:900px;color:#c0c8bc}article,.panel{background:#232d28;border:1px solid #455344;border-radius:10px;padding:22px;margin:20px 0}img,canvas{image-rendering:pixelated;max-width:100%}figure{margin:0;min-width:0}figcaption{color:#aebaa8;font-size:13px;margin-top:8px}.layout{display:grid;grid-template-columns:4fr 1fr;gap:18px;align-items:center}.layout img{width:100%;background:repeating-conic-gradient(#2e392f 0 25%,#344132 0 50%) 0/16px 16px}.comparison{display:grid;grid-template-columns:270px 1fr;gap:24px;align-items:center}.comparison img{background:#344132}.wide img{width:100%}.oldmap{width:96px}.mapstrip{width:864px}button,select{background:#40533d;color:#fff;border:1px solid #81916b;border-radius:5px;padding:8px 15px;font:inherit;cursor:pointer}.controls{display:flex;gap:12px;flex-wrap:wrap;align-items:center}.directions{display:grid;grid-template-columns:repeat(4,auto);gap:10px;margin:20px 0}.play{display:grid;grid-template-columns:300px 1fr;gap:30px;align-items:center}#maskCanvas{width:288px;height:288px;background:#344132}#damage{width:880px;background:#344132}.tag{color:#e4c48a}footer{padding:30px 0;color:#aebaa8}@media(max-width:760px){main{padding:16px}.layout,.comparison,.play{grid-template-columns:1fr}.layout figure:last-child{max-width:280px}.directions{grid-template-columns:repeat(2,auto)}h1{font-size:26px}article{padding:14px}}</style>
<main><span class="tag">K-087 · 연결형 지형 아트 · 검토용</span><h1>토벽은 한 줄로, 언덕은 한 능선으로</h1><p>한 번의 배치는 정확히 두 칸을 차지합니다. 한 칸은 5m이며, 대각선은 모서리까지 이어집니다. 이 페이지는 새 부품의 조합 예시입니다. 실제 게임 연결과 이동 차단은 Claude가 적용하고 Unity 실행·캡처는 사용자가 확인합니다.</p>
<div class="panel"><b>기준</b><p>전투 토벽: 가로 40점/m · 온전한 벽 높이 2.8m(112점). 직선 200점, 대각 283점. 기준선은 캔버스 맨 아래이며 중심 피벗을 사용합니다. 지도: 32×32점 = 5×5m. 인물용 96×128 규격은 이 지형 부품에 적용하지 않습니다.</p></div>
<h2>연결 방향을 직접 바꾸기</h2><div class="panel play"><canvas id="maskCanvas" width="96" height="96" aria-label="선택한 이웃 방향에 맞춘 지도 연결 예시"></canvas><div><div class="controls"><label>지형 <select id="kind"><option value="earthwall">토벽</option><option value="hill">언덕</option></select></label><button id="all">모두 연결</button><button id="none">연결 해제</button></div><div class="directions" id="directions"></div><p>선택한 이웃 칸과 중심 칸의 팔을 서로 마주 보게 겹칩니다. 대각 팔은 셀 모서리까지 채워 네 칸이 만나는 곳의 틈을 막습니다. 이 조작은 이음 부품 확인용이며 실제 배치 도구가 아닙니다.</p><output id="maskLabel"></output></div></div>
<h2>토벽 손상 상태 전환</h2><div class="panel"><div class="controls"><button id="toggle">일시 정지</button><span id="stageLabel">온전함</span></div><img id="damage" alt="같은 토벽 배치의 네 손상 상태 전환"><p>온전함 → 균열 → 붕괴 → 잔해. 상태 전환 미리보기이며 새 동작 애니메이션은 포함하지 않습니다. 붕괴·잔해의 낮아진 실루엣도 바닥선은 동일합니다.</p></div>
<h2>두 칸 조각 세 개의 배치 예시</h2>''' + ''.join(sections)+ '<h2>현재 전투 그림과 새 부품</h2>'+''.join(part_sections)+'<h2>현재 지도 그림과 새 부품</h2>'+''.join(maps)+'''<footer>서버 없이 열 수 있는 내장 이미지 페이지 · Game/ 원본 변경 없음 · 새 그림의 게임 적용 전 외형 검토</footer></main><script>
const animation=ANIMATION;const parts=MAPS;const names=['온전함','균열','붕괴','잔해'];let frame=0,playing=true;const damage=document.querySelector('#damage');damage.src=animation[0];setInterval(()=>{if(playing){frame=(frame+1)%4;damage.src=animation[frame];document.querySelector('#stageLabel').textContent=names[frame]}},1100);document.querySelector('#toggle').onclick=e=>{playing=!playing;e.target.textContent=playing?'일시 정지':'자동 전환'};
const dirs=['N','NE','E','SE','S','SW','W','NW'],ko=['북','북동','동','남동','남','남서','서','북서'],v=[[0,-1],[1,-1],[1,0],[1,1],[0,1],[-1,1],[-1,0],[-1,-1]],cache={};let ready=false;window.previewReady=false;
Promise.all(Object.entries(parts).flatMap(([kind,p])=>Object.entries(p).map(async([d,src])=>{let i=new Image();i.src=src;await i.decode();cache[kind+':'+d]=i}))).then(()=>{ready=true;draw();window.previewReady=true});
const dirbox=document.querySelector('#directions');dirs.forEach((d,i)=>{const label=document.createElement('label');label.innerHTML='<input type="checkbox" value="'+d+'" '+([1,2,5,6].includes(i)?'checked':'')+'>'+ko[i];dirbox.append(label)});
function draw(){if(!ready)return;const c=document.querySelector('#maskCanvas').getContext('2d');c.imageSmoothingEnabled=false;c.clearRect(0,0,96,96);const kind=document.querySelector('#kind').value;let active=[...dirbox.querySelectorAll('input')].map(e=>e.checked);const cells=[[1,1]];active.forEach((on,i)=>{if(on)cells.push([1+v[i][0],1+v[i][1]])});for(const [x,y] of cells)c.drawImage(cache[kind+':hub'],x*32,y*32);active.forEach((on,i)=>{if(on){const [dx,dy]=v[i];c.drawImage(cache[kind+':'+dirs[i]],32,32);c.drawImage(cache[kind+':'+dirs[(i+4)%8]],(1+dx)*32,(1+dy)*32)}});document.querySelector('#maskLabel').textContent='이웃 마스크: '+active.reduce((n,on,i)=>n+(on?(1<<i):0),0)+' / 255'}
dirbox.onchange=draw;document.querySelector('#kind').onchange=draw;document.querySelector('#all').onclick=()=>{dirbox.querySelectorAll('input').forEach(e=>e.checked=true);draw()};document.querySelector('#none').onclick=()=>{dirbox.querySelectorAll('input').forEach(e=>e.checked=false);draw()};
</script></html>'''
    html=html.replace('ANIMATION',json.dumps(anim)).replace('MAPS',json.dumps(mask_images))
    dest=REPO/'_HTML/5_그림_전장/codex_K-087_토벽언덕이음_v1.html'
    dest.parent.mkdir(parents=True,exist_ok=True); dest.write_text(html,encoding='utf-8')

if __name__=='__main__':
    BATTLE_PARTS=build_battle(); MAP_PARTS=build_map(); layouts=build_previews(BATTLE_PARTS,MAP_PARTS)
    dump('manifest.json',dict(card='K-087',version=1,generator='built-in image_gen; technical crop, alpha threshold, nearest-neighbour registration and atlas assembly',cell_size_m=5,placement_cell_count=2,rotation_step_deg=45,pixel_origin='top_left unless explicitly stated',battle=dict(nominal_px_per_metre=40,game_px_per_metre=120,board_height_m=2.8,canvas_height_native_px=128,baseline_native_y=128,pivot_normalized_bottom_left=[.5,0],diagonal_length_m=math.sqrt(50),diagonal_raster_length_px=283,rendering='upright vertical axis; orient length along neighbour vector in XZ; do not roll billboard in screen plane',compositing_order=['segments back to front','end caps at exposed terminals','joint at every connected cell centre and shared seam'],joint_native_width_px=48,terrain_exception_to_character_cells=True),map=dict(px_per_metre=6.4,cell_px=[32,32],pivot_px_top_left=[16,16],mask_bit_order=DIRS,composition_order=['all hubs','all connected arms'],opposite_arm_rule='enable opposite direction in adjacent cell; diagonals fill shared corner',anchor_coordinates_are_pixel_edges=True),layouts=layouts,assets=assets))
    build_html(layouts)
    print('생성 완료:',len(assets),'개 에셋')
