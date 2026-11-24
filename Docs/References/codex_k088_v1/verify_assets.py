from pathlib import Path
from PIL import Image
import numpy as np
import json, hashlib

def is_connected(mask):
    points=set(zip(*np.where(mask)))
    if not points:
        return False
    todo=[points.pop()]
    while todo:
        y,x=todo.pop()
        for p in ((y-1,x),(y+1,x),(y,x-1),(y,x+1)):
            if p in points:
                points.remove(p)
                todo.append(p)
    return not points

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
m=json.loads((HERE/'manifest.json').read_text(encoding='utf-8'))
checks=[]
for a in m['assets']:
    im=Image.open(HERE/a['native_file'])
    assert im.mode=='RGBA' and list(im.size)==a['native_size_px'],a['id']
    arr=np.asarray(im)
    cell_w=a.get('native_cell_px',list(im.size))[0]
    frame_reports=[]
    for f in range(a['frame_count']):
        cell=arr[:,f*cell_w:(f+1)*cell_w]
        alpha=cell[:,:,3]
        assert alpha.max()>0 and alpha.min()==0
        assert not (alpha[0].any() or alpha[-1].any() or alpha[:,0].any() or alpha[:,-1].any())
        assert is_connected(alpha>0),'분리 픽셀: '+a['id']
        assert not cell[alpha==0].any(),'투명 픽셀 RGB 잔류'
        box=Image.fromarray(cell).getbbox()
        if a['kind']=='billboard_vfx':
            assert list(box)==a['frame_alpha_bounds_top_left'][f]
            assert box[3]<120,'발 기준 아래 불필요한 픽셀'
        frame_reports.append(dict(frame=f,bounds=list(box),alpha_levels=len(np.unique(alpha))))
    if 'game_file' in a:
        game=np.asarray(Image.open(HERE/a['game_file']))
        assert np.array_equal(game,np.repeat(np.repeat(arr,3,0),3,1))
    preview=np.asarray(Image.open(HERE/a['preview_4x']))
    assert np.array_equal(preview,np.repeat(np.repeat(arr,4,0),4,1))
    if a['kind']=='ground_contact_shadow':
        alpha=arr[:,:,3]
        assert np.array_equal(alpha,alpha[:,::-1]) and np.array_equal(alpha,alpha[::-1,:])
        assert len(np.unique(alpha))>100
        half=alpha[im.height//2,:im.width//2]
        assert (np.diff(half.astype(int))>=0).all()
        assert not arr[:,:,:3].any()
    checks.append(dict(id=a['id'],passed=True,frames=frame_reports))
right=m['assets'][0]['frame_alpha_bounds_top_left']
back=m['assets'][1]['frame_alpha_bounds_top_left']
assert right[0][0]==right[1][0]==48
assert back[0][3]==back[1][3]==81
sm=m['assets'][2]['frame_alpha_bounds_top_left']
assert all(sm[i+1][1]<sm[i][1] for i in range(3))
for ref in m['source_references']:
    assert hashlib.sha256((ROOT/ref['file']).read_bytes()).hexdigest()==ref['sha256']
report=dict(passed=True,asset_count=len(checks),assets=checks,
    foot_line='N/A: VFX have no feet; y=120 reference grid preserved and no alpha reaches it.',
    socket_anchor_consistent=True,smoke_rises=True,no_disconnected_pixels=True,
    nearest_neighbor_3x_exact=True,previews_4x_exact=True,smooth_shadows=True,
    reference_files_unchanged=True,unity_tested=False)
(HERE/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('자산 검증 통과:',len(checks),'종')
