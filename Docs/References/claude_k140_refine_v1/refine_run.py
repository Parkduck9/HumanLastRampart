# K-140 side run concept -> sheet row 2 (same method as D-088 refine.py/finish.py)
from PIL import Image;import numpy as np,collections,math
S=r'C:\Users\User\AppData\Local\Temp\claude\C--Unity-1-HumanLastRampart\9f0dc00c-aa09-4284-9d23-fb368fa7bf5d\scratchpad'
G=r'C:\Unity\1\HumanLastRampart\Game\Assets\_Project\Art\Characters\Hero\hero_walk_run_grid'
con=np.array(Image.open(S+r'\k140_concept.png')).astype(float)
cur=np.array(Image.open(G+'.png'));curn=np.array(Image.open(G+'_normal.png'))
NH=74; TOP,GROUND=116,643   # common vertical range: ground line of the concept -> y=359
pal=collections.Counter();edge=collections.Counter()
for r in (0,2,3):
  c=cur[r*384:(r+1)*384];op=c[:,:,3]>128
  for y,x in zip(*np.where(op)):
    if y%3 or x%3: continue
    col=tuple(c[y,x,:3]);pal[col]+=1
    if 3<=x<c.shape[1]-3 and y+3<384 and (not op[y+3,x] or not op[y,x-3] or not op[y,x+3]): edge[col]+=1
P=np.array([k for k,v in pal.items() if v>=3],float);outline=np.array(edge.most_common(1)[0][0])
figs=[(81,467),(614,1015),(1121,1551),(1685,2032)]
s=NH/(GROUND-TOP)
sheet=cur.copy();sheet[2*384:3*384]=0
for k,(x0,x1) in enumerate(figs):
  f=con[TOP:GROUND,x0-4:x1+4].copy();a=f[:,:,3]/255.0;a[a<0.5]=0
  h,w=a.shape;nw=round(w*s)
  pm=np.dstack([f[:,:,:3]*a[:,:,None],a*255])
  n=np.array(Image.fromarray(pm.clip(0,255).astype(np.uint8),'RGBA').resize((nw,NH),Image.BOX)).astype(float)
  na=n[:,:,3]/255.0;op=na>0.5
  rgb=np.where(op[:,:,None],n[:,:,:3]/np.maximum(na,1e-3)[:,:,None],0)
  q=P[((rgb[:,:,None,:]-P[None,None])**2).sum(-1).argmin(-1)]
  pad=np.pad(op,1);border=op&~(pad[:-2,1:-1]&pad[2:,1:-1]&pad[1:-1,:-2]&pad[1:-1,2:]);q[border]=outline
  # horizontal anchor: centre of the head+torso (top 55%) -> x=144
  cols=np.where(op[:int(NH*0.55)].any(0))[0];anchor=(cols[0]+cols[-1]+1)/2
  big=np.kron(np.dstack([q,op*255.0]),np.ones((3,3,1)))
  oy=2*384+360-big.shape[0];ox=k*288+144-int(round(anchor))*3
  reg=sheet[oy:oy+big.shape[0],ox:ox+big.shape[1]];m=big[:,:,3]>0;reg[m]=big[m].astype(np.uint8)
  xs=np.where(op.any(0))[0];print(k,'cell x',ox-k*288+xs[0]*3,ox-k*288+xs[-1]*3+2,'native w',nw)
T=[0.52,0.42,0.29,0.15];tilt=lambda d:T[d] if d<4 else 0.0
norm=curn.copy();norm[2*384:3*384]=(128,128,255,255)
for k in range(4):
  a=sheet[2*384:3*384:3,k*288:(k+1)*288:3,3]>128;H,W=a.shape
  for y in range(H):
    for x in range(W):
      if not a[y,x]: continue
      def run(dy,dx):
        d=0
        while 0<=y+(d+1)*dy<H and 0<=x+(d+1)*dx<W and a[y+(d+1)*dy,x+(d+1)*dx]: d+=1
        return d
      dl,dr,du,dd=run(0,-1),run(0,1),run(-1,0),run(1,0)
      sx=-tilt(dl) if dl<dr else (tilt(dr) if dr<dl else 0.0);sy=tilt(du) if du<dd else (-tilt(dd) if dd<du else 0.0)
      z=math.sqrt(max(0.0,1-sx*sx-sy*sy));l=math.sqrt(sx*sx+sy*sy+z*z)
      Y=2*384+y*3;X=k*288+x*3;norm[Y:Y+3,X:X+3]=(round(128+127*sx/l),round(128+127*sy/l),round(128+127*z/l),255)
Image.fromarray(sheet).save(S+r'\sole\k140_sheet.png');Image.fromarray(norm).save(S+r'\sole\k140_sheet_normal.png')
bad=[]
for r in range(4):
  for c in range(4):
    cell=sheet[r*384:(r+1)*384,c*288:(c+1)*288];op=cell[:,:,3]>128;tr=cell[:,:,3]<20;best=0
    for d in (-1,1):
      for x in range(1,287):
        run_=0
        for y in range(384):
          if op[y,x] and tr[y,x+d]: run_+=1;best=max(best,run_)
          else: run_=0
    ys=np.where(op.any(1))[0];b=ys[-1];row=cell[b][op[b]];dark=((row[:,0]<55)&(row[:,1]<55)&(row[:,2]<55)).sum()
    if best>30 or dark<len(row)*0.85: bad.append((r,c,best,dark,len(row)))
print('BAD',bad)
o=Image.new('RGBA',(1152,768),(120,124,130,255))
o.alpha_composite(Image.fromarray(cur[768:1152]),(0,0));o.alpha_composite(Image.fromarray(sheet[768:1152]),(0,384))
o.resize((1728,1152),Image.NEAREST).save(S+r'\photos\k140_compare.png')
