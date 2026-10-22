from pathlib import Path
import json, base64, shutil, hashlib, math, re
from collections import deque
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
GEN = Path('C:/Users/User/.codex/generated_images/01a1622d-058b-74b0-9d9e-9a27fbab444a')
SOURCES = {'brazier':'exec-50f6ac8a-9ff5-4fcf-94e0-8e2171bc6cdc.png', 'fog':'exec-1ed51058-01e2-4984-a512-61e7920ba537.png', 'hud':'exec-100b06fe-a1ed-4333-aa90-8c3831dcf5a9.png'}
NN = Image.Resampling.NEAREST
assets = []
for folder in ['sources','native','game','icons','hud','previews']:
    (OUT/folder).mkdir(exist_ok=True)
for key, file in SOURCES.items():
    target=OUT/'sources'/f'{key}_atlas.png'
    if not target.exists():shutil.copyfile(GEN/file,target)

def blank(w=96,h=128): return Image.new('RGBA',(w,h))
def clean(im, threshold=190, minimum=3):
    a=np.array(im.convert('RGBA')).copy()
    mask=a[:,:,3]>=threshold
    seen=np.zeros(mask.shape,bool)
    for y,x in zip(*np.where(mask)):
        if seen[y,x]: continue
        q=[(y,x)]; seen[y,x]=True; component=[]
        while q:
            cy,cx=q.pop(); component.append((cy,cx))
            for dy,dx in [(0,1),(0,-1),(1,0),(-1,0)]:
                ny,nx=cy+dy,cx+dx
                if 0<=ny<mask.shape[0] and 0<=nx<mask.shape[1] and mask[ny,nx] and not seen[ny,nx]:
                    seen[ny,nx]=True;q.append((ny,nx))
        if len(component)<minimum:
            for cy,cx in component: mask[cy,cx]=False
    a[:,:,3]=np.where(mask,255,0);a[~mask]=0
    return Image.fromarray(a)
def trim(im): return im.crop(im.getbbox())
def deliver(file,im,**meta):
    im.save(OUT/(file+'.png'))
    assets.append({'file':file+'.png','size':list(im.size),'mode':im.mode,**meta})
def sheet(name,frames,**meta):
    w,h=frames[0].size;s=blank(w*len(frames),h)
    for i,f in enumerate(frames): s.alpha_composite(f,(i*w,0))
    common={'cell':[w,h],'frames':len(frames),'frame_order':list(range(len(frames))),**meta}
    deliver('native/'+name,s,**common)
    game_meta={**common,'cell':[w*3,h*3]}
    for key in ['pivot_px','anchor_px']:
        if key in game_meta:game_meta[key]=[v*3 for v in game_meta[key]]
    for key in ['baseline_y','center_x']:
        if game_meta.get(key) is not None:game_meta[key]*=3
    deliver('game/'+name,s.resize((s.width*3,s.height*3),NN),**game_meta)
    deliver('previews/'+name+'_4x',s.resize((s.width*4,s.height*4),NN),role='preview')
    return s

# 받침은 한 장만 사용하여 프레임마다 바닥과 폭이 바뀌지 않게 한다.
src=Image.open(OUT/'sources/brazier_atlas.png').convert('RGBA')
base=clean(src.crop((42,807,228,981)),200)
base=trim(base).resize((48,44),NN)
base=clean(base,200)
off=blank();off.alpha_composite(base,(24,77))
common={'pivot_px':[48,120],'baseline_y':120,'center_x':48,'blend':'alpha','filter':'Point'}
sheet('brazier_unlit',[off],**common)
levels=['low','normal','high']; names=['약','보통','강']; frames_by_level={};flames_by_level={}
for level,mouth in zip(levels,[116,370,656]):
    frames=[];flames=[]
    for i,cx in enumerate([134,387,642,896,1151,1405]):
        flame=src.crop((cx-84,mouth-158,cx+84,mouth+1))
        a=np.array(flame); rgb=a[:,:,:3].astype(float)
        mask=(a[:,:,3]>180)&(rgb[:,:,0]>110)&(rgb[:,:,0]>rgb[:,:,2]*1.10)&(rgb[:,:,1]>38)
        # 바로 위 행의 받침이 불꽃 영역에 섞이지 않게 원화 행 경계를 제한한다.
        row_start={'low':50,'normal':270,'high':510}[level]
        mask[:max(0,row_start-(mouth-158))]=False
        a[:,:,3]=np.where(mask,255,0);a[~mask]=0
        flame=Image.fromarray(a).resize((46,43),NN);flame=clean(flame,190,3)
        f=blank();f.alpha_composite(flame,(25,40));composite=off.copy();composite.alpha_composite(f)
        frames.append(composite);flames.append(f)
    frames_by_level[level]=frames;flames_by_level[level]=flames
    sheet('brazier_'+level,frames,**common,fps=8,loop=True)
    sheet('flame_'+level,flames,**{**common,'baseline_y':None,'anchor_px':[48,82]},fps=8,loop=True,role='flame_overlay')
    # 생성 원화의 불빛을 흐리게 확장하여 가산 광원용 스프라이트로 분리한다.
    glow=blank(192,192);light=flames[0].crop((20,35,76,86)).resize((72,66),Image.Resampling.BILINEAR)
    glow.alpha_composite(light,(60,63));glow=glow.filter(ImageFilter.GaussianBlur({'low':14,'normal':22,'high':30}[level]))
    ga=np.array(glow);yy,xx=np.mgrid[:192,:192];fade=np.clip((94-np.sqrt((xx-96)**2+(yy-96)**2))/18,0,1)
    ga[:,:,3]=(ga[:,:,3]*fade*{'low':2.0,'normal':2.8,'high':3.4}[level]).clip(0,255).astype('uint8');ga[:,:,:3]=[255,146,42];ga[ga[:,:,3]<2]=0
    glow=Image.fromarray(ga)
    deliver('native/glow_'+level,glow,role='additive_glow',cell=[192,192],frames=1,pivot_px=[96,96],blend='SrcAlpha One',attach_native_px=[48,72],filter='Bilinear')
    deliver('game/glow_'+level,glow.resize((576,576),NN),role='additive_glow',cell=[576,576],frames=1,pivot_px=[288,288],blend='SrcAlpha One',attach_game_px=[144,216],filter='Bilinear')
    deliver('previews/glow_'+level+'_4x',glow.resize((768,768),NN),role='preview')

# 안개 원화에 명시적인 바깥쪽/안쪽 알파와 주기 경계를 부여한다.
fogsrc=Image.open(OUT/'sources/fog_atlas.png').convert('RGBA')
tex=np.array(fogsrc.crop((0,0,512,512)).resize((512,128),NN)).astype(float)
yy,xx=np.mgrid[:128,:512];fade=np.clip((112-yy)/100,0,1)
tex[:,:,:3]=np.minimum(tex[:,:,:3]*.52,np.array([42,34,52]))
tex[:,:,3]=(fade**1.55*(198+tex[:,:,3]*.19)).clip(0,236)
for x in range(24):
    avg=(tex[:,x]+tex[:,-1-x])/2;weight=(1-x/24)**2
    tex[:,x]=tex[:,x]*(1-weight)+avg*weight
    tex[:,-1-x]=tex[:,-1-x]*(1-weight)+avg*weight
tex=tex.round().clip(0,255).astype('uint8');tex[tex[:,:,3]==0]=0
band=Image.fromarray(tex);profile=tex[:,0].astype(float)
corner=np.zeros((128,128,4),dtype='uint8')
for y in range(128):
    for x in range(128):
        a,b=profile[y],profile[x];wa,wb=a[3]/255,b[3]/255
        if wa+wb:
            corner[y,x,:3]=((a[:3]*wa+b[:3]*wb)/(wa+wb)).round()
            corner[y,x,3]=round((wa+wb-wa*wb)*255)
corner[:,-1]=tex[:,0];corner[-1,:]=tex[:,0]
corner_im=Image.fromarray(corner)
for name,im in [('fog_band',band),('fog_corner_nw',corner_im)]:
    sheet(name,[im],role='fog_edge',blend='alpha',filter='Bilinear',tile_axis='x' if name=='fog_band' else None)
wisps=[]
seed=fogsrc.crop((0,1050,512,1510)); seed.thumbnail((74,66),NN)
for i in range(4):
    w=blank();w.alpha_composite(seed,(11+[0,2,0,-2][i],38+[0,-1,-2,-1][i]))
    a=np.array(w);y,x=np.mgrid[:128,:96];edge=np.clip(np.minimum.reduce([x/10,(95-x)/10,(y-28)/10,(115-y)/10]),0,1)
    a[:,:,:3]=(a[:,:,:3]*.48).astype('uint8');a[:,:,3]=(a[:,:,3]*edge*[.60,.68,.64,.56][i]).astype('uint8');a[a[:,:,3]<3]=0
    wisps.append(Image.fromarray(a))
sheet('fog_wisp',wisps,role='wisp_overlay',fps=2,loop=True,pivot_px=[48,64],blend='alpha',filter='Bilinear')

# HUD는 기존 황동 테두리의 색과 두께를 그대로 활용한다.
hudsrc=Image.open(OUT/'sources/hud_atlas.png').convert('RGBA')
icons={}
for i,level in enumerate(levels):
    cell=clean(hudsrc.crop((i*hudsrc.width//3,0,(i+1)*hudsrc.width//3,hudsrc.height)),190,8)
    cell=trim(cell)
    for size in [32,48]:
        icon=blank(size,size); small=cell.copy();small.thumbnail((size-4,size-4),NN);icon.alpha_composite(small,((size-small.width)//2,(size-small.height)//2))
        icon=clean(icon,190,1);icons[level,size]=icon
        deliver(f'icons/lamp_{level}_{size}',icon,role='hud_icon',brightness=level,cell=[size,size],frames=1,filter='Point')
        deliver(f'previews/lamp_{level}_{size}_4x',icon.resize((size*4,size*4),NN),role='preview')
existing=Image.open(ROOT/'Game/Assets/_Project/Art/UI/Resources/HudArt/Repair/hp_frame.png').convert('RGBA')
def nine_slice(source,w,h,b=8):
    out=blank(w,h);sx=[0,b,source.width-b,source.width];sy=[0,b,source.height-b,source.height]
    dx=[0,b,w-b,w];dy=[0,b,h-b,h]
    for y in range(3):
        for x in range(3):
            piece=source.crop((sx[x],sy[y],sx[x+1],sy[y+1])).resize((dx[x+1]-dx[x],dy[y+1]-dy[y]),NN)
            out.alpha_composite(piece,(dx[x],dy[y]))
    return out
frame=nine_slice(existing,32,32)
deliver('hud/lamp_frame_9slice',frame,role='nine_slice',border_lbrt=[8,8,8,8],minimum_size=[32,32],filter='Point')
fontpath='C:/Windows/Fonts/malgun.ttf'
def font(size): return ImageFont.truetype(fontpath,size)
def panel(w,h): return nine_slice(frame,w,h)
toggles=[]
for active in range(3):
    toggle=panel(288,80);d=ImageDraw.Draw(toggle)
    for i,level in enumerate(levels):
        if i==active:d.rounded_rectangle((i*96+7,7,i*96+89,72),radius=3,fill='#443522',outline='#e8c87d',width=2)
        toggle.alpha_composite(icons[level,32],(i*96+32,10));d.text((i*96+48,46),names[i],font=font(16),fill='#f3dfae',anchor='mt')
    toggles.append(toggle);deliver('hud/lamp_toggle_'+levels[active],toggle,role='toggle_state',selected=levels[active],cell=[288,80],frames=1)
    deliver('previews/lamp_toggle_'+levels[active]+'_4x',toggle.resize((1152,320),NN),role='preview')

def uri(im):
    import io
    b=io.BytesIO();im.save(b,format='PNG');return 'data:image/png;base64,'+base64.b64encode(b.getvalue()).decode()
def add_glow(dst,glow,pos):
    layer=blank(*dst.size);layer.alpha_composite(glow,pos)
    a=np.array(dst).astype(float);b=np.array(layer).astype(float);a[:,:,:3]=np.minimum(255,a[:,:,:3]+b[:,:,:3]*(b[:,:,3:4]/255))
    return Image.fromarray(a.astype('uint8'))

# 실제 캡처 자료를 0.65로 어둡게 한 뒤 납품 에셋만 배치한 합성 목업이다.
battle_source='Docs/References/codex_k085_v1/battle_before.png'
battle=Image.open(ROOT/battle_source).convert('RGBA').resize((1920,1080),NN)
arr=np.array(battle);arr[:,:,:3]=(arr[:,:,:3]*.65).astype('uint8');battle=Image.fromarray(arr)
header=battle.crop((30,32,1256,112))
for x in [160,540,920,1300,1680]:
    battle=add_glow(battle,Image.open(OUT/'game/glow_high.png'),(x-144,566-288))
    battle.alpha_composite(frames_by_level['high'][0].resize((288,384),NN),(x,350))
edge=band.resize((1536,350),NN)
for x in range(0,1920,1536):battle.alpha_composite(edge,(x,0))
battle.alpha_composite(header,(30,32))
goblin=Image.open(ROOT/'Docs/References/codex_k070_v1/goblin_species56_96x128.png').convert('RGBA')
for x,y in [(290,380),(550,310),(1080,370),(1450,330)]:battle.alpha_composite(goblin.resize((144,192),NN),(x,y))
battle.alpha_composite(toggles[2].resize((432,120),NN),(1444,24))
d=ImageDraw.Draw(battle);d.rounded_rectangle((34,944,1134,1028),8,fill='#10131ddd',outline='#b29965',width=2)
d.text((56,955),'북쪽 성벽 · 밝기 강',font=font(24),fill='#ffde8a');d.text((56,990),'합성 목업 · 야간 밝기 0.65 · 안개는 전장 외곽에만 배치',font=font(20),fill='#e0d9cb')
deliver('mockup_north_wall_high_1920x1080',battle,role='concept_mockup',background_brightness=.65,source=battle_source)

# 안개를 포함하지 않는 45m 내부와 경계 연결을 따로 확인한다.
proof=blank(768,320);proof.paste((47,55,43,255),(0,0,768,320))
proof.alpha_composite(band.resize((768,128),NN),(0,0))
for x in [100,250,400,550]:proof.alpha_composite(goblin,(x,150))
deliver('previews/fog_clear_field_proof',proof,role='placement_proof',clear_region='y>=128; illustrative 45m inner field, not calibrated screen metres')
comparison=blank(480,256)
torch=Image.open(ROOT/'Game/Assets/_Project/Art/Battle/Resources/BattleArt/Props/torch_mount_off.png').convert('RGBA')
comparison.alpha_composite(torch,(0,0));comparison.alpha_composite(off.resize((192,256),NN),(128,0));comparison.alpha_composite(frames_by_level['high'][0].resize((192,256),NN),(288,0))
deliver('previews/scale_comparison',comparison,role='reference_comparison')

manifest={'card':'K-103','version':1,'status':'assets_only_integration_pending','generation':'built-in image_gen; raw sources preserved; deterministic crop, alpha cleanup, fixed-base assembly, nearest-neighbor scaling and fog edge conditioning',
 'coordinates':'top-left pixels; pivot_px uses top-left origin; Unity normalized brazier pivot is (0.5,0.0625)',
 'native_cell':[96,128],'game_cell':[288,384],'foot_native':[48,120],'foot_game':[144,360],'scale':3,
 'brazier_dimensions':{'bowl_width_native':48,'unlit_height_native':44,'relative_to_human_73px':'unlit 60%; high flame included approximately 80px'},
 'assembly':{'brazier':'Use either complete brazier loop, OR unlit plus flame overlay. Do not use both flames.','glow':'SrcAlpha One; alpha is intensity, straight RGB; attach glow centre to brazier native (48,72), game (144,216). Glows never obscure enemies.','hud':'Three exported toggle states are static visual states. Claude supplies input and state handling. 9-slice borders left/bottom/right/top = 8px. Icons available at exact 32 and 48 px.'},
 'fog':{'native_band_cell':[512,128],'native_corner_cell':[128,128],'game_band_cell':[1536,384],'game_corner_cell':[384,384],'corner':'NW; top/left are outer field; rotate clockwise 90/180/270 for NE/SE/SW. Right edge joins band x=0; bottom edge joins the same band rotated.','clear_margin':'Entire fog geometry, including wisps and falloff, must be outside 45m from the wall. Place band clear edge at >=45m. Do not stretch over the playable inner field. Assets alone cannot enforce world metres.','band_orientation':'top opaque outer field, bottom transparent inner field; horizontal repeat; wrap U Repeat / V Clamp; disable mipmaps or add atlas extrusion','wisps':'4 frames left-to-right, 2 fps; native 96x128; game 288x384; place entirely beyond 45m; can scroll U slowly; no gameplay code delivered','night':'mockup uses RGB * 0.65 before VFX; runtime exposure/sorting remains integration test'},
 'references':[battle_source,'Game/Assets/_Project/Art/Battle/Resources/BattleArt/World/wall_stone_albedo.png','Game/Assets/_Project/Art/Battle/Resources/BattleArt/Props/torch_mount_off.png','Game/Assets/_Project/Art/Battle/Resources/BattleArt/Effects/torch_flame.png','Game/Assets/_Project/Art/UI/Resources/HudArt/Repair/hp_frame.png'], 'assets':assets}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')

cards=''
for level,label in zip(levels,names):
    im=Image.open(OUT/f'native/brazier_{level}.png');cards+=f'<article><h3>{label} · 6프레임</h3><div class="stage"><div class="brazier anim" data-n="6" data-w="288" style="background-image:url({uri(im)})"></div></div><img alt="{label} 단계 불꽃 전체 프레임" src="{uri(im)}"></article>'
html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-103 성벽 화로와 안개</title><style>
*{box-sizing:border-box}body{margin:0;background:#11141c;color:#e7deca;font-family:Malgun Gothic,sans-serif}main{max-width:1240px;margin:auto;padding:32px 24px}h1{color:#f3d28c;font-size:32px}h2{margin-top:40px}p{line-height:1.8;color:#c2bcae}button{background:#362e24;border:1px solid #b59b61;color:#f2d99c;padding:12px 18px;margin:4px;cursor:pointer}button[aria-pressed=true]{background:#6b4a20;outline:2px solid #eccc85}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px}.two{display:grid;grid-template-columns:1fr 1fr;gap:20px}article{padding:18px;background:#1b202b;border:1px solid #544937;border-radius:8px}img{max-width:100%;image-rendering:pixelated}.stage{height:384px;display:flex;justify-content:center;background:repeating-conic-gradient(#242936 0 25%,#1c202b 0 50%) 0/24px 24px}.brazier{width:288px;height:384px;flex-shrink:0;background-size:1728px 384px;image-rendering:pixelated}.fogstage{height:230px;overflow:hidden;position:relative;background:#404937}.fogband{height:128px;width:100%;background-repeat:repeat-x;animation:drift 24s linear infinite}.wisp{position:absolute;left:42%;top:30px;width:192px;height:256px;background-size:768px 256px;image-rendering:pixelated}@keyframes drift{to{background-position-x:512px}}.tag{color:#efd18c}small{line-height:1.8;color:#b1aa9e}.mock{width:100%;border:1px solid #847047}.toggle{display:flex;justify-content:center;padding:18px;background:#151923}.icons{display:flex;flex-wrap:wrap;align-items:center;justify-content:center;gap:20px}table{width:100%;border-collapse:collapse}td,th{padding:10px;border-bottom:1px solid #454139;text-align:left}@media(max-width:760px){.grid,.two{grid-template-columns:1fr}main{padding:18px 12px}h1{font-size:25px}}</style><main>
<p class="tag">빛을 잇는 자들 · 에셋 검토 K-103</p><h1>성벽의 불빛, 전장 끝의 검은 안개</h1><p>밝기 약·보통·강을 불꽃 크기와 등불 표식 개수로 구별합니다. 생성 원화를 정리한 에셋과 합성 목업이며, 실제 게임 적용 화면은 아닙니다.</p><button id="pause">재생 멈춤</button><button id="night">배경 밤 / 격자</button><h2>성벽 화로 · 밝기별 반복</h2>'''+f'<div class="grid">{cards}</div>'+f'''
<h2>현재 게임 에셋과 나란히 보기</h2><div class="two"><article><h3>현재 횃불 · 신규 꺼짐 · 신규 강</h3><img alt="기존 횃불과 신규 화로 비교" src="{uri(comparison)}"><p>기존 횃불은 벽걸이형, 신규 화로는 성벽 위에 놓는 낮고 넓은 받침입니다. 교체 시트가 아니라 새 에셋입니다.</p></article><article><h3>현재 게임 불꽃 · 6프레임</h3><div class="stage"><div class="anim" data-n="6" data-w="256" style="width:256px;height:256px;background-image:url({uri(Image.open(ROOT/'Game/Assets/_Project/Art/Battle/Resources/BattleArt/Effects/torch_flame.png'))})"></div></div></article></div>
<h2>검은 안개 · 가로 반복과 느린 잔안개</h2><p>연한 자흑색 윤곽으로 밤에도 경계가 보이게 했습니다. 위쪽은 전장 밖, 아래쪽은 투명한 전장 안쪽입니다. 안개와 잔안개 전체를 성벽에서 45m 바깥에 놓아야 합니다.</p><div class="fogstage"><div class="fogband" style="background-image:url({uri(band)})"></div><div class="wisp anim" data-n="4" data-w="192" style="background-image:url({uri(Image.open(OUT/'native/fog_wisp.png'))})"></div></div>
<div class="two"><article><h3>북서쪽 코너 · 오른쪽과 아래로 연결</h3><img width="384" alt="안개 북서 코너" src="{uri(corner_im)}"></article><article><h3>안쪽 적 실루엣 보존 배치 예시</h3><img alt="안개 밖의 내부 적은 가리지 않는 배치" src="{uri(proof)}"><p>45m는 월드 좌표에서 적용할 조건입니다. 이 그림의 픽셀 거리가 45m로 보정된 것은 아닙니다.</p></article></div>
<h2>등불 밝기 · 3단계 HUD</h2><p>단추를 눌러 선택 상태를 비교할 수 있습니다. 게임 조작은 Claude 연결 범위입니다. 밝기별 실제 사거리·적 수·보상 수치는 이번 에셋에 고정하지 않았습니다.</p><div id="controls">'''+''.join(f'<button data-level="{i}" aria-pressed="{str(i==1).lower()}">{label}</button>' for i,label in enumerate(names))+f'''</div><div class="toggle"><img id="toggle" alt="등불 밝기 선택" src="{uri(toggles[1])}"></div><div class="icons">'''+''.join(f'<img width="64" alt="{name} 등불 32화소" src="{uri(icons[level,32])}"><img width="96" alt="{name} 등불 48화소" src="{uri(icons[level,48])}">' for level,name in zip(levels,names))+f'''</div>
<h2>북쪽 성벽 · 강 밝기 · 밤 전투 목업</h2><p>기존 전투 캡처 자료에 배경 밝기 0.65를 적용하고 신규 화로·가산 광원·안개·HUD를 합성했습니다. 실시간 조명, 카메라 거리, 45m 경계의 실제 적용은 Unity 연결 뒤 확인해야 합니다.</p><img class="mock" alt="북쪽 성벽 야간 전투 1920×1080 합성 목업" src="{uri(battle)}">
<h2>전달 규격</h2><table><tr><th>항목</th><th>규격</th></tr><tr><td>화로 / 분리 불꽃</td><td>96×128 → 최근접 3배 288×384 · 6프레임 · 초당 8프레임</td></tr><tr><td>고정 기준점</td><td>원본 (48,120) · 게임판 (144,360) · 받침 고정</td></tr><tr><td>안개</td><td>가로 512×128 · 코너 128×128 · 잔안개 4프레임, 초당 2프레임 · 각각 3배판 제공</td></tr><tr><td>광원 / HUD</td><td>밝기별 가산 광원 1장 · 등불 32/48화소 · 9분할 테두리 32×32, 사방 8화소</td></tr></table><p>화로 완성 시트와 분리 불꽃을 동시에 겹치지 않습니다. 광원은 가산 합성, 안개는 일반 알파 합성입니다. Game/ 수정과 Unity 실행은 하지 않았습니다.</p><small id="health">그림 로드 확인 중</small></main><script>
const toggles='''+json.dumps([uri(t) for t in toggles])+''';let paused=false,frame=0,night=false;document.getElementById('pause').onclick=e=>{paused=!paused;e.target.textContent=paused?'재생 계속':'재생 멈춤';document.querySelector('.fogband').style.animationPlayState=paused?'paused':'running'};document.getElementById('night').onclick=()=>{night=!night;document.querySelectorAll('.stage').forEach(s=>s.style.background=night?'#10131c':'')};document.querySelectorAll('[data-level]').forEach(b=>b.onclick=()=>{document.getElementById('toggle').src=toggles[+b.dataset.level];document.querySelectorAll('[data-level]').forEach(a=>a.setAttribute('aria-pressed',a===b))});setInterval(()=>{if(paused)return;frame++;document.querySelectorAll('.anim').forEach(a=>{let t=+a.dataset.n===4?Math.floor(frame/4):frame;a.style.backgroundPosition=(-t%+a.dataset.n*+a.dataset.w)+'px 0'})},125);Promise.all([...document.images].map(a=>a.complete?Promise.resolve(a.naturalWidth>0):new Promise(r=>{a.onload=()=>r(true);a.onerror=()=>r(false)}))).then(r=>document.getElementById('health').textContent=r.every(Boolean)?'모든 그림 로드 완료':'그림 로드 실패 있음');</script></html>'''
page=ROOT/'_HTML/6_그림_화면/codex_K-103_성벽화로와안개_v1.html';page.parent.mkdir(exist_ok=True);page.write_text(html,encoding='utf-8')
print(json.dumps({'assets':len(assets),'html_bytes':page.stat().st_size},ensure_ascii=False))
