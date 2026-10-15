"""Choose a non-mirrored crop from generated paired artwork; never repaint pixels.

Both halves use the exact same crop window. Source is preserved. Nearest resize
and normal-vector encoding happen in the main builder after this crop selection.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image

HERE=Path(__file__).resolve().parent

def ratios(im, rect, size):
    a=np.asarray(im.crop(rect).resize(tuple(size),Image.Resampling.NEAREST),dtype=float)
    ex=float(abs(a[:,0]-a[:,-1]).mean());ey=float(abs(a[0]-a[-1]).mean())
    ix=float(abs(a[:,1:]-a[:,:-1]).mean());iy=float(abs(a[1:]-a[:-1]).mean())
    return [ex/max(ix,.001),ey/max(iy,.001)]

def select(path,size):
    src=Image.open(path).convert('RGB');w,h=src.size
    assert w==2*h
    left=src.crop((0,0,h,h));right=src.crop((h,0,w,h))
    a=np.asarray(left,dtype=float);n=np.asarray(right,dtype=float)
    xs=range(0,int(h*.18),7);ends=range(int(h*.82),h,7)
    scores=[]
    for x0 in xs:
        for x1 in ends:
            cost=float(abs(a[:,x0]-a[:,x1]).mean()+.4*abs(n[:,x0]-n[:,x1]).mean())
            scores.append((cost,x0,x1+1))
    candidates=[]
    for _,x0,x1 in sorted(scores)[:12]:
        ys=[]
        for y0 in xs:
            for y1 in ends:
                cost=float(abs(a[y0,x0:x1]-a[y1,x0:x1]).mean()+.4*abs(n[y0,x0:x1]-n[y1,x0:x1]).mean())
                ys.append((cost,y0,y1+1))
        for _,y0,y1 in sorted(ys)[:12]:
            rect=(x0,y0,x1,y1);cr=ratios(left,rect,size);nr=ratios(right,rect,size)
            candidates.append((max(cr)+.25*max(nr),rect,cr,nr))
    _,rect,cr,nr=min(candidates)
    return {'cropPixels':list(rect),'colorRatios':cr,'normalRatiosBeforeEncoding':nr,'sourcePairSize':[w,h], 'method':'same-window crop only; no pixel painting, blending, mirroring or source modification'}

if __name__=='__main__':
    jobs=json.loads((HERE/'finalization_generation.json').read_text(encoding='utf-8'))['requests']
    outputs=[]
    for j in jobs:
        result=select(j['sourcePath'],j['size']);outputs.append(dict(id=j['id'],**result))
    (HERE/'selected_crop_review.json').write_text(json.dumps(outputs,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(outputs))
