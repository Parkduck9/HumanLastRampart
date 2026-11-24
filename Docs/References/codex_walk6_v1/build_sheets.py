"""4프레임 생성 원본을 공통 칸으로 포장한다. 보간·재색칠·팔다리 합성 없음."""
from pathlib import Path
from PIL import Image
import json,hashlib,statistics
HERE=Path(__file__).resolve().parent
ITEMS=[('archer','궁수','side'),('gunner','총병','side'),('cannon_crew','대포병','side'),('wizard','마법사','side'),('ogre','오우거','front'),('ladder_goblin','사다리 고블린','front')]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def pack(ident,label,facing):
 folder=HERE/ident;rawPath=folder/'source.png';raw=Image.open(rawPath).convert('RGBA');parts=[];maxHeight=0;maxLeft=maxRight=1
 for i in range(4):
  left=round(raw.width*i/4);right=round(raw.width*(i+1)/4);part=raw.crop((left,0,right,raw.height))
  box=part.getchannel('A').point(lambda v:255 if v>32 else 0).getbbox();assert box,(ident,i)
  patch=part.crop(box);w,h=patch.size
  # 상체 픽셀 면적의 중앙값으로 몸 기준축을 맞춘다. 머리만 잘라 고정하지 않는다.
  xs=[]
  for y in range(int(h*.08),max(1,int(h*.40))):
   for x in range(w):
    if patch.getpixel((x,y))[3]>100:xs.append(x)
  pivot=statistics.median(xs) if xs else w/2
  parts.append((patch,box,left,pivot));maxHeight=max(maxHeight,h);maxLeft=max(maxLeft,pivot);maxRight=max(maxRight,w-pivot)
 scale=min(300/maxHeight,112/maxLeft,112/maxRight)
 sheet=Image.new('RGBA',(1024,384));entries=[]
 for i,(patch,box,left,pivot) in enumerate(parts):
  w,h=patch.size;ww,hh=round(w*scale),round(h*scale);frame=patch.resize((ww,hh),Image.Resampling.NEAREST)
  x=128-round(pivot*ww/w);y=352-hh
  assert x>0 and y>0 and x+ww<256 and y+hh<384,(ident,i,x,y,ww,hh)
  cell=Image.new('RGBA',(256,384));cell.paste(frame,(x,y));cell.save(folder/f'frame_{i+1}.png');sheet.paste(cell,(i*256,0))
  assert len(set(cell.getdata())-(set(patch.getdata())|{(0,0,0,0)}))==0
  entries.append(dict(frame=i,sourceRect=[left+box[0],box[1],box[2]-box[0],box[3]-box[1]],sourceBodyX=left+box[0]+pivot,packedRect=[x,y,ww,hh]))
 path=folder/'walk4_grid.png';sheet.save(path);assert Image.open(path).convert('RGBA').tobytes()==sheet.tobytes()
 hashes=[sha(folder/f'frame_{i+1}.png') for i in range(4)];assert len(set(hashes))==4,(ident,'identical frames')
 info=dict(id=ident,label=label,facing=facing,source=rawPath.name,sourceSha256=sha(rawPath),image=path.name,columns=4,rows=1,size=[1024,384],cell=[256,384],frames=4,footPivotTopLeft=[128,352],unityPivotBottomLeft=[.5,32/384],aspect=2/3,frameOrder=[0,1,2,3],fps=6,loop='1-2-3-4',rightPreview='mirror same ordered frames' if facing=='side' else None,uniformScale=scale,framesData=entries,frameHashes=hashes,sheetSha256=sha(path),status='animation-draft-user-review',unityPlayVerified=False)
 (folder/'walk4_grid.json').write_text(json.dumps(info,ensure_ascii=False,indent=2),encoding='utf-8');return info
if __name__=='__main__':
 infos=[]
 for ident,label,facing in ITEMS:
  if (HERE/ident/'source.png').exists():infos.append(pack(ident,label,facing))
 refs=json.loads((HERE/'reference_hashes.json').read_text(encoding='utf-8'))
 for item in refs.values():assert sha(HERE.parents[2]/item['path'])==item['sha256']
 (HERE/'manifest.json').write_text(json.dumps(dict(sheets=infos),ensure_ascii=False,indent=2),encoding='utf-8')
 (HERE/'qa.json').write_text(json.dumps(dict(status='PASS',sheets=len(infos),uniqueFramesPerSheet=4,rgba=True,cell=[256,384],footPivot=[128,352],nearestOnly=True,noClipping=True,referencesUnchanged=True,unityPlayVerified=False),indent=2),encoding='utf-8')
 print('PASS',len(infos),'sheets;',len(infos)*4,'unique frame exports')
