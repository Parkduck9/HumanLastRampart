"""생성 그림의 자르기·발 등록·격자 포장만 수행한다. 색 그림 손그림 보정은 없다."""
import hashlib
import json
from pathlib import Path
from collections import deque
import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
SRC = OUT / 'source'
SCALE = 73 / 396


def bounds(im):
    y, x = np.where(np.asarray(im)[:, :, 3] > 128)
    return [int(x.min()), int(y.min()), int(x.max()+1), int(y.max()+1)]


def feet(im):
    a = np.asarray(im)
    bottom = bounds(im)[3]
    _, x = np.where(a[max(0, bottom-45):bottom, :, 3] > 128)
    return (float(x.min()+x.max()+1)/2, bottom)


def flame(im):
    a = np.asarray(im)
    m = (a[:,:,3]>128)&(a[:,:,0]>=225)&(a[:,:,1]>=100)&(a[:,:,2]<245)
    m[:,80:]=False
    m[110:]=False
    seen=set(); components=[]
    for y,x in zip(*np.where(m)):
        if (int(x),int(y)) in seen: continue
        points=[]; q=deque([(int(x),int(y))]); seen.add((int(x),int(y)))
        while q:
            xx,yy=q.popleft(); points.append((xx,yy))
            for dy in [-1,0,1]:
                for dx in [-1,0,1]:
                    nx,ny=xx+dx,yy+dy
                    if 0<=nx<144 and 0<=ny<128 and m[ny,nx] and (nx,ny) not in seen:
                        seen.add((nx,ny)); q.append((nx,ny))
        if len(points)>=4: components.append(np.array(points))
    p=min(components,key=lambda p:p[:,0].mean())
    return {'center':p.mean(axis=0).tolist(),'bounds':[int(p[:,0].min()),int(p[:,1].min()),int(p[:,0].max()+1),int(p[:,1].max()+1)],'pixels':len(p)}


source=Image.open(SRC/'ignite_attempt1.png').convert('RGBA')
previous=Image.open(OUT.parent/'codex_k086_v3/native/cannon_crew_ignite6.png').convert('RGBA')
contact=Image.open(SRC/'contact_attempt3.png').convert('RGBA')
sheet=Image.new('RGBA',(864,128)); rows=[]; cells=[]
for i in range(6):
    if i==3:
        im=contact
        # 단독 생성 머리폭 약332px를 기준 이미지 약166px로 정규화한 뒤 공통 배율을 쓴다.
        sc=SCALE*.5
        source_name='contact_attempt3.png'
        rect=[0,0,im.width,im.height]
    else:
        # 첫 시트 생성은 주변 광채가 사각형으로 잘려 최종 포장에 쓰지 않는다.
        # 기존 1·2·3·5칸을 그대로 보존하고 마지막 칸은 첫 칸과 같게 닫는다.
        n=0 if i==5 else i
        cell=previous.crop((n*144,0,(n+1)*144,128))
        cell.save(OUT/f'previews/frame_{i+1}.png')
        sheet.paste(cell,(i*144,0)); cells.append(cell)
        rows.append({'frame':i+1,'source':'../codex_k086_v3/native/cannon_crew_ignite6.png','source_frame':n+1,'source_scale':1,'hard_bounds':bounds(cell),'flame':flame(cell)})
        continue
    fx,fy=feet(im)
    size=(round(im.width*sc),round(im.height*sc))
    small=im.resize(size,Image.Resampling.NEAREST)
    px=round(96-fx*sc); py=round(120-fy*sc)
    cell=Image.new('RGBA',(144,128)); cell.paste(small,(px,py))
    adjust=120-bounds(cell)[3]
    if adjust:
        shifted=Image.new('RGBA',(144,128)); shifted.paste(cell,(0,adjust)); cell=shifted; py+=adjust
    cell.save(OUT/f'previews/frame_{i+1}.png')
    sheet.paste(cell,(i*144,0)); cells.append(cell)
    rows.append({'frame':i+1,'source':source_name,'source_rect':rect,'source_scale':sc,'source_feet':[fx,fy],'paste':[px,py], 'hard_bounds':bounds(cell),'flame':flame(cell)})
sheet.save(OUT/'native/cannon_crew_ignite6.png')
game=sheet.resize((2592,384),Image.Resampling.NEAREST); game.save(OUT/'game/cannon_crew_ignite6.png')
sheet.resize((3456,512),Image.Resampling.NEAREST).save(OUT/'previews/ignite_4x.png')

# 도화선은 imagegen 결과의 작은 구역만 포장한다. 기존 대포의 나머지 픽셀은 보존한다.
original=Image.open(SRC/'cannon_original.png').convert('RGBA')
generated=Image.open(SRC/'cannon_fuse_attempt1.png').convert('RGBA').resize((256,256),Image.Resampling.NEAREST)
patch=generated.crop((120,132,140,151))
cannon=original.copy(); edits=[]
for i,dy in enumerate([0,8,4]):
    box=(i*256+120,121+dy,i*256+140,140+dy)
    cannon.alpha_composite(patch,(box[0],box[1])); edits.append(list(box))
cannon.putalpha(original.getchannel('A'))
cannon.save(OUT/'game/cannon_recoil_fuse.png')

cscale=.60375*(73/.35)/234
target=[96-.4*73/.35,120+(134-240)*cscale]
wick_tip=[target[0]+6*cscale,target[1]-10*cscale]
f=rows[3]['flame']; b=f['bounds']
flame_touches_fixture=(b[0]<=target[0]<=b[2] and b[1]<=target[1]<=b[3])
flame_touches_tip=(b[0]<=wick_tip[0]<=b[2] and b[1]<=wick_tip[1]<=b[3])
old=np.asarray(original); new=np.asarray(cannon); allow=np.zeros(old.shape[:2],bool)
for x0,y0,x1,y1 in edits:allow[y0:y1,x0:x1]=True
validation={
 'exact_nearest_3x':bool(np.array_equal(np.asarray(game),np.repeat(np.repeat(np.asarray(sheet),3,axis=0),3,axis=1))),
 'foot_bottom_all_120':all(r['hard_bounds'][3]==120 for r in rows),
 'first_last_identical':bool(np.array_equal(np.asarray(cells[0]),np.asarray(cells[5]))),
 'unchanged_previous_frames_1_2_3_5':all(np.array_equal(np.asarray(cells[i]),np.asarray(previous.crop((i*144,0,(i+1)*144,128)))) for i in [0,1,2,4]),
 'cannon_outside_patch_unchanged':bool(np.array_equal(old[~allow],new[~allow])),
 'cannon_alpha_unchanged':bool(np.array_equal(old[:,:,3],new[:,:,3])),
 'contact_flame_bounds':b,'fixture_2d':target,'wick_tip_2d':wick_tip,
 'flame_bounds_include_fixture_2d':flame_touches_fixture,'flame_bounds_include_wick_tip_2d':flame_touches_tip,
 'actual_camera_contact_verified':False,'game_integrated':False,
}
manifest={'card':'K086-v5','user_anchor_approved':True,'user_quote':'다음진행해','native_cell':[144,128],'game_cell':[432,384],'native_pivot':[96,120],'game_pivot':[288,360],'frames':6,'fps':6,'reference_standing_figure':73,'frames_data':rows,'cannon_patch_boxes':edits,'cannon_fixture_source':[128,134],'normalization_note':'단독 생성 머리폭 약332px : 시트 머리폭 약166px, 해상도 차이 2:1 정규화. 첫칸 기준 인물73도트. 자세별 전체높이를 억지로 같게 늘리지 않음.','all_shaft_lengths_measured':False,'source_sha256':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in SRC.glob('*.png')}}
if (OUT/'camera_review.json').exists():
    validation.update(json.loads((OUT/'camera_review.json').read_text(encoding='utf8')))
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
(OUT/'validation.json').write_text(json.dumps(validation,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(validation,ensure_ascii=False))
