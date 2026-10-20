from pathlib import Path
from PIL import Image, ImageChops
import json, hashlib

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
NN=Image.Resampling.NEAREST
checks=[]
for name in ['battle','battle_danger','yard','shop','training']:
    native=Image.open(OUT/f'{name}_native.png')
    after=Image.open(OUT/f'{name}_after.png')
    four=Image.open(OUT/f'{name}_4x.png')
    assert native.size==(640,360) and after.size==(1920,1080) and four.size==(2560,1440)
    assert ImageChops.difference(native.resize(after.size,NN),after).getbbox() is None
    assert ImageChops.difference(native.resize(four.size,NN),four).getbbox() is None
    assert after.getchannel('A').getextrema()==(255,255)
    checks.append({'screen':name,'dimensions':True,'exact_nearest_3x':True,'exact_nearest_4x':True,'alpha_255':True,'off_grid_pixels':0})
for name in ['battle','yard','shop']:
    assert (OUT/f'{name}_before.png').read_bytes()==(ROOT/'Docs/References/ui_redesign_v2'/f'now_{name}.png').read_bytes()
    assert Image.open(OUT/f'{name}_pair.png').size==(3840,1080)
assert Image.open(OUT/'font_board.png').size==(1920,1080)
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
assert all(r['ratio']>=4.5 for r in manifest['contrast'])
report={'screens':checks,'original_before_files_byte_identical':True,'before_after_pair_dimensions':True,'font_board_dimensions':True,'minimum_text_contrast':min(r['ratio'] for r in manifest['contrast']),'foot_baseline':'해당 없음 · 캐릭터 시트가 아닌 화면 목업','stray_pixel_scope':'캔버스 밖 데이터 없음, 모든 3배 출력의 원본 격자 일치 확인. 장면 원본의 미술 결함까지 자동 판정하지 않음.'}
(OUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
