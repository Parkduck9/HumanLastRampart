"""생성 원안의 격자 포장·노멀 데이터 인코딩·독립 검토 페이지 제작."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np
import json, hashlib, base64, sys
sys.dont_write_bytecode = True
from components import components

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
ART = ROOT / 'Game/Assets/_Project/Art/Characters'
ITEMS = [
    ('archer_fall', '궁수 쓰러짐', 'Static/archer_static.png', 4, 6),
    ('gunner_fall', '총병 쓰러짐', 'Static/gunner_static.png', 4, 6),
    ('cannon_crew_fall', '대포병 쓰러짐', 'Static/cannon_crew_static.png', 4, 6),
    ('wizard_cast', '마법사 후면 시전', 'Static/wizard_back_static.png', 4, 6),
    ('hero_command', '주인공 후면 명령', 'Hero/hero_back_idle.png', 3, 6),
]

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def dump(name, value):
    (HERE / name).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')

def uri(path):
    return 'data:image/png;base64,' + base64.b64encode(path.read_bytes()).decode()

def pack(ident, label, ref, count, fps):
    source = HERE / 'source' / (ident + '.png')
    raw = Image.open(source).convert('RGBA')
    # 알파 128 이하는 기술적으로 비우고, 원안은 별도로 그대로 보관한다.
    alpha = raw.getchannel('A').point(lambda a: 255 if a > 128 else 0)
    raw.putalpha(alpha)
    labels, groups = components(alpha)
    groups = sorted(groups[:count],key=lambda a:a['bbox'][0])
    assert len(groups)==count
    parts = []
    for group in groups:
        box=group['bbox']
        isolated=raw.copy()
        isolated.putalpha(Image.fromarray(np.uint8(labels==group['index'])*255))
        parts.append((isolated.crop(box),box))
    # 서 있는 포즈의 발 중심은 유지하고, 쓰러짐은 포즈 중심으로 바닥에 정렬한다.
    anchors=[]
    for part,_ in parts:
        if ident in ('wizard_cast','hero_command'):
            ys,xs=np.where(np.asarray(part.getchannel('A'))[-12:]>128)
            anchors.append(float(np.median(xs)))
        else:
            anchors.append(part.width/2)
    top_limit=342 if ident=='wizard_cast' else 300
    extent=max(max(anchor,part.width-anchor) for (part,_),anchor in zip(parts,anchors))
    scale = min(top_limit / max(p.height for p, _ in parts), 124 / extent)
    sheet = Image.new('RGBA', (256 * count, 384))
    frames = []
    bounds = []
    for i, (part, source_box) in enumerate(parts):
        part = part.resize((round(part.width*scale), round(part.height*scale)), Image.Resampling.NEAREST)
        x, y = 128-round(anchors[i]*scale), 352-part.height
        assert x >= 4 and y >= 4
        frame = Image.new('RGBA', (256, 384))
        frame.paste(part, (x, y))
        path = HERE / 'sheets' / f'{ident}_{i+1:02}.png'
        frame.save(path)
        sheet.paste(frame, (i*256, 0))
        frames.append(uri(path))
        bounds.append({'sourceRect': source_box, 'packedRect': [x, y, part.width, part.height]})
    path = HERE / 'sheets' / (ident + '.png')
    sheet.save(path)
    reference = HERE / 'references' / (ident + '.png')
    reference.write_bytes((ART/ref).read_bytes())
    return {'id': ident, 'label': label, 'count': count, 'fps': fps, 'cell': [256,384],
            'size': [count*256,384], 'pivotTopLeft': [128,352], 'pivotUnity': [.5, 32/384],
            'playback': '마지막 칸에서 정지',
            'sheet': 'sheets/'+path.name, 'frames': frames, 'reference': uri(reference),
            'sourceSha256': sha(source), 'sheetSha256': sha(path), 'scale': scale,
            'registration': bounds, 'groundPolicy': '최하단 공통 지면 y352; 시전·명령은 아래 12화소의 발 중심을 x128에 맞춤; 쓰러짐은 포즈 가로 중심 정렬',
            'referencePath': str((ART/ref).relative_to(ROOT)).replace('\\','/'), 'referenceSha256': sha(ART/ref)}

def normal(ident, ref):
    color_path = ART / ref
    color = Image.open(color_path).convert('RGBA')
    raw_path = HERE/'source'/f'{ident}_normal.png'
    raw = Image.open(raw_path).convert('RGB')
    # 생성 원안은 캔버스 비율만 맞추고 위치는 색 원본 마스크로 제한한다.
    # 내부 주름의 대응은 근사이며 3D 모형에서 구운 데이터가 아니다.
    rgb = np.asarray(raw.resize(color.size, Image.Resampling.NEAREST)).astype(float)/255
    border = np.concatenate([rgb[:8,:,:2].reshape(-1,2),rgb[-8:,:,:2].reshape(-1,2)])
    background = np.median(border,axis=0)
    xy = (rgb[:,:,:2]-background)*2*.45
    z = np.sqrt(np.maximum(1-(xy*xy).sum(axis=2), .01))
    vec = np.dstack([xy,z])
    vec /= np.linalg.norm(vec, axis=2, keepdims=True)
    encoded = np.rint((vec+1)*127.5).astype('uint8')
    mask = np.asarray(color)[:,:,3]
    encoded[mask <= 32] = [128,128,255]
    path = HERE/'normals'/f'{ident}_normal.png'
    Image.fromarray(encoded).save(path)
    cp = HERE/'normals'/f'{ident}_albedo.png'
    cp.write_bytes(color_path.read_bytes())
    return {'id':ident, 'label': {'gunner':'총병','cannon_crew':'대포병','wizard':'마법사 후면'}[ident],
            'color':uri(cp), 'normal':uri(path), 'size':list(color.size), 'pivotTopLeft':[128,352],
            'path':'normals/'+path.name, 'albedoPath':'normals/'+cp.name,
            'sourceSha256':sha(raw_path), 'referencePath':str(color_path.relative_to(ROOT)).replace('\\','/'),
            'referenceSha256':sha(color_path), 'strength':.45, 'format':'RGB +Y; 투명색 영역 평면(128,128,255)',
            'registration':'원안 전체 캔버스 최근접 맞춤; 배경 XY 중간값 중심화; 내부 표면 대응은 근사',
            'backgroundXY':background.tolist()}

def main():
    assets = [pack(*item) for item in ITEMS]
    normals = [normal('gunner','Static/gunner_static.png'),normal('cannon_crew','Static/cannon_crew_static.png'),normal('wizard','Static/wizard_back_static.png')]
    manifest = {'card':'K-023','status':'사용자 확정 전 시안','unityVerified':False,
                'assets':[{k:v for k,v in a.items() if k not in ['frames','reference']} for a in assets],
                'normalSamples':[{k:v for k,v in a.items() if k not in ['color','normal']} for a in normals]}
    dump('manifest.json',manifest)
    page = (HERE/'review_template.html').read_text(encoding='utf-8')
    page = page.replace('__ASSETS__',json.dumps(assets,ensure_ascii=False)).replace('__NORMALS__',json.dumps(normals,ensure_ascii=False))
    (ROOT/'_HTML/codex_K-023_아군보강_v1.html').write_text(page,encoding='utf-8')
    # 큰 시트 전체를 한눈에 검토할 모아보기. 원본 PNG의 크기는 바꾸지 않는다.
    contact = Image.new('RGB',(1100, 5*330),(35,44,40))
    draw = ImageDraw.Draw(contact)
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',16)
    for i,a in enumerate(assets):
        sheet = Image.open(HERE/a['sheet']).convert('RGBA')
        sheet.thumbnail((1024,300),Image.Resampling.NEAREST)
        contact.paste(sheet,(20,i*330+25),sheet)
        draw.text((20,i*330+5),a['label'],fill=(235,220,180),font=font)
    contact.save(HERE/'contact_sheet.png')
    print('격자 5종·동작 19칸·노멀 견본 3장·독립 HTML 제작')

if __name__ == '__main__':
    main()
