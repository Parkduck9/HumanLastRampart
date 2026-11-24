from pathlib import Path
from PIL import Image
import numpy as np
import json, hashlib, base64, io

# 생성 원안에서 프레임 분리·도트화·발선 배치·최근접 확대를 재현한다.
P = Path(__file__).resolve().parent
R = P.parents[2]
NN = Image.Resampling.NEAREST
GAME = R / 'Game/Assets/_Project/Art/Characters'
PAGE = R / '_HTML/4_그림_캐릭터/codex_K-078_사다리고블린동작_v1.html'

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(name, obj): (P/name).write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding='utf-8')
def solid(im):
    a = np.array(im.convert('RGBA')).copy()
    a[a[:,:,3]<128] = 0
    a[a[:,:,3]>=128,3] = 255
    return Image.fromarray(a)

def components(im):
    a = np.array(im)[:,:,3]>0
    seen = set(); groups=[]
    for y,x in zip(*np.where(a)):
        if (y,x) in seen: continue
        stack=[(y,x)]; seen.add((y,x)); group=[]
        while stack:
            yy,xx=stack.pop(); group.append((yy,xx))
            for dy in (-1,0,1):
                for dx in (-1,0,1):
                    ny,nx=yy+dy,xx+dx
                    if 0<=ny<a.shape[0] and 0<=nx<a.shape[1] and a[ny,nx] and (ny,nx) not in seen:
                        seen.add((ny,nx)); stack.append((ny,nx))
        groups.append(group)
    return sorted(groups,key=len,reverse=True)

def clean(im):
    a=np.array(im).copy()
    for group in components(im)[1:]:
        if len(group)<=4:
            for y,x in group: a[y,x]=0
    return Image.fromarray(a)

def buckle(row,center):
    xx=np.where((row[:,0]>180)&(row[:,1]>160)&(row[:,2]>130)&(row[:,3]>0))[0]
    groups=np.split(xx,np.where(np.diff(xx)>1)[0]+1)
    groups=[g for g in groups if len(g) and 30<g.mean()<67]
    return min(groups,key=lambda g:abs((int(g.min())+int(g.max())+1)/2-center))

def palette(im,ladder=False):
    if ladder:
        a=np.array(im).copy(); rgb=a[:,:,:3].astype(np.int32)
        wood=np.array([(16,10,9),(55,30,20),(111,63,38),(185,126,70)],dtype=np.int32)
        iron=np.array([(16,10,9),(53,50,57),(112,111,122),(181,179,187)],dtype=np.int32)
        warm=(rgb[:,:,0]>rgb[:,:,1]*1.1)&(rgb[:,:,1]>rgb[:,:,2]*1.08)
        for mask,pal in [(warm,wood),(~warm,iron)]:
            d=((rgb[:,:,None,:]-pal)**2).sum(axis=3)
            a[:,:,:3][mask]=pal[d.argmin(axis=2)][mask]
        a[a[:,:,3]==0]=0
        return Image.fromarray(a)
    colors=json.loads((R/'Docs/References/codex_k066_v1/palettes.json').read_text(encoding='utf-8'))['ladder_empty']['colors']
    colors=list(dict.fromkeys(colors+['#100a09','#e7d9b9','#fff4db','#8b9298','#484b55']))
    pal=np.array([tuple(bytes.fromhex(c[1:])) for c in colors],dtype=np.int32)
    a=np.array(im).copy(); rgb=a[:,:,:3].astype(np.int32)
    d=((rgb[:,:,None,:]-pal)**2).sum(axis=3)
    a[:,:,:3]=pal[d.argmin(axis=2)]; a[a[:,:,3]==0]=0
    return Image.fromarray(a)

SPECS=[
 ('ladder_goblin_static','Static','사다리 들고 서기','static',1,['사다리 들고 서기']),
 ('ladder_goblin_walk','Walk','사다리 들고 걷기','carrier',4,['왼발 내딛기','엇갈림','오른발 내딛기','반대 엇갈림']),
 ('ladder_goblin_carry_run4','Ladder','운반 달리기','carry',4,['왼발 지지·오른무릎 들기','오른발 지지·왼무릎 들기','왼발 지지·오른무릎 들기','오른발 지지·왼무릎 들기']),
 ('ladder_goblin_deploy4','Ladder','사다리 설치','deploy',4,['준비','앞으로 잡기','위로 세우기','성벽에 밀어 기대기']),
 ('ladder_goblin_climb4','Ladder','정면 아래에서 본 등반','climb',4,['화면 오른쪽 손·무릎 올림','화면 왼쪽 손으로 옮겨 잡기','화면 왼쪽 손·무릎 올림','화면 오른쪽으로 다시 잡기'])]

ANCHOR_ROWS={'static':[96],'carrier':[96]*4,'carry':[97]*4,'deploy':[97]*4,'climb':[93,93,91,93]}

def encode(im):
    b=io.BytesIO(); im.save(b,format='PNG'); return 'data:image/png;base64,'+base64.b64encode(b.getvalue()).decode()

def build():
    cfg=json.loads((P/'sources.json').read_text(encoding='utf-8'))
    manifest={'card':'K-078','size_decision':'D-081','native_cell':[96,128],'game_cell':[288,384],
      'native_pivot_top_left':[48,120],'game_pivot_top_left':[144,360],
      'unity_normalized_pivot':[0.5,0.0625],'frame_layout':'left_to_right','sheets':[],
      'ladder_world_height_m':4.3,'source_hashes':{}}
    checks=[]; assets={}; native_by_key={}
    for key in ('static','carrier','carry','deploy','climb'):
        src=solid(Image.open(P/f'source_{key}.png')); frames=[]
        count=1 if key=='static' else 4
        for i in range(count):
            raw=src.crop((round(i*src.width/count),0,round((i+1)*src.width/count),src.height))
            if key=='climb' and i==1:
                corrected=solid(Image.open(P/'source_climb_corrected.png'))
                raw=corrected.crop((round(corrected.width/4),0,round(corrected.width/2),corrected.height))
            if key=='climb' and i==2: raw=solid(Image.open(P/'source_climb_left.png'))
            box=raw.getbbox(); raw=raw.crop(box)
            setting=cfg[key]
            height=setting['heights'][i]
            crop=raw.resize((round(raw.width*height/raw.height),height),NN)
            crop=clean(palette(solid(crop)))
            body_x=setting.get('body_x_ratio',[0.5]*4)[i]*crop.width
            out=Image.new('RGBA',(96,128)); out.paste(crop,(round(48-body_x),120-height))
            # 허리띠 버클 중심을 몸통 기준점으로 삼아 칸마다 같은 x=48에 놓는다.
            ay=ANCHOR_ROWS[key][i]
            a=np.array(out); row=a[ay]
            xx=buckle(row,50)
            assert len(xx), (key,i,'버클 기준점 없음')
            delta=round(48-(int(xx.min())+int(xx.max())+1)/2)
            centered=Image.new('RGBA',(96,128)); centered.paste(out,(delta,0)); out=centered
            frames.append(out)
        if key=='carry': frames=[frames[j] for j in (1,0,3,2)]
        native_by_key[key]=frames
    for name,folder,label,key,count,order in SPECS:
        frames=native_by_key[key][:count]
        sheet=Image.new('RGBA',(96*count,128))
        for i,f in enumerate(frames): sheet.paste(f,(i*96,0))
        native=P/f'{name}_native.png'; game=P/f'{name}.png'; preview=P/f'{name}_4x.png'
        sheet.save(native); sheet.resize((288*count,384),NN).save(game); sheet.resize((384*count,512),NN).save(preview)
        bboxes=[]; comps=[]; anchors=[]
        for fi,f in enumerate(frames):
            box=f.getbbox(); bboxes.append(list(box)); comps.append([len(g) for g in components(f)])
            ay=ANCHOR_ROWS[key][fi]
            row=np.array(f)[ay]; xx=buckle(row,48)
            anchor=(int(xx.min())+int(xx.max())+1)/2; anchors.append([anchor,ay])
            assert abs(anchor-48)<=0.5, (key,anchors,xx.tolist())
            assert box[3]==120 and box[0]>0 and box[2]<96
            assert set(np.unique(np.array(f)[:,:,3]))=={0,255}
            assert len(components(f))==1
        assert np.array_equal(np.array(Image.open(game)),np.repeat(np.repeat(np.array(sheet),3,0),3,1))
        assert len({f.tobytes() for f in frames})==count
        old=GAME/folder/f'{name}.png'; manifest['source_hashes'][str(old.relative_to(R))]=sha(old)
        oldim=Image.open(old).convert('RGBA'); assert oldim.size==(256*count,384)
        rec={'name':name,'destination':str(old.relative_to(R)).replace('\\','/'),'native':native.name,'game':game.name,'preview_4x':preview.name,
          'frame_count':count,'frame_order':order,'native_cell':[96,128],'game_cell':[288,384],
          'native_pivot_top_left':[48,120],'game_pivot_top_left':[144,360],'unity_normalized_pivot':[0.5,0.0625],
          'includes_ladder':key in ('carrier','static'),'native_opaque_bboxes':bboxes,'component_sizes':comps,
          'native_body_anchors':anchors,'source_frame_indices':([1,0,3,2] if key=='carry' else list(range(count))),
          'sha256':sha(game),'source':f'source_{key}.png'}
        if key=='climb':
            rec['frame_sources']=['source_climb.png#0','source_climb_corrected.png#1','source_climb_left.png#0','source_climb.png#3']
        manifest['sheets'].append(rec); assets[name]=(sheet,oldim,label,count,order)
        checks.append({'name':name,'sizes_alpha_baseline_nearest':True,'unique_frames':count,'body_anchors':anchors,'components':comps})
    for view in ('front','side'):
        name=f'ladder_{view}'; im=solid(Image.open(P/f'source_{name}.png')); crop=im.crop(im.getbbox())
        height=288; w=cfg[name]['width']
        crop=clean(palette(solid(crop.resize((w,height),NN)),ladder=True))
        native=Image.new('RGBA',(64,384)); native.paste(crop,(32-w//2,368-height))
        game=native.resize((256,1536),NN)
        native.save(P/f'{name}_native.png'); game.save(P/f'{name}.png'); native.resize((256,1536),NN).save(P/f'{name}_4x.png')
        assert game.getbbox()[3]==1472 and game.size==(256,1536)
        assert set(np.unique(np.array(game)[:,:,3]))=={0,255}
        assert len(components(native))==1
        assert np.array_equal(np.array(game),np.repeat(np.repeat(np.array(native),4,0),4,1))
        old=GAME/'Ladder'/f'{name}.png'; manifest['source_hashes'][str(old.relative_to(R))]=sha(old)
        rec={'name':name,'destination':str(old.relative_to(R)).replace('\\','/'),'native':f'{name}_native.png','game':f'{name}.png',
          'preview_4x':f'{name}_4x.png','frame_count':1,'frame_order':['정면' if view=='front' else '측면'],
          'native_cell':[64,384],'game_cell':[256,1536],'native_pivot_top_left':[32,368],'game_pivot_top_left':[128,1472],
          'unity_normalized_pivot':[0.5,1-1472/1536],'scale':4,'world_height_m':4.3,
          'game_opaque_bbox':list(game.getbbox()),'sha256':sha(P/f'{name}.png')}
        manifest['sheets'].append(rec); assets[name]=(native,Image.open(old).convert('RGBA'),'사다리 '+('정면' if view=='front' else '측면'),1,rec['frame_order'])
        checks.append({'name':name,'size_alpha_baseline':True,'components':[len(g) for g in components(native)]})
    write('manifest.json',manifest); write('validation.json',checks)
    make_page(assets)

def make_page(assets):
    sections=[]
    for key,(new,old,label,n,order) in assets.items():
        ladder=key in ('ladder_front','ladder_side')
        scale=2 if not ladder else .75
        nh=round(new.height*scale); nw=round(new.width/n*scale)
        oh=nh; ow=round(old.width/n*nh/old.height)
        # 비교는 같은 칸 높이로 맞추며 원본 수치와 구도를 함께 표시한다.
        panel=[]
        for im,w,h,title in [(old,ow,oh,'현재 게임 시트'),(new,nw,nh,'새 원본 · 최근접 확대')]:
            uri=encode(im)
            panel.append(f'<div class="panel"><h3>{title}</h3><canvas width="{w}" height="{h}" data-src="{uri}" data-count="{n}"></canvas><p>{im.width}×{im.height} · {n}칸</p></div>')
        sections.append(f'<section><h2>{label}</h2><p>{" → ".join(order)}</p><div class="pair">'+''.join(panel)+f'</div><details><summary>전체 프레임 펼쳐 보기</summary><img alt="{label} 전체 프레임" src="{encode(new)}"></details></section>')
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>K-078 사다리 고블린 동작 1판</title>
<style>*{box-sizing:border-box}body{background:#101613;color:#eee4cf;font:16px/1.6 system-ui,sans-serif;margin:0;padding:24px;max-width:1200px;margin:auto}h1,h2{color:#d5b16b}section{border-top:1px solid #57634f;margin:28px 0;padding:12px 0}.pair{display:flex;gap:20px;flex-wrap:wrap}.panel{background:#26352d;padding:12px;min-width:220px}canvas,img{image-rendering:pixelated;max-width:100%;background:repeating-conic-gradient(#202923 0% 25%,#29342c 0% 50%) 0/16px 16px}canvas{display:block}details{overflow:auto}details img{max-width:none}button,input{font:inherit}button{background:#26352d;color:#eee4cf;border:1px solid #d5b16b;padding:10px 18px;cursor:pointer}nav{position:sticky;top:0;background:#101613;padding:10px 0;z-index:1;display:flex;gap:12px;flex-wrap:wrap}small{color:#c9c0a9}</style>
<h1>K-078 · 사다리 고블린 동작</h1><p>D-081 확정 크기: 고블린 몸 약 56점. 원본 96×128 / 발 (48,120), 게임용 288×384 / 발 (144,360). 캐릭터 시트는 3배 최근접 확대입니다.</p>
<p>걷기·서기는 사다리 포함, 운반 달리기·설치·등반은 인물만 그렸습니다. 등반은 요청한 정면 아래 시점입니다. 별도 사다리는 256×1536 / 바닥 y=1472, 높이 4.3m 연결 기준을 유지합니다. 화면 비교는 칸 높이를 맞춘 원화 비교이며 실제 게임 캡처가 아닙니다.</p>
<nav><button id="toggle">일시 정지</button><button id="step">한 칸씩</button><label>초당 칸 수 <input id="fps" type="range" min="1" max="12" value="6"></label><span id="frame">1 / 4</span><span id="loaded">그림 확인 중</span></nav>
'''+''.join(sections)+'''<p>Unity 실행·게임 연결·실제 플레이 검증은 수행하지 않았습니다. 주인공 v6 처음 생성본과 K-066 통일판, K-070 종족 비율을 참고했습니다. 적 종족의 황색 눈을 유지했습니다.</p>
<script>let frame=0,playing=true,last=0;const canvases=[...document.querySelectorAll('canvas')];const imgs=canvases.map(c=>{let i=new Image();i.src=c.dataset.src;return i});function draw(){canvases.forEach((c,k)=>{let i=imgs[k],n=+c.dataset.count;if(!i.complete||!i.naturalWidth)return;let ctx=c.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.clearRect(0,0,c.width,c.height);ctx.drawImage(i,(frame%n)*i.width/n,0,i.width/n,i.height,0,0,c.width,c.height);ctx.strokeStyle='#d5b16b';let y=c.height*(n===1&&i.height>128?368/384:120/128);ctx.beginPath();ctx.moveTo(0,y+.5);ctx.lineTo(c.width,y+.5);ctx.stroke()});document.querySelector('#frame').textContent=(frame%4+1)+' / 4'}Promise.all([...imgs,...document.images].map(i=>i.decode())).then(()=>{draw();document.querySelector('#loaded').textContent='모든 그림 불러옴';window.reviewReady=true});document.querySelector('#toggle').onclick=()=>{playing=!playing;document.querySelector('#toggle').textContent=playing?'일시 정지':'재생'};document.querySelector('#step').onclick=()=>{playing=false;document.querySelector('#toggle').textContent='재생';frame++;draw()};function tick(t){if(playing&&t-last>1000/+document.querySelector('#fps').value){frame++;last=t;draw()}requestAnimationFrame(tick)}requestAnimationFrame(tick);</script></html>'''
    PAGE.parent.mkdir(parents=True,exist_ok=True); PAGE.write_text(html,encoding='utf-8')

if __name__=='__main__': build()
