"""Pack generated artwork; no procedural drawing, mirroring, or normal synthesis."""
from pathlib import Path
import base64
import hashlib
import json
import shutil
import zipfile
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
NN = Image.Resampling.NEAREST

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def write(path, obj):
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def uri(path):
    return 'data:image/png;base64,' + base64.b64encode(path.read_bytes()).decode('ascii')

def bbox(im):
    # Threshold only chooses crop bounds; original alpha is never repainted.
    return im.getchannel('A').point(lambda a: 255 if a >= 32 else 0).getbbox()

def build():
    texture_jobs = read(HERE / 'textures_generation.json')['requests']
    prop_jobs = read(HERE / 'props_generation.json')['requests']
    damage_jobs = read(HERE / 'damage_generation.json')['requests']
    jobs = prop_jobs + damage_jobs
    originals = {}
    for j in jobs:
        d = HERE / j['id']
        d.mkdir(exist_ok=True)
        src = d / 'source.png'
        if not src.exists():
            shutil.copyfile(j['sourcePath'], src)
        originals[j['id']] = Image.open(src).convert('RGBA')

    scales = {}
    for family in ('tower_square', 'battery_low'):
        members = [j for j in jobs if j.get('family') == family]
        intact = bbox(originals[family + '_intact'])
        max_width = max(bbox(originals[j['id']])[2] - bbox(originals[j['id']])[0] for j in members)
        max_height = max(bbox(originals[j['id']])[3] - bbox(originals[j['id']])[1] for j in members)
        target = 294 if family == 'tower_square' else 165
        scales[family] = min(224 / max_width, target / (intact[3] - intact[1]), 310 / max_height)

    records = []
    images = {}
    labels = {}
    asset_ids = []
    for j in texture_jobs:
        d = HERE / j['id']
        image = Image.open(d / 'source.png').convert('RGBA').resize(tuple(j['size']), NN)
        image.save(d / 'albedo.png')
        rec = {'id': j['id'], 'label': j['label'], 'kind': 'texture', 'file': f"{j['id']}/albedo.png", 'size': j['size'], 'meters': j['meters'], 'pixelsPerMeter': 32, 'seamlessCertified': False, 'normalMapIncluded': False, 'sha256': digest(d / 'albedo.png')}
        records.append(rec)
        images[j['id']] = uri(d / 'albedo.png')
        labels[j['id']] = j['label']
        asset_ids.append(j['id'])

    for j in jobs:
        key = j['id']
        im = originals[key]
        rect = bbox(im)
        crop = im.crop(rect)
        scale = scales[j['family']] if j.get('family') else min(224 / crop.width, 300 / crop.height)
        size = (round(crop.width * scale), round(crop.height * scale))
        packed = Image.new('RGBA', (256, 384), (0, 0, 0, 0))
        packed.paste(crop.resize(size, NN), (128 - size[0] // 2, 352 - size[1]))
        dest = HERE / key / 'sprite.png'
        packed.save(dest)
        family = j.get('family')
        state = j.get('state')
        label = j.get('label') or ({'tower_square': '사각 포탑', 'battery_low': '낮은 포대'}[family] + ' · ' + {'damaged': '파손', 'rubble': '잔해'}[state])
        rec = {'id': key, 'label': label, 'kind': 'cutout_reference', 'family': family, 'state': state, 'file': f'{key}/sprite.png', 'size': [256, 384], 'pivotPixelsTopLeft': [128, 352], 'pivotUnityNormalized': [0.5, 1 / 12], 'sourceSize': list(im.size), 'sourceCrop': list(rect), 'sharedSourceScale': scale, 'packedBodySize': list(size), 'pixelsPerMeter': None, 'normalMapIncluded': False, 'sha256': digest(dest), 'sourceSha256': digest(HERE / key / 'source.png')}
        write(HERE / key / 'asset.json', rec)
        records.append(rec)
        images[key] = uri(dest)
        labels[key] = label
        asset_ids.append(key)

    old = HERE.parent / 'codex_world_materials_v1'
    for key, old_key in [('wall_gray', 'wall_stone'), ('walkway_gray', 'walkway_stone'), ('grass_muted', 'field_grass')]:
        images['old_' + key] = uri(old / old_key / 'albedo.png')
    images['reference'] = uri(HERE / 'user_octopath_reference.png')
    images['hero'] = uri(HERE.parent / 'codex_idle_assets_v1' / 'hero_back_idle_grid.png')
    manifest = {'task': 'T-CX-051', 'author': 'Codex', 'status': 'reference_review_pending', 'rasterAssets': len(records), 'artGenerator': 'image_gen.imagegen', 'processing': 'nearest-neighbor resize and transparent cell packing only; no content repaint/mirror', 'gameChanges': False, 'meshesIncluded': False, 'assets': records}
    write(HERE / 'manifest.json', manifest)
    data = {'images': images, 'labels': labels, 'assetIds': asset_ids, 'readme': (HERE / 'README.md').read_text(encoding='utf-8'), 'comparison': {
        'wall_gray': ['규칙적인 블록과 짙은 줄눈, 갈색 기운.', '낡은 중성 회색 돌, 풍화된 모서리와 억제된 이끼.'],
        'walkway_gray': ['벽과 다른 색감, 반복되는 돌 배열.', '벽과 같은 회색 계열의 닳은 석판. 작은 캐릭터의 식별성은 게임에서 다시 확인합니다.'],
        'grass_muted': ['촘촘한 풀과 비교적 선명한 반복 무늬.', '낮은 채도의 올리브 풀과 흙 여백. 작은 크기에서 잔무늬가 남는 부분은 수정 후보입니다.']}}
    html = (HERE / 'template.html').read_text(encoding='utf-8').replace('__DATA__', json.dumps(data, ensure_ascii=False).replace('</', '<\\/'))
    (HERE / 'preview.html').write_text(html, encoding='utf-8')
    (ROOT / '_HTML' / 'codex_회색성벽_나무_포탑비교_v2.html').write_text(html, encoding='utf-8')
    with zipfile.ZipFile(HERE / 'gray_fortress_v2_assets.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
        archive.write(HERE / 'manifest.json', 'manifest.json')
        archive.write(HERE / 'README.md', 'README.md')
        for rec in records:
            archive.write(HERE / rec['file'], rec['file'])
            meta = HERE / rec['id'] / 'asset.json'
            if meta.exists():
                archive.write(meta, rec['id'] + '/asset.json')
    # Contact sheet: presentation only, actual assets remain individual PNGs.
    sheet = Image.new('RGB', (4 * 256, 2 * 384), '#46514b')
    cutouts = [r for r in records if r['kind'] == 'cutout_reference']
    for i, rec in enumerate(cutouts):
        spr = Image.open(HERE / rec['file'])
        sheet.paste(spr, ((i % 4) * 256, (i // 4) * 384), spr)
    sheet.save(HERE / 'cutout_review.png')
    print(json.dumps({'assets': len(records), 'htmlBytes': len(html.encode('utf-8')), 'sharedScales': scales}, ensure_ascii=False))

if __name__ == '__main__':
    build()
