from pathlib import Path
from PIL import Image
import base64, hashlib, io, json, shutil

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
PAGE = ROOT / '_HTML/4_그림_캐릭터/codex_K-071_스토리초상6판_v1.html'
NAMES = {'hero': '장교', 'wizard': '마법사', 'archer': '궁수'}
LABELS = {'neutral': '기본', 'determined': '결의', 'surprised': '놀람', 'tired': '피로', 'alarmed': '경계'}
ORDERS = {'hero': ['neutral','determined','surprised','tired'], 'wizard': ['neutral','determined','surprised','tired'], 'archer': ['neutral','alarmed']}
SOURCES = {'hero': 'Game/Assets/_Project/Art/Characters/Hero/hero_walk_run_grid.png', 'wizard': 'Game/Assets/_Project/Story/Resources/StoryArt/wizard_front_chestnut.png', 'archer': 'Game/Assets/_Project/Art/Characters/Static/archer_static.png'}


def components(im):
    alpha = im.getchannel('A')
    unseen = {(x,y) for y in range(im.height) for x in range(im.width) if alpha.getpixel((x,y))}
    found = []
    while unseen:
        first = unseen.pop()
        group, queue = [first], [first]
        while queue:
            x,y = queue.pop()
            for dx,dy in [(1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,1),(1,-1),(-1,-1)]:
                p = x+dx,y+dy
                if p in unseen:
                    unseen.remove(p)
                    queue.append(p)
                    group.append(p)
        found.append(group)
    return sorted(found, key=len, reverse=True)


def normalize(path, height):
    im = Image.open(path).convert('RGBA')
    im.putalpha(im.getchannel('A').point(lambda a: 255 if a >= 128 else 0))
    im = im.crop(im.getbbox())
    im = im.resize((round(im.width * height / im.height), height), Image.Resampling.NEAREST)
    # 생성 가장자리의 고립된 점만 정리하고 얼굴과 의상은 다시 그리지 않는다.
    groups = components(im)
    for group in groups[1:]:
        if len(group) <= 3:
            for p in group:
                im.putpixel(p,(0,0,0,0))
    feet = im.crop((0,height-6,im.width,height)).getbbox()
    foot_mid = (feet[0]+feet[2])/2
    x = round(32-foot_mid)
    assert x >= 0 and x+im.width <= 64, (path, x, im.size)
    canvas = Image.new('RGBA',(64,96))
    canvas.paste(im,(x,88-height))
    # 투명한 점의 색상도 비워 보간 시 색 번짐을 막는다.
    for yy in range(96):
        for xx in range(64):
            if canvas.getpixel((xx,yy))[3] == 0:
                canvas.putpixel((xx,yy),(0,0,0,0))
    return canvas


def uri(im):
    buf = io.BytesIO()
    im.save(buf,format='PNG')
    return 'data:image/png;base64,'+base64.b64encode(buf.getvalue()).decode()


def build():
    jobs = json.loads((OUT/'generation_manifest.json').read_text(encoding='utf-8'))
    records, figures, old = [], {}, {}
    for ident, source in SOURCES.items():
        original = Image.open(ROOT/source).convert('RGBA')
        old[ident] = original.crop((0,1152,256,1536)) if ident == 'hero' else original
        old[ident].save(OUT/f'current_{ident}.png')
        for expr in ORDERS[ident]:
            key = f'{ident}_{expr}'
            job = jobs[key]
            local = OUT/f'{key}_generated.png'
            if Path(job['source']).exists():
                shutil.copyfile(job['source'],local)
            assert local.exists(), local
            native = normalize(local, {'hero':76,'wizard':74,'archer':75}[ident])
            native.save(OUT/f'{key}_64x96.png')
            full = native.resize((256,384),Image.Resampling.NEAREST)
            full.save(OUT/f'{key}.png')
            full.resize((1024,1536),Image.Resampling.NEAREST).save(OUT/f'{key}_4x.png')
            figures[key] = full
            groups = components(native)
            bbox = full.getbbox()
            assert bbox[3] == 352
            assert set(full.getchannel('A').get_flattened_data()) == {0,255}
            assert len(groups) == 1, (key,[len(g) for g in groups])
            assert full.resize((64,96),Image.Resampling.NEAREST).tobytes() == native.tobytes()
            foot_box = full.crop((0,328,256,352)).getbbox()
            records.append({'speaker':NAMES[ident], 'expression':expr,'file':f'{key}.png','size':[256,384], 'native_size':[64,96], 'alpha_bbox':list(bbox),'foot_pivot_top_left':[128,352],'boot_region_bbox_center_x':(foot_box[0]+foot_box[2])/2,'connected_components':len(groups),'frame_order':[expr]})
        strip = Image.new('RGBA',(256*len(ORDERS[ident]),384))
        for i,expr in enumerate(ORDERS[ident]):
            strip.paste(figures[f'{ident}_{expr}'],(i*256,0))
        strip.save(OUT/f'{ident}_expressions.png')
        strip.resize((strip.width*4,1536),Image.Resampling.NEAREST).save(OUT/f'{ident}_expressions_4x.png')
    figures['wizard_neutral'].save(OUT/'wizard_front_chestnut.png')
    figures['archer_neutral'].save(OUT/'archer_static.png')
    # 전투 동작은 보존한다. 이야기에서 읽는 12번 칸만 바꾼 별도 복사본이다.
    original_hero = Image.open(ROOT/SOURCES['hero']).convert('RGBA')
    hero_copy = original_hero.copy()
    hero_copy.paste(figures['hero_neutral'],(0,1152))
    hero_copy.save(OUT/'hero_walk_run_grid.png')
    for cell in range(16):
        x,y = cell%4*256,cell//4*384
        if cell != 12:
            assert original_hero.crop((x,y,x+256,y+384)).tobytes() == hero_copy.crop((x,y,x+256,y+384)).tobytes()
    manifest = {'card':'K-071','cell_size':[256,384],'native_size':[64,96],'pivot_top_left':[128,352],'screen_rect_pivot':[0.5,0],'screen_rect_size':[512,768],'screen_anchors':[[0.25,0],[0.75,0]],'screen_offset_y':140,'expressions':ORDERS,'portraits':records,'source_files':SOURCES,'source_cells':{'hero':12,'archer':0,'wizard':0},'swaps':[{'file':'hero_walk_run_grid.png','size':[1024,1536],'columns':4,'rows':4,'frame_count':16,'changed_cells':[12],'warning':'Story-only copy; other 15 battle cells retained byte-for-byte. Do not overwrite the shared battle sheet.'},{'file':'archer_static.png','size':[256,384],'frame_count':1},{'file':'wizard_front_chestnut.png','size':[256,384],'frame_count':1}]}
    manifest['expression_sheets'] = [{'file':f'{ident}_expressions.png','size':[256*len(order),384],'frame_count':len(order),'frame_order':order,'pivot_per_cell_top_left':[128,352],'preview_4x_size':[1024*len(order),1536]} for ident,order in ORDERS.items()]
    manifest['direction_references'] = ['Docs/References/codex_hero_octopath_review_v6/generated.png','Docs/References/codex_k065_v1/wizard_front_4x.png','Docs/References/codex_k065_v1/archer_front_4x.png']
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    cards = []
    for ident in NAMES:
        frames = [uri(figures[f'{ident}_{e}']) for e in ORDERS[ident]]
        choices = ''.join(f'<button data-index="{i}">{LABELS[e]}</button>' for i,e in enumerate(ORDERS[ident]))
        cards.append(f'''<section data-frames='{json.dumps(frames)}' data-labels='{json.dumps([LABELS[e] for e in ORDERS[ident]],ensure_ascii=False)}'><h2>{NAMES[ident]}</h2><div class="compare"><figure><figcaption>현재 게임 · {12 if ident=='hero' else 0}번 칸</figcaption><div class="canvas"><img src="{uri(old[ident])}" alt="현재 게임 {NAMES[ident]}"></div></figure><figure><figcaption>새 초상 · <span class="expression">기본</span></figcaption><div class="canvas"><img class="animated" src="{frames[0]}" alt="새 {NAMES[ident]} 표정"></div></figure></div><div class="controls">{choices}<button class="toggle">자동 넘김 멈춤</button></div><details><summary>모든 표정 펼치기</summary><div class="all">{''.join(f'<figure><img src="{frames[i]}" alt="{NAMES[ident]} {LABELS[e]}"><figcaption>{LABELS[e]}</figcaption></figure>' for i,e in enumerate(ORDERS[ident]))}</div></details></section>''')
    html = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>K-071 이야기 초상 6판</title><style>*{box-sizing:border-box}body{margin:0;background:#171b22;color:#eee6d6;font:16px/1.65 system-ui,sans-serif}main{max-width:1120px;margin:auto;padding:32px}h1{font-size:30px}h2{color:#e7bf72}p{max-width:900px}section{padding:20px 0;border-top:1px solid #666}.compare{display:flex;flex-wrap:wrap;gap:32px}figure{margin:0}figcaption{padding:8px 0}.canvas{position:relative;width:256px;height:384px;background:repeating-conic-gradient(#272d37 0 25%,#343c47 0 50%) 0/24px 24px}.canvas:after{content:'';position:absolute;left:0;right:0;top:352px;border-top:1px dashed #bd9656}.canvas img{width:256px;height:384px;image-rendering:pixelated}button{padding:9px 14px;margin:10px 6px 10px 0;background:#35302b;color:#f7e9c9;border:1px solid #a88b5b;cursor:pointer}button[aria-pressed=true]{background:#7b5e31}.all{display:flex;flex-wrap:wrap;gap:20px}.all img{width:256px;height:384px;image-rendering:pixelated}details{margin-top:16px}aside{border-left:3px solid #d1a45b;padding:8px 20px;background:#242932}code{overflow-wrap:anywhere}a{color:#e7bf72}</style><main><h1>이야기 초상 · 주인공 6판 방향</h1><p>장교 4표정 · 마법사 4표정 · 궁수 2표정. 흰 머리 장교, 밤색 머리와 보라 로브 마법사, 밝은 갈색 머리와 남색·크림 제복 궁수의 정체성을 유지했습니다. 처음 생성한 주인공 6판의 큰 머리·파란 눈·굵은 점을 기준으로 만들었습니다.</p><aside><p>이 화면은 에셋 검토용이며 실제 게임 캡처가 아닙니다. 256×384 칸 · 중심 128 · 발선 352. 원래 화면처럼 전신을 유지했습니다. 표정은 1.2초마다 바뀝니다. 단추를 누르면 해당 표정에 멈춥니다.</p><p>장교는 현재 전투 시트 12번 칸을 재사용합니다. 제공한 같은 이름의 복사본은 그 칸만 교체했으므로 <strong>이야기 전용으로 연결</strong>해야 합니다. 전투 공유 파일에 덮어쓰지 마세요. 새 표정 파일은 0번 칸입니다. 궁수와 마법사 기본 파일은 기존 파일명을 함께 제공합니다.</p></aside>''' + ''.join(cards) + '''<p>내레이션(빈 화자)은 초상이 없습니다. 총병·대포병은 현재 이야기 데이터의 화자가 아니므로 추가하지 않았습니다. Game·이야기 데이터·기존 참고 자료는 수정하지 않았습니다. 사용자 외형 검토와 Claude의 게임 연결이 남아 있습니다.</p><script>document.querySelectorAll('section').forEach(s=>{const frames=JSON.parse(s.dataset.frames),labels=JSON.parse(s.dataset.labels);let i=0,playing=true;const show=()=>{s.querySelector('.animated').src=frames[i];s.querySelector('.expression').textContent=labels[i];s.querySelectorAll('[data-index]').forEach(b=>b.setAttribute('aria-pressed',+b.dataset.index===i));};s.querySelectorAll('[data-index]').forEach(b=>b.onclick=()=>{i=+b.dataset.index;playing=false;s.querySelector('.toggle').textContent='자동 넘김 시작';show();});s.querySelector('.toggle').onclick=()=>{playing=!playing;s.querySelector('.toggle').textContent=playing?'자동 넘김 멈춤':'자동 넘김 시작';};setInterval(()=>{if(playing){i=(i+1)%frames.length;show();}},1200);show();});</script></main></html>'''
    PAGE.parent.mkdir(parents=True,exist_ok=True)
    PAGE.write_text(html,encoding='utf-8')
    before = json.loads((OUT/'source_hashes_before.json').read_text(encoding='utf-8'))
    unchanged = all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==sha for p,sha in before.items())
    assert unchanged
    # 내장 PNG를 전부 디코딩해 파일 없이도 검토 화면의 그림이 열리는지 확인한다.
    import re
    embedded = re.findall(r'data:image/png;base64,([A-Za-z0-9+/=]+)',html)
    for encoded in embedded:
        check = Image.open(io.BytesIO(base64.b64decode(encoded)))
        check.load()
        assert check.size == (256,384)
    validation = {'portraits':len(records),'alpha_binary':True,'footline_all':352,'native_integer_scale':4,'stray_component_check':'all native portraits have exactly one 8-connected alpha component','source_files_unchanged':len(before),'hero_other_cells_unchanged':15,'embedded_png_decoded':len(embedded),'external_dependencies':0,'browser_render_verified':False,'unity_run':False}
    (OUT/'validation.json').write_text(json.dumps(validation,indent=2),encoding='utf-8')
    overview = Image.new('RGBA',(256*4,384*3))
    for row,ident in enumerate(NAMES):
        for col,expr in enumerate(ORDERS[ident]):
            overview.paste(figures[f'{ident}_{expr}'],(col*256,row*384))
    overview.save(OUT/'all_portraits.png')
    print(json.dumps(validation,ensure_ascii=False))


if __name__ == '__main__':
    build()
