from PIL import Image, ImageDraw, ImageFont, ImageEnhance
from pathlib import Path
import json, base64, hashlib

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
HTML = ROOT / '_HTML/6_그림_화면/codex_K-100_전투단계경고UI_v1.html'
NN = Image.Resampling.NEAREST
INK = '#171512'
PANEL = '#302219'
BRASS = '#d5b16b'
LIGHT = '#eee4cf'
COLORS = ['#d5b16b', '#e2be77', '#e99a78', '#d98680']
PHASES = ['선봉', '본대', '공성', '최후 돌격']
KEYS = ['vanguard', 'main', 'siege', 'final']
assets = []

def save(name, im, **meta):
    im = im.convert('RGBA')
    im.save(OUT / name)
    assets.append(dict(file=name, size=list(im.size), pivot_px=[im.width/2, im.height/2], **meta))
    if meta.get('role') != 'mockup':
        p = OUT / 'preview_4x' / name
        p.parent.mkdir(exist_ok=True)
        im.resize((im.width*4, im.height*4), NN).save(p)
    return im

def emblem(kind, size, color=BRASS):
    im = Image.new('RGBA', (32,32))
    d = ImageDraw.Draw(im)
    if kind == 0:
        d.rectangle((8,5,10,27), fill=INK)
        d.line((9,6,9,26), fill=LIGHT)
        d.polygon([(11,6),(25,6),(21,12),(25,17),(11,17)], fill=INK)
        d.polygon([(11,7),(23,7),(19,12),(23,15),(11,15)], fill=color)
        d.line((12,8,19,8), fill=LIGHT)
        d.rectangle((6,27,14,28), fill=color)
    elif kind == 1:
        for x,y in [(7,11),(16,7),(25,11)]:
            d.rectangle((x-3,y-3,x+2,y+2), fill=INK)
            d.rectangle((x-2,y-2,x+1,y+1), fill=LIGHT)
            d.polygon([(x-3,y+4),(x+2,y+4),(x+4,y+11),(x-5,y+11)], fill=INK)
            d.rectangle((x-2,y+4,x+1,y+9), fill=color)
            d.line((x-3,y+13,x-1,y+11), fill=color, width=2)
            d.line((x+1,y+11,x+3,y+13), fill=color, width=2)
    elif kind == 2:
        d.line((8,27,14,5), fill=INK, width=5)
        d.line((20,27,26,5), fill=INK, width=5)
        for x in [8,20]: d.line((x,27,x+6,5), fill=color, width=3)
        for y in [9,15,21,26]:
            x=8+round((27-y)*6/22)
            d.line((x,y,x+12,y), fill=INK, width=4)
            d.line((x,y-1,x+12,y-1), fill=LIGHT, width=2)
    else:
        d.polygon([(9,12),(4,10),(3,5),(7,7),(11,7),(15,9),(21,7),(25,7),(29,4),(28,11),(23,14)], fill=INK)
        d.polygon([(9,11),(5,9),(5,7),(9,9),(14,10),(20,9),(25,9),(27,7),(26,11),(22,13)], fill=color)
        d.polygon([(10,11),(21,11),(24,15),(23,22),(20,23),(20,27),(12,27),(12,23),(8,21),(8,15)], fill=INK)
        d.polygon([(11,12),(20,12),(22,15),(21,21),(18,22),(18,25),(14,25),(14,22),(10,20),(10,15)], fill=LIGHT)
        d.rectangle((11,16,14,19), fill=INK)
        d.rectangle((18,16,21,19), fill=INK)
        d.polygon([(16,20),(14,22),(17,22)], fill=INK)
        d.line((16,24,16,26), fill=INK)
    return im.resize((size,size), NN)

def warning(level, direction, size, frame):
    im=Image.new('RGBA',(32,32));d=ImageDraw.Draw(im)
    color=COLORS[level]
    d.polygon([(5,9),(26,9),(29,12),(29,26),(26,29),(5,29),(2,26),(2,12)], fill=INK)
    d.rectangle((4,11,27,27),fill=PANEL)
    border=[ '#8d7449', BRASS, LIGHT, BRASS ][frame]
    d.line((5,10,26,10,28,12,28,26,26,28,5,28,3,26,3,12,5,10),fill=border)
    # 방향 화살표만 돌려 인원·사다리가 항상 똑바로 보이게 한다.
    arrow=Image.new('RGBA',(32,32));a=ImageDraw.Draw(arrow)
    a.polygon([(16,1),(23,6),(19,6),(19,7),(12,7),(12,6),(8,6)],fill=INK)
    a.polygon([(16,2),(21,5),(17,5),(17,6),(14,6),(14,5),(11,5)],fill=color)
    arrow=arrow.rotate(-90*direction, resample=NN)
    symbol=emblem(2 if level==2 else 1,24,color)
    if level==0:
        symbol=Image.new('RGBA',(24,24));s=ImageDraw.Draw(symbol)
        for x,y in [(8,7),(16,10)]:
            s.rectangle((x-2,y-2,x+1,y+1),fill=LIGHT)
            s.rectangle((x-3,y+3,x+2,y+8),fill=color)
            s.line((x-2,y+9,x-2,y+10),fill=LIGHT)
            s.line((x+1,y+9,x+1,y+10),fill=LIGHT)
    im.alpha_composite(symbol.resize((18,18),NN),(7,8))
    # 단계 수는 아래의 짧은 눈금으로도 구분한다.
    for i in range(level+1): d.rectangle((12-3*level+6*i,26,15-3*level+6*i,27), fill=color)
    im.alpha_composite(arrow)
    return im.resize((size,size),NN)

for size in (32,48):
    for level,key in enumerate(KEYS[:3]):
        for direction,compass in enumerate('NESW'):
            frames=[warning(level,direction,size,f) for f in range(4)]
            base=f'warning_{key}_{compass}_{size}'
            save(base+'.png',frames[1],role='direction_warning',level=level+1,direction=compass,cell=[size,size])
            sheet=Image.new('RGBA',(size*4,size))
            for f,im in enumerate(frames):sheet.alpha_composite(im,(size*f,0))
            save(base+'_pulse.png',sheet,role='pulse_sheet',cell=[size,size],frame_pivot_px=[size/2,size/2],frames=['dim','rise','peak','fall'],fps=4,loop=True,level=level+1,direction=compass)
    for i,key in enumerate(KEYS):save(f'emblem_{key}_{size}.png',emblem(i,size,COLORS[i]),role='phase_emblem',phase=PHASES[i])

def panel(w,h):
    im=Image.new('RGBA',(w,h));d=ImageDraw.Draw(im)
    d.polygon([(3,0),(w-4,0),(w-1,3),(w-1,h-4),(w-4,h-1),(3,h-1),(0,h-4),(0,3)],fill=INK)
    d.rectangle((2,2,w-3,h-3),fill=BRASS)
    d.rectangle((3,3,w-4,h-4),fill='#6d5133')
    d.rectangle((4,4,w-5,h-5),fill=PANEL)
    d.line((5,5,w-6,5),fill='#473324')
    for x in (6,w-7):
        for y in (6,h-7):d.point((x,y),fill=LIGHT)
    return im

banner=save('phase_banner_9slice.png',panel(96,32),role='nine_slice',border_lbrt=[10,8,10,8],min_size=[96,32],text_safe_inset_ltrb=[40,8,14,8])
track=save('timeline_track_9slice.png',panel(64,24),role='nine_slice',border_lbrt=[8,7,8,7],min_size=[64,24],content_inset_ltrb=[9,8,9,8])
for i,key in enumerate(KEYS):
    im=Image.new('RGBA',(16,8));d=ImageDraw.Draw(im)
    d.rectangle((0,0,15,7),fill='#1e1a16');d.rectangle((1,1,14,6),fill=COLORS[i]);d.line((1,1,14,1),fill=LIGHT)
    save(f'timeline_segment_{key}_9slice.png',im,role='nine_slice',border_lbrt=[2,2,2,2],phase=PHASES[i],min_size=[4,4])
im=Image.new('RGBA',(16,4),'#8d4e43');ImageDraw.Draw(im).rectangle((0,0,15,1),fill='#e99a78')
fill=save('enemy_remaining_fill_9slice.png',im,role='nine_slice',border_lbrt=[2,1,2,1],min_size=[4,2],fill_direction='left_to_right',value='(alive + unspawned) / total')

def stretch(im,size,borders):
    l,b,r,t=borders;w,h=im.size;W,H=size
    result=Image.new('RGBA',size)
    xs=[0,l,w-r,w];ys=[0,t,h-b,h];xd=[0,l,W-r,W];yd=[0,t,H-b,H]
    for row in range(3):
        for col in range(3):
            tile=im.crop((xs[col],ys[row],xs[col+1],ys[row+1]))
            result.alpha_composite(tile.resize((xd[col+1]-xd[col],yd[row+1]-yd[row]),NN),(xd[col],yd[row]))
    return result

FONT='C:/Windows/Fonts/malgun.ttf'
def text(im,xy,words,size=22,color=LIGHT):ImageDraw.Draw(im).text(xy,words,font=ImageFont.truetype(FONT,size),fill=color)
def band(im,xy,phase,words,width=1120):
    x,y=xy;im.alpha_composite(stretch(banner,(width,64),[10,8,10,8]),xy)
    im.alpha_composite(emblem(phase,48,COLORS[phase]),(x+12,y+8))
    text(im,(x+76,y+12),words,26)
    text(im,(x+width-170,y+21),'전환 5초 전',17,COLORS[phase])

source=ROOT/'Docs/References/codex_k085_v1/battle_before.png'
day=Image.open(source).convert('RGBA').resize((1920,1080),NN)
night=ImageEnhance.Brightness(day.convert('RGB')).enhance(.65).convert('RGBA')
# 기존 화면 HUD와 새 UI는 야간 밝기와 분리한다.
night.paste(day.crop((30,30,1260,112)),(30,30))
night.paste(day.crop((25,1008,350,1065)),(25,1008))
band(night,(400,128),2,'공성 병력 접근  ·  북쪽 성벽')
for level,di,x,y,label in [(2,0,1632,24,'북 · 공성'),(1,1,1848,510,'동 · 본대'),(0,3,24,510,'서 · 소규모')]:
    night.alpha_composite(warning(level,di,48,2),(x,y))
    tx=min(1780,max(12,x-26));ty=y+54
    ImageDraw.Draw(night).rectangle((tx-5,ty-2,tx+106,ty+29),fill=INK)
    text(night,(tx,ty),label,18)
text(night,(1330,1024),'야간 배치 목업 · 배경 밝기 0.65',22)
save('mockup_battle_night_1920x1080.png',night,role='mockup',brightness_world=.65,brightness_ui=1.0,source=str(source.relative_to(ROOT)).replace('\\','/'),runtime_capture=False)

mapim=Image.new('RGBA',(960,540),'#161d19');d=ImageDraw.Draw(mapim)
for x in range(30,930,30):d.line((x,70,x,430),fill='#202a22')
for y in range(70,430,30):d.line((30,y,930,y),fill='#202a22')
d.rectangle((350,190,610,380),fill='#665d4d',outline=BRASS,width=4)
d.rectangle((368,208,592,362),fill='#26352d')
text(mapim,(403,266),'성벽 지도',26)
for level,di,x,y in [(2,0,456,102),(1,1,752,258),(0,3,162,258)]:mapim.alpha_composite(warning(level,di,48,2),(x,y))
text(mapim,(516,115),'북 · 공성 예고',20)
text(mapim,(715,316),'동 · 본대',20)
text(mapim,(126,316),'서 · 소규모',20)
text(mapim,(30,20),'전투 흐름  /  공성 전환 5초 전',24)
mapim.alpha_composite(stretch(track,(900,46),[8,7,8,7]),(30,438))
for i,key in enumerate(KEYS):
    seg=Image.open(OUT/f'timeline_segment_{key}_9slice.png')
    mapim.alpha_composite(stretch(seg,(216,18),[2,2,2,2]),(42+i*219,451))
    text(mapim,(73+i*219,404),PHASES[i],19,COLORS[i])
mapim.alpha_composite(stretch(fill,(round(876*.62),6),[2,1,2,1]),(42,474))
text(mapim,(30,498),'남은 적 62% · 생존한 적 + 아직 나타나지 않은 적',19)
save('mockup_map_timeline.png',mapim,role='mockup',enemy_remaining_example=.62,phase_segment_widths='equal_visual_example_not_time_proportions',runtime_capture=False)

manifest=dict(card='K-100',version=1,palette=dict(ink=INK,panel=PANEL,brass=BRASS,text=LIGHT,phase_colors=COLORS),coordinate_system='top_left_pixels; border_lbrt = Unity left,bottom,right,top',filter='Point',compression='None',alpha='Straight RGBA',assets=assets,preview_scale=4,preview_filter='nearest_neighbor',warning_semantics='Arrow points TOWARD incoming source, not enemy travel; N up E right S down W left. Glyph remains upright.',pulse=dict(frame_order=['dim','rise','peak','fall'],duration_ms=[250]*4,frame_layout='left_to_right',anchoring='cell_center_fixed_no_translation'),timeline=dict(phases=PHASES,phase_segments='Separate nine-slice pieces; widths controlled by integration, not timing values.',enemy_remaining='(alive + unspawned) / total; crop fill to fraction, hide at zero; do not squeeze borders below minimum size.'),ui_contract_note='UI-only card: character 96x128 cells and foot baselines are not applicable. No existing game sheet is replaced.',source_reference=dict(file=str(source.relative_to(ROOT)).replace('\\','/'),sha256=hashlib.sha256(source.read_bytes()).hexdigest()))
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')

def uri(path):return 'data:image/png;base64,'+base64.b64encode(path.read_bytes()).decode()
def img(path,alt,cls=''):return f'<img class="{cls}" src="{uri(path)}" alt="{alt}">'
cards=''
for level,key in enumerate(KEYS[:3]):
    cards+=f'<article><h3>{["소규모 · 두 명","본대 · 행군 대열","공성 · 사다리"][level]}</h3><div class="samples">'
    for size in (32,48):
        for di in 'NESW':
            p=OUT/f'warning_{key}_{di}_{size}_pulse.png'
            cards+=f'<div><span class="pulse" data-size="{size}" style="width:{size}px;height:{size}px;background-image:url({uri(p)})"></span><small>{dict(N="북",E="동",S="남",W="서")[di]} · {size}점</small></div>'
    cards+='</div></article>'
emblems=''.join('<div>'+img(OUT/f'emblem_{key}_48.png',PHASES[i])+'<p>'+PHASES[i]+'</p></div>' for i,key in enumerate(KEYS))
current=ROOT/'Game/Assets/_Project/Art/UI/Resources/HudArt'
comparisons=''.join('<div>'+img(current/p,label,'ref')+f'<p>{label}</p></div>' for p,label in [('Icons/priority_climbing.png','현재 공성 우선 아이콘'),('Map/move_north.png','현재 북쪽 이동 표식'),('Command/command_band_normal.png','현재 명령 띠')])
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-100 전투 단계 경고</title><style>
*{box-sizing:border-box}body{margin:0;background:#101613;color:#eee4cf;font:16px/1.7 'Malgun Gothic',sans-serif}main{max-width:1280px;margin:auto;padding:32px 24px}h1,h2,h3{color:#d5b16b}h1{font-size:32px;margin-bottom:8px}h2{margin-top:0}section{background:#1b251f;border:1px solid #66553b;padding:24px;margin:24px 0;border-radius:4px}p{max-width:1000px}img{max-width:100%;height:auto;image-rendering:pixelated}.full{width:100%;display:block}.grid{display:grid;grid-template-columns:1fr 1fr;gap:24px}.samples,.emblems,.refs{display:flex;flex-wrap:wrap;align-items:center;gap:26px}.samples>div{text-align:center;min-width:74px}article{border-bottom:1px solid #51432e;padding-bottom:24px}.pulse{display:block;margin:16px auto;background-repeat:no-repeat;image-rendering:pixelated}small{display:block;color:#bfb7a3}.ref{max-height:110px;max-width:280px;min-width:64px}button{background:#d5b16b;color:#171512;border:0;padding:12px 20px;font:inherit;cursor:pointer}code{overflow-wrap:anywhere}table{width:100%;border-collapse:collapse}td,th{text-align:left;padding:10px;border-bottom:1px solid #51432e}@media(max-width:750px){main{padding:16px}.grid{grid-template-columns:1fr}section{padding:16px}h1{font-size:26px}}
</style><main><p>K-100 · 그림 검토 · 게임 연결 전</p><h1>어느 쪽에서, 어떤 병력이 오는가</h1><p>선봉 → 본대 → 공성 → 최후 돌격. 전환 5초 전 예고용 황동 띠와 방향 경고입니다. 색뿐 아니라 두 명·행군 대열·사다리의 형태와 하단 눈금으로 위험 단계를 나눴습니다.</p><button id="pause">움직임 멈추기</button>
<section><h2>야간 전투 배치</h2><p>기존 전투 사진에 새 그림을 합성했습니다. 배경 RGB 밝기는 0.65배, HUD와 경고는 원래 밝기를 유지합니다. 실제 게임 캡처가 아닙니다. 북쪽 공성을 주 경고로, 동·서 표식은 동시 노출 비교 예시로 배치했습니다.</p>MOCK</section>
<section><h2>화면 가장자리·지도 공용 경고</h2><p>원본 크기 32점 / 48점. 화살표는 적이 오는 쪽을 가리킵니다. 사다리와 인원 그림은 회전하지 않습니다. 4칸은 어두움 → 상승 → 최고 → 하강, 초당 4칸입니다.</p>CARDS</section>
<section><h2>현재 게임 그림과 비교</h2><div class="grid"><div><h3>현재 HUD 원본</h3><div class="refs">REFS</div><p>명령 띠의 짙은 바탕, 황동 테두리와 밝은 획을 기준으로 삼았습니다. 현재 이동 표식과 새 적 접근 경고는 의미가 다릅니다.</p></div><div><h3>새 단계 상징</h3><div class="emblems">EMBLEMS</div><p>선봉 깃발 · 본대 행군 대열 · 공성 사다리 · 최후 돌격 뿔 해골. 각 32점·48점, 문자는 포함하지 않았습니다.</p></div></div></section>
<section><h2>가로로 늘어나는 부품</h2><p>위 띠와 지도 타임라인은 9-slice입니다. 단계 상징과 한국어 문구는 별도 레이어로 얹습니다. 단계 구간과 남은 적 채움은 독립된 그림입니다.</p><div class="samples">SLICES</div><table><tr><th>부품</th><th>원본</th><th>왼쪽 / 아래 / 오른쪽 / 위 경계</th></tr><tr><td>단계 띠</td><td>96 × 32</td><td>10 / 8 / 10 / 8</td></tr><tr><td>지도 타임라인 틀</td><td>64 × 24</td><td>8 / 7 / 8 / 7</td></tr><tr><td>단계 구간</td><td>16 × 8</td><td>2 / 2 / 2 / 2</td></tr><tr><td>남은 적 채움</td><td>16 × 4</td><td>2 / 1 / 2 / 1</td></tr></table></section>
<section><h2>지도 배치 예시</h2><p>각 구간의 같은 폭은 도식 예시이며 시간 비율을 확정하지 않습니다. 남은 적은 살아 있는 적과 아직 등장하지 않은 적을 합한 비율입니다. 분홍 막대는 62% 예시입니다.</p>MAP</section>
<section><h2>전달 규격</h2><p>PNG는 투명 RGBA, 도트 유지 확대, 압축 없음 기준입니다. 원본과 모든 부품의 4배 미리보기, 크기·중심점·칸 순서·9-slice 경계를 담은 명세를 함께 제공합니다. UI 전용이므로 캐릭터 발 기준선은 적용하지 않습니다. 게임 연결·예고 조건·실제 야간 가독성 확인은 Claude의 후속 작업입니다.</p></section></main><script>
let paused=matchMedia('(prefers-reduced-motion: reduce)').matches, frame=0;const button=document.querySelector('#pause');function label(){button.textContent=paused?'움직임 재생':'움직임 멈추기'}label();button.onclick=()=>{paused=!paused;label()};setInterval(()=>{if(paused)return;frame=(frame+1)%4;document.querySelectorAll('.pulse').forEach(e=>e.style.backgroundPosition=`${-frame*Number(e.dataset.size)}px 0`)},250);
</script></html>'''
slices=''.join('<div>'+img(OUT/name,label,'ref')+f'<small>{label}</small></div>' for name,label in [('phase_banner_9slice.png','단계 띠'),('timeline_track_9slice.png','타임라인 틀'),('enemy_remaining_fill_9slice.png','남은 적 채움')])
for key in ('MOCK','CARDS','REFS','EMBLEMS','SLICES','MAP'):page=page.replace(key,'@@'+key+'@@')
for key,value in dict(MOCK=img(OUT/'mockup_battle_night_1920x1080.png','야간 전투 경고 배치 목업','full'),CARDS=cards,REFS=comparisons,EMBLEMS=emblems,SLICES=slices,MAP=img(OUT/'mockup_map_timeline.png','지도 단계와 남은 적 막대 목업','full')).items():page=page.replace('@@'+key+'@@',value)
HTML.parent.mkdir(parents=True,exist_ok=True);HTML.write_text(page,encoding='utf-8')
(OUT/'README.md').write_text('''# K-100 전투 단계 경고 UI

선봉·본대·공성·최후 돌격의 예고용 그림입니다. 게임 코드와 기존 에셋은 바꾸지 않았습니다.

- `warning_{vanguard,main,siege}_{N,E,S,W}_{32,48}.png`: 소규모·본대·공성 방향 표식. 화살표는 적이 오는 쪽을 가리킵니다. 상징은 똑바로 유지됩니다.
- 같은 이름의 `_pulse.png`: 왼쪽부터 어두움·상승·최고·하강 4칸, 각 250ms. 칸 중심 고정, 이동·확대 없음. 정지 표식은 상승 칸입니다.
- `emblem_*.png`: 깃발·행군 대열·사다리·뿔 해골, 32점과 48점. 한국어 글자는 별도 UI로 표시합니다.
- `phase_banner_9slice.png`: 96×32. Unity 경계 왼쪽/아래/오른쪽/위 = 10/8/10/8. 상징은 왼쪽 40점 구역, 글자는 그 오른쪽에 둡니다. 최소 96×32, 실제 문구 길이만큼 늘립니다.
- `timeline_track_9slice.png`: 64×24, 경계 8/7/8/7. 단계 구간 4개는 각각 16×8, 경계 2/2/2/2. 단계별 폭은 연결할 때 정하며 예시의 같은 폭은 시간 비율이 아닙니다.
- `enemy_remaining_fill_9slice.png`: 16×4, 경계 2/1/2/1. (살아 있는 적 + 미등장 적) / 전체 적 비율로 왼쪽부터 잘라 표시합니다. 0이면 숨기고, 낮은 비율에서 최소 크기 아래로 찌그러뜨리지 않습니다. 단계 막대 아래 별도 줄에 둡니다.
- `preview_4x/`: 모든 원본 UI 그림의 최근접 4배 미리보기.
- `mockup_battle_night_1920x1080.png`: 기존 전투 사진 합성. 배경 RGB만 0.65배, 기존 HUD·새 UI는 1.0. 밤 조명 시뮬레이션이나 실제 게임 캡처는 아닙니다.
- `mockup_map_timeline.png`: 지도 배치 도식. 남은 적 62% 예시.

명세는 `manifest.json`에 있습니다. 좌표는 왼쪽 위 기준 픽셀, 중심점은 칸 중심입니다. 투명 RGBA / Point / 압축 없음 / UI 밝기 독립을 권장합니다. 캐릭터용 96×128 규격과 발 기준선은 적용 대상이 아닙니다. 기존 시트 교체는 없습니다.

검토 HTML은 저장소 `_HTML/6_그림_화면/codex_K-100_전투단계경고UI_v1.html`입니다. 모든 그림이 내장되어 서버 없이 열립니다. 움직임 정지 버튼을 제공하며 움직임 줄이기 환경에서는 정지로 시작합니다. `build.py`는 재생성 자료이고 게임 런타임 코드가 아닙니다.

전환 5초 전 표시 조건, 화면 밖 방향 투영, 지도 연결과 실제 Play·야간 가독성 검증은 Claude가 후속 진행합니다. 타임라인은 정확한 출현 시간이나 최후 돌격 시점을 그림에 고정하지 않았습니다.
''',encoding='utf-8')
print(json.dumps(dict(assets=len(assets),html=str(HTML)),ensure_ascii=False))
