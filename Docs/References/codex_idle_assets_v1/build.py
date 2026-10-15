"""생성 원본 보존, 투명 여백 자르기와 전체 최근접 배치만 수행."""
from pathlib import Path
from PIL import Image
import json,hashlib,base64,shutil
HERE=Path(__file__).resolve().parent; ROOT=HERE.parents[2]
GENERATED=Path('C:/Users/User/.codex/generated_images/01a1331e-99b0-7811-8751-cad0561a4e6e')
SOURCES={'hero_back_idle':'exec-32c3617c-f855-4263-a998-cb9f7ffc40b6.png','wizard_front_chestnut':'exec-d5a574a5-e68b-4db6-a54e-245c23f48226.png'}
LABELS={'hero_back_idle':'주인공 · 후면 서 있기','wizard_front_chestnut':'마법사 · 밤색 정면'}
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
info=[]
for key,name in SOURCES.items():
 source=HERE/(key+'_source.png');shutil.copyfile(GENERATED/name,source)
 im=Image.open(source).convert('RGBA');box=im.getchannel('A').point(lambda a:255 if a>32 else 0).getbbox();patch=im.crop(box);w,h=patch.size
 counts=[]
 for x in range(w):
  n=0
  for y in range(int(h*.88),h):
   r,g,b,a=patch.getpixel((x,y))
   if a>128 and r>45 and g>20 and r>g*1.08 and r>b*1.45:n+=1
  counts.append(n)
 regions=[];left=None
 for x,n in enumerate(counts+[0]):
  if n>max(counts)*.25 and n>0:
   if left is None:left=x
  elif left is not None:
   if x-left>h*.03:regions.append((left,x))
   left=None
 assert len(regions)>=2,(key,regions)
 center=(min(a for a,b in regions)+max(b for a,b in regions))/2
 scale=min(300/h,116/max(1,center),116/max(1,w-center));sz=(round(w*scale),round(h*scale))
 packed=patch.resize(sz,Image.Resampling.NEAREST);dx=128-round(center*sz[0]/w);dy=352-sz[1]
 assert dx>0 and dx+sz[0]<256 and dy>0
 out=Image.new('RGBA',(256,384));out.paste(packed,(dx,dy));dest=HERE/(key+'_grid.png');out.save(dest)
 assert Image.open(dest).convert('RGBA').tobytes()==out.tobytes()
 m=dict(id=key,label=LABELS[key],image=dest.name,size=[256,384],cell=[256,384],columns=1,rows=1,frames=1,animated=False,fps=0,footPivotTopLeft=[128,352],unityPivotBottomLeft=[.5,32/384],facing='back' if key.startswith('hero') else 'front',source=source.name,sourceSha256=sha(source),sheetSha256=sha(dest),sourceRect=list(box),sourceBootRegions=regions,sourceFootX=center,packedRect=[dx,dy,*sz],scale=scale,status='user-review-pending',unityPlayVerified=False)
 (HERE/(key+'_grid.json')).write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf-8');info.append(m)
cards=''
for m in info:
 data=base64.b64encode((HERE/m['image']).read_bytes()).decode()
 cards+=f'<article><h2>{m["label"]}</h2><div class="stage"><img src="data:image/png;base64,{data}" width="256" height="384" alt="{m["label"]}"></div><p>静止</p><p>1칸 · 256×384 · 발 기준 128,352</p><div class="sizes">'+''.join(f'<img src="data:image/png;base64,{data}" width="{int(256*s/384)}" height="{s}" alt="{s}px 크기 비교">' for s in [48,72,96])+'</div></article>'
cards=cards.replace('<p>静止</p>','<p>애니메이션 없는 정지 에셋</p>')
html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>정지 캐릭터 2종 검토</title><style>*{box-sizing:border-box}body{margin:0;background:#161c20;color:#eee4d1;font:16px/1.6 'Malgun Gothic',sans-serif}main{max-width:1100px;margin:auto;padding:32px}h1,h2{font-family:'Batang',serif;color:#e7c47f}header{border-bottom:1px solid #8c754d;padding-bottom:20px}.cards{display:grid;grid-template-columns:1fr 1fr;gap:24px}article{background:#232c30;border:1px solid #706347;padding:24px;text-align:center}.stage{background:repeating-conic-gradient(#334345 0 25%,#2a383a 0 50%) 0/24px 24px;position:relative;max-width:300px;margin:auto}.stage:after{content:'';position:absolute;bottom:8.333%;left:0;right:0;border-bottom:1px solid #e7c47f}img{image-rendering:pixelated;max-width:100%;object-fit:contain;vertical-align:bottom}.sizes{display:flex;align-items:end;justify-content:center;gap:24px;padding:20px;background:#36423d}.note{color:#c7bca6}@media(max-width:650px){.cards{grid-template-columns:1fr}main{padding:16px}}</style><main><header><p>HUMAN LAST RAMPART / T-CX-045</p><h1>멈췄을 때의 두 사람</h1><p>주인공은 양발을 바닥에 둔 후면 정지 자세. 마법사는 승인된 밤색 방향을 정면에도 적용했습니다.</p><p class="note">외형 검토용 · 게임 연결 전 · 두 PNG 모두 투명 배경 / 원본 보존</p></header><div class="cards">'''+cards+'''</div><p class="note">아래 소형 비교는 48·72·96px 높이입니다. 금색 선은 발 기준 높이. 생성 과정에서 작은 외형 차이가 생길 수 있어 기존 모습과 함께 검토합니다. Unity 표시·정지 전환은 Claude 연결 후 확인합니다.</p></main></html>'''
(HERE/'preview.html').write_text(html,encoding='utf-8');(ROOT/'_HTML/codex_정지캐릭터2종_v1.html').write_text(html,encoding='utf-8')
(HERE/'qa.json').write_text(json.dumps(dict(status='PASS',cell=[256,384],pivot=[128,352],framesPerAsset=1,transparent=True,noClipping=True,packingOnly=True,assets=info,unityPlayVerified=False),ensure_ascii=False,indent=2),encoding='utf-8')
print('2 transparent single-cell assets packed; no Game files modified')
