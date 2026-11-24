from pathlib import Path
import json, hashlib
from PIL import Image

HERE=Path(__file__).resolve().parent
REF=HERE.parent
CW,CH=256,384

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def hair(im):
    # Read-only measurement used for whole-sprite scale, never separate/move head pixels.
    a=im.load(); pts=[]
    for y in range(im.height//2):
        for x in range(im.width):
            r,g,b,alpha=a[x,y]
            if alpha>32 and min(r,g,b)>=130 and max(r,g,b)-min(r,g,b)<65: pts.append((x,y))
    assert pts
    return (min(x for x,y in pts),min(y for x,y in pts),max(x for x,y in pts)+1,max(y for x,y in pts)+1)

def main():
    original=REF/'codex_hero_grid_v1/hero_walk_grid.png'
    before=sha(original);walk=Image.open(original).convert('RGBA')
    out=Image.new('RGBA',(1024,1536))
    out.paste(walk.crop((0,0,1024,768)),(0,0))
    out.paste(walk.crop((0,768,1024,1152)),(0,1152))
    reference=walk.crop((256,384,512,768));hb=hair(reference)
    # Running leans forward; one shared left offset keeps all trailing coat tails inside the cell.
    target_width=hb[2]-hb[0];anchor=((hb[0]+hb[2])/2-8,(hb[1]+hb[3])/2)
    info=[]
    for i in range(4):
        path=HERE/f'run_pose_{i}.png';im=Image.open(path).convert('RGBA')
        box=im.getchannel('A').point(lambda v:255 if v>32 else 0).getbbox()
        assert box;patch=im.crop(box);h=hair(patch)
        scale=target_width/(h[2]-h[0]);w=round(patch.width*scale);height=round(patch.height*scale)
        patch=patch.resize((w,height),Image.Resampling.NEAREST)
        dx=round(anchor[0]-(h[0]+h[2])/2*scale)
        dy=round(anchor[1]-(h[1]+h[3])/2*scale)+(0 if i%2==0 else -3)
        assert dx>=0 and dy>=0 and dx+w<CW and dy+height<CH,(i,dx,dy,w,height)
        cell=Image.new('RGBA',(CW,CH));cell.paste(patch,(dx,dy));out.paste(cell,(i*CW,2*CH))
        info.append(dict(frame=i,index=8+i,source=path.name,sourceSha256=sha(path),sourceRect=list(box),
                         scale=scale,packedRect=[dx,dy,w,height],footPivot=[128,352],wholeSpriteBob=0 if i%2==0 else -3))
    target=HERE/'hero_walk_run_grid.png';out.save(target)
    check=Image.open(target).convert('RGBA')
    assert check.tobytes()==out.tobytes()
    assert check.crop((0,0,1024,768)).tobytes()==walk.crop((0,0,1024,768)).tobytes()
    assert check.crop((0,1152,1024,1536)).tobytes()==walk.crop((0,768,1024,1152)).tobytes()
    assert before==sha(original)
    assert len({check.crop((i*CW,2*CH,(i+1)*CW,3*CH)).tobytes() for i in range(4)})==4
    meta=dict(image=target.name,size=[1024,1536],columns=4,rows=4,cell=[CW,CH],
              rowOrder=['back_walk','left_walk','left_run','front_walk'],footPivotTopLeft=[128,352],
              unityPivotBottomLeft=[0.5,32/384],aspect=2/3,framesPerAction=4,walkFps=6,runFps=8,
              runAvailable=True,runRow=2,rightWalkPhaseColumns=[2,3,0,1],rightRunPhaseColumns=[0,1,2,3],
              rightRequiresFlipX=True,requestedWorldSpeed=10,worldSpeedImplementedHere=False,
              walkSourceSha256=before,runFrames=info,sheetSha256=sha(target))
    (HERE/'hero_walk_run_grid.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
    (HERE/'qa.json').write_text(json.dumps(dict(status='PASS',walkPixelsUnchanged=True,runFramesDistinct=4,
        noClipping=True,commonPivot=[128,352],exactExport=True,unityPlayVerified=False),indent=2),encoding='utf-8')
    print('PASS old walk pixels preserved, 4 distinct run frames, cell bounds, common pivot, exact export')

if __name__=='__main__':main()
