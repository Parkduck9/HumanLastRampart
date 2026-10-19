from pathlib import Path
import base64, hashlib, json, shutil, colorsys
import numpy as np
from PIL import Image, ImageDraw

# 내장 이미지 도구로 그린 원안을 최근접 축소하고 검토 자료를 만든다.
OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
ART = ROOT / 'Game/Assets/_Project/Art/Characters/Static'
PAGE = ROOT / '_HTML/4_그림_캐릭터/codex_K-065_아군6판_다시_v1.html'
HERO = ROOT / 'Docs/References/codex_hero_octopath_review_v6/generated.png'
NAMES = {'archer':'궁수', 'gunner':'총병', 'cannon_crew':'대포병', 'wizard':'마법사'}
NEAR = Image.Resampling.NEAREST

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def bounds(im):
    a = np.asarray(im.convert('RGBA'))[:,:,3] >= 128
    y,x = np.where(a)
    return [int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)]

def save_pair(im, name):
    im.save(OUT / (name+'.png'))
    preview_name = name.removesuffix('_64x96') + '_4x.png'
    im.resize((im.width*4,im.height*4),NEAR).save(OUT / preview_name)

def place(im, body_top=None, axis=None):
    box = bounds(im)
    top = box[1] if body_top is None else body_top
    if body_top == 'auto':
        a=np.array(im.convert('RGBA'))[:,:,3]>=128
        lo,hi=max(0,int(axis)-120),min(im.width,int(axis)+121)
        top=int(np.where(a[:,lo:hi])[0].min())
    scale = 52/(box[3]-top)
    w,h = round((box[2]-box[0])*scale),round((box[3]-box[1])*scale)
    crop = im.crop(box).resize((w,h),NEAR)
    a = np.array(crop)
    a[a[:,:,3]<128] = 0
    a[a[:,:,3]>=128,3] = 255
    crop = Image.fromarray(a)
    center = (box[0]+box[2])/2 if axis is None else axis
    x = round(32-(center-box[0])*scale)
    y = 88-h
    assert x>=0 and x+w<=64 and y>=0, (box,x,y,w,h)
    canvas = Image.new('RGBA',(64,96))
    canvas.paste(crop,(x,y))
    return canvas, {'원안경계':box,'머리꼭대기':top,'가로축':center,'배치':[x,y,w,h]}

def palette_for(kind):
    # 같은 원본 색에서 약 세 단계씩 선택하여 머리·가죽·금속의 색상을 유지한다.
    paths = [ART / (('wizard_back' if kind=='wizard' else kind)+'_static.png')]
    if kind=='wizard': paths += [ART/'wizard_static.png']
    colors = np.concatenate([np.array(Image.open(p).convert('RGBA')).reshape(-1,4) for p in paths])
    colors = colors[colors[:,3]>=250,:3]
    groups = {}
    for c in np.unique(colors,axis=0):
        r,g,b = map(int,c)
        h,s,v = colorsys.rgb_to_hsv(r/255,g/255,b/255)
        if v<.16: key='외곽선'
        elif s<.19: key='금속'
        elif .49<=h<.69 and s>.25: key='파랑'
        elif .69<=h<.92 and s>.29: key='보라'
        elif .09<h<.20 and s<.37 and v>.56: key='크림'
        elif .09<h<.20 and s>=.37: key='금색'
        elif .025<h<=.10 and v>.70 and s<.66: key='피부'
        elif .65<=h<.92: key='검회색머리'
        else: key='갈색'
        groups.setdefault(key,[]).append(c)
    chosen=[]
    for k,v in groups.items():
        a=np.array(v,dtype=np.uint8)
        count=1 if k=='외곽선' else 6 if k=='갈색' else 3
        if len(a)<=count: chosen.extend(a.tolist());continue
        q=Image.fromarray(a.reshape(1,-1,3)).quantize(colors=count,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE)
        centers=np.array(q.getpalette(),dtype=np.int32).reshape(-1,3)[:count]
        dist=((centers[:,None]-a[None].astype(np.int32))**2).sum(2)
        chosen.extend(a[dist.argmin(1)].tolist())
    # 눈의 파란 홍채와 흰 반사점은 주인공 기준의 공통 표현이다.
    chosen += [[24,63,102],[53,112,170],[255,247,224]]
    return np.unique(np.array(chosen,dtype=np.uint8),axis=0),paths

def map_colors(im,palette):
    a=np.array(im)
    rgb=a[:,:,:3].astype(np.int32)
    diff=((rgb[:,:,None]-palette[None,None].astype(np.int32))**2).sum(3)
    a[:,:,:3]=palette[diff.argmin(2)]
    a[a[:,:,3]==0]=0
    return Image.fromarray(a)

def embedded(path):
    return 'data:image/png;base64,'+base64.b64encode(Path(path).read_bytes()).decode()

def tile(path,label,scale):
    return f'<figure><figcaption>{label}</figcaption><div class="cell" style="width:{64*scale}px;height:{96*scale}px"><img alt="{label}" width="{64*scale}" height="{96*scale}" src="{embedded(path)}"><span class="foot" style="top:{88*scale}px"></span></div></figure>'

def run():
    entries=json.loads((OUT/'generation_manifest.json').read_text(encoding='utf-8'))
    im=Image.open(HERO).convert('RGBA')
    hero,hm=place(im)
    save_pair(hero,'reference_grid_64x96')
    hero.resize((256,384),NEAR).save(OUT/'reference_grid_4x.png')
    native=np.array(im.resize((64,96),NEAR))
    native[native[:,:,3]<128]=0
    native[native[:,:,3]>=128,3]=255
    native=Image.fromarray(native)
    nb=bounds(native)
    native_canvas=Image.new('RGBA',(64,96))
    native_canvas.alpha_composite(native,(0,88-nb[3]))
    save_pair(native_canvas,'hero_native_grid_estimate_64x96')
    measure={'원본':str(HERO.relative_to(ROOT)),'sha256':sha(HERO),'원본크기':list(im.size),'알파128경계':bounds(im),'원본인물높이':1167,'복원키':52,'복원격자간격':1167/52,'머리원본범위':[224,610],'머리복원높이약':17,'머리비율약':17/52,'외곽선주요두께':1,'주의':'원본은 완전한 정수 격자가 아닌 생성 그림. 52점은 약22화소 점 간격에 따른 복원 추정. 턱 위치는 시각 판독.'}
    rgb=np.array(im)[:,:,:3].astype(float)
    opaque=np.array(im)[:,:,3]>=128
    delta=np.linalg.norm(np.diff(rgb,axis=0),axis=2)
    energy=(delta*(delta>70)*(opaque[1:]&opaque[:-1])).sum(1)
    peaks=np.where((energy>energy.max()*.13)&(energy>=np.roll(energy,1))&(energy>=np.roll(energy,-1)))[0]+1
    samples=[int(y) for y in peaks if 240<=y<=387]
    measure.update({'원본고유점간격추정':'강한 경계 간격 약16~17화소, 완벽한 균일 격자는 아님','경계표본원본y':samples,'경계표본간격':np.diff(samples).tolist(),'고유격자추정인물높이':bounds(native_canvas)[3]-bounds(native_canvas)[1],'고유격자머리높이약':24,'정정':'22.44화소는 요청한 52점으로 정규화할 때의 환산 간격. 고유 격자 간격으로 단정하지 않는다.'})
    measure['주의']='52점은 요청된 비교 규격으로 정규화한 키이며 고유 격자 측정값과 구분한다. 턱 위치는 시각 판독.'
    (OUT/'reference_measurements.json').write_text(json.dumps(measure,ensure_ascii=False,indent=2),encoding='utf-8')
    reports={}
    for kind in NAMES:
        palette,paths=palette_for(kind)
        reports[kind]={'원본':[{'경로':str(p.relative_to(ROOT)),'sha256':sha(p)} for p in paths],'팔레트':['#%02x%02x%02x'%tuple(c) for c in palette]}
        for view in ['back','front']:
            key=kind+'_'+view
            e=entries[key]
            target=OUT/(key+'_generated.png')
            source = OUT / e['source']
            if source.resolve()!=target.resolve(): shutil.copyfile(source,target)
            sprite,meta=place(Image.open(target).convert('RGBA'),e.get('body_top'),e.get('axis'))
            sprite=map_colors(sprite,palette)
            save_pair(sprite,key+'_64x96')
            # 짧은 파일명으로도 4배 확인본을 만든다.
            sprite.resize((256,384),NEAR).save(OUT/(key+'_4x.png'))
            b=bounds(sprite)
            assert sprite.size==(64,96) and b[3]==88
            central_y=np.where(np.array(sprite)[:,26:39,3]>=128)[0]
            actual_top,actual_bottom=int(central_y.min()),int(central_y.max()+1)
            assert (actual_top,actual_bottom)==(36,88), (key,actual_top,actual_bottom)
            assert np.array_equal(np.array(Image.open(OUT/(key+'_4x.png'))),np.repeat(np.repeat(np.array(sprite),4,0),4,1))
            assert set(np.unique(np.array(sprite)[:,:,3]))<={0,255}
            head_height = round((e['head_bottom'] - meta['머리꼭대기']) * 52 / (meta['원안경계'][3]-meta['머리꼭대기'])) if 'head_bottom' in e else None
            reports[kind][view]={'경계':b,'인물높이':actual_bottom-actual_top,'머리높이목표':17,'머리높이시각판독':head_height,'몸중심검사열':[26,39],'머리꼭대기':actual_top,'발바닥아래':actual_bottom,'배치':meta,'sha256':sha(OUT/(key+'_64x96.png'))}
        current=Image.open(paths[0]).convert('RGBA')
        current,_=place(current)
        save_pair(current,kind+'_current_back')
        old,_=place(Image.open(ROOT/('Docs/References/codex_k063_v1/'+kind+'_back_56x112.png')).convert('RGBA'))
        save_pair(old,kind+'_k063_back_comparison')
        if kind=='wizard':
            front,_=place(Image.open(paths[1]).convert('RGBA'))
            save_pair(front,'wizard_current_front')
    for filename,name in [('Docs/References/codex_hero_octopath_review_v6/corrected.png','hero_corrected_comparison'),('Docs/References/codex_k064_v1/goblin_front.png','k064_goblin_comparison')]:
        c,_=place(Image.open(ROOT/filename).convert('RGBA'))
        save_pair(c,name)
    (OUT/'validation.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2),encoding='utf-8')
    sections=[]
    for scale in [1,4]:
        cards=[]
        for kind,label in NAMES.items():
            row=tile(OUT/'reference_grid_64x96.png','선택된 주인공 · 첫 생성본',scale)
            row+=tile(OUT/(kind+'_current_back.png'),'현재 게임 · 뒤',scale)
            row+=tile(OUT/(kind+'_k063_back_comparison.png'),'K-063 이전 방향 · 뒤',scale)
            row+=tile(OUT/(kind+'_back_64x96.png'),'이번 그림 · 뒤',scale)
            row+=tile(OUT/(kind+'_front_64x96.png'),'이번 그림 · 앞',scale)
            if kind=='wizard':row+=tile(OUT/'wizard_current_front.png','현재 게임 · 앞',scale)
            cards.append(f'<article><h3>{label}</h3><div class="row">{row}</div></article>')
        sections.append(f'<section id="scale-{scale}"><h2>{scale}배 · 동일 격자·동일 발선</h2><p>모든 칸 64×96점, 인물 키 52점, 중심 x=32, 발바닥 아래 y=88. 무기는 인물 키에서 제외합니다. 가로로 스크롤해 비교할 수 있습니다.</p>'+''.join(cards)+'</section>')
    comparison=tile(OUT/'reference_grid_64x96.png','채택 · 첫 생성본',4)+tile(OUT/'hero_corrected_comparison.png','비교만 · 머리·눈 보정본',4)+tile(OUT/'archer_k063_back_comparison.png','폐기된 K-063 방향',4)+tile(OUT/'k064_goblin_comparison.png','폐기된 K-064 방향',4)
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-065 · 아군 6판 다시</title><style>
    *{box-sizing:border-box}body{margin:0;background:#171c25;color:#f1e9db;font:16px/1.65 system-ui,sans-serif}main{max-width:1600px;margin:auto;padding:32px}h1{font-size:32px;margin:0 0 12px}h2{margin-top:36px}h3{margin:0 0 12px}p{max-width:1000px;color:#cbc8c0}a{color:#eac580}article{margin:24px 0;padding:20px;border:1px solid #464a53;border-radius:10px;background:#222833}.row{display:flex;gap:14px;overflow-x:auto;padding-bottom:12px;align-items:flex-start}figure{margin:0;flex:none}figcaption{font-size:13px;min-height:25px;color:#e9dabd}.cell{position:relative;background:repeating-conic-gradient(#3d444e 0% 25%,#343a43 0% 50%) 0 0/16px 16px}.cell img{display:block;image-rendering:pixelated}.foot{position:absolute;left:0;right:0;border-top:1px solid #e3b66a;pointer-events:none}table{border-collapse:collapse;width:100%;max-width:1000px}td,th{padding:9px;border:1px solid #4c5059;text-align:left}.notice{border-left:3px solid #d6ae6c;padding:4px 18px;background:#282d34}button{padding:8px 14px;margin-right:8px;background:#303944;border:1px solid #707780;color:#fff;cursor:pointer;border-radius:4px}@media(max-width:600px){main{padding:18px}h1{font-size:25px}}
    </style><main><h1>K-065 · 선택한 첫 6판으로 아군 다시 그리기</h1><p>궁수 · 총병 · 대포병 · 마법사 / 후면 대기 우선 · 정면 대기 / 2026-10-19</p><div class="notice"><p>사용자가 고른 기준은 <strong>generated.png, 처음 생성한 주인공 6판</strong>입니다. 머리와 눈을 줄인 corrected.png 방향은 D-080 정정으로 대체되었습니다. 아래 주인공은 기존 원본의 최근접 축소 비교본이며 새로 그리지 않았습니다.</p></div><h2>원본 직측과 비교 규격</h2><table><tr><th>항목</th><th>직접 확인한 값</th></tr><tr><td>원본 크기 / 인물 알파 경계</td><td>1024×1536 / x=266~764, y=224~1390 · 499×1167화소 (알파≥128)</td></tr><tr><td>원본 고유 격자 추정</td><td>주요 강한 경계 간격 약16~17화소 · 인물 약73점 · 머리 약24점</td></tr><tr><td>이번 비교 규격</td><td>인물 22×52점 · 52점 환산 간격 약22.44화소 · 최근접 축소</td></tr><tr><td>머리카락~턱 / 비율</td><td>원본 y≈224~610 · 약386화소 → 약17점 / 약33%</td></tr><tr><td>외곽선 / 명암</td><td>복원본 주 외곽선 약1점 · 재질별 약3단 · 갈색은 머리·가죽·목재가 함께 있어 복수 색군</td></tr><tr><td>공통 칸 / 발선 / 중심</td><td>64×96점 / 발바닥 아래 y=88 (마지막 행87) / 몸 중심 x=32</td></tr></table><p>생성 그림의 점 간격은 완벽한 정수 격자가 아닙니다. 원본의 강한 경계 간격을 직측하면 약16~17화소이며, 고유 격자에서는 키 약73점·머리 약24점으로 추정됩니다. 요청한 키52점은 이번 비교용 정규화 규격이며 고유 격자 측정값과 구분합니다. 턱 경계는 시각 판독입니다. 4배 파일은 최종 1배의 점 하나를 정확히4×4로 표시합니다.</p><p><button onclick="setBg('grid')">격자 배경</button><button onclick="setBg('light')">밝은 배경</button><button onclick="setBg('dark')">어두운 배경</button></p>'''+''.join(sections)+'''<section><h2>이전 K-063·K-064와 왜 다른가</h2><p>이전 두 카드는 머리·눈을 줄인 보정본과 96점 높이를 따라 몸통과 다리를 더 길고 세밀하게 표현했습니다. 이번에는 첫 생성본의 머리 약1/3, 또렷한 파란 눈, 짧고 튼튼한 몸, 큰 장화와 둥근 손을 52점에 맞췄습니다. 아래 이전 자료도 인물 높이를52점으로 맞췄으므로 비율과 점의 밀도 차이를 비교할 수 있습니다. K-064 고블린은 이전 방향 설명용이며 이번 카드의 제작 대상이 아닙니다.</p><div class="row">'''+comparison+'''</div></section><section><h2>유지한 병종 외형</h2><p>궁수: 밝은 갈색 머리·남색/크림 제복·나무 활·등 화살통. 총병: 짙은 갈색 머리·남색/크림 제복·회색 어깨 보호대·목재/금속 머스킷. 대포병: 검회색 머리·남색/크림 제복·갈색 멜빵·솔 끝 포신 청소봉. 마법사: 따뜻한 밤색 머리·보라/금색 로브·푸른 보석 지팡이. 현재 그림은 각 병종 Static 원본을 읽어 동일 높이로 축소한 비교본입니다. 궁수·총병·대포병 원본에는 정면 대기 그림이 없어 정면 전후 비교는 마법사만 제공합니다.</p><h2>확인 범위와 다음 단계</h2><p>이번 결과는 서기 참고 아트입니다. 8장 규격·발선·알파·4배 최근접 일치와 내장 이미지 참조를 검사했습니다. 앞뒤 그림은 각각 생성했으므로 머리와 장비 위치의 일관성은 사용자 외형 검토가 필요합니다. 게임 연결·동작 시트·노멀·Unity 실행·실제 전투 캡처는 수행하지 않았습니다. 사용자 확인 후 Claude가 게임에 연결합니다.</p></section></main><script>function setBg(mode){document.querySelectorAll('.cell').forEach(el=>{el.style.background=mode==='light'?'#e9e1d1':mode==='dark'?'#11161d':''})}</script></html>'''
    native_panel='<div class="row">'+tile(OUT/'hero_native_grid_estimate_64x96.png','원본 격자 추정 · 키 약73점',4)+tile(OUT/'reference_grid_64x96.png','비교 규격 정규화 · 키52점',4)+'</div>'
    html=html.replace('<p><button',native_panel+'<p><button',1)
    head_table='<h2>이번 그림의 비율 확인</h2><table><tr><th>병종</th><th>인물 키</th><th>뒤 머리 높이</th><th>앞 머리 높이</th></tr>'
    for kind,label in NAMES.items():
        head_table+=f'<tr><td>{label}</td><td>52점</td><td>약{reports[kind]["back"]["머리높이시각판독"]}점</td><td>약{reports[kind]["front"]["머리높이시각판독"]}점</td></tr>'
    head_table+='</table><p>머리카락 꼭대기부터 턱 또는 뒤통수 아래까지 시각 판독한 값으로, 머리 비율은 약31~33%입니다. 새 그림은 몸 중심 부근에서 머리 꼭대기 y=36·발바닥 아래 y=88을 픽셀로 확인했습니다. 외곽선은 주로1점이며 모든 구간의 두께를 별도로 증명한 것은 아닙니다.</p>'
    html=html.replace('<h2>유지한 병종 외형</h2>',head_table+'<h2>유지한 병종 외형</h2>')
    html=html.replace('후면 대기 우선 · 정면 대기 / 2026-10-19','후면 대기 우선 · 정면 대기 / 2026-10-20')
    html=html.replace('앞뒤 그림은 각각 생성했으므로','실제 브라우저 렌더링 검사는 환경의 GPU 접근 오류와 시간 초과로 완료하지 못했습니다. 앞뒤 그림은 각각 생성했으므로')
    PAGE.write_text(html,encoding='utf-8')
    assert 'http://' not in html and 'https://' not in html
    count=html.count('data:image/png;base64,')
    assert count==48,count
    # 새 앞뒤 그림을 모두 같은 발선에 놓는다.
    contact=Image.new('RGBA',(64*4,96*2),(47,54,65,255))
    for i,kind in enumerate(NAMES):
        for j,view in enumerate(['back','front']):
            s=Image.open(OUT/(kind+'_'+view+'_64x96.png'))
            contact.alpha_composite(s,(64*i,96*j))
    contact.resize((1024,768),NEAR).save(OUT/'all_allies_4x.png')
    faces=Image.new('RGBA',(1024,320),(47,54,65,255))
    for i,kind in enumerate(NAMES):
        face=Image.open(OUT/(kind+'_front_64x96.png')).crop((24,36,40,56)).resize((256,320),NEAR)
        faces.alpha_composite(face,(256*i,0))
    faces.save(OUT/'faces_inspection_16x.png')
    print(json.dumps({'최종그림':8,'내장이미지':count,'페이지':str(PAGE),'검사':'규격·발선·알파·4배 일치 통과'},ensure_ascii=False))

if __name__=='__main__': run()
