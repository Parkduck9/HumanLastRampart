from PIL import Image;import numpy as np,collections,sys
S=r'C:\Users\User\AppData\Local\Temp\claude\C--Unity-1-HumanLastRampart\9f0dc00c-aa09-4284-9d23-fb368fa7bf5d\scratchpad'
G=r'C:\Unity\1\HumanLastRampart\Game\Assets\_Project\Art\Characters\Hero\hero_walk_run_grid.png'
con=np.array(Image.open(S+r'\k139_concept.png')).astype(float)
cur=np.array(Image.open(G))
NH=int(sys.argv[1]) if len(sys.argv)>1 else 73   # native figure height
# palette + outline colour from current rows 0/3
pal=collections.Counter();edge=collections.Counter()
for r in (0,3):
  c=cur[r*384:(r+1)*384]
  op=c[:,:,3]>128
  for y,x in zip(*np.where(op)):
    if y%3 or x%3: continue
    col=tuple(c[y,x,:3]);pal[col]+=1
    if y+3<384 and x+3<c.shape[1] and x>=3 and (not op[y+3,x] or not op[y,x-3] or not op[y,x+3]): edge[col]+=1
P=np.array([k for k,v in pal.items() if v>=3],float)
outline=np.array(edge.most_common(1)[0][0])
print('palette',len(P),'outline',outline)
al=con[:,:,3]>40
figs=[]
for (y0,y1) in [(47,422),(463,845)]:
  sub=al[y0:y1].any(0);xs=np.where(sub)[0]
  # split columns
  runs=[];s=xs[0];p=xs[0]
  for x in xs[1:]:
    if x>p+1: runs.append((s,p+1));s=x
    p=x
  runs.append((s,p+1))
  figs.append([(y0,y1,a,b) for a,b in runs if b-a>20])
out=np.zeros((768,1152,4),np.uint8)
for ri,row in enumerate(figs):
  for k,(y0,y1,x0,x1) in enumerate(row):
    f=con[y0:y1,x0:x1]
    a=f[:,:,3]/255.0
    ys=np.where(a.max(1)>0.15)[0];f=f[ys[0]:ys[-1]+1];a=a[ys[0]:ys[-1]+1]
    h,w=a.shape;s=NH/h;nw=max(1,round(w*s))
    pm=np.dstack([f[:,:,:3]*a[:,:,None],a*255])
    im=Image.fromarray(pm.clip(0,255).astype(np.uint8),'RGBA').resize((nw,NH),Image.BOX)
    n=np.array(im).astype(float)
    na=n[:,:,3]/255.0;op=na>0.5
    rgb=np.where(op[:,:,None],n[:,:,:3]/np.maximum(na,1e-3)[:,:,None],0)
    # nearest palette colour
    d=((rgb[:,:,None,:]-P[None,None])**2).sum(-1);q=P[d.argmin(-1)]
    # outline on silhouette border
    pad=np.pad(op,1)
    border=op&~(pad[:-2,1:-1]&pad[2:,1:-1]&pad[1:-1,:-2]&pad[1:-1,2:])
    q[border]=outline
    big=np.kron(np.dstack([q,op*255.0]),np.ones((3,3,1)))
    bh,bw=big.shape[:2]
    oy=ri*384+360-bh; ox=k*288+144-bw//2
    ox-= ox%3 - (k*288)%3   # keep 3px grid
    reg=out[oy:oy+bh,ox:ox+bw];m=big[:,:,3]>0
    reg[m]=big[m].astype(np.uint8)
Image.fromarray(out).save(S+r'\k139_new.png')
# preview: current row0/3 vs new, 2x on grey
pv=Image.new('RGBA',(1152,768*2),(120,124,130,255))
c0=Image.fromarray(np.vstack([cur[0:384],cur[3*384:4*384]]))
pv.alpha_composite(c0,(0,0));pv.alpha_composite(Image.fromarray(out),(0,768))
pv.resize((1152,1536),Image.NEAREST).save(S+r'\photos\k139_refine_preview.png')
