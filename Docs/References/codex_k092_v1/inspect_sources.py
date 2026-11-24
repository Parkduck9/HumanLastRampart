from pathlib import Path
from PIL import Image, ImageDraw
import json

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
paths = sorted((ROOT/'Game/Assets/_Project/Art/Characters').rglob('*.png')) + sorted((ROOT/'Game/Assets/_Project/Art/Battle/Resources/BattleArt/Cannon').glob('*.png'))
paths = [p for p in paths if not p.stem.endswith(('_normal', '_spec'))]
board = Image.new('RGB', (1200, ((len(paths)+5)//6)*260), '#353a46')
d = ImageDraw.Draw(board)
data = []
for i,p in enumerate(paths):
    im = Image.open(p).convert('RGBA')
    w,h = im.size
    cw = 288 if w%288==0 and h%384==0 else 256
    ch = 384
    if p.stem in ['crew_pull6','crew_push6']: cw=384
    if p.stem.startswith('haul_cannon'): cw=w; ch=h
    if p.stem in ['cannon_recoil','muzzle_smoke']: ch=256
    if p.parent.name=='Projectiles': ch=128
    if p.stem in ['ladder_front','ladder_side']: ch=h
    first=im.crop((0,0,min(cw,w),min(ch,h)))
    first.thumbnail((190,218),Image.Resampling.NEAREST)
    x=(i%6)*200; y=(i//6)*260
    board.paste(first,(x+(200-first.width)//2,y+24),first)
    d.text((x+3,y+3),p.stem,fill='white',font_size=10)
    d.text((x+3,y+240),f'{w}x{h} / {cw}x{ch}',fill='white',font_size=12)
    colors=sorted(im.getcolors(w*h) or [],reverse=True)
    data.append({'name':p.stem,'path':p.relative_to(ROOT).as_posix(),'size':[w,h],'cell':[min(cw,w),min(ch,h)],'colors':[[n,list(c)] for n,c in colors if c[3]>0][:80]})
board.save(OUT/'source_contact.png')
(OUT/'source_inventory.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(paths))
