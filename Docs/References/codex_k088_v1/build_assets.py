from pathlib import Path
from PIL import Image
import numpy as np
import json, base64, hashlib, shutil

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
NEAREST = Image.Resampling.NEAREST
assets = []

def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')

def largest(mask):
    seen = np.zeros(mask.shape, bool)
    best = []
    for y, x in zip(*np.where(mask)):
        if seen[y, x]:
            continue
        todo, group = [(y, x)], []
        seen[y, x] = True
        while todo:
            cy, cx = todo.pop()
            group.append((cy, cx))
            for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                ny, nx = cy+dy, cx+dx
                if 0 <= ny < mask.shape[0] and 0 <= nx < mask.shape[1] and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    todo.append((ny, nx))
        if len(group) > len(best):
            best = group
    out = np.zeros(mask.shape, bool)
    for y, x in best:
        out[y, x] = True
    return out

def extract(image, box, size, colors, opacity=255):
    # 생성 원본에서 연결된 주 덩어리만 추출하고 목표 픽셀 격자에 맞춘다.
    im = image.crop(box).convert('RGBA')
    a = np.asarray(im).copy()
    a[:, :, 3] = largest(a[:, :, 3] >= 100).astype('uint8') * 255
    im = Image.fromarray(a)
    im = im.crop(im.getbbox()).resize(size, NEAREST)
    a = np.asarray(im).copy()
    palette = np.array(colors, dtype=np.int32)
    distance = ((a[:, :, None, :3].astype(np.int32)-palette[None, None, :, :])**2).sum(axis=3)
    a[:, :, :3] = palette[distance.argmin(axis=2)]
    a[:, :, 3] = largest(a[:, :, 3] > 0).astype('uint8') * opacity
    a[a[:, :, 3] == 0] = 0
    return Image.fromarray(a)

def save_texture(im, stem, name, kind, **extra):
    target = HERE / 'native' / (stem+'.png')
    im.save(target)
    preview = 'previews/'+stem+'_4x.png'
    im.resize((im.width*4, im.height*4), NEAREST).save(HERE/preview)
    entry = dict(id=stem, name_ko=name, kind=kind, native_file='native/'+stem+'.png',
                 native_size_px=list(im.size), preview_4x=preview, **extra)
    assets.append(entry)
    return entry

vfx = Image.open(HERE/'source/vfx_generated.png').convert('RGBA')
flame = [(239,132,46),(255,195,90),(255,245,192)]
smoke = [(115,110,99),(159,153,138),(204,198,179)]
right_boxes = [(300,50,580,270),(700,50,1040,270),(1160,50,1430,270)]
back_boxes = [(330,300,560,550),(770,280,1010,550),(1200,300,1420,550)]
smoke_boxes = [(100,590,300,840),(500,590,770,840),(930,570,1230,850),(1360,550,1700,850)]
specs = [
    ('muzzle_flash_right','총구 불꽃 · 오른쪽',right_boxes,[(10,7),(17,11),(9,8)],[255,255,125]),
    ('muzzle_flash_back','총구 불꽃 · 카메라 반대쪽',back_boxes,[(8,10),(11,16),(8,9)],[255,255,125]),
    ('smoke_up','총 연기 · 위로 흐름',smoke_boxes,[(8,8),(11,10),(14,12),(17,14)],[155,125,80,32])
]
for stem, label, boxes, sizes, opacities in specs:
    frames, bounds = [], []
    for i, (box, size, opacity) in enumerate(zip(boxes,sizes,opacities)):
        palette = smoke if stem == 'smoke_up' or i == 2 else flame
        cut = extract(vfx, box, size, palette, opacity)
        cell = Image.new('RGBA',(96,128))
        if stem.endswith('right'):
            xy = (48+(3 if i == 2 else 0),80-size[1]//2)
        elif stem.endswith('back'):
            xy = (48-size[0]//2,80-size[1]+1-(3 if i == 2 else 0))
        else:
            xy = (48+[0,1,2,4][i]-size[0]//2,80-[2,6,11,17][i]-size[1]//2)
        cell.paste(cut,xy)
        bounds.append(list(cell.getbbox()))
        frames.append(cell)
    sheet = Image.new('RGBA',(96*len(frames),128))
    for i, frame in enumerate(frames):
        sheet.paste(frame,(96*i,0))
    entry = save_texture(sheet,stem,label,'billboard_vfx',native_cell_px=[96,128],
        game_cell_px=[288,384],frame_count=len(frames),frame_order=(['발화','짧은 화염','잔여 퍼프'] if len(frames)==3 else ['작은 퍼프','상승 1','상승 2','소멸']),
        frame_duration_ms=([35,45,40] if len(frames)==3 else [70,90,110,130]),
        loop=False,background='transparent',alpha='straight',
        blend=('SrcAlpha OneMinusSrcAlpha' if stem=='smoke_up' else 'SrcAlpha One'),
        filter='Point',anchor_top_left_px=[48,80],game_anchor_top_left_px=[144,240],
        socket_pivot_bottom_left_normalized=[0.5,0.375],
        reference_body_center_x=48,reference_foot_line_y=120,reference_game_foot_pivot_px=[144,360],
        foot_line_test='해당 없음: 인물 없는 총구 부착 효과. 기준 격자만 공유.',frame_alpha_bounds_top_left=bounds)
    entry['game_file']='game/'+stem+'.png'
    entry['game_size_px']=[sheet.width*3,sheet.height*3]
    sheet.resize((sheet.width*3,sheet.height*3),NEAREST).save(HERE/entry['game_file'])

for stem, label, w, h, peak, extent in [
    ('contact_shadow_small','작은 접지 그림자 · 병사·고블린',64,64,135,[0.85,0.48]),
    ('contact_shadow_medium','중간 접지 그림자 · 오우거·대포',128,64,145,[1.8,0.95]),
    ('contact_shadow_wall_5m','넓은 접지 그림자 · 토벽 5 m',192,64,150,[5.5,1.8]),
    ('contact_shadow_wall_7m','넓은 접지 그림자 · 토벽 7 m',256,64,150,[7.7,1.8])]:
    yy, xx=np.mgrid[0:h,0:w]
    r2=((xx-(w-1)/2)/(w*.46))**2+((yy-(h-1)/2)/(h*(.30 if w==64 else .38)))**2
    a=np.rint(peak*np.maximum(0,1-r2)**2).astype('uint8')
    rgba=np.zeros((h,w,4),dtype='uint8')
    rgba[:,:,3]=a
    save_texture(Image.fromarray(rgba),stem,label,'ground_contact_shadow',
        frame_count=1,pivot_bottom_left_normalized=[.5,.5],filter='Bilinear',
        alpha='straight',background='transparent',rgb=[0,0,0],peak_alpha=peak,
        blend='alpha_weighted_multiply',blend_formula='ground_rgb * (1 - texture_alpha * opacity)',
        quad_size_m=extent,orientation='horizontal_XZ',recommended_opacity=.65,
        notes_ko='검정 RGB와 연속 알파. 알파를 무시하는 순수 곱셈 재질에 직접 넣지 않는다. 지면에 한 번만 투영한다.')

dirt_source=Image.open(HERE/'source/dirt_generated.png').convert('RGBA')
dirt_palette=[(81,69,43),(117,97,60),(165,142,92)]
for suffix, width, extent in [('5m',192,[5.6,1.8]),('7m',256,[7.8,1.8])]:
    cut=extract(dirt_source,(0,0,dirt_source.width,dirt_source.height),(width-12,40),dirt_palette,210)
    im=Image.new('RGBA',(width,64))
    im.paste(cut,(6,12))
    entry=save_texture(im,'dirt_skirt_'+suffix,'토벽 흙자락 · '+suffix.replace('m',' m'),'ground_dirt_decal',
        frame_count=1,pivot_bottom_left_normalized=[.5,.5],filter='Point',alpha='straight',
        blend='SrcAlpha OneMinusSrcAlpha',quad_size_m=extent,orientation='horizontal_XZ',
        recommended_opacity=.8,notes_ko='픽셀 흙자락을 먼저, 토벽을 나중에 표시한다. 지면 투영은 한 번만 적용한다.')
    entry['game_file']='game/dirt_skirt_'+suffix+'.png'
    entry['game_size_px']=[width*3,192]
    im.resize((width*3,192),NEAREST).save(HERE/entry['game_file'])

gun_path=ROOT/'Game/Assets/_Project/Art/Characters/Fire/gunner_fire.png'
wall_path=ROOT/'Docs/References/codex_k068_v1/sprites/earthwall_intact.png'
old_dirt_path=ROOT/'Docs/References/codex_k068_v1/sprites/dirt_01.png'
references=[gun_path,wall_path,old_dirt_path]
manifest=dict(card='K-088',version=1,coordinates='pixel bounds / anchor: top-left; normalized pivots: bottom-left',
    generation=dict(tool='built-in image_gen',sources=['source/vfx_generated.png','source/dirt_generated.png'],
        processing_ko='생성 이미지에서 주 덩어리 추출, 최근접 축소, 3색 팔레트 정리, 프레임 배치. 그림자는 수식으로 생성.'),
    sheets_replaced=[],assets=assets,
    source_references=[dict(file=str(p.relative_to(ROOT)).replace('\\','/'),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in references],
    integration_ko=[
        '총구 불꽃은 사격 확정 시 1회, 35/45/40 ms. 연기는 발사 후 80 ms부터 400 ms 재생한다.',
        '총구 소켓이 효과 앵커에 오도록 배치한다. 발 피벗은 인물용 기준이며 효과 부착 피벗과 다르다.',
        '현재 총병 시트 2번 프레임(0 시작)에는 기존 불꽃이 포함된다. 검토 합성은 불꽃 없는 1번 겨누기 프레임을 사용한다. 게임 연결 시 기존 불꽃 중복 처리가 필요하다.',
        '원본 사수는 뒤·오른쪽 사선 시점이다. 반대쪽 효과 검토는 같은 총구에서 위로 방출한 방향 견본이며 새로운 정후면 사수 동작은 제공하지 않는다.',
        '접지 그림자는 지면 XZ 판에 놓고 깊이 겹침을 피한다. 순수 RGB 곱셈 대신 알파 가중 곱셈을 사용한다.',
        '그림자는 64~256 px 원본을 Bilinear로 사용한다. 3배 픽셀 확대 계약은 불꽃·연기 시트와 흙자락에 적용한다.'
    ],unity_tested=False)
write_json(HERE/'manifest.json',manifest)

def data(path):
    return 'data:image/png;base64,'+base64.b64encode(path.read_bytes()).decode()

images={a['id']:data(HERE/a['native_file']) for a in assets}
images.update(gunner=data(gun_path),wall=data(wall_path),old_dirt=data(old_dirt_path))
payload=json.dumps(images)
gallery=''.join('<article class="'+('smooth' if a['kind']=='ground_contact_shadow' else 'pixel')+'"><h3>'+a['name_ko']+'</h3><img alt="'+a['name_ko']+' 원본 텍스처" src="'+images[a['id']]+'"><p>'+str(a['native_size_px'][0])+' × '+str(a['native_size_px'][1])+' · '+('부드러운 알파' if a['kind']=='ground_contact_shadow' else '최근접 픽셀')+'</p></article>' for a in assets)
html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-088 총구 불꽃과 접지 그림자</title>
<style>
:root{color-scheme:dark;font-family:"Malgun Gothic",sans-serif;background:#151b1b;color:#eee9db}*{box-sizing:border-box}body{margin:0}main{max-width:1180px;margin:auto;padding:32px 24px 80px}h1{font-size:32px;margin:12px 0}h2{font-size:22px;margin-top:42px}h3{font-size:16px}p{color:#bebfb2;line-height:1.8}header{border-bottom:1px solid #414b46;padding-bottom:20px}.eyebrow{color:#e7ba6d;letter-spacing:2px;font-size:13px}.row,.gallery{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px}.duo{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px}article{background:#202927;border:1px solid #3a4540;border-radius:10px;padding:16px;overflow:hidden}canvas{width:100%;height:auto;display:block;image-rendering:pixelated;background:#171d22}canvas.ground{background:#8c9072}img{display:block;max-width:100%;height:auto;image-rendering:pixelated;background:repeating-conic-gradient(#28302d 0% 25%,#303a35 0% 50%) 0/16px 16px}article img{width:100%;max-height:190px;object-fit:contain}.smooth img{image-rendering:auto}.controls{display:flex;gap:18px;align-items:center;flex-wrap:wrap;position:sticky;top:0;background:#151b1bf2;padding:14px 0;z-index:2}button,select{background:#d6b579;color:#18211c;border:0;border-radius:5px;padding:9px 15px;font:inherit;cursor:pointer}label{font-size:14px;color:#c9cdbd}.note{border-left:3px solid #d6b579;padding:8px 16px;background:#292d25}footer{margin-top:40px;color:#949e91}code{word-break:break-word}a{color:#edca8f}@media(max-width:700px){main{padding:20px 14px}.row,.gallery,.duo{grid-template-columns:1fr}h1{font-size:26px}}
</style><main><header><div class="eyebrow">HUMAN LAST RAMPART · K-088</div><h1>총구의 한순간, 발밑의 무게</h1><p>총구 불꽃 2방향 · 상승 연기 · 접지 그림자 4종 · 토벽 흙자락 2종<br>사람 약 73 px 기준 / 효과 원본 셀 96×128 / 게임용 3배 최근접 확대</p></header>
<div class="controls"><button id="play">일시 정지</button><button id="restart">처음부터</button><label>재생 속도 <select id="speed"><option value="1">실제 속도</option><option value="0.25">느리게 · ¼</option></select></label><label>바탕 <select id="bg"><option value="#171d22">어두운 바탕</option><option value="#9a9b78">밝은 들판</option></select></label><span id="clock"></span></div>
<h2>01 · 현재 사수 위에 겹친 발사 효과</h2><p>왼쪽은 현재 게임 시트 6프레임 그대로입니다. 가운데와 오른쪽은 같은 시트의 겨누기 프레임에 새 효과를 붙였습니다. 0.12초 불꽃 뒤에 연기가 올라갑니다.</p>
<div class="row"><article><h3>현재 · 원본 6프레임</h3><canvas id="before" width="240" height="150"></canvas></article><article><h3>새 효과 · 오른쪽</h3><canvas id="right" width="240" height="150"></canvas></article><article><h3>새 효과 · 카메라 반대쪽</h3><canvas id="back" width="240" height="150"></canvas></article></div>
<p class="note">현재 발사 프레임에는 기존 불꽃이 들어 있습니다. 새 효과 합성에는 불꽃 없는 겨누기 프레임을 썼습니다. 반대쪽 효과는 같은 사선 사수에 붙인 방향 견본입니다. 실제 총구 소켓·방향·가림 순서는 게임에서 연결하고 확인해야 합니다.</p>
<h2>02 · 효과만 보기</h2><div class="row"><article><h3>오른쪽 · 발화 → 화염 → 퍼프</h3><canvas id="soloRight" width="192" height="128"></canvas></article><article><h3>반대쪽 · 짧게 위로 방출</h3><canvas id="soloBack" width="192" height="128"></canvas></article><article><h3>연기 · 4프레임 상승과 소멸</h3><canvas id="soloSmoke" width="192" height="128"></canvas></article></div>
<h2>03 · 접지 비교</h2><p>같은 높이와 위치를 유지한 도식 합성입니다. 게임 화면 캡처는 아닙니다. 접지 그림자는 매끈하게, 흙자락은 픽셀 그대로 표시합니다.</p><div class="duo"><article><h3>병사 · 그림자 없음 / 있음</h3><canvas class="ground" id="soldierGround" width="400" height="160"></canvas></article><article><h3>토벽 · 바닥 없음 / 그림자와 흙자락</h3><canvas class="ground" id="wallGround" width="400" height="160"></canvas></article></div>
<h2>04 · 납품 원본</h2><div class="gallery">__GALLERY__</div><h2>05 · 기존 바닥 표현과 나란히</h2><div class="duo"><article><h3>K-068 · 기존 흙 얼룩</h3><img alt="기존 K-068 흙 얼룩" src="__OLD_DIRT__"></article><article><h3>K-088 · 새 토벽 흙자락</h3><img alt="새 토벽 5미터 흙자락" src="__NEW_DIRT__"></article></div>
<p class="note">불꽃: 투명 바탕, 알파 가중 가산 합성. 연기·흙: 일반 알파 합성. 그림자: 검정 RGB와 연속 알파를 사용한 알파 가중 곱셈. 그림자에 알파를 무시한 순수 곱셈을 쓰면 검정 사각형이 됩니다.</p>
<footer>모든 이미지를 내장했습니다. 서버와 외부 연결 없이 열립니다. Unity 실행·실제 배치 캡처는 미수행입니다.</footer></main>
<script>
const sources=__PAYLOAD__, imgs={}, state={running:true,time:0,ready:false,draws:0};
window.reviewState=state;
let previous=0;
function ctx(id){const c=document.getElementById(id),g=c.getContext('2d');g.clearRect(0,0,c.width,c.height);g.imageSmoothingEnabled=false;return g;}
function phase(t,d){for(let i=0;i<d.length;i++){if(t<d[i])return i;t-=d[i];}return -1;}
function effect(g,id,t,x,y,scale=1,add=true){const ds=id==='smoke_up'?[70,90,110,130]:[35,45,40],f=phase(t,ds);if(f<0)return;g.save();g.globalCompositeOperation=add?'lighter':'source-over';g.drawImage(imgs[id],f*96,0,96,128,x-48*scale,y-80*scale,96*scale,128*scale);g.restore();}
function gun(g,f,x,y,s){g.drawImage(imgs.gunner,f*288,0,288,384,x,y,96*s,128*s);}
function draw(){const t=state.time%1200;state.draws++;document.getElementById('clock').textContent=Math.floor(t)+' ms';let g=ctx('before');gun(g,Math.min(5,Math.floor(t/160)),64,0,1.15);
for(const [id,key] of [['right','muzzle_flash_right'],['back','muzzle_flash_back']]){g=ctx(id);gun(g,1,64,0,1.15);effect(g,key,t,64+87*1.15,59*1.15,1.15);if(t>=80)effect(g,'smoke_up',t-80,64+87*1.15,59*1.15,1.15,false);}
for(const [id,key] of [['soloRight','muzzle_flash_right'],['soloBack','muzzle_flash_back'],['soloSmoke','smoke_up']]){g=ctx(id);effect(g,key,key==='smoke_up'?t:t,72,92,2,key!=='smoke_up');}
g=ctx('soldierGround');g.imageSmoothingEnabled=true;g.drawImage(imgs.contact_shadow_small,247,126,62,24);g.imageSmoothingEnabled=false;gun(g,0,48,0,1.15);gun(g,0,220,0,1.15);
g=ctx('wallGround');g.imageSmoothingEnabled=true;g.drawImage(imgs.contact_shadow_wall_5m,208,101,184,34);g.imageSmoothingEnabled=false;g.drawImage(imgs.dirt_skirt_5m,208,93,184,42);g.drawImage(imgs.wall,5,42,190,95);g.drawImage(imgs.wall,205,42,190,95);}
document.getElementById('play').onclick=()=>{state.running=!state.running;document.getElementById('play').textContent=state.running?'일시 정지':'계속 재생';};document.getElementById('restart').onclick=()=>{state.time=0;draw();};document.getElementById('bg').onchange=e=>document.querySelectorAll('canvas:not(.ground)').forEach(c=>c.style.background=e.target.value);
Promise.all(Object.entries(sources).map(([key,url])=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{imgs[key]=im;resolve();};im.onerror=reject;im.src=url;}))).then(()=>{state.ready=true;draw();requestAnimationFrame(function tick(ts){if(previous&&state.running)state.time+=(ts-previous)*Number(document.getElementById('speed').value);previous=ts;draw();requestAnimationFrame(tick);});}).catch(()=>{document.getElementById('clock').textContent='이미지 로드 실패';});
window.reviewSeek=ms=>{state.time=ms;draw();};
</script></html>'''
html=html.replace('__GALLERY__',gallery).replace('__PAYLOAD__',payload).replace('__OLD_DIRT__',images['old_dirt']).replace('__NEW_DIRT__',images['dirt_skirt_5m'])
target=ROOT/'_HTML/5_그림_전장/codex_K-088_총구불꽃그림자_v1.html'
target.write_text(html,encoding='utf-8')
print('생성 완료:',len(assets),'종 /',target)
