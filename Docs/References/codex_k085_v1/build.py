from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import json, base64, html, hashlib, random, shutil

ROOT=Path(__file__).resolve().parents[3]
OUT=Path(__file__).resolve().parent
OUT.mkdir(exist_ok=True)
NN=Image.Resampling.NEAREST
INK='#21170f'; WOOD='#302219'; LIGHT='#eee4cf'; BRASS='#d5b16b'; PAPER='#bda16c'; RED='#ffb29c'
FONT='C:/Windows/Fonts/gulim.ttc'
def font(size=11,index=0): return ImageFont.truetype(FONT,size,index=index)
def text(im,xy,s,size=11,color=LIGHT,index=0):
    f=font(size,index); box=f.getbbox(s); m=Image.new('L',(max(1,box[2]+2),size+6)); d=ImageDraw.Draw(m);d.text((0,-box[1]),s,font=f,fill=255)
    m=m.point(lambda x:255 if x>=110 else 0)
    im.paste(color,xy,m)
def rect(im,box,c): ImageDraw.Draw(im).rectangle(box,fill=c)
def wood(im,box):
    x,y,x2,y2=box;rect(im,box,WOOD);d=ImageDraw.Draw(im)
    for yy in range(y+5,y2,9):
        for xx in range(x+3,x2,37):d.line((xx,yy,min(xx+19,x2-2),yy),fill='#38271b')
    d.line((x+2,y,x2-2,y),fill='#6d5133'); d.line((x,y+2,x,y2-2),fill='#171512');d.line((x+2,y2,x2-2,y2),fill='#171512')
    for xx in [x+2,x2-2]:
        for yy in [y+2,y2-2]:rect(im,(xx,yy,xx+1,yy+1),'#8c714a')
def icon(im,name,x,y,size=32):
    a=Image.open(ROOT/'Docs/References/codex_k067_v1'/f'{name}.png').convert('RGBA').resize((size,size),NN);im.paste(a,(x,y),a)
def photo(name):return Image.open(ROOT/'Docs/References/ui_redesign_v2'/f'now_{name}.png').convert('RGB')
def yardbase():return Image.open(ROOT/'Game/Assets/_Project/Art/UI/Resources/ScreenArt/prep.png').convert('RGB').resize((640,360),NN).quantize(colors=96).convert('RGB')
def hero(im,x,y):
    a=Image.open(ROOT/'Docs/References/codex_k070_v1/hero_native_96x128.png').convert('RGBA');im.paste(a,(x,y),a)
def band(im):wood(im,(11,10,628,39))
def save(name,im):
    im=im.convert('RGBA'); im.save(OUT/f'{name}_native.png');im.resize((im.width*3,im.height*3),NN).save(OUT/f'{name}_after.png')
    im.resize((im.width*4,im.height*4),NN).save(OUT/f'{name}_4x.png')
def tag(im,x,y,w,h):
    d=ImageDraw.Draw(im);d.line((x+w//2,y-10,x+w//2,y+3),fill=BRASS,width=1)
    d.polygon([(x+4,y),(x+w-5,y),(x+w,y+5),(x+w,y+h),(x,y+h),(x,y+5)],fill=PAPER)
    rng=random.Random(x+y)
    for i in range(w*h//90):
        xx=rng.randrange(x+5,x+w-4);yy=rng.randrange(y+7,y+h-1)
        d.line((xx,yy,min(xx+2,x+w-2),yy),fill='#c5ab79')
    rect(im,(x+w//2-1,y+3,x+w//2+1,y+5),INK)
def button(im,box,s):
    rect(im,box,BRASS);x,y,x2,y2=box;rect(im,(x+2,y+2,x2-2,y+3),'#f5d99c');text(im,(x+(x2-x-len(s)*12)//2,y+9),s,12,INK)

# 전투: 기존 사진의 장면을 유지하고 UI가 있던 영역만 인접 장면으로 복원한다.
b=photo('battle').resize((640,360),NN)
b.paste(b.crop((11,40,419,68)).resize((408,28),NN),(11,11))
b.paste(b.crop((240,333,345,352)),(8,333))
b=b.quantize(colors=96).convert('RGB');band(b)
# 다섯 값은 한 줄, 기존 1600 사진의 약 20px 글자를 1080에서 36px로 확대한다.
icon(b,'badge_per_wall',16,16,18);text(b,(38,17),'15,000/15,000',12)
icon(b,'merc_archer',178,16,18);text(b,(200,17),'63/63',12)
a=Image.open(ROOT/'Docs/References/codex_k070_v1/goblin_species56_96x128.png').convert('RGBA')
bb=a.getbbox();a=a.crop((bb[0],bb[1],bb[2],bb[1]+24)).resize((18,18),NN);b.paste(a,(292,16),a);text(b,(314,17),'0/750',12)
icon(b,'mark_gold_coin',407,16,18);text(b,(430,17),'0',12)
# 모래시계는 기존 시계 아이콘 계열을 쓴다.
icon(b,'item_hourglass_charm',521,16,18);text(b,(544,17),'0:00',12)
wood(b,(12,334,66,352));text(b,(17,338),'F1 조작',11)
save('battle',b)
danger=b.copy();rect(danger,(12,11,176,38),'#48281f')
ImageDraw.Draw(danger).polygon([(17,31),(25,16),(33,31)],fill=RED);rect(danger,(24,20,25,25),INK);rect(danger,(24,28,25,29),INK)
text(danger,(38,17),'북 2,400/15,000',12,RED);save('battle_danger',danger)

y=yardbase();band(y);text(y,(22,18),'정비 마당',12);text(y,(140,18),'전투 2 앞',11)
icon(y,'mark_gold_coin',390,15,20);text(y,(414,18),'160',12);icon(y,'merc_archer',480,15,20);text(y,(504,18),'251/2,000',12)
for x,label in [(80,'성벽 수리'),(365,'병영')]:wood(y,(x,155,x+98,176));text(y,(x+15,160),label,12)
hero(y,230,151);wood(y,(12,334,90,352));text(y,(17,338),'A·D 걷기',11);save('yard',y)

s=yardbase().point(lambda v:int(v*.32));band(s);text(s,(22,18),'상점',16);icon(s,'mark_gold_coin',495,15,20);text(s,(520,18),'5,160',12)
wood(s,(12,57,459,337));wood(s,(17,80,454,192));wood(s,(17,213,454,327))
# 얼굴은 기존 승인 방향 자료에서 잘라 쓰며 새 얼굴을 생성하지 않는다.
def face(im,kind,x,y):
    p=ROOT/('Docs/References/codex_k070_v1/hero_native_96x128.png' if kind=='hero' else 'Docs/References/codex_k065_v1/wizard_front_64x96.png')
    a=Image.open(p).convert('RGBA');bb=a.getbbox();a=a.crop((max(0,bb[0]),bb[1],min(a.width,bb[2]),min(a.height,bb[1]+24))).resize((24,24),NN);im.paste(a,(x,y),a)
face(s,'hero',19,53);face(s,'wizard',19,188)
items=json.loads((ROOT/'Docs/References/codex_k067_v1/manifest.json').read_text(encoding='utf-8-sig'));items={i['id']:i for i in items}
rows=[['telescope_lens','messenger_horn','loud_whistle','command_badge','surgeon_tent','fire_arrows'],['gem_staff','ember_charm','hourglass_charm','powder_keg','shield_rail']]
for row,ids in enumerate(rows):
    for col,key in enumerate(ids):
        x=24+col*71; yy=89+row*135; chosen=key=='telescope_lens'
        if chosen:
            ImageDraw.Draw(s).rectangle((x-2,yy-6,x+65,yy+60),outline=BRASS,width=1)
        icon(s,'item_'+key,x,yy-(4 if chosen else 0),64)
        name=items['item_'+key]['name'];name=name.replace('큰 목청 호루라기','큰 목청\n호루라기').replace('불화살 꾸러미','불화살\n꾸러미').replace('모래시계 부적','모래시계\n부적')
        for line,t in enumerate(name.split('\n')):text(s,(x,yy+65+line*11),t,11)
        ty=yy+91;tag(s,x+29,ty,34,14);text(s,(x+33,ty+4),items['item_'+key]['price'],11,INK)
        if col<5:rect(s,(x+68,yy-8,x+69,yy+105),'#171512')
tag(s,476,72,150,242);icon(s,'item_telescope_lens',520,96,64)
text(s,(486,181),'망원경 렌즈',16,INK);text(s,(486,212),'망원경 반경이 넓어진다',11,INK);text(s,(486,240),'400골드',12,INK);button(s,(490,268,610,299),'사기');save('shop',s)

t=yardbase().point(lambda v:int(v*.32));band(t);text(t,(22,18),'훈련',16);icon(t,'mark_gold_coin',495,15,20);text(t,(520,18),'5,160',12)
wood(t,(46,65,595,328))
for col,(kind,label,level) in enumerate([('archer','궁수',2),('gunner','총병',1),('cannon_crew','대포병',3)]):
    x=76+col*175;text(t,(x+43,76),label,16)
    for rank in range(1,4):
        yy=277-(rank-1)*65;wood(t,(x,yy-2,x+130,yy+53))
        icon(t,f'training_{kind}_{rank}',x+14,yy+9,32)
        text(t,(x+62,yy+11),f'{rank}단계',12)
        text(t,(x+62,yy+31),'수료' if rank<=level else '미수료',11,BRASS if rank<=level else LIGHT)
        if rank==level:ImageDraw.Draw(t).rectangle((x-2,yy-4,x+132,yy+55),outline=BRASS,width=2)
    text(t,(x+20,111),f'현재 {level}단계',12)
save('training',t)

# 후보 글꼴은 미보유로 표시한다. 실제 설치 글꼴의 비트맵 표본만 별도로 제공한다.
strings=['출정','전투 1 보고 · 승리','망원경 렌즈 400골드에 사기','장교님! 정말 돌아오셨군요.']
f=Image.new('RGB',(640,360),INK);text(f,(14,12),'한글 글꼴 · 오프라인 확인판',16)
text(f,(14,41),'갈무리11·14 / Neo둥근모 / 둥근모꼴: 파일 없음',12)
text(f,(14,62),'아래는 설치된 굴림·돋움 표본이며 후보를 대체하지 않습니다.',11)
f=f.resize((1920,1080),NN)
for index,label in [(0,'굴림'),(2,'돋움')]:
    y0=255+index//2*390
    for col,bg in enumerate([WOOD,PAPER]):
        x=42+col*936;rect(f,(x,y0,x+894,y0+366),bg)
        if col==1:
            rng=random.Random(y0)
            for i in range(300):
                xx=x+rng.randrange(8,880);yy=y0+rng.randrange(8,355);rect(f,(xx//3*3,yy//3*3,xx//3*3+5,yy//3*3+2),'#c5ab79')
        title=Image.new('RGB',(290,20),bg);text(title,(2,3),label+' · 1배 12px / 3배 36px',12,LIGHT if col==0 else INK);f.paste(title.resize((870,60),NN),(x+9,y0+9))
        for row,st in enumerate(strings):
            sample=Image.new('RGB',(280,17),bg);text(sample,(0,2),st,12,LIGHT if col==0 else INK,index)
            f.paste(sample,(x+12,y0+78+row*69));f.paste(sample.resize((840,51),NN),(x+12,y0+95+row*69))
f.convert('RGBA').save(OUT/'font_board.png')

for key in ['battle','yard','shop']:
    before=photo(key);shutil.copyfile(ROOT/'Docs/References/ui_redesign_v2'/f'now_{key}.png',OUT/f'{key}_before.png')
    pair=Image.new('RGB',(3840,1080),INK);pair.paste(before.resize((1920,1080),NN),(0,0));pair.paste(Image.open(OUT/f'{key}_after.png'),(1920,0));pair.save(OUT/f'{key}_pair.png')

def lum(h):
    rgb=[int(h[i:i+2],16)/255 for i in (1,3,5)];rgb=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb];return sum(a*b for a,b in zip(rgb,[.2126,.7152,.0722]))
def contrast(a,b):a,b=sorted([lum(a),lum(b)]);return round((b+.05)/(a+.05),2)
pairs=[('나무 본문',LIGHT,'#38271b'),('황동 보조',BRASS,'#38271b'),('꼬리표 먹글',INK,PAPER),('황동 단추 먹글',INK,BRASS),('위험 글',RED,'#48281f')]
ratios=[dict(role=n,foreground=a,background=b,ratio=contrast(a,b)) for n,a,b in pairs]
manifest={'card':'K-085','purpose':'검토용 목업 · 게임 연결 전','native_canvas':[640,360],'screen_canvas':[1920,1080],'screen_pixel_scale':3,'preview_scale':4,'alpha':'모든 목업 완전 불투명 RGBA','sprite_sheets':'해당 없음 · UI 목업 카드','pivots':'해당 없음','item_icon_native':[64,64],'item_icon_screen':[192,192],'selected_lift_native':4,'selected_lift_screen':12,'fonts':{'used':'굴림 11/12/16px, 이진화 후 최근접 3배','diagnostic':'굴림·돋움 12px 1배/3배','unavailable':['Galmuri11','Galmuri14','NeoDunggeunmo','DungGeunMo'],'searched':['카드 저장소 전체','Windows 설치 글꼴 목록','Windows Fonts','사용자 Fonts','원본 저장소 후보 파일명']},'contrast':ratios,'training_levels':{'궁수':2,'총병':1,'대포병':3},'notes':['가격·효과는 K-067 manifest에서 읽음','훈련 현재 단계와 위험 체력은 설명용 예시','훈련 현재 게임 사진은 제공되지 않아 전후쌍을 만들지 않음','64px와 4px는 원본 단위로 해석하여 3px 격자 유지','공용 물건의 선반 배치는 장착 규칙을 뜻하지 않음','전투 아이콘 18 원본 px = 54 화면 px; 기존 약30 화면 px ×1080/900 대비 1.5배','배경 96색 감색 · 전체 캔버스 3배 최근접']}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')

def emb(name):return 'data:image/png;base64,'+base64.b64encode((OUT/name).read_bytes()).decode()
def img(name,alt,cls=''):return f'<img class="{cls}" src="{emb(name)}" alt="{alt}" loading="eager">'
sections=[]
notes={'battle':'명세 4.5·5 전투: 다섯 정보만 한 띠에 두고 숫자·아이콘을 약 1.5배 확대했습니다. S-15에 따라 하단은 F1 조작 하나만 남겼습니다.','yard':'명세 4.3·4.4·5 정비 마당: 의미 없는 금색 밑줄을 지웠습니다. 배경을 640×360·96색으로 맞춘 뒤 3배 확대했습니다. 상단은 정비 마당·전투 2 앞·골드·병력입니다.','shop':'명세 4.2·4.6·5 상점: 크림 판과 긴 단추 격자를 어두운 나무 진열대로 바꿨습니다. 아이콘이 먼저 보이고 이름·가격이 뒤따릅니다. 오른쪽은 꼬리표 한 장과 사기 단추 하나입니다.'}
for key,title in [('battle','전투 HUD'),('yard','정비 마당'),('shop','상점 진열대')]:
    sections.append(f'<section><h2>{title}</h2><p>{notes[key]}</p><div class="pair"><figure><figcaption>현재 · 실제 게임 사진 1600×900</figcaption>{img(key+"_before.png",title+" 현재 사진")}</figure><figure><figcaption>기대 화면 · 목업 1920×1080</figcaption>{img(key+"_after.png",title+" 기대 화면")}</figure></div><details><summary>원본 1920px로 확인 · 가로 스크롤</summary><div class="scroll">{img(key+"_after.png",title+" 원본 크기","full")}</div></details></section>')
sections.insert(1,f'<section><h2>위험 표시 예시</h2><p>명세 4.5·S-27: 북 성벽 체력을 예시로 낮췄습니다. 붉은 바탕과 삼각 경고 모양을 함께 썼습니다. 소리는 정지 목업에서 확인할 수 없습니다.</p>{img("battle_danger_after.png","북 성벽 위험 목업")}</section>')
sections.append(f'<section><h2>훈련 · 세 기둥</h2><p>명세 5 훈련: 궁수·총병·대포병의 1단계를 아래에, 3단계를 위에 쌓았습니다. 수료 글과 현재 단계 황동 테를 함께 표시했습니다. 예시 단계는 2·1·3이며 실제 저장값이 아닙니다. 현재 훈련 사진이 없어 전후 비교는 제공하지 않습니다.</p>{img("training_after.png","훈련 세 기둥 기대 화면")}</section>')
lift_icon='data:image/png;base64,'+base64.b64encode((ROOT/'Docs/References/codex_k067_v1/item_telescope_lens.png').read_bytes()).decode()
sections.append(f'''<section><h2>상점 선택 움직임</h2><p>명세 5·S-22: 선택할 때 원본 4점, 화면 12px만큼 들어 올리고 황동 테를 남기는 동작 예시입니다. 한 점씩 보간하지 않고 격자 위치를 바꿉니다.</p><style>.lift-demo{{width:240px;height:240px;background:#302219;padding:24px;margin:24px 0}}.lift-icon{{width:192px;height:192px;animation:lift 1.6s steps(1,end) infinite}}@keyframes lift{{0%,49%{{transform:translateY(0);outline:0 solid #d5b16b}}50%,100%{{transform:translateY(-12px);outline:3px solid #d5b16b}}}}@media(prefers-reduced-motion:reduce){{.lift-icon{{animation:none;transform:translateY(-12px);outline:3px solid #d5b16b}}}}button{{background:#d5b16b;color:#21170f;border:0;padding:12px 20px;font:inherit;cursor:pointer}}</style><div class="lift-demo"><img class="lift-icon" src="{lift_icon}" alt="선택에 따라 올라가는 망원경 렌즈"></div><button type="button" aria-pressed="false" onclick="let i=document.querySelector('.lift-icon');let p=this.getAttribute('aria-pressed')==='true';i.style.animationPlayState=p?'running':'paused';this.setAttribute('aria-pressed',String(!p));this.textContent=p?'움직임 멈추기':'움직임 재생';">움직임 멈추기</button></section>''')
sections.append(f'<section><h2>한글 픽셀 글꼴 비교판</h2><p>명세 4.3: 갈무리11·갈무리14·Neo둥근모·둥근모꼴은 오프라인에서 찾지 못했습니다. 해당 후보의 표본을 꾸며 넣지 않았습니다. 아래는 실제 설치된 굴림과 돋움의 참고 표본입니다. 후보 채택은 보류입니다. 네 문장을 두 재질 위에 각각 12px와 최근접 36px로 그렸습니다.</p>{img("font_board.png","굴림 돋움 실제 설치 글꼴 1배 3배 비교판")}<details><summary>1배 글꼴을 실제 크기로 확인</summary><div class="scroll">{img("font_board.png","글꼴 비교판 원본","full")}</div></details></section>')
rowshtml=''.join(f'<tr><td>{r["role"]}</td><td>{r["foreground"]}</td><td>{r["background"]}</td><td>{r["ratio"]}:1</td></tr>' for r in ratios)
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>K-085 · UI 2판 기대 화면 2</title><style>*{box-sizing:border-box}body{margin:0;background:#17130f;color:#eee4cf;font:18px/1.7 "Malgun Gothic",sans-serif}main{max-width:1800px;margin:auto;padding:30px}h1{font-size:34px;line-height:1.35}h2{font-size:27px;margin-top:0}p{max-width:1100px}section{margin:42px 0 60px}img{display:block;width:100%;height:auto;image-rendering:pixelated}figure{margin:0;min-width:0}figcaption{margin:8px 0;color:#d5b16b}.pair{display:grid;grid-template-columns:1fr 1fr;gap:18px}.scroll{overflow:auto}.full{width:1920px;max-width:none}summary{cursor:pointer;padding:12px 0;color:#d5b16b}table{border-collapse:collapse}td,th{text-align:left;padding:10px 20px;border-bottom:1px solid #765c3d}a{color:#f5d99c}li{margin:9px 0}@media(max-width:800px){main{padding:16px}.pair{grid-template-columns:1fr}h1{font-size:28px}table{font-size:14px}td,th{padding:8px}}</style><main><h1>UI 다시 설계 2판 · 기대 화면 2</h1><p>전투 · 정비 마당 · 상점 · 훈련 · 한글 글꼴. 아래의 ‘기대 화면’은 검토용 목업입니다. 실제 게임에 적용한 사진이 아닙니다. 원본 사진과 결과 이미지를 모두 내장하여 서버·인터넷 없이 열립니다.</p><p>기준: UI_Redesign_v2.md 4.2~4.6·5장, UI_NoSlop.md S-03·S-06·S-14·S-15·S-22·S-27·S-30. 새 명세의 전투 다섯 정보 규칙을 앞선 S-20보다 우선했습니다.</p>'''+''.join(sections)+f'''<section><h2>대비 · 실제 사용 색 측정</h2><p>sRGB 상대 휘도 공식 (밝은 쪽 + 0.05) / (어두운 쪽 + 0.05)로 계산했습니다. 나무는 글 뒤 가장 밝은 결 색을 사용한 보수적 값입니다. 글은 중간색 없이 이진화했습니다. 모든 본문 조합이 4.5:1 이상입니다.</p><div class="scroll"><table><tr><th>용도</th><th>글자</th><th>바탕</th><th>대비</th></tr>{rowshtml}</table></div></section><section><h2>크기 해석과 확인 범위</h2><p>화면은 640×360 원본을 최근접 3배로 키웠습니다. 선반의 ‘64px 아이콘’과 ‘4px 들어 올림’은 원본 단위로 해석하여 화면에서는 192px·12px입니다. 화면에서 정확히 64px·4px로 만들면 3px 격자를 지킬 수 없어 이 해석을 명시합니다. 전투 숫자는 원본 12px → 화면 36px, 아이콘은 원본 18px → 화면 54px입니다.</p><p>훈련·상점의 단계와 배치는 검토용입니다. 두 선반에 놓인 공용 물건은 장착 대상을 확정하는 표시가 아닙니다. 인물 그림은 K-070 주인공과 K-065 마법사를 재사용했습니다.</p></section><section><h2>아쉬운 점</h2><ul><li>지정 후보 글꼴 파일이 없어 최종 한글 픽셀 글꼴을 고를 수 없습니다. 목업은 굴림을 격자에 맞춘 임시 표본입니다.</li><li>배경 감색·격자 정렬은 적용했지만 원래 그림의 큰 명암 덩어리까지 다시 그린 것은 아닙니다. 인물과 배경의 체감 점 크기는 추가 검토가 필요합니다.</li><li>전투의 파란 십자 조각은 원본 사진에도 있는 별도 오류입니다. 이 카드는 원인 수정·게임 실행을 하지 않았습니다.</li><li>전투 HUD가 있던 자리는 인접 안개·바닥으로 복원했습니다. 실제 카메라의 무UI 캡처가 아닙니다.</li><li>공용 물건의 두 선반 분류는 장착 규칙과 구분하여 확정해야 합니다.</li><li>훈련의 현재 사진이 없어 기대 화면만 있습니다. 소리·첫 3회 안내·실제 구매·입력은 검증 대상이 아닙니다.</li></ul></section></main></html>'''
review=ROOT/'_HTML/6_그림_화면/codex_K-085_UI2판_기대사진2_v1.html';review.parent.mkdir(parents=True,exist_ok=True);review.write_text(page,encoding='utf-8')
(OUT/'README.md').write_text('''# K-085 · UI 다시 설계 2판 기대 화면 2

검토용 목업입니다. Game 폴더를 변경하거나 Unity를 실행하지 않았습니다.

- 전투·위험 예시·정비 마당·상점·훈련: `_native.png` 640×360, `_after.png` 1920×1080, `_4x.png` 2560×1440.
- 전투·마당·상점: `_before.png`는 제공된 1600×900 원본, `_pair.png`는 전후 3840×1080.
- `font_board.png`: 1920×1080. 지정 후보 네 종은 미보유이며 실제 굴림·돋움 표본만 표시합니다.
- `manifest.json`: 점 배율, 단위 해석, 글꼴 재고, 대비 측정값, 검토 유의점.
- `build.py`: 기존 에셋을 재사용하는 재현용 제작 스크립트. Pillow 필요.
- 리뷰: `../../../_HTML/6_그림_화면/codex_K-085_UI2판_기대사진2_v1.html`. 이미지 내장, 직접 열기 가능.

선반 아이콘 64px와 선택 올림 4px는 원본 단위입니다. 화면에서 각각 192px와 12px로 표시됩니다. 훈련 현재 단계 2·1·3과 위험 체력은 예시입니다. 후보 글꼴 확보·사용권 확인·최종 글꼴 선택 및 실제 게임 연결은 남아 있습니다.
''',encoding='utf-8')
print(json.dumps({'outputs':len(list(OUT.glob('*.png'))),'contrast':ratios,'review_bytes':review.stat().st_size},ensure_ascii=False))
