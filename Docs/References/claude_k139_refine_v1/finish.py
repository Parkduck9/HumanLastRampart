from PIL import Image;import numpy as np,math
S=r'C:\Users\User\AppData\Local\Temp\claude\C--Unity-1-HumanLastRampart\9f0dc00c-aa09-4284-9d23-fb368fa7bf5d\scratchpad'
G=r'C:\Unity\1\HumanLastRampart\Game\Assets\_Project\Art\Characters\Hero\hero_walk_run_grid'
new=np.array(Image.open(S+r'\k139_new.png'))
cur=np.array(Image.open(G+'.png'));curn=np.array(Image.open(G+'_normal.png'))
order={0:[0,1,3,1],1:[0,2,3,1]}   # back: L-up, both down, R-up, both down / front: L step, R lift, R step, L lift
sheet=cur.copy();sheet[0:384]=0;sheet[3*384:]=0
for nr,sr in ((0,0),(1,3)):
  for k,src in enumerate(order[nr]):
    sheet[sr*384:(sr+1)*384,k*288:(k+1)*288]=new[nr*384:(nr+1)*384,src*288:(src+1)*288]
# round the coat hem corner above the lifted left leg in back frame 0 (otherwise one 30 px straight side)
sheet[321:324,108:111]=0; sheet[321:324,111:114]=(23,19,29,255)
T=[0.52,0.42,0.29,0.15]
def tilt(d): return T[d] if d<4 else 0.0
norm=curn.copy()
for sr in (0,3):
  norm[sr*384:(sr+1)*384]=(128,128,255,255)
  for k in range(4):
    a=sheet[sr*384:(sr+1)*384:3,k*288:(k+1)*288:3,3]>128
    H,W=a.shape
    for y in range(H):
      for x in range(W):
        if not a[y,x]: continue
        def run(dy,dx):
          d=0
          while 0<=y+(d+1)*dy<H and 0<=x+(d+1)*dx<W and a[y+(d+1)*dy,x+(d+1)*dx]: d+=1
          return d
        dl,dr,du,dd=run(0,-1),run(0,1),run(-1,0),run(1,0)
        sx=-tilt(dl) if dl<dr else (tilt(dr) if dr<dl else 0.0)
        sy=tilt(du) if du<dd else (-tilt(dd) if dd<du else 0.0)
        z=math.sqrt(max(0.0,1-sx*sx-sy*sy));l=math.sqrt(sx*sx+sy*sy+z*z)
        v=(round(128+127*sx/l),round(128+127*sy/l),round(128+127*z/l),255)
        Y=sr*384+y*3;X=k*288+x*3
        norm[Y:Y+3,X:X+3]=v
assert (sheet[384:3*384]==cur[384:3*384]).all() and (norm[384:3*384]==curn[384:3*384]).all()
Image.fromarray(sheet).save(S+r'\sole\k139_sheet.png');Image.fromarray(norm).save(S+r'\sole\k139_sheet_normal.png')
# checks (same as HeroSheetEdgeTests)
bad=[]
for r in range(4):
  for c in range(4):
    cell=sheet[r*384:(r+1)*384,c*288:(c+1)*288];op=cell[:,:,3]>128;tr=cell[:,:,3]<20
    best=0
    for d in (-1,1):
      for x in range(1,287):
        run=0
        for y in range(384):
          if op[y,x] and tr[y,x+d]: run+=1;best=max(best,run)
          else: run=0
    ys=np.where(op.any(1))[0];b=ys[-1];row=cell[b][op[b]]
    dark=((row[:,0]<55)&(row[:,1]<55)&(row[:,2]<55)).sum()
    print(r,c,'edge',best,'bottom',b,'dark',dark,'/',len(row))
    if best>30 or dark<len(row)*0.85: bad.append((r,c))
print('BAD',bad)
