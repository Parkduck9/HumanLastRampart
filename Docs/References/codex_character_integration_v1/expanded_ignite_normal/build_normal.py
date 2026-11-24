"""T094 점화 v5: K081 기존 결정적 기술 법선 공식을 144×128 칸에 그대로 적용."""
from pathlib import Path
import hashlib
import json
import shutil
import numpy as np
from PIL import Image, ImageFilter

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
COLOR = ROOT / 'Game/Assets/_Project/Art/Characters/Fire/cannon_crew_ignite6.png'
TARGET = COLOR.with_name('cannon_crew_ignite6_normal.png')

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def smooth(a, r):
    return np.asarray(Image.fromarray(np.uint8(np.clip(a, 0, 1)*255)).filter(ImageFilter.GaussianBlur(r)), dtype=float)/255

def main():
    source_hash = sha(COLOR)
    game = Image.open(COLOR).convert('RGBA')
    assert game.size == (2592,384)
    native = game.resize((864,128),Image.Resampling.NEAREST)
    assert np.array_equal(np.asarray(native.resize(game.size,Image.Resampling.NEAREST)),np.asarray(game)), '색 원본이 정확한 3배 격자가 아님'
    native.save(OUT/'cannon_crew_ignite6_native.png')
    a = np.asarray(native)
    out = a.copy()
    out[:,:,:3] = [128,128,255]
    for col in range(6):
        sl = np.s_[:,col*144:(col+1)*144]
        f = a[sl]
        mask = f[:,:,3] > 0
        lum = (f[:,:,:3] @ np.array([.2126,.7152,.0722]))/255
        height = 3.8*smooth(mask,2.)+1.8*smooth(mask,5.)+.45*smooth(lum*mask,.8)
        dy, dx = np.gradient(height)
        n = np.stack([-dx,dy,np.ones_like(dx)],axis=-1)
        n /= np.linalg.norm(n,axis=-1,keepdims=True)
        rgb = np.uint8(np.clip(np.rint(128+127*n),0,255))
        rgb[~mask] = [128,128,255]
        out[sl][:,:,:3] = rgb
    normal = Image.fromarray(out)
    normal.save(OUT/'cannon_crew_ignite6_native_normal.png')
    expanded = normal.resize(game.size,Image.Resampling.NEAREST)
    expanded.save(OUT/'cannon_crew_ignite6_normal.png')
    rows = []
    for col in range(6):
        nc = out[:,col*144:(col+1)*144]
        cc = a[:,col*144:(col+1)*144]
        mask = cc[:,:,3] > 0
        lengths = np.linalg.norm((nc[:,:,:3].astype(float)-128)/127,axis=-1)
        checks = dict(size_equal=nc.shape==cc.shape,
                      alpha_equal=np.array_equal(nc[:,:,3],cc[:,:,3]),
                      transparent_rgb_flat=bool(np.all(nc[~mask,:3]==[128,128,255])),
                      vector_length_valid=bool(np.all((lengths[mask]>.99)&(lengths[mask]<1.01))),
                      positive_z=bool(np.all(nc[mask,2]>128)))
        rows.append(dict(frame=col+1,checks=checks,vector_length_min=float(lengths[mask].min()),vector_length_max=float(lengths[mask].max())))
    assert all(all(x['checks'].values()) for x in rows)
    assert np.array_equal(np.asarray(expanded)[:,:,3],np.asarray(game)[:,:,3])
    assert sha(COLOR)==source_hash
    before_meta = Path(str(TARGET)+'.meta')
    before_meta_hash = sha(before_meta) if before_meta.exists() else None
    shutil.copyfile(OUT/'cannon_crew_ignite6_normal.png',TARGET)
    assert sha(TARGET)==sha(OUT/'cannon_crew_ignite6_normal.png')
    assert sha(COLOR)==source_hash
    if before_meta_hash: assert sha(before_meta)==before_meta_hash
    report = dict(date='2026-10-21',method_ko='K081 resume_wizard_normals.py의 smooth/gradient 수식을 동일하게 재사용한 근사 기술 법선. 3D 베이크·새 그림 생성 아님.',
                  source_color=str(COLOR.relative_to(ROOT)).replace('\\','/'),source_color_sha256=source_hash,source_color_unmodified=True,
                  native_cell=[144,128],game_cell=[432,384],native_pivot_top_left=[96,120],game_pivot_top_left=[288,360],columns=6,rows=1,
                  normal_sha256=sha(TARGET),target_normal=str(TARGET.relative_to(ROOT)).replace('\\','/'),
                  native_size=[864,128],game_size=[2592,384],scale=3,exact_grid_verified=True,game_alpha_equal=True,copy_hash_equal=True,
                  frames=rows,all_pass=True,passed_checks=30,total_checks=30,unity_verified=False)
    (OUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('6 frames / 30 checks passed; color hash unchanged; normal copy exact.')

if __name__=='__main__':
    main()
