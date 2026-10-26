import sys
sys.dont_write_bytecode=True
from build import ROOT, OUT, CELL, GROUND, NN, components
from PIL import Image
import numpy as np
import json, hashlib, re, base64, io

m=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
checks=[];failures=[]
def check(name,value):
    checks.append({'check':name,'passed':bool(value)})
    if not value:failures.append(name)

check('동작 수17',len(m['motions'])==17)
check('프레임 합계120',sum(x['n'] for x in m['motions'])==120)
max_normal_error=0
for mo in m['motions']:
    name=mo['name'];n=mo['n'];f=mo['files']
    color=Image.open(OUT/f['native']).convert('RGBA');a=np.array(color)
    normal=Image.open(OUT/f['native_normal']).convert('RGB');b=np.array(normal)
    mask=a[:,:,3]>0
    check(name+' 원본 칸',color.size==(256*n,288) and normal.size==color.size)
    check(name+' 알파0/255',set(np.unique(a[:,:,3])).issubset({0,255}))
    check(name+' 투명색0',np.all(a[~mask]==0))
    check(name+' 투명 노멀',np.all(b[~mask]==[128,128,255]))
    check(name+' 게임3배',np.array_equal(np.array(Image.open(OUT/f['game'])),np.array(color.resize((color.width*3,864),NN))))
    check(name+' 노멀3배',np.array_equal(np.array(Image.open(OUT/f['game_normal'])),np.array(normal.resize((normal.width*3,864),NN))))
    check(name+' 미리보기4배',np.array_equal(np.array(Image.open(OUT/f['preview_4x'])),np.array(color.resize((color.width*4,1152),NN))))
    v=b[mask].astype(float)/127.5-1
    err=float(np.abs(np.linalg.norm(v,axis=1)-1).max());max_normal_error=max(max_normal_error,err)
    check(name+' 단위 노멀',err<.012)
    for i,meta in enumerate(mo['frames']):
        im=color.crop((i*256,0,(i+1)*256,288));bbox=im.getbbox()
        check(name+f' {i} 바운드',list(bbox)==meta['bbox_native'] and bbox[0]>0 and bbox[1]>0 and bbox[2]<256 and bbox[3]<288)
        check(name+f' {i} 단일 윤곽',len(components(np.array(im)[:,:,3]>0))==1)
        if mo['mode']=='ground':check(name+f' {i} 지면',bbox[3]==280 and meta['anchor_native']==[128,280])
        if mo['mode']=='grip':check(name+f' {i} 손 기준',bbox[1]==16 and meta['anchor_native']==[128,16])
        if mo['mode']=='pelvis':check(name+f' {i} 몸 기준',meta['anchor_native']==[128,152])
        check(name+f' {i} 빈 칸 아님',bool(np.array(im)[:,:,3].any()))
right=Image.open(OUT/'native/wallhand_walk_right.png')
left=Image.open(OUT/'native/wallhand_walk_left.png')
check('좌우 반전',all(np.array_equal(np.array(left.crop((i*256,0,(i+1)*256,288))),np.array(right.crop((i*256,0,(i+1)*256,288)).transpose(Image.Transpose.FLIP_LEFT_RIGHT))) for i in range(8)))
for u in m['ui']:
    a=np.array(Image.open(OUT/u['file']))
    check(u['name']+' 크기',a.shape[:2]==tuple(u['size'][::-1]))
    check(u['name']+' 이진알파',set(np.unique(a[:,:,3])).issubset({0,255}))
    check(u['name']+' 단일윤곽',len(components(a[:,:,3]>0))==1)
hashes=json.loads((OUT/'source_hashes.json').read_text(encoding='utf-8'))
changed=[p for p,h in hashes.items() if hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h]
check('입력 및 Game 해시 보존',not changed)
html=(ROOT/'_HTML/6_그림_화면/codex_K-129_우두머리동작_v1.html').read_text(encoding='utf-8')
embedded=re.findall(r'data:image/png;base64,([A-Za-z0-9+/=]+)',html)
for i,s in enumerate(embedded):
    im=Image.open(io.BytesIO(base64.b64decode(s)));im.verify()
check('HTML 내장 그림수',len(embedded)>=44)
report={'checks':checks,'failed':failures,'total_checks':len(checks),'source_files_hashed':len(hashes),'changed_sources':changed,'embedded_pngs_decoded':len(embedded),'max_normal_unit_length_error':max_normal_error,'unity_run':False,'git_writes':False}
(OUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='checks'},ensure_ascii=False))
if failures:raise SystemExit(1)
