"""Produce M-CL-045 row-compatible sibling from the four-row walk/run sheet."""
from pathlib import Path
from PIL import Image
import json,hashlib
P=Path(__file__).resolve().parent
source=P/'hero_walk_run_grid.png'
im=Image.open(source).convert('RGBA')
out=Image.new('RGBA',im.size)
for row,original in enumerate([0,1,3,2]):
    out.paste(im.crop((0,original*384,1024,(original+1)*384)),(0,row*384))
walk=Image.open(P.parent/'codex_hero_grid_v1/hero_walk_grid.png').convert('RGBA')
assert out.crop((0,0,1024,1152)).tobytes()==walk.tobytes()
target=P/'hero_walk_run_append_grid.png';out.save(target)
meta=json.loads((P/'hero_walk_run_grid.json').read_text(encoding='utf-8'))
meta['image']=target.name;meta['rowOrder']=['back_walk','left_walk','front_walk','left_run'];meta['runRow']=3
for f in meta['runFrames']:f['index']=12+f['frame']
meta['sheetSha256']=hashlib.sha256(target.read_bytes()).hexdigest()
(P/'hero_walk_run_append_grid.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS append compatibility: original first3 rows unchanged, run row3')
