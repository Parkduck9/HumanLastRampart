from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np
import base64, hashlib, json, re, io

# 생성 원안의 최근접 도트화와 요청된 비교 합성을 재현한다.
P = Path(__file__).resolve().parent
R = P.parents[2]
NN = Image.Resampling.NEAREST
PAGE = R / '_HTML/4_그림_캐릭터/codex_K-070_인물크기비교_v1.html'
HERO = R / 'Docs/References/codex_hero_octopath_review_v6/generated.png'
SHOT = R / 'Docs/References/codex_k068_v1/input/01_pf_day_clean.png'

def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def solid(im):
    a = np.array(im.convert('RGBA'))
    a[a[:,:,3] < 128] = 0
    a[a[:,:,3] >= 128,3] = 255
    return Image.fromarray(a)
def save(im, name):
    im.save(P / (name+'.png'))
    im.resize((im.width*4, im.height*4), NN).save(P / (name+'_4x.png'))
def place(im, height, size=(96,128), foot=120, axis=None):
    box = im.getbbox()
    k = height/(box[3]-box[1])
    crop = im.crop(box).resize((round((box[2]-box[0])*k),height), NN)
    axis = (box[0]+box[2])/2 if axis is None else axis
    x = round(size[0]/2-(axis-box[0])*k)
    assert x>=0 and x+crop.width<=size[0]
    out=Image.new('RGBA',size)
    out.paste(crop,(x,foot-height))
    return out
def recolor(im, pal):
    a=np.array(im)
    colors=np.array(pal,dtype=np.int32)
    rgb=a[:,:,:3].astype(np.int32)
    dist=((rgb[:,:,None]-colors[None,None])**2 * np.array([2,3,2])).sum(3)
    a[:,:,:3]=colors[dist.argmin(2)]
    a[a[:,:,3]==0]=0
    return Image.fromarray(a)
def inline(p): return 'data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode()
def image_tag(name, alt, cls='', width=None):
    p=P/(name+'.png'); im=Image.open(p)
    return f'<img class="{cls}" alt="{alt}" width="{width or im.width}" height="{round(im.height*(width or im.width)/im.width)}" src="{inline(p)}">'

def run():
    cfg=json.loads((P/'generation.json').read_text(encoding='utf-8'))
    records=[]
    for name,c in cfg.items():
        src=solid(Image.open(P/(name+'_generated.png')))
        old=solid(Image.open(R/c['old']))
        pal=sorted({p[:3] for p in old.getdata() if p[3]})
        out=recolor(place(src,c['height'],axis=c['axis']),pal)
        save(out,name+'_96x128')
        save(place(old,old.getbbox()[3]-old.getbbox()[1]),name+'_old_cell')
        b=src.getbbox()
        records.append({'name':name,'cell':[96,128],'foot_bottom_exclusive':120,'body_axis_x':48,
            'figure_height':c['height'],'source_bbox':list(b),'source_axis':c['axis'],
            'head_height_estimate':round((c['chin_y']-b[1])*c['height']/(b[3]-b[1])),
            'palette_colors':len(pal),'opaque_bbox':list(out.getbbox()),'sha256':digest(P/(name+'_96x128.png'))})
    old_goblin=solid(Image.open(R/cfg['goblin_front']['old']))
    goblin_pal=sorted({p[:3] for p in old_goblin.getdata() if p[3]})
    species=recolor(place(solid(Image.open(P/'goblin_species56_generated.png')),56,axis=555),goblin_pal)
    save(species,'goblin_species56_96x128')
    # 원본 전체를 약16화소 간격으로 표본화한 뒤 발선만 옮긴다.
    hero=solid(Image.open(HERO).resize((64,96),NN))
    hb=hero.getbbox(); h=hb[3]-hb[1]
    hero=place(hero,h)
    save(hero,'hero_native_96x128')
    assert h==73
    for name in cfg:
        board=Image.new('RGBA',(480,128),(27,32,43,255))
        draw=ImageDraw.Draw(board)
        for i in range(3):
            draw.rectangle((i*160+2,4,i*160+157,125),outline=(70,78,94))
        for i,im in enumerate([Image.open(P/(name+'_old_cell.png')),Image.open(P/(name+'_96x128.png')),hero]):
            # 동일 화면 키73px. 1배는 이 비교판 자체의 화면 배율이다.
            item=place(im,73,(160,128),110)
            board.alpha_composite(item,(160*i,0))
        draw.line((0,110,479,110),fill=(116,144,157),width=1)
        save(board,'board_'+name)
    shot=Image.open(SHOT).convert('RGBA')
    assert shot.size==(1920,1080)
    cropbox=(560,200,1360,600)
    crop=shot.crop(cropbox)
    crop.save(P/'screenshot_crop_before.png')
    mocks=[]
    # 같은 배경·위치·화면 키를 유지한다. 기존 병사를 지우거나 배경을 보정하지 않는다.
    positions=[('archer_back',70,90),('archer_back',80,210),('archer_back',90,330),
               ('archer_front',80,465),('goblin_front',round(80*40/52),595)]
    for version in ['old','native']:
        mock=crop.copy()
        for name,height,x in positions:
            path=P/(name+('_old_cell.png' if version=='old' else '_96x128.png'))
            if name=='goblin_front' and version=='native': path=P/'goblin_species56_96x128.png'
            im=Image.open(path).convert('RGBA'); box=im.getbbox()
            k=height/(box[3]-box[1])
            sprite=im.crop(box).resize((round((box[2]-box[0])*k),height),NN)
            mock.alpha_composite(sprite,(x-sprite.width//2,355-height))
            mocks.append({'version':version,'subject':name,'screen_height':height,'scale':k,
                'resampling':'nearest; fractional pixel repetition' if k!=int(k) else 'nearest; integer',
                'screenshot_foot_xy':[cropbox[0]+x,cropbox[1]+355]})
        ref=place(hero,80,(96,100),90).crop((0,0,96,90))
        mock.alpha_composite(ref,(690,265))
        mock.save(P/('mock_'+version+'.png'))
    # 정수 확대의 도트 보존을 따로 확인할 수 있는 원래 칸 비교다.
    native_board=Image.new('RGBA',(480,128),(27,32,43,255))
    for i,name in enumerate(['archer_back','archer_front','goblin_front']):
        native_board.alpha_composite(Image.open(P/(name+'_96x128.png')),(96*i,0))
    native_board.alpha_composite(species,(288,0))
    native_board.alpha_composite(hero,(384,0))
    save(native_board,'native_lineup')

    sections=[]
    titles={'archer_back':'궁수 · 후면 대기','archer_front':'궁수 · 정면 대기','goblin_front':'고블린 · 정면 대기'}
    for name in cfg:
        small=image_tag('board_'+name,titles[name]+' 같은 화면 키 73화소 비교','board one')
        big=image_tag('board_'+name+'_4x',titles[name]+' 비교판 최근접 4배','board four')
        note='왼쪽은 인간52px 기준에서 키40px였던 기존 고블린입니다. 가운데는 요청대로 키73px로 새로 그린 비교용 고블린입니다. 세 인물 모두 화면 키73px로 표시합니다. 실전 목업에는 종족 비율을 유지하는 별도56px 시안을 사용합니다.' if name=='goblin_front' else '왼쪽52px도73px로 늘려 보입니다. 가운데와 오른쪽은73px이며, 세 인물의 화면 키와 발선이 같습니다.'
        sections.append(f'<section><h2>{titles[name]}</h2><p>{note}</p><div class="scroll"><div class="labels"><b>52px 기준 · 기존</b><b>73px 기준 · 새 그림</b><b>선택 영웅 · 고유 격자 추정</b></div>{small}{big}</div></section>')
    page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-070 인물 크기 비교</title><style>
    *{box-sizing:border-box}body{margin:0;background:#10151e;color:#e9edf4;font:16px/1.7 system-ui,'Malgun Gothic',sans-serif}main{max-width:1180px;margin:auto;padding:28px}h1{font-size:34px;line-height:1.3}h2{font-size:23px;margin:0 0 10px}h3{font-size:18px}p{color:#bdc9d9}section,.intro{background:#1b222e;border:1px solid #354258;border-radius:14px;padding:24px;margin:22px 0}.recommend{border-left:5px solid #e8bb6a;padding:8px 20px;background:#292b2c}.recommend strong{color:#ffd48a}button{font:inherit;border:1px solid #64748b;background:#303e52;color:white;padding:9px 20px;border-radius:8px;cursor:pointer}button[aria-pressed=true]{background:#d6ad65;color:#161b24;border-color:#f4cd8d}.toolbar{position:sticky;top:0;z-index:2;background:#10151ef2;padding:12px 0}.scroll{overflow:auto}.labels{display:grid;grid-template-columns:repeat(3,160px);width:480px;text-align:center;font-size:12px}.board{display:block;max-width:none;image-rendering:pixelated}.four{display:none}body.big .one{display:none}body.big .four{display:block}body.big .labels{width:1920px;grid-template-columns:repeat(3,640px);font-size:16px}.fixed img{display:block;max-width:none;image-rendering:pixelated}.caption{font-size:14px;color:#c1cad9}table{width:100%;border-collapse:collapse;min-width:630px}td,th{padding:11px;text-align:left;border-bottom:1px solid #465265}th{color:#f1cc8c}a{color:#e1c590}.tag{color:#e1bc7d;font-size:13px;letter-spacing:.12em}details{margin:20px 0}summary{cursor:pointer;color:#e7c98e}@media(max-width:600px){main{padding:14px}h1{font-size:26px}section,.intro{padding:16px}}
    </style><main><div class="tag">HUMAN LAST RAMPART / K-070 / 제작 규격 선택 자료</div><h1>52px vs 약73px<br>같은 크기로 보이면, 어느 쪽이 좋은가?</h1>
    <div class="intro"><div class="recommend"><strong>추천: 인간 약73px · 96×128칸</strong><p>선택한 영웅의 약24px 머리와 큰 홍채를 가장 자연스럽게 이어갑니다. 전투에서70~90px로 보일 때 원본 한 점이 화면의 약 한 점이어서, 52px 그림을 약1.35~1.73배 늘릴 때보다 눈·머리·옷깃의 형태를 더 많이 남길 수 있습니다. 외곽선과 색 덩어리를 단순하게 유지하는 조건으로73px를 권합니다.</p></div><p>선택 영웅 generated.png의 강한 색 경계는 원본16~17화소 간격입니다. 1024×1536 원본 전체를 최근접64×96으로 표본화하면 인물 약73px, 머리 약24px가 됩니다. 생성 그림이라 완벽한 균일 격자를 복원한 수치는 아닙니다. 52px는 기존 카드가 지정한 제작 키였습니다.</p><p>주 비교용3장은 모두 인물 키73px로 새로 그린 정지 시안입니다. 고블린은 종족 비율을 위한56px 보조 시안도 제공합니다. 52px 그림을 단순 확대한 결과가 아닙니다. 갈색 머리·남색/크림 제복·갈색 가죽과 활, 고블린의 올리브 피부·황색 눈·곤봉을 유지했습니다.</p></div>
    <div class="toolbar"><button id="one" aria-pressed="true">1배 · 화면 키73px</button> <button id="four" aria-pressed="false">4배 · 비교판 확대</button><div class="caption">브라우저 확대100% 기준 CSS 화소. 1배는 원본 점1:1이라는 뜻이 아닙니다. 작은 화면은 그림 영역을 가로로 넘겨 보세요.</div></div>
    '''+''.join(sections)+'''
    <section><h2>실제 전투 배경 · 70 / 80 / 90px</h2><p>아래 두 장은 실제1920×1080 캡처의 같은800×400 영역을 잘라, 같은 위치에 두 규격을 각각 합성했습니다. 병사 폭약0.35m라는 게임 기준과 카드에서 제시한 화면 키70~90px를 비교 조건으로 사용했습니다. 이 캡처에서 직접 그 키를 측정한 것은 아닙니다. 월드 좌표·카메라 투영을 재현한 결과는 아닙니다. 가독성을 분리해 보기 위해 성벽 바로 앞 빈 땅에 표본을 놓았습니다.</p><p>왼쪽부터 후면궁수70·80·90px / 정면궁수80px / 고블린62px / 선택영웅80px입니다. 고블린은 인간의약77% 키를 유지합니다. 배경은 지우거나 밝히지 않았습니다.</p><h3>52px 기준 · 기존 그림</h3><div class="scroll fixed">'''+image_tag('mock_old','기존52px 기준을 실제 화면 키70 80 90px로 합성')+'''</div><h3>73px 기준 · 새 그림</h3><div class="scroll fixed">'''+image_tag('mock_native','새73px 기준을 같은 화면 키70 80 90px로 합성')+'''</div><p class="caption">모든 삽입은 최근접 보간입니다. 위 목표 키에서는 배율이 정수가 아니므로 어떤 원본 점은 화면1점, 어떤 점은2점으로 반복됩니다. 부드럽게 흐리는 보간은 사용하지 않았습니다. 73px도70~90px에서는 완전한 정수 배율이 아닙니다. 위 1배/4배 버튼은 앞의 비교판3개만 바꾸며, 이 목업은 항상 캡처1화소=화면1화소로 표시합니다.</p><details><summary>합성 전 같은 영역</summary><div class="scroll fixed">'''+image_tag('screenshot_crop_before','합성 전 실제 전투 캡처 영역')+'''</div><p class="caption">출처: Docs/References/codex_k068_v1/input/01_pf_day_clean.png · 잘라낸 범위 x560~1360, y200~600. Game/playprobe/portfolio에는 사용 가능한 캡처가 없었습니다.</p></details></section>
    <section><h2>동작 시트 제작 비용</h2><div class="scroll"><table><tr><th>항목</th><th>52px 기준</th><th>73px 기준</th></tr><tr><td>인간 / 머리</td><td>52 / 약17px</td><td>73 / 약24px</td></tr><tr><td>칸 · 축 / 발 아래 경계</td><td>64×96 · x32 / y88</td><td>96×128 · x48 / y120</td></tr><tr><td>고블린 키</td><td>40px</td><td>비교용73px / 종족 비율용56px (인간 대비 약77%)</td></tr><tr><td>칸당 화소 / RGBA32</td><td>6,144 / 24KiB</td><td>12,288 / 48KiB · 정확히2배</td></tr><tr><td>가로6칸 예시</td><td>384×96 / 144KiB</td><td>576×128 / 288KiB</td></tr><tr><td>가로6×세로4칸 예시</td><td>384×384 / 576KiB</td><td>576×512 / 1,152KiB</td></tr><tr><td>동작 수 · 프레임 수</td><td colspan="2">키 변경만으로 늘지 않습니다. 예: 사격6칸은 그대로6칸. 이번 카드는 대기3장만 제작했습니다.</td></tr></table></div><p>메모리는 압축·여백·밉맵 없는 RGBA32 계산입니다. 밉맵 전체를 넣으면 대략4/3배, 같은 크기의 노멀맵도 만들면 그만큼 추가됩니다. 압축 포맷과 아틀라스 배치는 실제 용량을 바꿉니다. 인물 수 자체는 공유 텍스처의 용량을 배수로 늘리지 않습니다.</p><p>작화 면적은 키 비율의 제곱으로 약1.97배지만, 작업 시간이 정확히2배라는 뜻은 아닙니다. 기존52px 동작은 확대 후 눈·손·외곽선·무기 접점을 다시 그려야 합니다. 칸 크기·잘라내기·피벗·무기 여백도 함께 점검해야 합니다. 현재 게임 계약에 적힌256×384칸과의 연결은 별도 결정이며, 이 비교 자료가 가져오기 규격을 바꾸지는 않습니다.</p><p><strong>52px를 고를 이유:</strong> 적은 점으로 실루엣을 빨리 만들고 전체 병종과 동작을 통일하기 쉽습니다. 작은 얼굴의 미세한 표현보다 빠른 동작 제작을 우선한다면 합리적입니다. 다만 같은 화면 키에서는 눈과 옷깃이 더 거칠게 읽힙니다.</p><p><strong>73px를 고를 이유:</strong> 이미 선택한 영웅의 점 밀도와 눈 표현을 유지하기 좋습니다. 이번 비교에서는 그 장점이 제작 비용 증가보다 중요하다고 판단했습니다. 최종 규격은 사용자 선택 후 확정합니다.</p></section>
    <section><h2>원래 점과 칸 확인</h2><p>아래는 규격을 섞어 같은 키로 맞추지 않은 최근접4배입니다. 왼쪽부터 궁수73px 두 장 · 비교용 고블린73px · 종족 비율용 고블린56px · 영웅73px. 공통96×128칸, 중심x48, 발선y120입니다. 새 궁수 머리는 육안 표식상 약25px, 비교용 고블린은 약29px로 영웅 약24px와는 차이가 있습니다.</p><div class="scroll fixed">'''+image_tag('native_lineup_4x','새 궁수 후면 정면 고블린과 선택영웅의 원래 격자 4배')+'''</div><p class="caption">앞뒤는 각각 생성한 정지 시안이라 머리 가닥·옷 주름이 동작 시트처럼 완전히 일치하지 않습니다. 머리 높이는 육안 턱 표식을 사용한 근삿값입니다. 단일점 외곽선은 목표이며 겹친 장비·머리 안쪽은 더 두꺼운 어두운 덩어리가 있습니다.</p></section><p class="caption">내장 이미지 생성 도구로 원안 제작 → 알파128 기준 정리 → 최근접 도트화 → 기존 그림 팔레트 대응. Unity 실행·게임 연결·실제 플레이 검증은 수행하지 않았습니다. 이 페이지는 모든 그림을 내장해 서버 없이 열립니다.</p></main><script>for(const [id,big] of [['one',false],['four',true]])document.getElementById(id).onclick=()=>{document.body.classList.toggle('big',big);document.getElementById('one').setAttribute('aria-pressed',String(!big));document.getElementById('four').setAttribute('aria-pressed',String(big));};</script></html>'''
    PAGE.parent.mkdir(parents=True,exist_ok=True)
    page=page.replace('</style>','section{scroll-margin-top:130px}</style>')
    PAGE.write_text(page,encoding='utf-8')
    checks=[]
    for rec in records:
        name=rec['name']; im=Image.open(P/(name+'_96x128.png'))
        assert im.size==(96,128) and im.getbbox()[3]==120
        assert im.getbbox()[3]-im.getbbox()[1]==rec['figure_height']
        assert set(im.getchannel('A').getdata()) <= {0,255}
        big=Image.open(P/(name+'_96x128_4x.png'))
        assert big.tobytes()==im.resize(big.size,NN).tobytes()
        pal={p[:3] for p in Image.open(R/cfg[name]['old']).convert('RGBA').getdata() if p[3]>=128}
        assert all(p[:3] in pal for p in im.getdata() if p[3])
        for item in [Image.open(P/(name+'_old_cell.png')),im,hero]:
            bb=place(item,73,(160,128),110).getbbox()
            assert bb[3]==110 and bb[3]-bb[1]==73
        checks.append(name+': 칸·발선·키·이진알파·정수4배 확인')
    assert species.size==(96,128) and species.getbbox()[3]==120 and species.getbbox()[1]==64
    assert set(species.getchannel('A').getdata()) <= {0,255}
    assert Image.open(P/'goblin_species56_96x128_4x.png').tobytes()==species.resize((384,512),NN).tobytes()
    embeds=re.findall(r'src="data:image/png;base64,([^"]+)"',page)
    for b64 in embeds: Image.open(io.BytesIO(base64.b64decode(b64))).verify()
    assert not re.search(r'(?:src|href)="https?://',page)
    hashes=json.loads((P/'source_hashes_before.json').read_text(encoding='utf-8'))
    changed=[p for p,v in hashes.items() if not (R/p).exists() or digest(R/p)!=v]
    assert not changed,changed
    report={'sprites':records,'species_variant':{'file':'goblin_species56_96x128.png','figure_height':56,'head_height_estimate':22,'cell':[96,128],'foot_bottom_exclusive':120},'hero_native_height':h,'screenshot_size':list(shot.size),
        'crop':list(cropbox),'mock_samples':mocks,'inline_images_decoded':len(embeds),
        'source_hashes_unchanged':len(hashes),'checks':checks,'browser_validation':'별도 review_validation.json 참조'}
    (P/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'sprites':records,'inline_images':len(embeds),'unchanged_sources':len(hashes)},ensure_ascii=False))

if __name__=='__main__': run()
