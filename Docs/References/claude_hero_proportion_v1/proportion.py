# 걷기·달리기 머리·어깨 비율을 서 있기 그림에 맞춘다: 코덱스 시안(K-139·K-140)에서 머리와 몸을 나눠 따로 줄인 뒤 다시 붙인다
from PIL import Image;import numpy as np,collections,math,sys
S='C:/Users/User/AppData/Local/Temp/claude/C--Unity-1-HumanLastRampart/9f0dc00c-aa09-4284-9d23-fb368fa7bf5d/scratchpad'
G='C:/Unity/1/HumanLastRampart/Game/Assets/_Project/Art/Characters/Hero/hero_walk_run_grid'
HK_WALK,HK_RUN,WK=[float(v) for v in sys.argv[1:4]] if len(sys.argv)>3 else (0.85,0.80,0.92)
cur=np.array(Image.open(G+'.png'));curn=np.array(Image.open(G+'_normal.png'))
pal=collections.Counter();edge=collections.Counter()
for r in (0,2,3):
  c=cur[r*384:(r+1)*384];op=c[:,:,3]>128
  for y,x in zip(*np.where(op)):
    if y%3 or x%3: continue
    col=tuple(c[y,x,:3]);pal[col]+=1
    if 3<=x<c.shape[1]-3 and y+3<384 and (not op[y+3,x] or not op[y,x-3] or not op[y,x+3]): edge[col]+=1
P=np.array([k for k,v in pal.items() if v>=3],float);outline=np.array(edge.most_common(1)[0][0])

def scale(f,w,h):
  a=f[:,:,3]/255.0
  pm=np.dstack([f[:,:,:3]*a[:,:,None],a*255])
  return np.array(Image.fromarray(pm.clip(0,255).astype(np.uint8),'RGBA').resize((max(1,w),max(1,h)),Image.BOX)).astype(float)

def native(f,NH,hk,wk):
  """f: 시안 한 사람(위 끝~땅). 목에서 나눠 머리는 hk배, 몸은 가로 wk배로 줄이고 세로는 전체 NH가 되게 채운다"""
  al=f[:,:,3]>128;H,W=al.shape
  rows=np.where(al.any(1))[0];top=rows[0]
  wid=al.sum(1)
  lo,hi=top+int((H-top)*0.22),top+int((H-top)*0.45)
  neck=lo+int(wid[lo:hi].argmin())
  xs=np.where(al[neck])[0];ncx=(xs[0]+xs[-1]+1)/2
  s0=NH/H
  hh=round(neck*s0*hk);hw=round(W*s0*hk)
  bh=NH-hh;bw=round(W*s0*wk)
  head=scale(f[:neck],hw,hh);body=scale(f[neck:],bw,bh)
  CW=2*max(hw,bw)+8;out=np.zeros((NH,CW,4))
  cx=CW/2
  hx=int(round(cx-ncx*hw/W));bx=int(round(cx-ncx*bw/W))
  out[hh:,bx:bx+bw]=body
  # 머리는 몸 위에 덮는다 (머리카락이 어깨를 살짝 덮는 자리)
  ha=head[:,:,3:4]/255.0;reg=out[:hh,hx:hx+hw]
  out[:hh,hx:hx+hw]=head+reg*(1-ha)
  na=out[:,:,3]/255.0;op=na>0.5
  rgb=np.where(op[:,:,None],out[:,:,:3]/np.maximum(na,1e-3)[:,:,None],0)
  q=P[((rgb[:,:,None,:]-P[None,None])**2).sum(-1).argmin(-1)]
  pad=np.pad(op,1);border=op&~(pad[:-2,1:-1]&pad[2:,1:-1]&pad[1:-1,:-2]&pad[1:-1,2:]);q[border]=outline
  xs=np.where(op.any(0))[0];q=q[:,xs[0]-1:xs[-1]+2];op=op[:,xs[0]-1:xs[-1]+2]
  cols=np.where(op[:int(NH*0.55)].any(0))[0];anchor=(cols[0]+cols[-1]+1)/2
  return q,op,anchor

def place(sheet,row,k,q,op,anchor):
  big=np.kron(np.dstack([q,op*255.0]),np.ones((3,3,1)))
  oy=row*384+360-big.shape[0];ox=k*288+144-int(round(anchor))*3
  reg=sheet[oy:oy+big.shape[0],ox:ox+big.shape[1]];m=big[:,:,3]>0;reg[m]=big[m].astype(np.uint8)

sheet=cur.copy();sheet[0:384]=0;sheet[2*384:]=0
# K-139: 앞·뒤 걷기 (행 0 = 시안 위줄, 행 3 = 시안 아래줄)
con=np.array(Image.open(S+'/k139_concept.png')).astype(float);al=con[:,:,3]>40
order={0:[0,1,3,1],3:[0,2,3,1]}
for (y0,y1),row in zip([(47,422),(463,845)],(0,3)):
  xs=np.where(al[y0:y1].any(0))[0];runs=[];s=xs[0];p=xs[0]
  for x in xs[1:]:
    if x>p+1: runs.append((s,p+1));s=x
    p=x
  runs.append((s,p+1));runs=[r for r in runs if r[1]-r[0]>20]
  figs=[]
  for (x0,x1) in runs:
    f=con[y0:y1,x0:x1];ys=np.where((f[:,:,3]/255).max(1)>0.15)[0];f=f[ys[0]:ys[-1]+1].copy()
    figs.append(native(f,73,HK_WALK,WK))
  for k,src in enumerate(order[row]): place(sheet,row,k,*figs[src])
# K-140: 옆 달리기 (공통 위·땅 범위, 3번 칸은 공중 자세라 살짝 뜬다)
con=np.array(Image.open(S+'/k140_concept.png')).astype(float)
TOP,GROUND=116,643
for k,(x0,x1) in enumerate([(81,467),(614,1015),(1121,1551),(1685,2032)]):
  f=con[TOP:GROUND,x0-4:x1+4].copy();f[f[:,:,3]<128]=0
  place(sheet,2,k,*native(f,74,HK_RUN,WK))
# 달리기 1번 칸 뒷장화 발뒤꿈치 아래 모서리를 한 점 둥글게 (곧은 옆선 33칸 -> 30칸)
Y0=2*384;X0=288
sheet[Y0+330:Y0+333,X0+225:X0+228]=0;sheet[Y0+330:Y0+333,X0+222:X0+225]=(*outline.astype(np.uint8),255)
assert (sheet[384:768]==cur[384:768]).all()
T=[0.52,0.42,0.29,0.15];tilt=lambda d:T[d] if d<4 else 0.0
norm=curn.copy()
for sr in (0,2,3):
  norm[sr*384:(sr+1)*384]=(128,128,255,255)
  for k in range(4):
    a=sheet[sr*384:(sr+1)*384:3,k*288:(k+1)*288:3,3]>128;H,W=a.shape
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
        Y=sr*384+y*3;X=k*288+x*3;norm[Y:Y+3,X:X+3]=(round(128+127*sx/l),round(128+127*sy/l),round(128+127*z/l),255)
Image.fromarray(sheet).save(S+'/prop/sheet.png');Image.fromarray(norm).save(S+'/prop/sheet_normal.png')
bad=[]
for r in range(4):
  for c in range(4):
    cell=sheet[r*384:(r+1)*384,c*288:(c+1)*288];op=cell[:,:,3]>128;tr=cell[:,:,3]<20;best=0
    for d in (-1,1):
      for x in range(1,287):
        rn=0
        for y in range(384):
          if op[y,x] and tr[y,x+d]: rn+=1;best=max(best,rn)
          else: rn=0
    ys=np.where(op.any(1))[0];b=ys[-1];rw=cell[b][op[b]];dark=((rw[:,0]<55)&(rw[:,1]<55)&(rw[:,2]<55)).sum()
    if best>30 or dark<len(rw)*0.85 or b!=359 and r!=2: bad.append((r,c,best,dark,len(rw),b))
print('BAD',bad)
# 비교: 궁수, 주인공 서 있기, 앞 걷기 0, 뒤 걷기 0, 달리기 0·2 — 위 지금, 아래 새것
R='C:/Unity/1/HumanLastRampart/Game/Assets/_Project/Art/Characters/'
arch=Image.open(R+'Static/archer_static.png').convert('RGBA');idle=Image.open(R+'Hero/hero_back_idle.png').convert('RGBA')
def cell(a,r,c): return Image.fromarray(a[r*384:(r+1)*384,c*288:(c+1)*288])
pv=Image.new('RGBA',(288*7,500),(70,72,90,255))
for i,(r,c) in enumerate([(3,0),(3,2),(0,0),(2,0),(2,2)]):
  pv.alpha_composite(cell(cur,r,c).crop((0,130,288,380)),(288*(i+2),0));pv.alpha_composite(cell(sheet,r,c).crop((0,130,288,380)),(288*(i+2),250))
for y in (0,250):
  pv.alpha_composite(arch.crop((0,130,288,380)),(0,y));pv.alpha_composite(idle.crop((0,130,288,380)),(288,y))
pv.save(S+'/photos/prop_compare.png')
