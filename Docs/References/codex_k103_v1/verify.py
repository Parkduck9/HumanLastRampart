# 납품 이미지의 크기, 기준점, 확대 배율, 안개 연결 경계를 검사한다.
from pathlib import Path
import json, re, base64, io
import numpy as np
from PIL import Image

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
checks=[]
def check(name,condition,detail=None):
    checks.append({'check':name,'passed':bool(condition),'detail':detail})
def read(name): return np.array(Image.open(OUT/name).convert('RGBA'))
for a in manifest['assets']:
    im=Image.open(OUT/a['file'])
    check('image_size_and_rgba:'+a['file'],list(im.size)==a['size'] and im.mode=='RGBA')
    ar=np.array(im)
    check('alpha_present:'+a['file'],bool((ar[:,:,3]==0).any()) if a.get('role')!='concept_mockup' and 'fog_clear_field_proof' not in a['file'] else True)
    if a['file'].startswith('native/'):
        game=read(a['file'].replace('native/','game/'))
        check('exact_3x_nearest:'+a['file'],np.array_equal(game,np.repeat(np.repeat(ar,3,axis=0),3,axis=1)))
base=read('native/brazier_unlit.png')
baseline=[]
for state in ['unlit','low','normal','high']:
    ar=read('native/brazier_'+state+'.png')
    for i in range(ar.shape[1]//96):
        frame=ar[:,i*96:(i+1)*96];ys,xs=np.where(frame[:,:,3]>0)
        baseline.append({'state':state,'frame':i,'bottom':int(ys.max()),'bounds':[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)]})
        check(f'baseline_and_center:{state}:{i}',ys.max()==120 and (xs.min()+xs.max()+1)/2==48)
        check(f'fixed_base:{state}:{i}',np.array_equal(frame[84:],base[84:]))
        check(f'no_canvas_clipping:{state}:{i}',not frame[0,:,3].any() and not frame[-1,:,3].any() and not frame[:,0,3].any() and not frame[:,-1,3].any())
        # 연결 성분 한 화소짜리는 실수로 남은 점으로 취급한다.
        mask=frame[:,:,3]>0;seen=np.zeros(mask.shape,bool);sizes=[]
        for y,x in zip(*np.where(mask)):
            if seen[y,x]:continue
            q=[(y,x)];seen[y,x]=True;n=0
            while q:
                cy,cx=q.pop();n+=1
                for dy,dx in [(0,1),(0,-1),(1,0),(-1,0)]:
                    ny,nx=cy+dy,cx+dx
                    if 0<=ny<128 and 0<=nx<96 and mask[ny,nx] and not seen[ny,nx]:seen[ny,nx]=True;q.append((ny,nx))
            sizes.append(n)
        check(f'no_single_pixel_islands:{state}:{i}',min(sizes)>=2,sizes)
band=read('native/fog_band.png');corner=read('native/fog_corner_nw.png')
check('fog_exact_horizontal_seam',np.array_equal(band[:,0],band[:,-1]))
check('corner_right_matches_band_start',np.array_equal(corner[:,-1],band[:,0]))
check('corner_bottom_matches_rotated_band',np.array_equal(corner[-1,:],band[:,0]))
check('fog_inner_edge_fully_clear',not band[112:,:,3].any() and not corner[112:,112:,3].any())
for level in ['low','normal','high']:
    a=read('native/glow_'+level+'.png')
    check('glow_soft_alpha_and_clear_border:'+level,len(np.unique(a[:,:,3]))>10 and not a[0,:,3].any() and not a[-1,:,3].any() and not a[:,0,3].any() and not a[:,-1,3].any())
proof=read('previews/fog_clear_field_proof.png')
check('placement_proof_enemies_uncovered',bool((proof[128:,:,3]==255).all()))
page=(ROOT/'_HTML/6_그림_화면/codex_K-103_성벽화로와안개_v1.html').read_text(encoding='utf-8')
embedded=re.findall(r'data:image/png;base64,([A-Za-z0-9+/=]+)',page)
for i,s in enumerate(embedded):
    im=Image.open(io.BytesIO(base64.b64decode(s)));im.verify()
check('review_self_contained',len(embedded)>=20 and not re.search(r'(?:src|href)=["\']https?://',page),len(embedded))
report={'passed':all(c['passed'] for c in checks),'checks':checks,'baseline_frames':baseline,'not_tested':['Unity import, Play, runtime lighting/sorting, world-space 45m mask and final camera scale'],'visual_inspection':'Native 4x brazier loops, HUD 4x and 1920x1080 mockup inspected separately.'}
(OUT/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'passed':report['passed'],'checks':len(checks),'failures':[c for c in checks if not c['passed']]},ensure_ascii=False))
raise SystemExit(0 if report['passed'] else 1)
