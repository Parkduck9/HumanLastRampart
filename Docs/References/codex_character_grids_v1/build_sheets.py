"""Pack existing static demos into renderer-compatible 1x1 sheets; no new action frames."""
from pathlib import Path
from PIL import Image
import hashlib,json

HERE=Path(__file__).resolve().parent
REF=HERE.parent
ITEMS=[('archer','궁수','back','codex_soldier_back_v1/archer_back.png'),
       ('gunner','총병','back','codex_soldier_back_v1/gunner_back.png'),
       ('cannon_crew','대포병','back','codex_soldier_back_v1/cannon_crew_back.png'),
       ('ogre','오우거','front','codex_character_pixel_v2/ogre.png'),
       ('ladder_goblin','사다리 고블린','front','codex_character_pixel_v2/ladder_goblin.png'),
       ('wizard','마법사','front','codex_character_pixel_v2/wizard.png')]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    entries=[]
    for ident,label,facing,rel in ITEMS:
        source=REF/rel;original_hash=sha(source);raw=Image.open(source).convert('RGBA')
        box=raw.getchannel('A').point(lambda v:255 if v>32 else 0).getbbox()
        assert box;patch=raw.crop(box);w,h=patch.size
        # Existing demos lack a foot annotation. Infer boot center from lower brown leather,
        # using quantiles so a narrow wooden staff does not move the body pivot.
        boots=[]
        for y in range(int(h*.88),h):
            for x in range(w):
                r,g,b,a=patch.getpixel((x,y))
                if a>100 and r>45 and g>20 and r>g*1.08 and r>b*1.45:boots.append(x)
        boots.sort();pivot=(boots[int(len(boots)*.1)]+boots[min(len(boots)-1,int(len(boots)*.9))])/2 if boots else w/2
        scale=min(300/h,112/max(1,pivot),112/max(1,w-pivot))
        width,height=round(w*scale),round(h*scale)
        transformed=patch.resize((width,height),Image.Resampling.NEAREST)
        dx=128-round(pivot*width/w);dy=352-height
        assert 0<=dx and dx+width<256 and 0<=dy and dy+height<384
        cell=Image.new('RGBA',(256,384));cell.paste(transformed,(dx,dy))
        path=HERE/f'{ident}_static_grid.png';cell.save(path)
        loaded=Image.open(path).convert('RGBA');assert loaded.tobytes()==cell.tobytes()
        assert original_hash==sha(source)
        bbox=loaded.getchannel('A').point(lambda v:255 if v>32 else 0).getbbox()
        assert bbox and bbox[0]>0 and bbox[1]>0 and bbox[2]<256 and bbox[3]<384
        # Nearest-only packing must not synthesize any color or alter the costume palette.
        assert set(loaded.getdata()).issubset(set(patch.getdata())|{(0,0,0,0)})
        info=dict(id=ident,label=label,facing=facing,image=path.name,size=[256,384],columns=1,rows=1,
                  cell=[256,384],footPivotTopLeft=[128,352],unityPivotBottomLeft=[.5,32/384],aspect=2/3,
                  frameIndex=0,frames=1,animated=False,fps=0,status='demo-static-only',
                  source=rel,sourceSha256=original_hash,sourceRect=list(box),sourceFootX=pivot,
                  pivotMethod='lower boot-color quantiles; demo inference, review in Unity',
                  uniformScale=scale,packedRect=[dx,dy,width,height],sheetSha256=sha(path),
                  unityPlayVerified=False)
        (HERE/f'{ident}_static_grid.json').write_text(json.dumps(info,ensure_ascii=False,indent=2),encoding='utf-8')
        entries.append(info)
    (HERE/'manifest.json').write_text(json.dumps(dict(staticOnly=True,sheets=entries),ensure_ascii=False,indent=2),encoding='utf-8')
    (HERE/'qa.json').write_text(json.dumps(dict(status='PASS',sheets=6,transparent=True,
        noClipping=True,commonCell=[256,384],commonPivot=[128,352],sourceHashesUnchanged=True,
        noNewColors=True,exactExport=True,unityPlayVerified=False),indent=2),encoding='utf-8')
    print('PASS 6 static sheets: dimensions, alpha, bounds, shared pivot, original hashes, palette, exact export')

if __name__=='__main__':main()
