from pathlib import Path
from PIL import Image, ImageFilter, ImageDraw
import numpy as np
import json, base64, hashlib, io
from collections import deque

P = Path(__file__).resolve().parent
R = P.parents[2]
NN = Image.Resampling.NEAREST
PAGE = R / '_HTML/6_그림_화면/codex_K-122_등불기둥_깃발_v1.html'
DAY = R / 'Docs/References/courtyard_20261023/259_w27_day_2_courtyard_overview.png'
PAL = ['#10151e', '#172334', '#29394d', '#3f5265', '#252d38', '#47515d', '#6e7880', '#45474a', '#777970', '#aca99a', '#73552e', '#a47836', '#d1aa58', '#efcf85', '#ab421b', '#e67524', '#ffb640', '#fff0a5']
COLORS = np.array([tuple(bytes.fromhex(c[1:])) for c in PAL], dtype=np.int32)

def dump(name, obj):
    (P / name).write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding='utf-8')

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def clean(im):
    a = np.array(im.convert('RGBA')).copy()
    mask = a[:, :, 3] >= 160
    # 도트 격자로 줄인 뒤 본체와 연결되지 않은 잡점을 제거한다.
    seen = set()
    groups = []
    for y, x in zip(*np.where(mask)):
        if (y, x) in seen:
            continue
        q, group = deque([(y, x)]), []
        seen.add((y, x))
        while q:
            yy, xx = q.popleft()
            group.append((yy, xx))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = yy + dy, xx + dx
                    if 0 <= ny < im.height and 0 <= nx < im.width and mask[ny, nx] and (ny, nx) not in seen:
                        seen.add((ny, nx))
                        q.append((ny, nx))
        groups.append(group)
    for group in groups:
        if len(group) < 3:
            for y, x in group:
                mask[y, x] = False
    rgb = a[:, :, :3].astype(np.int32)
    near = ((rgb[:, :, None, :] - COLORS[None, None, :, :]) ** 2).sum(axis=3).argmin(axis=2)
    a[:, :, :3] = COLORS[near]
    a[:, :, 3] = np.where(mask, 255, 0)
    a[~mask] = 0
    return Image.fromarray(a)

def extract(key, n, height, pole=True):
    im = Image.open(P / 'source' / (key + '_generated.png')).convert('RGBA')
    frames, boxes = [], []
    for i in range(n):
        cell = im.crop((round(i * im.width / n), 0, round((i + 1) * im.width / n), im.height))
        # 생성 원안 가장자리의 미세 반투명 노이즈만 정리하고 실제 도형은 보존한다.
        mask = cell.getchannel('A').point(lambda a: 255 if a >= 200 else 0).filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MaxFilter(5))
        box = mask.getbbox()
        cell.putalpha(mask)
        crop = cell.crop(box)
        w = round(crop.width * height / crop.height)
        small = clean(crop.resize((w, height), NN))
        a = np.array(small)
        if pole:
            yy, xx = np.where(a[-max(2, height // 8):, :, 3] > 0)
            axis = (int(xx.min()) + int(xx.max()) + 1) / 2
        else:
            axis = w / 2
        out = Image.new('RGBA', (96, 128))
        out.alpha_composite(small, (round(48 - axis), 120 - height))
        frames.append(out)
        boxes.append(list(box))
    return frames, boxes

def lock_regions(frames, ranges, pole_x=None):
    # 움직이지 않는 지붕·기둥·받침·걸쇠는 첫 프레임으로 등록을 고정한다.
    for frame in frames[1:]:
        for y0, y1 in ranges:
            frame.paste(frames[0].crop((0, y0, 96, y1)), (0, y0))
        if pole_x:
            x0, x1, y0, y1 = pole_x
            frame.paste(frames[0].crop((x0, y0, x1, y1)), (x0, y0))

def sheet(frames):
    out = Image.new('RGBA', (96 * len(frames), 128))
    for i, im in enumerate(frames):
        out.paste(im, (i * 96, 0))
    return out

assets = []
def deliver(key, label, frames, height, fps, order, note, static_ranges, pole_x=None):
    native = sheet(frames)
    native.save(P / (key + '_native.png'))
    native.resize((native.width * 3, 384), NN).save(P / (key + '_game.png'))
    native.resize((native.width * 4, 512), NN).save(P / (key + '_4x.png'))
    assets.append(dict(id=key, label=label, native_sheet=key+'_native.png', game_sheet=key+'_game.png', preview_4x=key+'_4x.png', frame_count=len(frames), frame_order=order, fps=fps, loop=True if fps else False, native_cell=[96,128], game_cell=[288,384], pivot_top_left_native=[48,120], pivot_top_left_game=[144,360], unity_pivot_normalized=[.5,.0625], visible_height_native=height, approximate_world_height_m=round(height/32,4), native_pixels_per_unit=32, game_pixels_per_unit=96, note=note, static_y_ranges=static_ranges, static_pole_rectangle=pole_x))

hash_paths = [DAY, R/'Game/Assets/_Project/Combat/Battle/BattleRoad.cs', R/'Game/Assets/_Project/Art/Battle/Resources/BattleArt/Effects/torch_flame.png', R/'Docs/References/codex_hero_octopath_review_v6/generated.png']
dump('reference_hashes.json', {str(p.relative_to(R)):digest(p) for p in hash_paths})
lamp, lampboxes = extract('lamp', 7, 51)
lock_regions(lamp, [(69,81),(94,120)])
day, night = lamp[:1], lamp[1:]
deliver('lamp_post_day', '낮 · 꺼진 철제 등롱', day, 51, 0, ['소등'], '화염과 바닥빛 없음. 작은 석재 받침 기준.', [(69,81),(94,120)])
deliver('lamp_post_night', '밤 · 등롱 속 불꽃', night, 51, 10, ['불꽃 1','불꽃 2','불꽃 3','불꽃 4','불꽃 5','불꽃 6'], '불꽃과 유리 안쪽만 변화. 외부 후광과 부유 불티 없음.', [(69,81),(94,120)])
banner, bannerboxes = extract('banner', 4, 58)
lock_regions(banner, [(62,75),(104,120)], (47,50,75,104))
deliver('beacon_banner', '문 앞 · 등불지기 깃발', banner, 58, 4, ['오른쪽 주름','중립 주름 A','왼쪽 주름','중립 주름 B'], '철제 깃대·가로대와 석재 받침. 남색 천과 황동색 자수.', [(62,75),(104,120)], [47,50,75,104])
pennant, pennantboxes = extract('pennant', 4, 32)
lock_regions(pennant, [(88,92),(108,120)], (47,50,92,108))
deliver('beacon_pennant', '회랑 · 작은 삼각기', pennant, 32, 4, ['처진 끝','높아진 끝','낮아진 끝','돌아오는 끝'], '오른쪽을 향한 삼각기. 깃대 축은 x=48이며 천은 오른쪽으로 펼쳐짐.', [(88,92),(108,120)], [47,50,92,108])
wall, wallboxes = extract('wall', 4, 40, False)
lock_regions(wall, [(80,89)])
deliver('beacon_wall_banner', '건물 · 벽걸이 깃발', wall, 40, 4, ['오른쪽 주름','중립 주름 A','왼쪽 주름','중립 주름 B'], '바닥 깃대 없음. 요청한 밑단 피벗은 가상 기준점. 실제 벽 고정점은 원본 (48,84), 게임 (144,252).', [(80,89)])

# 문장은 생성 원안에서 잘라 동일한 도트 격자·제한 색상으로 정규화한다.
em = Image.open(P/'source/emblem_generated.png').convert('RGBA')
mask = em.getchannel('A').point(lambda a:255 if a>=200 else 0).filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MaxFilter(5))
em.putalpha(mask)
em = em.crop(mask.getbbox())
ew = round(em.width * 56 / em.height)
em = clean(em.resize((ew,56),NN))
icon = Image.new('RGBA',(64,64))
icon.alpha_composite(em,((64-ew)//2,4))
icon.save(P/'beacon_emblem_64.png')
icon.resize((256,256),NN).save(P/'beacon_emblem_256.png')

# 생성된 별도 바닥빛의 불투명도를 제한하고 가장자리를 매끈하게 정규화한다.
g = Image.open(P/'source/glow_generated.png').convert('RGBA').resize((128,128),Image.Resampling.LANCZOS)
ga = np.array(g.getchannel('A').filter(ImageFilter.GaussianBlur(3)),dtype=float)
yy,xx = np.mgrid[0:128,0:128]
rad = np.sqrt(((xx-63.5)/52)**2+((yy-63.5)/52)**2)
fall = np.clip(1-rad**2,0,1)**2
alpha = np.rint(46*(ga/max(ga.max(),1))*fall).astype('uint8')
glowarr = np.zeros((128,128,4),dtype='uint8')
glowarr[:,:,:3]=[239,177,88]
glowarr[:,:,3]=alpha
glowarr[alpha==0]=0
glow=Image.fromarray(glowarr)
glow.save(P/'lamp_ground_glow_128.png')
glow.resize((512,512),NN).save(P/'lamp_ground_glow_4x.png')

manifest = dict(card='K-122', version=1, status='에셋 납품 · 게임 연결 전', generation='내장 이미지 생성 도구, 원안은 source/에 보존', direction='D-080 최초 생성본과 D-081 원본 칸·최근접 3배 계약', coordinates='픽셀 원점은 좌상단. y=120은 마지막 불투명 행 119 바로 아래 경계.', pixel_filter='Point', wrap_mode='Clamp', mipmaps=False, texture_compression='None', alpha='본체와 문장 0/255. 바닥빛만 0~46의 연속 알파.', palette=PAL, assets=assets, icons=[dict(file='beacon_emblem_64.png',size=[64,64],pivot=[32,32]),dict(file='beacon_emblem_256.png',size=[256,256],pivot=[128,128],scale='64px 아이콘의 최근접 4배')], decals=[dict(file='lamp_ground_glow_128.png',size=[128,128],pivot=[64,64],unity_pivot_normalized=[.5,.5],max_alpha=46,world_diameter_m=3.6,note='바닥 평면에 눕혀 사용. 낮에는 숨김. 불꽃과 별도 렌더링. 원형 원안이며 카메라 투영은 게임에서 처리.')], existing_game=dict(geometry='Game/Assets/_Project/Combat/Battle/BattleRoad.cs',lamp='상자 메시와 별도 torch_flame.png. 기존 등불 기둥 시트 없음.',banner='가로대·남색 천·금빛 마름모를 상자 메시로 생성. 기존 깃발 시트 없음.',height_difference='현 구현 LampPostHeight=0.7m, 깃대 1.5m. 이번 카드는 등불 51/32m, 깃발 58/32m 지시를 따름. Claude가 연결 시 크기 조정.'), integration_notes=['기존 메시 기둥·그릇·깃발·문양과 기존 횃불 화염을 새 시트와 중복 표시하지 않기.', '26개 등불과 문 앞 깃발 2개의 기존 배치 좌표는 Claude가 유지.', '게임용 3배 시트는 96 PPU로 사용하면 원본 32 PPU와 같은 크기. 32 PPU를 그대로 쓰면 3배 커짐.', '벽걸이 밑단 피벗과 벽 장착점은 용도에 맞게 구분.', '빛 데칼은 불꽃 시트에 합치지 않았으며 실제 광원/그림자를 생성하지 않음.'], source_crop_boxes=dict(lamp=lampboxes,banner=bannerboxes,pennant=pennantboxes,wall=wallboxes))
dump('manifest.json',manifest)

# 실제 캡처의 보이는 등불 여섯 자리와 문 앞 두 자리에만 납품 에셋을 합성한다.
before = Image.open(DAY).convert('RGBA')
mock = before.copy()
lamp_spots=[(645,665,.72),(704,680,.80),(729,658,.76),(772,697,.90),(793,672,.82),(863,686,.90)]
def place(scene,frame,x,y,k):
    sprite=frame.resize((round(96*k),round(128*k)),NN)
    scene.alpha_composite(sprite,(round(x-48*k),round(y-120*k)))
for x,y,k in lamp_spots:
    place(mock,day[0],x,y,k)
for x,y in [(921,735),(1044,716)]:
    place(mock,banner[1],x,y,1)
mock.save(P/'mockup_courtyard_day.png')
cropbox=(595,615,1090,752)
before.crop(cropbox).resize((1485,411),NN).save(P/'current_road_crop_3x.png')
mock.crop(cropbox).resize((1485,411),NN).save(P/'mockup_road_crop_3x.png')
before.crop((846,649,881,695)).resize((140,184),NN).save(P/'current_lamp_crop_4x.png')
before.crop((906,671,939,738)).resize((132,268),NN).save(P/'current_banner_crop_4x.png')

board=Image.new('RGBA',(576,128))
for i,frame in enumerate([day[0],night[1],banner[1],pennant[1],wall[1],Image.open(R/'Docs/References/codex_k070_v1/hero_native_96x128.png')]):
    board.alpha_composite(frame,(i*96,0))
board.save(P/'lineup_native.png')
board.resize((2304,512),NN).save(P/'lineup_4x.png')

def uri(p):
    return 'data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode()

def pic(file, alt, cls=''):
    return '<img class="'+cls+'" src="'+uri(P/file)+'" alt="'+alt+'">'

cards=''
for asset in assets:
    key=asset['id']
    old=''
    if key.startswith('lamp'):
        old='<div class="old">'+pic('current_lamp_crop_4x.png','현재 게임의 상자 기둥')+'<small>현재 게임 캡처<br>상자 기둥·그릇</small></div>'
    elif key=='beacon_banner':
        old='<div class="old">'+pic('current_banner_crop_4x.png','현재 게임의 남색 깃발')+'<small>현재 게임 캡처<br>남색 천·작은 마름모</small></div>'
    else:
        old='<div class="old empty"><span>신규 변형</span><small>기존 게임 시트 없음</small></div>'
    cards+='<article><h3>'+asset['label']+'</h3><div class="assetpair">'+old+'<div><div class="sprite" data-count="'+str(asset['frame_count'])+'" data-fps="'+str(asset['fps'])+'" data-frame="0">'+pic(key+'_native.png',asset['label']+' 애니메이션 시트','strip')+'<i></i></div><small class="counter">1 / '+str(asset['frame_count'])+'</small></div></div><p>'+asset['note']+'</p><small>실루엣 '+str(asset['visible_height_native'])+'px · '+str(asset['frame_count'])+'프레임'+(' · '+str(asset['fps'])+'fps' if asset['fps'] else '')+'</small></article>'

html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-122 · 등불기둥과 등불지기 깃발</title>
<style>
:root{color-scheme:dark;--gold:#d1aa58;--ink:#0e1520;--muted:#a7b0bc}*{box-sizing:border-box}body{margin:0;background:var(--ink);color:#ece8dd;font-family:"Malgun Gothic",sans-serif;line-height:1.65}main{max-width:1420px;margin:auto;padding:42px 30px 70px}header{border-bottom:1px solid #59604f;padding-bottom:28px;margin-bottom:30px}h1{font-size:34px;line-height:1.3;margin:10px 0 16px}h2{font-size:23px;margin-top:36px}h3{font-size:17px;margin:0 0 12px}p{margin:9px 0;color:#c3c8cf}.eyebrow{color:var(--gold);letter-spacing:.12em;font-size:13px}.tags{display:flex;gap:10px;flex-wrap:wrap}.tags span{border:1px solid #47505c;border-radius:4px;padding:4px 10px;font-size:12px}.toolbar{display:flex;align-items:center;gap:10px;flex-wrap:wrap;margin:16px 0}button,select{font:inherit;color:#ede6d2;background:#273446;border:1px solid #6c725f;border-radius:4px;padding:7px 14px;cursor:pointer}button:hover{border-color:var(--gold)}button.active{background:#69542a}.hero{width:100%;display:block;border:1px solid #45505a}.compare{display:grid;grid-template-columns:1fr 1fr;gap:16px}.compare img{width:100%;display:block;image-rendering:pixelated}.caption{color:var(--muted);font-size:13px;margin-top:7px}.gallery{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:18px}.gallery article{padding:20px;background:#182230;border:1px solid #364350;border-radius:7px}.assetpair{display:flex;justify-content:center;align-items:center;gap:8px;min-height:260px}.sprite{position:relative;width:192px;height:256px;overflow:hidden;background:repeating-conic-gradient(#27313d 0% 25%,#222b36 0% 50%) 0/20px 20px}.strip{position:absolute;top:0;left:0;height:256px;max-width:none;image-rendering:pixelated}.sprite i{position:absolute;top:240px;left:0;right:0;border-top:1px dashed #b78c43;opacity:.55;pointer-events:none}.old{width:100px;flex-shrink:0;text-align:center;display:flex;flex-direction:column;align-items:center;gap:8px}.old img{max-height:210px;max-width:100px;object-fit:contain;image-rendering:pixelated}.old small,.counter{font-size:11px;color:#b9c0c9;display:block;text-align:center}.empty{font-size:12px;color:#d1aa58}.gallery article p{font-size:13px;min-height:64px}.gallery article>small{color:var(--gold);font-size:12px}.lineup{overflow:auto;border:1px solid #364350;background:#28313b}.lineup img{height:384px;width:1728px;display:block;image-rendering:pixelated}.icons{display:flex;gap:32px;align-items:center;flex-wrap:wrap;padding:24px;background:#202b39;border:1px solid #364350}.icons img{image-rendering:pixelated}.icons figure{margin:0;text-align:center}.icons .big{width:180px;height:180px}.icons .small{width:64px;height:64px}.icons .glow{width:160px;height:160px}.icons figcaption{font-size:12px;color:#b8c1ca;margin-top:10px}.note{border-left:3px solid var(--gold);padding:10px 18px;background:#202a37;font-size:13px}table{border-collapse:collapse;width:100%;font-size:13px}td,th{text-align:left;padding:11px;border-bottom:1px solid #3a4655}th{color:var(--gold)}footer{font-size:12px;color:var(--muted);margin-top:34px}a{color:#e8c878}@media(max-width:1150px){.gallery{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:720px){main{padding:24px 14px}.gallery,.compare{grid-template-columns:1fr}h1{font-size:27px}.lineup img{height:256px;width:1152px}.assetpair{gap:18px}}
</style><main><header><div class="eyebrow">HUMAN LAST RAMPART · K-122</div><h1>길을 밝히는 등롱,<br>봉화선을 잇는 깃발</h1><p>연철 기둥과 작은 돌받침. 남색 천 위에 등대와 빛줄기를 새긴 황실 등불지기의 문장.</p><div class="tags"><span>에셋 납품 · 게임 연결 전</span><span>원본 96×128 · 게임 3배</span><span>등불 불꽃 6프레임</span><span>깃발 3종 · 각 4프레임</span></div></header>
<h2>실제 마당 위 배치 목업</h2><p>지정한 실제 낮 캡처를 그대로 바탕으로 썼습니다. 화면에 드러난 등불 여섯 자리와 문 앞 깃발 두 자리에 합성했습니다.</p><div class="toolbar"><button id="showNew" class="active">새 에셋 목업</button><button id="showOld">현재 게임 캡처</button><span class="caption" id="mockLabel">새 에셋 목업 · 실제 게임 적용 화면 아님</span></div>'''
html+='<img id="overview" class="hero" src="'+uri(P/'mockup_courtyard_day.png')+'" alt="실제 마당 캡처에 합성한 등불과 깃발 목업">'
html+='<p class="caption">원본: courtyard_20261023/259_w27_day_2_courtyard_overview.png · 카메라·성벽·건물·바닥 색상 보존. 기존 메시가 밑으로 일부 보일 수 있는 단순 합성입니다.</p><div class="compare"><div>'+pic('current_road_crop_3x.png','현재 게임 길 확대')+'<div class="caption">현재 · 단순 기둥과 문양 없는 남색 천</div></div><div>'+pic('mockup_road_crop_3x.png','새 등롱과 깃발 배치 확대')+'<div class="caption">목업 · 등롱의 지붕과 돌받침, 황동 문장</div></div></div><p class="note">현 구현의 기둥은 0.7m, 이번 카드 지시는 약 1.6m입니다. 새 실루엣은 51px(약 1.59m), 문 앞 깃발은 58px(약 1.81m)로 제작했습니다. 목업은 배치 참고이며 최종 화면 크기·가림·야간 조명은 Claude 연결 후 실제 게임에서 확인해야 합니다.</p>'
html+='<h2>작은 크기에서 읽히는 도트</h2><p>오른쪽 끝은 승인된 K-070 주인공 크기 비교용입니다. 바닥선과 축을 고정하고 재질별 명암을 제한했습니다.</p><div class="lineup">'+pic('lineup_native.png','낮 등불, 밤 등불, 문 앞 깃발, 삼각기, 벽걸이, 주인공 크기 비교')+'</div>'
html+='<h2>동작과 현재 모습 비교</h2><div class="toolbar"><button id="play">일시정지</button><button id="step">한 프레임씩</button><label>배경 <select id="backdrop"><option value="grid">투명 격자</option><option value="day">낮의 돌바닥</option><option value="night">짙은 밤</option></select></label><span class="caption">점선은 원본 y=120 기준선 · 각 칸 2배 표시</span></div><div class="gallery">'+cards+'</div>'
html+='<h2>문장과 별도 바닥빛</h2><div class="icons"><figure>'+pic('beacon_emblem_256.png','등대와 여섯 빛줄기 문장 256px','big')+'<figcaption>문장 원안 · 256×256</figcaption></figure><figure>'+pic('beacon_emblem_64.png','작은 등대 문장 64px','small')+'<figcaption>64×64 실제 크기</figcaption></figure><figure>'+pic('lamp_ground_glow_128.png','절제된 원형 바닥빛','glow')+'<figcaption>별도 바닥빛 · 128×128<br>최대 불투명도 약 18%</figcaption></figure><p>등대 지붕·등실·두 단 받침과 빛줄기.<br>깃발은 같은 등대 문장을 황동색 자수로 단순화했습니다.<br>바닥빛은 원형 텍스처를 바닥 평면에 눕혀 사용합니다.</p></div>'
html+='<h2>연결 기준</h2><table><tr><th>항목</th><th>원본</th><th>게임용</th></tr><tr><td>한 칸</td><td>96×128</td><td>288×384 · 최근접 3배</td></tr><tr><td>밑단 피벗 · 좌상단 기준</td><td>(48,120)</td><td>(144,360)</td></tr><tr><td>월드 크기 유지</td><td>32px = 1m</td><td>96px = 1m</td></tr><tr><td>벽걸이 실제 장착점</td><td>(48,84)</td><td>(144,252)</td></tr><tr><td>프레임 순서</td><td colspan="2">왼쪽 → 오른쪽 · manifest.json에 동작명 기록</td></tr></table><p class="note">현재 기둥과 깃발은 코드가 만든 메시이므로 비교할 기존 스프라이트 시트가 없습니다. 위 비교는 실제 캡처입니다. Claude는 기존 메시와 별도 횃불 불꽃을 중복 표시하지 않고, 기존 26개 등불·문 앞 2개 깃발의 자리에 연결해야 합니다.</p><footer>모든 미리보기 이미지는 이 HTML 안에 내장했습니다. 서버·네트워크 없이 열립니다. Unity 실행·실제 적용 검증·Git 쓰기는 수행하지 않았습니다.</footer>'
html+='<script>const before='+json.dumps(uri(DAY))+';const after='+json.dumps(uri(P/'mockup_courtyard_day.png'))+';'
html+='''let playing=true,stepIndex=0;const sprites=[...document.querySelectorAll('.sprite')];function paint(s,i){const n=+s.dataset.count;s.dataset.frame=i%n;s.querySelector('img').style.transform=`translateX(${-192*(i%n)}px)`;s.parentElement.querySelector('.counter').textContent=`${i%n+1} / ${n}`}setInterval(()=>{if(!playing)return;const t=performance.now()/1000;sprites.forEach(s=>paint(s,Math.floor(t*(+s.dataset.fps))))},50);document.querySelector('#play').onclick=()=>{playing=!playing;document.querySelector('#play').textContent=playing?'일시정지':'재생'};document.querySelector('#step').onclick=()=>{playing=false;document.querySelector('#play').textContent='재생';stepIndex++;sprites.forEach(s=>paint(s,stepIndex))};document.querySelector('#backdrop').onchange=e=>sprites.forEach(s=>s.style.background={day:'#756b58',night:'#060b16',grid:''}[e.target.value]);function show(old){document.querySelector('#overview').src=old?before:after;document.querySelector('#mockLabel').textContent=old?'현재 게임 캡처 · 원본':'새 에셋 목업 · 실제 게임 적용 화면 아님';document.querySelector('#showOld').classList.toggle('active',old);document.querySelector('#showNew').classList.toggle('active',!old)}document.querySelector('#showNew').onclick=()=>show(false);document.querySelector('#showOld').onclick=()=>show(true);</script></main></html>'''
PAGE.parent.mkdir(parents=True,exist_ok=True)
PAGE.write_text(html,encoding='utf-8')
print('제작 완료:',len(assets),'종 시트, 문장 2종, 바닥빛, 목업, 내장형 검토 페이지')
