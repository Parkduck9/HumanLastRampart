from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json, base64, hashlib, random

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parents[1]
NEAR = Image.Resampling.NEAREST
for folder in ['tiles', 'overlays', 'icons', 'qa', 'originals']:
    (OUT / folder).mkdir(exist_ok=True)
assets = {}
def save(key, title, im, group, **extra):
    path = OUT / group / (key + '.png')
    im.save(path)
    assets[key] = dict(title=title, file=str(path.relative_to(OUT)).replace('\\','/'), size=list(im.size), **extra)
    return im
def cell(im, i):
    w,h=im.size
    return im.crop((round(i%4*w/4),round(i//4*h/2),round((i%4+1)*w/4),round((i//4+1)*h/2))).resize((32,32),NEAR).convert('RGBA')
terrain = Image.open(OUT/'originals/terrain.png')
names = [('grass_a','풀밭 가'),('grass_b','풀밭 나'),('dirt','흙 바탕'),('hill_1','언덕 낮음'),('hill_2','언덕 중간'),('hill_3','언덕 높음'),('trees','나무 덩이'),('rocks','바위 덩이')]
tiles = {k:cell(terrain,i) for i,(k,_) in enumerate(names)}
# 조립 경계 두 화소를 같은 풀밭 띠로 맞춘다. 원안은 별도로 보존한다.
edge = tiles['grass_a'].copy()
for y in range(32):
    for x in range(32):
        if x<2 or x>29 or y<2 or y>29:
            edge.putpixel((x,y), tiles['grass_a'].getpixel((min(x,31-x),min(y,31-y))))
for k,title in names:
    if k!='dirt':
        for y in range(32):
            for x in range(32):
                if x<2 or x>29 or y<2 or y>29: tiles[k].putpixel((x,y),edge.getpixel((x,y)))
    else:
        for y in range(32): tiles[k].putpixel((31,y),tiles[k].getpixel((0,y)))
        for x in range(32): tiles[k].putpixel((x,31),tiles[k].getpixel((x,0)))
    save(k,title,tiles[k],'tiles',layer='지형',cellMetres=5)
# 방향 비트 북1·동2·남4·서8. 같은 흙 바탕을 마스크로 조립한 길 16종.
for mask in range(16):
    m=Image.new('L',(32,32)); d=ImageDraw.Draw(m)
    d.rectangle((10,10,21,21),fill=255)
    for bit,rect in [(1,(10,0,21,15)),(2,(16,10,31,21)),(4,(10,16,21,31)),(8,(0,10,15,21))]:
        if mask&bit: d.rectangle(rect,fill=255)
    path=Image.composite(tiles['dirt'],tiles['grass_a'],m)
    # 길 출구는 동일한 흙 띠를 사용해 인접 칸 연결을 맞춘다.
    for bit in [1,2,4,8]:
        if mask&bit:
            for n in range(10,22):
                for depth in range(2):
                    pos={1:(n,depth),2:(31-depth,n),4:(n,31-depth),8:(depth,n)}[bit]
                    path.putpixel(pos,tiles['dirt'].getpixel((n,0)))
    tiles[f'path_{mask:02d}']=save(f'path_{mask:02d}',f'흙길 연결 {mask:02d}',path,'tiles',connections=mask,layer='지형')
props=Image.open(OUT/'originals/props.png')
for i,(k,title) in enumerate([('wall_n','북 성벽'),('wall_e','동 성벽'),('wall_nw','북서 모서리'),('wall_ne','북동 모서리'),('wall_sw','남서 모서리'),('wall_se','남동 모서리'),('trap_buried','묻힌 함정'),('trap_spent','터진 자리')]):
    im=cell(props,i)
    tiles[k]=save(k,title,im,'icons' if i>=6 else 'tiles',layer='표식' if i>=6 else '성벽')
for k,src in [('wall_s','wall_n'),('wall_w','wall_e')]:
    tiles[k]=save(k,'남 성벽' if k=='wall_s' else '서 성벽',tiles[src].transpose(Image.Transpose.ROTATE_180),'tiles',layer='성벽')
states=[('empty','빈 칸',(220,224,206,12),(220,224,206,72)),('allowed','설치 가능',(77,192,141,52),(104,222,164,170)),('blocked','설치 불가',(204,80,78,58),(231,111,102,180)),('selected','선택',(247,210,107,65),(255,224,136,230)),('placed','설치됨',(83,164,211,60),(128,203,245,200))]
for k,title,fill,line in states:
    im=Image.new('RGBA',(32,32),fill);d=ImageDraw.Draw(im);d.rectangle((0,0,31,31),outline=line)
    if k=='blocked':
        d.line((11,11,20,20),fill=line,width=2);d.line((20,11,11,20),fill=line,width=2)
    if k=='selected': d.rectangle((2,2,29,29),outline=line,width=1)
    if k=='placed': d.line((20,6,23,9,28,4),fill=line,width=2)
    tiles['grid_'+k]=save('grid_'+k,title,im,'overlays',layer='격자',alpha='모든 화소 반투명')
# 조합도는 게임 캡처가 아닌 정적 검토 그림이다.
def map_image(seed):
    rng=random.Random(seed); im=Image.new('RGBA',(640,512)); layout=[]
    for y in range(16):
        row=[]
        for x in range(20):
            k=rng.choices(['grass_a','grass_b','hill_1','hill_2','hill_3','trees','rocks'],[35,35,6,4,2,12,6])[0]
            if x in [9,10] or y in [7,8]:
                mask=(1 if y>0 else 0)|(2 if x<19 and y in [7,8] else 0)|(4 if y<15 else 0)|(8 if x>0 and y in [7,8] else 0)
                if y in [7,8] and x not in [9,10]: mask=10
                k=f'path_{mask:02d}'
            if 7<=x<=12 and 5<=y<=10: k='grass_a'
            im.alpha_composite(tiles[k],(x*32,y*32));row.append(k)
        layout.append(row)
    for y in range(5,11):
        for x in range(7,13):
            k=None
            if y==5: k='wall_nw' if x==7 else 'wall_ne' if x==12 else 'wall_n'
            elif y==10:k='wall_sw' if x==7 else 'wall_se' if x==12 else 'wall_s'
            elif x==7:k='wall_w'
            elif x==12:k='wall_e'
            if k: im.alpha_composite(tiles[k],(x*32,y*32));layout[y][x]=k
    return im,layout
preview,layout=map_image(29);preview.save(OUT/'map_preview.png')
(OUT/'sample_layout.json').write_text(json.dumps(layout,ensure_ascii=False,indent=2),encoding='utf-8')
fontpath='C:/Windows/Fonts/malgun.ttf';font=ImageFont.truetype(fontpath,16)
sheet=Image.new('RGB',(1000,((len(assets)+5)//6)*130+50),'#151d1a');d=ImageDraw.Draw(sheet);d.text((16,12),'K-029 함정 지도 · 32×32화소 · 사용자 확정 전',font=font,fill='#eadbb3')
for i,(k,a) in enumerate(assets.items()):
    x=12+i%6*164;y=50+i//6*130
    im=tiles[k] if k in tiles else Image.open(OUT/a['file'])
    sheet.paste(im.resize((96,96),NEAR),(x,y),im.resize((96,96),NEAR));d.text((x,y+98),a['title'],font=font,fill='#dedbc9')
sheet.save(OUT/'contact_sheet.png')
payload={k:dict(**v,data='data:image/png;base64,'+base64.b64encode((OUT/v['file']).read_bytes()).decode()) for k,v in assets.items()}
template=(OUT/'source/review.template.html').read_text(encoding='utf-8')
html=template.replace('__ASSETS__',json.dumps(payload,ensure_ascii=False))
target=ROOT/'_HTML/codex_K-029_함정지도_v1.html';target.write_text(html,encoding='utf-8')
manifest=dict(status='사용자 확정 전 시안',cellPixels=32,cellMetres=5,pixelsPerMetre=6.4,origin='왼쪽 위',pathBits=dict(북=1,동=2,남=4,서=8),assets=assets,importProposal='점 필터·밉맵 끔·무압축·개별 그림 가장자리 고정, 격자/아이콘은 RGBA 유지')
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
checks=[]
def check(name,ok):
    checks.append(dict(항목=name,통과=bool(ok)))
for k,a in assets.items():
    im=Image.open(OUT/a['file']);check(k+' 크기',im.size==(32,32));check(k+' RGBA',im.mode=='RGBA')
for k,_,_,_ in states:
    extrema=tiles['grid_'+k].getchannel('A').getextrema();check(k+' 반투명',0<extrema[0]<=extrema[1]<255)
for k in ['grass_a','grass_b','hill_1','hill_2','hill_3','trees','rocks','dirt']:
    im=tiles[k];check(k+' 좌우 연결',all(im.getpixel((0,n))==im.getpixel((31,n)) for n in range(32)));check(k+' 위아래 연결',all(im.getpixel((n,0))==im.getpixel((n,31)) for n in range(32)))
for k in ['trap_buried','trap_spent']:check(k+' 알파 배경',tiles[k].getchannel('A').getextrema()[0]==0)
check('내장 PNG 수',html.count('data:image/png;base64,')==len(assets))
check('외부 리소스 없음','src="http' not in html and 'href="http' not in html)
check('길 16종',sum(k.startswith('path_') for k in assets)==16)
for mask in range(16):
    im=tiles[f'path_{mask:02d}']
    for bit in [1,2,4,8]:
        if mask&bit:
            check(f'길 {mask} 출구 {bit}',all(im.getpixel({1:(n,0),2:(31,n),4:(n,31),8:(0,n)}[bit])==tiles['dirt'].getpixel((n,0)) for n in range(10,22)))
(OUT/'qa/verification.json').write_text(json.dumps(dict(통과=sum(c['통과'] for c in checks),전체=len(checks),검사=checks,미수행=['실제 브라우저','Unity','실게임'],sha256={a['file']:hashlib.sha256((OUT/a['file']).read_bytes()).hexdigest() for a in assets.values()}),ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(assets=len(assets),checks=len(checks),failed=[c for c in checks if not c['통과']]),ensure_ascii=False))
