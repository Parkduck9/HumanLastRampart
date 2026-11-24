from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import json, base64, hashlib, io, math, random

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[2]
N = Image.Resampling.NEAREST
assets = []

def save(im, ident, label, group, source, extra=None):
    path = ROOT / 'tiles' / (ident + '.png')
    im.save(path)
    entry = dict(id=ident, label=label, group=group, file='tiles/'+path.name,
                 width=im.width, height=im.height, source=source,
                 sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    entry.update(extra or {})
    assets.append(entry)
    return im

def periodic(im):
    # 바깥 한 화소를 맞춰 같은 타일의 반복 경계를 일치시킨다.
    im = im.copy()
    for y in range(32): im.putpixel((31,y), im.getpixel((0,y)))
    for x in range(32): im.putpixel((x,31), im.getpixel((x,0)))
    return im

source = Image.open(ROOT/'source/terrain_source.png').convert('RGBA')
names = [('grass_a','풀밭 가'),('grass_b','풀밭 나'),('dirt','흙 바닥'),
         ('hill_1','낮은 언덕'),('hill_2','중간 언덕'),('hill_3','높은 언덕')]
terrain = {}
for i,(ident,label) in enumerate(names):
    x,y = (i%3)*512,(i//3)*512
    crop = (x+4,y+4,x+508,y+508)
    im = periodic(source.crop(crop).resize((32,32),N))
    if i != 2 and i:
        # 풀과 언덕끼리 섞을 때 공유하는 한 화소 띠다.
        for k in range(32):
            for p in [(k,0),(k,31),(0,k),(31,k)]:
                im.putpixel(p, terrain['grass_a'].getpixel(p))
    terrain[ident] = save(im,ident,label,'지형','source/terrain_source.png',dict(crop=list(crop)))

# 길 연결 비트: 북 1, 동 2, 남 4, 서 8. 마스크만 기하학적으로 만든다.
for mask in range(16):
    alpha = Image.new('L',(32,32)); d=ImageDraw.Draw(alpha)
    d.rectangle((9,9,22,22),fill=255)
    for bit,rect in [(1,(9,0,22,15)),(2,(16,9,31,22)),(4,(9,16,22,31)),(8,(0,9,15,22))]:
        if mask & bit: d.rectangle(rect,fill=255)
    im=Image.composite(terrain['dirt'],terrain['grass_a'],alpha)
    save(im,'path_'+str(mask),'흙길 연결 '+str(mask),'길','지형 타일과 기하 마스크',dict(connectionMask=mask))

for ident,label,parts in [
    ('trees','나무 덩이',[('oak_top',1,4,20),('pine_top',14,1,17),('oak_top',12,14,18)]),
    ('rocks','바위 덩이',[('rock_a_top',2,7,22),('rock_b_top',19,3,11),('rock_a_top',18,20,12)])]:
    im=Image.new('RGBA',(32,32))
    for name,x,y,size in parts:
        old=REPO/'Docs/References/codex_k025_v1/sprites'/(name+'.png')
        original=Image.open(old).convert('RGBA')
        ref=ROOT/'source'/(name+'.png')
        ref.write_bytes(old.read_bytes())
        old=original.crop(original.getbbox()); old.thumbnail((size,size),N)
        im.alpha_composite(old,(x,y))
    save(im,ident,label,'소품','K-025 윗면 사본 조합',dict(parts=parts))

castle=Image.open(ROOT/'source/castle_source.png').convert('RGBA')
save(castle.resize((256,256),N),'castle_perimeter','성 둘레 완성 견본 · 40m','성 둘레','source/castle_source.png')
# 8×8 조립 시 원안의 전체 모습을 정확하게 복원하는 칸별 조각이다.
for y in range(8):
    for x in range(8):
        im=castle.resize((256,256),N).crop((x*32,y*32,(x+1)*32,(y+1)*32))
        if im.getbbox(): save(im,f'castle_{x}_{y}',f'성 조립 {x},{y}','성 조각','castle_perimeter',dict(cell=[x,y]))

overlays=[('empty','빈 칸',(235,228,207,35)),('allowed','설치 가능',(122,183,147,65)),
          ('blocked','설치 불가',(195,126,112,75)),('selected','선택',(239,197,104,92)),
          ('placed','설치됨',(107,169,210,78))]
for ident,label,color in overlays:
    im=Image.new('RGBA',(32,32),color); d=ImageDraw.Draw(im)
    d.rectangle((0,0,31,31),outline=(*color[:3],180 if ident!='selected' else 240),width=2 if ident=='selected' else 1)
    if ident=='blocked':
        for p in range(-32,64,8): d.line((p,0,p+32,32),fill=(*color[:3],115))
        d.line((11,11,20,20),fill=(*color[:3],235),width=2)
        d.line((20,11,11,20),fill=(*color[:3],235),width=2)
    if ident=='placed': d.line((10,16,14,20,22,10), fill=(213,235,247,240),width=2)
    if ident=='allowed': d.rectangle((26,26,28,28),fill=(*color[:3],230))
    save(im,'grid_'+ident,label,'격자','기하학적 반투명 겉칠')

# 지도 표시는 실제 함정 모양 대신 작은 크기에서도 읽히는 기호다.
for ident,label in [('trap_buried','묻힌 함정 표시'),('trap_spent','터진 자리 표시')]:
    im=Image.new('RGBA',(32,32));d=ImageDraw.Draw(im)
    if ident=='trap_buried':
        d.polygon([(16,3),(28,16),(16,29),(3,16)],fill=(40,42,30,235),outline=(233,194,111,255))
        d.line((11,11,21,21),fill=(235,207,140,255),width=3)
        d.line((21,11,11,21),fill=(235,207,140,255),width=3)
        d.rectangle((14,14,17,17),fill=(112,73,45,255))
    else:
        d.polygon([(4,11),(10,5),(21,4),(28,12),(26,23),(17,28),(6,24),(2,18)],fill=(143,110,72,245))
        d.polygon([(7,12),(13,8),(22,10),(25,16),(21,22),(12,24),(6,19)],fill=(47,41,32,250))
        d.line((6,24,1,28),fill=(202,167,111,255),width=2)
        d.line((26,7,30,3),fill=(202,167,111,255),width=2)
    save(im,ident,label,'표시','기하학적 지도 기호')

manifest=dict(card='K-029',status='사용자 확정 전',cellMeters=5,cellPixels=32,pixelsPerMeter=6.4,
              pivotTopLeft=[16,16],filter='Point',assets=assets,
              notes=['게임 API가 아닌 그림 배치 설명이다.','길 연결 비트 북1 동2 남4 서8.',
                     '성은 8×8칸 원안 분할 조립이다. 성벽 길이 임의 증감은 미검증.'])
(ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')

images={a['id']:Image.open(ROOT/a['file']).convert('RGBA') for a in assets}
def map_image(seed):
    rng=random.Random(seed); im=Image.new('RGBA',(1024,768))
    for y in range(24):
        for x in range(32):
            ident='grass_a' if rng.random()<.6 else 'grass_b'
            im.alpha_composite(images[ident],(32*x,32*y))
    # 길은 같은 연결 비트 규칙으로 조립한다.
    for y in list(range(0,8))+list(range(16,24)):
        im.alpha_composite(images['path_5'],(16*32,y*32))
    for x,y,ident in [(4,3,'hill_1'),(5,3,'hill_2'),(6,3,'hill_3'),(25,5,'hill_3'),(24,17,'hill_2')]:
        im.alpha_composite(images[ident],(x*32,y*32))
    for _ in range(100):
        x,y=rng.randrange(32),rng.randrange(24)
        if 10<=x<=21 and 6<=y<=17: continue
        im.alpha_composite(images[rng.choice(['trees','rocks'])],(x*32,y*32))
    im.alpha_composite(images['castle_perimeter'],(12*32,8*32))
    return im

map_image(29).save(ROOT/'map_preview.png')
review=map_image(29)
for y in range(24):
    for x in range(32):
        state='allowed' if y<6 or y>17 or x<10 or x>21 else 'empty'
        if (x,y) in [(4,3),(5,3),(6,3),(25,5),(24,17)]: state='blocked'
        if (x,y)==(16,5): state='selected'
        if (x,y) in [(14,5),(16,18)]: state='placed'
        review.alpha_composite(images['grid_'+state],(x*32,y*32))
for x,y in [(14,5),(16,18)]: review.alpha_composite(images['trap_buried'],(x*32,y*32))
review.alpha_composite(images['trap_spent'],(18*32,4*32))
review.save(ROOT/'map_grid_review.png')
chosen=[a for a in assets if a['group'] not in ['성 조각','성 둘레']]
sheet=Image.new('RGB',(960,math.ceil(len(chosen)/8)*144),(21,30,25));d=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',12)
for i,a in enumerate(chosen):
    x,y=(i%8)*120,(i//8)*144
    tile=images[a['id']]
    bg=Image.new('RGBA',(96,96),(78,87,66,255));bg.alpha_composite(tile.resize((96,96),N))
    sheet.paste(bg.convert('RGB'),(x+12,y+8));d.text((x+7,y+109),a['label'],font=font,fill='#eee4cf')
sheet.save(ROOT/'contact_sheet.png')

embedded={a['id']:'data:image/png;base64,'+base64.b64encode((ROOT/a['file']).read_bytes()).decode() for a in assets}
template=(ROOT/'source/review.template.html').read_text(encoding='utf-8')
html=template.replace('__MANIFEST__',json.dumps(manifest,ensure_ascii=False)).replace('__IMAGES__',json.dumps(embedded))
(REPO/'_HTML/codex_K-029_함정지도_v1.html').write_text(html,encoding='utf-8')
print('그림',len(assets),'장 및 검토 페이지 생성')
