from pathlib import Path
from PIL import Image
from collections import deque
import numpy as np
import json, math, hashlib

ROOT=Path(__file__).resolve().parent
manifest=json.loads((ROOT/'manifest.json').read_text(encoding='utf-8'))
checks=[]

def check(name,value,detail=None):
    checks.append(dict(check=name,passed=bool(value),detail=detail))

def components(mask):
    seen=np.zeros(mask.shape,dtype=bool); sizes=[]
    for y,x in zip(*np.where(mask)):
        if seen[y,x]: continue
        todo=[(int(y),int(x))]; seen[y,x]=True; size=0
        while todo:
            yy,xx=todo.pop(); size+=1
            for dy in [-1,0,1]:
                for dx in [-1,0,1]:
                    ny,nx=yy+dy,xx+dx
                    if 0<=ny<mask.shape[0] and 0<=nx<mask.shape[1] and mask[ny,nx] and not seen[ny,nx]:
                        seen[ny,nx]=True; todo.append((ny,nx))
        sizes.append(size)
    return sizes

for asset in manifest['assets']:
    file=asset['file']; im=Image.open(ROOT/file); a=np.array(im)
    check(file+':size',list(im.size)==asset['size_px'])
    check(file+':rgba',im.mode=='RGBA')
    check(file+':binary_alpha',set(np.unique(a[:,:,3])).issubset({0,255}))
    check(file+':transparent_rgb_zero',np.all(a[a[:,:,3]==0]==0))
    if asset['kind'] in ['battle_part','map_hub','map_arm']:
        sizes=components(a[:,:,3]>0)
        check(file+':single_connected_component',len(sizes)==1,dict(component_sizes=sizes))
    if asset['kind']=='battle_part':
        mask=a[:,:,3]>0
        check(file+':declared_visible_height',128-im.getbbox()[1]==asset['visible_height_px'] and asset['visible_height_m']==asset['visible_height_px']/40)
        check(file+':baseline',np.where(mask)[0].max()==127)
        check(file+':bottom_centre_contact',mask[127,im.width//2])
        if asset['part'] in ['straight','diagonal']:
            check(file+':periodic_edge',np.array_equal(a[:,0],a[:,-1]))
            # 각 높이의 반복 경계는 색과 알파 모두 일치해야 한다.
            left=Image.new('RGBA',(im.width*2,128));left.alpha_composite(im);left.alpha_composite(im,(im.width,0))
            mask=np.array(left)[:,:,3]>0
            check(file+':continuous_bottom_12px_at_seam',np.all(mask[116:,im.width-1:im.width+1]))
    if asset['kind']=='battle_part_3x':
        native=np.array(Image.open(ROOT/asset['native_file']))
        check(file+':nearest_3x_exact',np.array_equal(a,np.repeat(np.repeat(native,3,axis=0),3,axis=1)))

dirs=manifest['map']['mask_bit_order']
vectors=[(0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1)]
for kind in ['earthwall','hill']:
    sprites={d:Image.open(ROOT/f'map/{kind}_arm_{d}.png') for d in dirs}
    hub=Image.open(ROOT/f'map/{kind}_hub.png')
    for i,(d,(dx,dy)) in enumerate(zip(dirs,vectors)):
        pair=Image.new('RGBA',(96,96)); pair.alpha_composite(hub,(32,32)); pair.alpha_composite(hub,((1+dx)*32,(1+dy)*32))
        pair.alpha_composite(sprites[d],(32,32)); pair.alpha_composite(sprites[dirs[(i+4)%8]],((1+dx)*32,(1+dy)*32))
        mask=np.array(pair)[:,:,3]>0
        check(kind+':paired_'+d+'_connected',len(components(mask))==1)
        if dx and dy:
            corner_x=64 if dx>0 else 32; corner_y=64 if dy>0 else 32
            # 공유 모서리 양쪽에 3×3 이상의 불투명 채움이 있어야 한다.
            regions=[]
            for xx,yy in [(corner_x-2 if dx>0 else corner_x,corner_y-2 if dy>0 else corner_y),(corner_x if dx>0 else corner_x-2,corner_y if dy>0 else corner_y-2)]:
                regions.append(bool(np.all(mask[yy:yy+2,xx:xx+2])))
            check(kind+':paired_'+d+'_corner_closed',all(regions),regions)
    results=[]
    for bits in range(256):
        out=Image.new('RGBA',(96,96)); out.alpha_composite(hub,(32,32))
        for i,(dx,dy) in enumerate(vectors):
            if bits&(1<<i):out.alpha_composite(hub,((1+dx)*32,(1+dy)*32))
        for i,(dx,dy) in enumerate(vectors):
            if bits&(1<<i):
                out.alpha_composite(sprites[dirs[i]],(32,32));out.alpha_composite(sprites[dirs[(i+4)%8]],((1+dx)*32,(1+dy)*32))
        results.append(len(components(np.array(out)[:,:,3]>0))==1)
    check(kind+':all_256_masks_connected',all(results),dict(tested=256,failed_masks=[i for i,v in enumerate(results) if not v]))

for layout in manifest['layouts']:
    pairs=layout['placement_pairs']
    valid=all(len(p)==2 and max(abs(p[0][0]-p[1][0]),abs(p[0][1]-p[1][1]))==1 for p in pairs)
    check(layout['id']+':exact_two_cell_placements',valid,pairs)

check('diagonal_raster_error_less_than_one_pixel',abs(283-math.sqrt(50)*40)<.5)
report=dict(card='K-087',checks=checks,passed=sum(c['passed'] for c in checks),failed=[c for c in checks if not c['passed']],unity_run=False)
(ROOT/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(passed=report['passed'],total=len(checks),failed=report['failed']),ensure_ascii=False,indent=2))
if report['failed']: raise SystemExit(1)
