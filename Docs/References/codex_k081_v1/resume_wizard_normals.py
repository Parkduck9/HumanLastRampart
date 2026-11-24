"""K-081: 기존 색 그림을 변경하지 않는 결정적 기술 법선 재계산/검사."""
from pathlib import Path
import json, hashlib, base64, re
import numpy as np
from PIL import Image, ImageFilter

R = Path(__file__).resolve().parents[3]
O = Path(__file__).resolve().parent
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def smooth(a, r):
    return np.asarray(Image.fromarray(np.uint8(np.clip(a, 0, 1)*255)).filter(ImageFilter.GaussianBlur(r)), dtype=float)/255
def save(im, p):
    p.parent.mkdir(parents=True, exist_ok=True)
    im.save(p)
def uri(p): return 'data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode()

m = json.loads((O/'manifest.json').read_text(encoding='utf-8-sig'))
preserved = {p.relative_to(O).as_posix(): sha(p) for p in O.rglob('*.png')}
source_hashes = {}
changed = []
for s in m['sheets']:
    if s['source_card'] != 'K-076': continue
    stem = Path(s['sheet']).stem
    src = R/'Docs/References/codex_k076_v1/native'/s['sheet']
    game = R/s['source_game']
    source_hashes[str(src)] = sha(src)
    source_hashes[str(game)] = sha(game)
    ni = Image.open(src).convert('RGBA'); gi = Image.open(game).convert('RGBA')
    assert np.array_equal(np.asarray(ni.resize(gi.size, Image.Resampling.NEAREST)), np.asarray(gi))
    a = np.asarray(ni); out = a.copy(); out[:, :, :3] = [128, 128, 255]
    cw, ch = s['native_cell']
    for row in range(s['rows']):
        for col in range(s['columns']):
            sl = np.s_[row*ch:(row+1)*ch, col*cw:(col+1)*cw]
            f = a[sl]; mask = f[:, :, 3] > 0
            lum = (f[:, :, :3] @ np.array([.2126, .7152, .0722]))/255
            height = 3.8*smooth(mask, 2.)+1.8*smooth(mask, 5.)+.45*smooth(lum*mask, .8)
            dy, dx = np.gradient(height)
            n = np.stack([-dx, dy, np.ones_like(dx)], axis=-1)
            n /= np.linalg.norm(n, axis=-1, keepdims=True)
            rgb = np.uint8(np.clip(np.rint(128+127*n), 0, 255)); rgb[~mask] = [128,128,255]
            out[sl][:, :, :3] = rgb
    normal = Image.fromarray(out)
    s['source_native'] = src.relative_to(R).as_posix()
    s['source_sha256'] = sha(game)
    s['native'] = f'native/resume_v2/{stem}_native_normal.png'
    s['game'] = f'game/resume_v2/{stem}_normal.png'
    s['preview_4x'] = f'previews/resume_v2/{stem}_normal_4x.png'
    save(normal, O/s['native']); save(normal.resize(gi.size, Image.Resampling.NEAREST), O/s['game'])
    save(normal.resize((ni.width*4, ni.height*4), Image.Resampling.NEAREST), O/s['preview_4x'])
    v = (out[:, :, :3].astype(float)-128)/127
    v /= np.maximum(np.linalg.norm(v, axis=-1, keepdims=True), 1e-8)
    for key, vec, tint in [('upper_left',[-.6,.7,.65],[1,.98,.92]),('torch_lower_right',[.7,-.65,.6],[1,.65,.34])]:
        lv = np.array(vec); lv /= np.linalg.norm(lv)
        factor = .26+np.maximum(0, v@lv)[:, :, None]*np.array(tint)[None,None,:]*.95
        b = a.copy(); b[:, :, :3] = np.uint8(np.clip(a[:, :, :3]*factor, 0, 255)); b[a[:, :, 3]==0,:3] = 0
        s['lit_'+key] = f'previews/resume_v2/{stem}_{key}.png'
        save(Image.fromarray(b), O/s['lit_'+key])
    changed.append(s['sheet'])
m['resume_note_ko'] = '최신 K076 마법사 4시트만 별도 resume_v2 기술 법선으로 대체. 옛 manifest와 기존 PNG 보존.'
(O/'manifest_current.json').write_text(json.dumps(m, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
checks=[]
for s in m['sheets']:
    sn=Image.open(R/s['source_native']).convert('RGBA'); sg=Image.open(R/s['source_game']).convert('RGBA')
    nn=Image.open(O/s['native']).convert('RGBA'); ng=Image.open(O/s['game']).convert('RGBA')
    flags={
        'native_size_equal': nn.size==sn.size,
        'native_alpha_equal': np.array_equal(np.asarray(nn)[:,:,3],np.asarray(sn)[:,:,3]),
        'game_size_equal': ng.size==sg.size,
        'game_alpha_equal': np.array_equal(np.asarray(ng)[:,:,3],np.asarray(sg)[:,:,3]),
        'exact_manifest_scale': ng.size==(nn.width*s['scale'],nn.height*s['scale']) and np.array_equal(np.asarray(nn.resize(ng.size,Image.Resampling.NEAREST)),np.asarray(ng)),
        'source_hash_equal': sha(R/s['source_game'])==s['source_sha256']}
    checks.append({'sheet':s['sheet'],'checks':flags})
assert all(sha(O/p)==h for p,h in preserved.items()), '기존 PNG 변경 감지'
assert all(sha(Path(p))==h for p,h in source_hashes.items()), '색 그림 변경 감지'
result={'date':'2026-10-20','manifest':'manifest_current.json','sheets':len(checks),'frames':sum(s['frame_count'] for s in m['sheets']), 'checks':checks, 'all_pass':all(all(x['checks'].values()) for x in checks), 'passed_checks':sum(sum(x['checks'].values()) for x in checks), 'total_checks':len(checks)*6, 'source_images_modified':False, 'existing_png_preserved':True, 'regenerated_sheets':changed, 'unity_verified':False, 'hash_basis':'source_game'}
assert result['all_pass'], [x for x in checks if not all(x['checks'].values())]
(O/'resume_validation_current.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
html_path=O/'resume_preview.html'
h=html_path.read_text(encoding='utf-8-sig')
start=h.index('const data=')+len('const data='); end=h.index(';const sel=',start)
data=json.loads(h[start:end])
for item,s in zip(data,m['sheets']):
    assert item['name']==s['sheet']
    item['images']=[uri(R/s['source_native']),uri(O/s['native']),uri(O/s['lit_upper_left']),uri(O/s['lit_torch_lower_right'])]
h=h[:start]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+h[end:]
h=h.replace('마법사 4시트는 최신 색 그림과 불일치하여 연결 보류입니다.','마법사 4시트를 최신 K076 색 그림에 맞춰 기술 법선으로 재계산했습니다.')
h=h.replace('검사 210개 중 202개 통과, 마법사 4시트의 알파/원색해시 8개 불일치. 31시트·125프레임은 검사 통과했습니다. 기존 그림과 노멀 PNG는 변경하지 않았습니다.','최신 manifest_current.json 기준 35시트·135프레임 / 210개 검사 모두 통과. 마법사 4시트는 resume_v2 별도 출력이며 기존 색 그림과 노멀 PNG는 보존했습니다.')
h=h.replace('이번 재개에서는 남아 있던 PNG를 보존하고 불일치를 검출했으며, 마법사 노멀은 아직 재생성하지 않았습니다.','이번 재개에서 최신 마법사 4개 기술 법선을 별도 출력했습니다. 색 그림 재제작이나 3D 베이크는 수행하지 않았습니다.')
html_path.write_text(h,encoding='utf-8')
(R/'_HTML/codex_K081_노멀맵재개검토_v1.html').write_text(h,encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='checks'},ensure_ascii=False))
