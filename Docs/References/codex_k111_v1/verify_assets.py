import json
from pathlib import Path
from collections import deque
from PIL import Image, ImageChops, ImageStat

root = Path(__file__).resolve().parent
repo = root.parents[2]
checks = []
assets = []

def check(name, ok, detail=None):
    checks.append(dict(name=name, passed=bool(ok), detail=detail))

def components(im):
    a = im.getchannel('A')
    pts = {(x,y) for y in range(im.height) for x in range(im.width) if a.getpixel((x,y))}
    sizes = []
    while pts:
        q = deque([pts.pop()]); n = 0
        while q:
            x,y = q.popleft(); n += 1
            for dx in (-1,0,1):
                for dy in (-1,0,1):
                    p = (x+dx,y+dy)
                    if p in pts: pts.remove(p); q.append(p)
        sizes.append(n)
    return sizes

names = ['neutral','determined','alarmed','tired']
for i,n in enumerate(names):
    im = Image.open(root/f'portrait_hagen_{n}.png').convert('RGBA')
    old = Image.open(repo/f'Docs/References/codex_k108_v1/portrait_hagen_{n}.png').convert('RGBA')
    delta = ImageChops.difference(im,old)
    changed = [(x,y) for y in range(384) for x in range(256) if delta.getpixel((x,y)) != (0,0,0,0)]
    check(f'{n}: mouth changed', len(changed)>0, len(changed))
    check(f'{n}: only mouth changed', all(128<=x<152 and 128<=y<140 for x,y in changed))
    check(f'{n}: original alpha preserved', im.getchannel('A').tobytes()==old.getchannel('A').tobytes())
    check(f'{n}: original foot bounds preserved', im.getbbox()==old.getbbox())
    check(f'{n}: no stray components',len(components(im))==1)

native=Image.open(root/'hagen_expressions_native_96x128.png').convert('RGBA')
game=Image.open(root/'hagen_expressions_game_288x384.png').convert('RGBA')
check('Hagen exact 3x nearest',game.tobytes()==native.resize((1152,384),Image.Resampling.NEAREST).tobytes())
for i,n in enumerate(names):
    cell=native.crop((96*i,0,96*(i+1),128)); box=cell.getbbox()
    old=Image.open(repo/'Docs/References/codex_k108_v1/hagen_expressions_native_96x128.png').crop((96*i,0,96*(i+1),128)).convert('RGBA')
    check(f'{n}: native foot y120',box[3]==120,box)
    check(f'{n}: native body bounds unchanged',box==old.getbbox())
    check(f'{n}: native no stray components',len(components(cell))==1)

for n,rad,peak in zip(['low','normal','high'],[36,46,56],[40,64,88]):
    im=Image.open(root/f'native/glow_{n}.png').convert('RGBA'); box=im.getbbox()
    check(f'glow {n}: compact transparent border', box[0]>=40 and box[1]>=40 and box[2]<=152 and box[3]<=152,box)
    check(f'glow {n}: restrained alpha',im.getchannel('A').getextrema()==(0,peak))
    check(f'glow {n}: connected',len(components(im))==1)
    gi=Image.open(root/f'game/glow_{n}.png').convert('RGBA')
    check(f'glow {n}: exact 3x', gi.tobytes()==im.resize(gi.size,Image.Resampling.NEAREST).tobytes())

fog=Image.open(root/'native/fog_band.png').convert('RGBA'); box=fog.getbbox()
check('fog only thin band',box[1]>=36 and box[3]<=64,box)
check('fog exact tiling edges',fog.crop((0,0,1,128)).tobytes()==fog.crop((511,0,512,128)).tobytes())
check('fog no stray components',len(components(fog))==1)
check('fog lower half empty',fog.crop((0,64,512,128)).getbbox() is None)
fg=Image.open(root/'game/fog_band.png').convert('RGBA')
check('fog exact 3x',fg.tobytes()==fog.resize(fg.size,Image.Resampling.NEAREST).tobytes())
for n in ['dead','relit']:
    im=Image.open(root/f'story_{n}_beacon_1920x1080.png').convert('RGBA')
    check(f'story {n}: size and opaque',im.size==(1920,1080) and im.getchannel('A').getextrema()==(255,255))
    lum=im.convert('L'); lower=ImageStat.Stat(lum.crop((0,720,1920,1080)))
    check(f'story {n}: calm dialogue third',lower.mean[0]<60 and lower.stddev[0]<30,dict(mean=lower.mean[0],stddev=lower.stddev[0]))

for p in sorted(root.glob('previews/*_4x.png')):
    stem=p.stem[:-3]
    if stem.startswith('portrait_'): src=root/(stem+'.png')
    elif stem=='hagen_expressions_native': src=root/'hagen_expressions_native_96x128.png'
    elif stem.startswith('glow_') or stem=='fog_band': src=root/'native'/(stem+'.png')
    else: src=root/(stem+'_1920x1080.png')
    im=Image.open(src).convert('RGBA'); prev=Image.open(p).convert('RGBA')
    check(f'{p.name}: exact 4x',prev.size==(im.width*4,im.height*4) and prev.tobytes()==im.resize(prev.size,Image.Resampling.NEAREST).tobytes())

for p in sorted(root.rglob('*.png')):
    if any(s in p.parts for s in ['sources']) or p.name.startswith('review_'): continue
    im=Image.open(p).convert('RGBA'); rel=p.relative_to(root).as_posix()
    entry=dict(file=rel,size=list(im.size),mode='RGBA',alpha_extrema=list(im.getchannel('A').getextrema()))
    if rel.startswith('previews/'):entry.update(role='preview_4x',scale=4,filter='nearest')
    elif p.name.startswith('portrait_hagen_'):entry.update(role='story_portrait',frames=1,frame_order=[p.stem.replace('portrait_hagen_','')],cell=[256,384],pivot_px=[128,352])
    elif p.name.startswith('hagen_expressions_'):
        cell=[96,128] if 'native' in p.name else [288,384] if 'game' in p.name else [256,384]
        entry.update(role='expression_sheet',frames=4,frame_order=names,cell=cell,pivot_px=[48,120] if cell[0]==96 else [144,360] if cell[0]==288 else [128,352])
    elif 'glow_' in p.name:entry.update(role='compact_glow',frames=1,frame_order=[0],pivot_px=[im.width//2,im.height//2],blend='SrcAlpha One',filter='Point')
    elif p.name=='fog_band.png':entry.update(role='far_edge_fog',frames=1,frame_order=[0],pivot_px=[im.width//2,im.height//2],blend='alpha',wrap='repeat U clamp V')
    else:entry.update(role='story_background' if p.name.startswith('story_') else 'screenshot_composite',frames=1,frame_order=[0],pivot_px=[960,540],safe_dialogue_rect_px=[0,720,1920,1080] if p.name.startswith('story_') else None)
    check(f'{rel}: transparent RGB clean', all(a!=0 or (r==g==b==0) for r,g,b,a in im.get_flattened_data()))
    assets.append(entry)

manifest=dict(card='K-111',version=1,status='assets_only_integration_pending',generation='built-in image_gen; exact mouth-only transfer; compact alpha and edge cleanup; nearest scaling; real screenshot compositing',coordinates='top-left pixels; foot baseline is exclusive bottom edge',frame_order=names,assets=assets,reference_inputs=['Docs/References/codex_k108_v1/','Docs/References/codex_k103_v1/','Docs/References/codex_k105_v1/','Docs/References/beacon_game_20261022/05_pf_night_clean.png','Docs/References/beacon_game_20261022/01_pf_day_clean.png','Docs/References/beacon_game_20261022/08_pf_map_side.png'],replacement_policy='Keep source filenames. K-103 bodies/flames unchanged; replace only glows and fog band, disable legacy broad fog corners/wisps. K-108 and K-105 assets are not yet in Game at this worktree snapshot.',fog=dict(native_size=[512,128],occupied_y_limit=[36,64],game_size=[1536,384],placement='entire geometry beyond 45m; far edge only',legacy_corners_and_wisps='disable; no revised corner or wisp delivered'),glow=dict(native_size=[192,192],game_size=[576,576],native_radii=[36,46,56],maximum_alpha=[40,64,88],anchor_on_brazier_native=[48,72],anchor_on_brazier_game=[144,216],shadows='mockup contact/cast shadows only; runtime shadow/occlusion integration pending'),story=dict(size=[1920,1080],safe_dialogue_rect_px=[0,720,1920,1080],people='distant faceless silhouettes; no hero-specific costume',camera='pulled-back oblique view; square footprint from real tactical screenshot, masonry reference from actual battle screenshot'),mockup=dict(source='Docs/References/beacon_game_20261022/05_pf_night_clean.png',type='asset composite, not a new game capture',preserved='original camera, field, soldiers, wall geometry, original torch and distant fire effects',changes='blue-black grade, two K-103 braziers, warm local lighting, geometric cast shadows, thin far-edge band'),integration_pending=['Unity import/Play and actual screenshot','World-space >=45m fog placement and removal of legacy broad overlays','Runtime point light, shadow and occlusion setup','User visual approval'],verification_file='validation.json')
(root/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
result=dict(passed=all(c['passed'] for c in checks),checks_count=len(checks),checks=checks)
(root/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(passed=result['passed'],checks=len(checks),failures=[c for c in checks if not c['passed']]),ensure_ascii=False))
raise SystemExit(0 if result['passed'] else 1)
