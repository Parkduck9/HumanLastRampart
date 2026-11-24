from pathlib import Path
from PIL import Image
import json

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
sheets={s['name']:s for s in json.loads((OUT/'manifest.json').read_text(encoding='utf8'))['sheets']}
probes=[
    ('hero_back_idle',(128,80),(13,26),'흰 머리는 무광'),
    ('hero_back_idle',(77,189),(255,217),'금색 견장은 금속'),
    ('wizard_static',(140,80),(13,26),'주황 머리는 무광'),
    ('wizard_static',(136,166),(13,26),'얼굴 피부는 무광'),
    ('wizard_static',(123,219),(255,217),'목 장식은 금속'),
    ('gunner_static',(189,210),(255,191),'총열은 금속'),
    ('gunner_static',(138,247),(13,26),'파란 옷은 무광'),
    ('gunner_static',(137,159),(13,26),'갈색 머리는 무광'),
    ('cannon_recoil',(128,100),(255,191),'밝은 포신은 금속'),
    ('cannon_recoil',(128,200),(13,26),'나무 포가는 무광'),
    ('cannon_recoil',(80,205),(13,26),'어두운 나무도 무광'),
    ('goblin_walk_attack4',(87,313),(13,26),'나무 몽둥이는 무광'),
    ('goblin_walk_attack4',(137,245),(13,26),'녹색 피부는 무광'),
    ('ogre_static',(136,214),(13,26),'오우거 피부는 무광'),
    ('ogre_static',(127,289),(242,140),'오우거 버클은 철'),
]
results=[]
for name,point,rg,note in probes:
    s=sheets[name]
    source=Image.open(ROOT/s['source']).convert('RGBA').getpixel(point)
    mask=Image.open(OUT/s['mask']).convert('RGBA').getpixel(point)
    results.append({'sheet':name,'point':point,'note':note,'source_rgba':source,'mask_rgba':mask,'expected_rg':rg,'pass':mask[:2]==rg and mask[3]==source[3] and source[3]>=128})
report={'all_pass':all(r['pass'] for r in results),'probes':results}
(OUT/'material_validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({'probes':len(results),'all_pass':report['all_pass']}))
assert report['all_pass']
