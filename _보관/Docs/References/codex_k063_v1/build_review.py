from pathlib import Path
import base64, hashlib, json, shutil, colorsys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

# 그림 생성은 내장 이미지 도구로 수행하고, 여기서는 규격·팔레트·검토 자료만 정리한다.
ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
ART = ROOT / 'Game/Assets/_Project/Art/Characters/Static'
HERO = ROOT / 'Docs/References/codex_hero_octopath_review_v6/hero_idle_56x112.png'
NAMES = {'archer': '궁수', 'gunner': '총병', 'cannon_crew': '대포병', 'wizard': '마법사'}
ENTRIES = json.loads((OUT / '생성기록.json').read_text(encoding='utf-8'))
NEAR = Image.Resampling.NEAREST

def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def source_paths(kind):
    return [ART / ('wizard_back_static.png' if kind == 'wizard' else kind + '_static.png')] + ([ART / 'wizard_static.png'] if kind == 'wizard' else [])

def extract_palette(paths):
    # 완전 불투명한 실제 원본 색만 후보로 쓰고, 대표 색마다 원본 RGB로 되돌린다.
    colors = np.concatenate([np.asarray(Image.open(p).convert('RGBA')).reshape(-1, 4) for p in paths])
    colors = colors[colors[:, 3] >= 250, :3]
    groups = {}
    # 적은 면적의 보석·금속도 별도 색군으로 보존한다.
    for color in colors:
        r, g, b = [int(v) for v in color]
        h, s, v = colorsys.rgb_to_hsv(r/255, g/255, b/255)
        if v < .16:
            key = '외곽선'
        elif s < .18:
            key = '무채색금속'
        elif .49 <= h < .69 and s > .25:
            key = '파랑'
        elif .69 <= h < .92 and s > .29:
            key = '보라'
        elif .09 < h < .20 and s < .37 and v > .56:
            key = '크림'
        elif .09 < h < .20 and s >= .37:
            key = '황동'
        elif .025 < h <= .10 and v > .70 and s < .66:
            key = '피부'
        elif .65 <= h < .92:
            key = '검회색머리'
        else:
            key = '갈색'
        groups.setdefault(key, []).append((r, g, b))
    chosen = []
    for key, group in groups.items():
        count = 8 if key == '갈색' else 2 if key == '외곽선' else 4
        group = np.array(group, dtype=np.uint8)
        unique = np.unique(group, axis=0)
        if len(unique) <= count:
            chosen.extend(unique.tolist())
            continue
        reduced = Image.fromarray(group.reshape(1, -1, 3), 'RGB').quantize(colors=count, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
        centers = np.array(reduced.getpalette(), dtype=np.int32).reshape(-1, 3)[:count]
        diff = ((centers[:, None] - unique[None].astype(np.int32)) ** 2).sum(2)
        chosen.extend(unique[diff.argmin(1)].tolist())
    return np.unique(np.array(chosen, dtype=np.uint8), axis=0)

def normalize(path, anchor=512, palette=None):
    im = Image.open(path).convert('RGBA')
    arr = np.array(im)
    mask = arr[:, :, 3] >= 128
    ys, xs = np.where(mask)
    bounds = [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]
    crop = im.crop(bounds)
    width = round(crop.width * 96 / crop.height)
    tile = crop.resize((width, 96), NEAR)
    left = round(28 - (anchor - bounds[0]) * 96 / crop.height)
    if width > 54:
        raise ValueError(f'칸을 벗어남: {path}, {left}, {width}')
    left = max(1, min(55 - width, left))
    pixels = np.array(tile)
    opaque = pixels[:, :, 3] >= 128
    if palette is not None:
        rgb = pixels[:, :, :3].astype(np.int32)
        distances = ((rgb[:, :, None, :] - palette[None, None].astype(np.int32)) ** 2).sum(3)
        # 갈색 가죽이 황동색으로 바뀌지 않도록 색상각도 함께 비교한다.
        pixel_hsv = np.array([colorsys.rgb_to_hsv(*(c/255)) for c in rgb.reshape(-1, 3)]).reshape(rgb.shape)
        palette_hsv = np.array([colorsys.rgb_to_hsv(*(c/255)) for c in palette])
        hue_diff = np.abs(pixel_hsv[:, :, None, 0] - palette_hsv[None, None, :, 0])
        hue_diff = np.minimum(hue_diff, 1-hue_diff)
        chromatic = (pixel_hsv[:, :, None, 1] > .20) & (palette_hsv[None, None, :, 1] > .20)
        distances = distances + chromatic * hue_diff**2 * 1000000
        pixels[:, :, :3] = palette[distances.argmin(2)]
    pixels[:, :, 3] = opaque.astype('uint8') * 255
    pixels[~opaque] = 0
    result = Image.new('RGBA', (56, 112))
    result.paste(Image.fromarray(pixels), (left, 8))
    return result, bounds, left, width

palettes = {k: extract_palette(source_paths(k)) for k in NAMES}
palette_records = {}
for kind, pal in palettes.items():
    paths = source_paths(kind)
    palette_records[kind] = {'병종': NAMES[kind], '출처': [{'경로': str(p.relative_to(ROOT)).replace('\\', '/'), 'sha256': sha(p)} for p in paths], '색상': ['#%02x%02x%02x' % tuple(c) for c in pal]}
    swatch = Image.new('RGB', (len(pal) * 18, 36), '#202622')
    draw = ImageDraw.Draw(swatch)
    for i, c in enumerate(pal):
        draw.rectangle((i * 18, 0, i * 18 + 17, 35), fill=tuple(int(x) for x in c))
    swatch.save(OUT / (kind + '_palette.png'))
(OUT / '팔레트.json').write_text(json.dumps(palette_records, ensure_ascii=False, indent=2), encoding='utf-8')

records = []
for entry in ENTRIES:
    ident = entry['id']
    kind, view = ident.rsplit('_', 1)
    raw_path = OUT / (ident + '_generated.png')
    if not raw_path.exists():
        shutil.copy2(entry['source'], raw_path)
    im, bounds, left, width = normalize(raw_path, palette=palettes[kind])
    dest = OUT / (ident + '_56x112.png')
    im.save(dest)
    im.resize((224, 448), NEAR).save(OUT / (ident + '_4x.png'))
    arr = np.array(im)
    mask = arr[:, :, 3] == 255
    yy, xx = np.where(mask)
    used = np.unique(arr[mask, :3], axis=0)
    assert set(np.unique(arr[:, :, 3])) <= {0, 255}
    assert int(yy.max()) == 103 and int(yy.min()) == 8
    assert all(tuple(c) in {tuple(p) for p in palettes[kind]} for c in used)
    assert im.size == (56, 112)
    assert np.array_equal(np.asarray(Image.open(OUT / (ident + '_4x.png'))), np.repeat(np.repeat(arr, 4, 0), 4, 1))
    records.append({'파일': dest.name, '병종': NAMES[kind], '방향': '뒷모습' if view == 'back' else '앞모습', '칸': [56, 112], '발선_y': 104, '중앙_x': 28, '인물높이': 96, '생성원본_크롭': bounds, '가로배치': [left, width], '불투명영역': [int(xx.min()), int(yy.min()), int(xx.max()) + 1, int(yy.max()) + 1], '사용색수': len(used), '알파': [0, 255], '팔레트검사': True, '최근접4배검사': True, 'sha256': sha(dest)})

# 기존 그림은 같은 인물 높이·발선으로만 줄인다. 기존 팔레트나 외형은 바꾸지 않는다.
for kind in NAMES:
    p = source_paths(kind)[0]
    im = Image.open(p).convert('RGBA')
    arr = np.asarray(im)
    ys, xs = np.where(arr[:, :, 3] >= 128)
    bounds = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)
    crop = im.crop(bounds)
    width = round(crop.width * 96 / crop.height)
    height = 96
    if width > 56:
        height = round(96 * 56 / width)
        width = 56
    result = Image.new('RGBA', (56, 112))
    result.paste(crop.resize((width, height), NEAR), ((56 - width) // 2, 104 - height))
    result.save(OUT / (kind + '_previous_back.png'))
    if kind == 'wizard':
        p = ART / 'wizard_static.png'
        im = Image.open(p).convert('RGBA')
        box = im.getchannel('A').point(lambda a: 255 if a >= 128 else 0).getbbox()
        crop = im.crop(box)
        w = round(crop.width * 96 / crop.height)
        result = Image.new('RGBA', (56, 112))
        result.paste(crop.resize((w, 96), NEAR), ((56-w)//2, 8))
        result.save(OUT / 'wizard_previous_front.png')

shutil.copy2(HERO, OUT / 'hero_v6_reference.png')
report = {'검사': '8장 규격·발선·알파·실제 팔레트 소속·최근접 4배 일치', '결과': records, '원본유지': all(sha(ROOT / item['경로']) == item['sha256'] for p in palette_records.values() for item in p['출처']), '주의': ['이 검사는 시각적 사용자 승인이나 Unity 검증을 대신하지 않는다.', '1점 외곽선과 재질별 명암 품질은 확대 검토 대상이다.']}
(OUT / '검사결과.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')

def uri(path):
    return 'data:image/png;base64,' + base64.b64encode(Path(path).read_bytes()).decode()

def sprite(file, label, scale=4):
    return f'<figure><div class="cell" style="--s:{scale}"><img src="{uri(OUT / file)}" alt="{label}" width="{56*scale}" height="{112*scale}"></div><figcaption>{label}</figcaption></figure>'

sections = []
for kind, name in NAMES.items():
    chips = ''.join(f'<span title="{c}" style="background:{c}"></span>' for c in palette_records[kind]['색상'])
    old = sprite(kind + '_previous_back.png', '기존 뒷모습')
    oldfront = sprite('wizard_previous_front.png', '기존 앞모습') if kind == 'wizard' else ''
    sections.append(f'<section id="{kind}"><h2>{name}</h2><p>{ {"archer":"밝은 갈색 머리 · 남색 제복 · 활과 등 화살통", "gunner":"짙은 갈색 머리 · 회색 어깨 보호대 · 머스킷", "cannon_crew":"검회색 머리 · 가죽 멜빵 · 빈손 서기", "wizard":"밤색 머리 · 보라 로브 · 푸른 보석 지팡이"}[kind] }</p><h3>4배 · 발선 y=104</h3><div class="strip">{sprite("hero_v6_reference.png", "주인공 6판 · 기존 기준")}{old}{sprite(kind+"_back_56x112.png", "새 뒷모습 · 전투 기준")}{oldfront}{sprite(kind+"_front_56x112.png", "새 앞모습")}</div><h3>1배 · 동일 칸과 발선</h3><div class="strip small">{sprite("hero_v6_reference.png", "주인공 6판", 1)}{sprite(kind+"_previous_back.png", "기존 뒤", 1)}{sprite(kind+"_back_56x112.png", "새 뒤", 1)}{sprite(kind+"_front_56x112.png", "새 앞", 1)}</div><p class="muted">궁수·총병·대포병의 비교 원본은 기존 뒷모습 서기다. 앞모습을 기존 뒷모습과 같은 방향인 것처럼 표시하지 않는다.</p><h3>기존 실제 팔레트 · {len(palettes[kind])}색</h3><div class="chips">{chips}</div></section>')

game_sprites = [{'name':'주인공 6판','file':'hero_v6_reference.png'}] + [{'name':name+' '+('뒤' if view=='back' else '앞'), 'file':kind+'_'+view+'_56x112.png'} for view in ['back','front'] for kind,name in NAMES.items()]
game_html = ''.join(f'<div class="game-person"><img src="{uri(OUT / s["file"])}" alt="{s["name"]}"><span>{s["name"]}</span></div>' for s in game_sprites)
html = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-063 아군 서기 · 6판 방향</title><style>
*{box-sizing:border-box}body{margin:0;background:#141a18;color:#ece5d6;font:16px/1.65 "Malgun Gothic",sans-serif}main{max-width:1500px;margin:auto;padding:32px}h1{font-size:30px;margin:0}h2{font-size:26px;margin:0}h3{font-size:16px;color:#d7bb80;margin:22px 0 8px}p{max-width:1000px}a{color:#e2c48a}nav{display:flex;gap:24px;margin:20px 0}section{padding:30px 0;border-top:1px solid #435149}.strip{display:flex;gap:20px;align-items:flex-start;overflow:auto;padding-bottom:12px}.strip figure{flex:none;margin:0;text-align:center}.cell{position:relative;width:calc(56px * var(--s));height:calc(112px * var(--s));background-color:#27312e;background-image:linear-gradient(45deg,#ffffff07 25%,transparent 25%,transparent 75%,#ffffff07 75%),linear-gradient(45deg,#ffffff07 25%,transparent 25%,transparent 75%,#ffffff07 75%);background-size:16px 16px;background-position:0 0,8px 8px}.cell:after{content:"";position:absolute;left:0;right:0;top:calc(104px * var(--s));border-top:1px solid #ceaa68;pointer-events:none}.cell img{display:block;image-rendering:pixelated}figcaption{font-size:13px;margin-top:8px}.small figure{width:100px}.small .cell{margin:auto}.muted{font-size:13px;color:#b0bdb5}.chips{display:flex;flex-wrap:wrap;gap:3px}.chips span{width:24px;height:24px;border:1px solid #8886}.notice{border-left:3px solid #d7bb80;padding:10px 18px;background:#202b26}button,select{font:inherit;background:#2e3c34;color:#eee4cf;border:1px solid #7e8d82;padding:6px 14px}button{cursor:pointer}label{margin-right:15px}.game{position:relative;height:540px;width:960px;min-width:960px;background:linear-gradient(#394a43 0%,#60705b 55%,#66675c 55%,#777264 76%,#42443d 76%);overflow:hidden;margin-top:16px;border:1px solid #6b756b;--tile:81.9px}.game:after{content:"";font-size:0;position:absolute;left:0;right:0;top:76%;border-top:1px solid #c6b081}.game-row{display:flex;justify-content:space-evenly;position:absolute;left:0;right:0;bottom:calc(24% - var(--tile) * 8 / 112)}.game-person{display:flex;flex-direction:column;position:relative;align-items:center}.game-person img{height:var(--tile);width:calc(var(--tile) / 2);image-rendering:pixelated}.game-person span{position:absolute;top:100%;font-size:11px;white-space:nowrap;background:#18231ee0;padding:1px 3px}.game-wrap{overflow:auto}.legend{position:absolute;left:18px;top:16px;color:#fff6dc;font-size:13px}.light .cell{background:#d7d6ca}.dark .cell{background:#090d0b}footer{border-top:1px solid #435149;margin-top:30px;padding-top:15px;color:#b0bdb5}@media(max-width:700px){main{padding:18px}h1{font-size:24px}.strip{gap:12px}}
</style><main><h1>K-063 · 아군 서기 기준 그림</h1><p>궁수 · 총병 · 대포병 · 마법사 / 주인공 6판의 작은 머리·눈, 또렷한 몸통과 다리 방향을 적용한 검토안.</p><p class="notice">서기 8장만 제작했다. 주인공은 기존 6판을 비교용으로 표시한다. 동작 시트·노멀·게임 연결·실제 게임 캡처 검증은 다음 단계다.</p><p>새 그림: 56×112 · 인물 높이 96점 · 중앙 x=28 · 발선 y=104 · 알파 0/255. 원본 1배와 최근접 4배 PNG를 함께 보관했다.</p><nav><a href="#archer">궁수</a><a href="#gunner">총병</a><a href="#cannon_crew">대포병</a><a href="#wizard">마법사</a><a href="#game">화면 크기 비교</a></nav><label>투명 가장자리 보기 <select id="bg"><option value="">격자</option><option value="light">밝은 배경</option><option value="dark">어두운 배경</option></select></label>''' + ''.join(sections) + '''<section id="game"><h2>화면 높이의 13% · 크기 비교 목업</h2><p>인물 96점이 화면 높이의 13%가 되도록 계산했다. 아래 기본 화면은 960×540 상당, 인물 키 70.2화소다. 1080 높이로 바꾸면 인물 키 140.4화소다. 배경·배치는 가독성 비교용이며 실제 Unity 캡처가 아니다.</p><label>화면 높이 <select id="screen"><option value="540">540 · 인물 70.2화소</option><option value="1080">1080 · 인물 140.4화소</option></select></label><div class="game-wrap"><div class="game" id="stage"><div class="legend">인물 키 = 화면 높이 × 0.13 / 모두 같은 발선</div><div class="game-row">''' + game_html + '''</div></div></div></section><footer>제작: 내장 이미지 생성 도구 · 생성 및 수정 프롬프트는 생성기록.json에 보관. 최종 색은 기존 병종 시트에서 추출한 실제 RGB로 맞췄다. 모든 비교 이미지는 이 HTML 안에 내장되어 서버 없이 열린다. 사용자 외형 검토 대기.</footer></main><script>
document.getElementById('bg').addEventListener('change',e=>{document.body.className=e.target.value});
document.getElementById('screen').addEventListener('change',e=>{const h=Number(e.target.value),s=document.getElementById('stage');s.style.height=h+'px';s.style.minWidth=(h*16/9)+'px';s.style.width=(h*16/9)+'px';s.style.setProperty('--tile',(h*0.13*112/96)+'px')});
</script></html>'''
page = ROOT / '_HTML/4_그림_캐릭터/codex_K-063_아군6판방향_v1.html'
page.parent.mkdir(parents=True, exist_ok=True)
page.write_text(html, encoding='utf-8')

# 출력 PNG를 한눈에 검사할 수 있는 최근접 확대 접촉판.
font_path = Path('C:/Windows/Fonts/malgun.ttf')
font = ImageFont.truetype(str(font_path), 17)
sheet = Image.new('RGB', (9*232, 490), '#27312e')
draw = ImageDraw.Draw(sheet)
items = [('주인공 6판', 'hero_v6_reference.png')] + [(NAMES[e['id'].rsplit('_',1)[0]] + (' 뒤' if e['id'].endswith('back') else ' 앞'), e['id'] + '_56x112.png') for e in ENTRIES]
for i, (name, file) in enumerate(items):
    im = Image.open(OUT / file).convert('RGBA').resize((224, 448), NEAR)
    sheet.paste(im, (i*232, 0), im)
    draw.line((i*232, 416, i*232+223, 416), fill='#ceaa68')
    draw.text((i*232+10, 455), name, font=font, fill='#eee4cf')
sheet.save(OUT / '전체_4배_비교.png')
print(json.dumps({'완료':len(records), '원본유지':report['원본유지'], '검토페이지':str(page)}, ensure_ascii=False))
