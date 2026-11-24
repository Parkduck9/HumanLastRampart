"""기존 규격에 맞춰 생성 원본을 최근접 배치한다. 그림 내용은 다시 그리지 않는다."""
from pathlib import Path
from PIL import Image
import hashlib,json
HERE=Path(__file__).resolve().parent
source=HERE/'wizard_back_source.png'
raw=Image.open(source).convert('RGBA')
box=raw.getchannel('A').point(lambda v:255 if v>32 else 0).getbbox()
assert box
patch=raw.crop(box);w,h=patch.size
# 아래쪽의 넓은 갈색 부츠 픽셀만으로 몸 중심을 추정한다. 얇은 지팡이 영향은 제외한다.
counts=[0]*w
for y in range(int(h*.88),h):
 for x in range(w):
  red,green,blue,alpha=patch.getpixel((x,y))
  if alpha>100 and red>45 and green>20 and red>green*1.08 and red>blue*1.45:counts[x]+=1
regions=[];left=None
for x,count in enumerate(counts+[0]):
 if count>max(counts,default=0)*.2 and count>0:
  if left is None:left=x
 elif left is not None:
  if x-left>=h*.035:regions.append((left,x))
  left=None
assert regions, '부츠 중심을 수동 확인해야 함'
center=(min(a for a,b in regions)+max(b for a,b in regions))/2
scale=min(300/h,112/max(1,center),112/max(1,w-center))
w2,h2=round(w*scale),round(h*scale)
scaled=patch.resize((w2,h2),Image.Resampling.NEAREST)
dx=128-round(center*w2/w);dy=352-h2
assert dx>0 and dx+w2<256 and dy>0 and dy+h2<384
cell=Image.new('RGBA',(256,384));cell.paste(scaled,(dx,dy));path=HERE/'wizard_back_static_grid.png';cell.save(path)
loaded=Image.open(path).convert('RGBA');assert loaded.tobytes()==cell.tobytes()
assert set(loaded.getdata()).issubset(set(patch.getdata())|{(0,0,0,0)})
bbox=loaded.getchannel('A').point(lambda v:255 if v>32 else 0).getbbox()
assert bbox and bbox[0]>0 and bbox[1]>0 and bbox[2]<256 and bbox[3]<384
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
info=dict(id='wizard_back',label='마법사 뒷모습',facing='back',image=path.name,size=[256,384],columns=1,rows=1,cell=[256,384],footPivotTopLeft=[128,352],unityPivotBottomLeft=[.5,32/384],aspect=2/3,frameIndex=0,frames=1,animated=False,fps=0,status='demo-static-only',source=source.name,sourceSha256=sha(source),sourceRect=list(box),sourceFootX=center,pivotMethod='lower boot-width regions; demo inference, confirm in Unity',bootRegions=regions,uniformScale=scale,packedRect=[dx,dy,w2,h2],sheetSha256=sha(path),unityPlayVerified=False)
(HERE/'wizard_back_static_grid.json').write_text(json.dumps(info,ensure_ascii=False,indent=2),encoding='utf-8')
(HERE/'qa.json').write_text(json.dumps(dict(status='PASS',sheets=1,transparent=True,noClipping=True,commonCell=[256,384],commonPivot=[128,352],noNewColors=True,exactExport=True,sourceSha256=sha(source),unityPlayVerified=False),indent=2),encoding='utf-8')
print(json.dumps(info,ensure_ascii=False))
