from pathlib import Path
from PIL import Image
import json,hashlib,base64,html
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2];OLD=HERE.parent/'codex_static_world_v1'
SPECS=[('cannon_left','대포 왼쪽 조준'),('cannon_center','대포 중앙 조준'),('cannon_right','대포 오른쪽 조준'),('wall_a','A 큰 사각석'),('wall_b','B 거친 거석'),('wall_c','C 두꺼운 요새벽')]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
dump=lambda p,v:p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
data=lambda p,m:'data:'+m+';base64,'+base64.b64encode(p.read_bytes()).decode()
def pack(ident,label):
 folder=HERE/ident;raw=Image.open(folder/'source.png').convert('RGBA');box=raw.getchannel('A').point(lambda v:255 if v>32 else 0).getbbox();assert box
 patch=raw.crop(box);cannon=ident.startswith('cannon');size=(512,384) if cannon else (1024,512)
 scale=min(464/patch.width,320/patch.height) if cannon else min(992/patch.width,480/patch.height)
 wh=(round(patch.width*scale),round(patch.height*scale));patch=patch.resize(wh,Image.Resampling.NEAREST);x=(size[0]-wh[0])//2;y=352-wh[1] if cannon else (size[1]-wh[1])//2
 cell=Image.new('RGBA',size);cell.paste(patch,(x,y));assert x>0 and y>0 and x+wh[0]<size[0] and y+wh[1]<size[1]
 cell.save(folder/'static_grid.png');assert Image.open(folder/'static_grid.png').convert('RGBA').tobytes()==cell.tobytes();assert raw.getchannel('A').getextrema()[0]==0
 muzzle={'cannon_left':[155,55],'cannon_center':[256,55],'cannon_right':[365,55]}.get(ident)
 info=dict(id=ident,label=label,size=list(size),columns=1,rows=1,frames=1,fps=0,pivotTopLeft=[256,352] if cannon else [512,y+wh[1]],packedRect=[x,y,*wh],sourceRect=list(box),sourceSha256=sha(folder/'source.png'),packedSha256=sha(folder/'static_grid.png'),demoMuzzle=muzzle,demoMuzzleVerified=False,status='user-review',unityPlayVerified=False)
 dump(folder/'asset.json',info);return info
if __name__=='__main__':
 infos=[pack(*s) for s in SPECS];sheet=Image.new('RGBA',(1536,384))
 for i,info in enumerate(infos[:3]):sheet.paste(Image.open(HERE/info['id']/'static_grid.png'),(i*512,0))
 sheet.save(HERE/'cannon_aim3_grid.png');direction=dict(columns=3,rows=1,cell=[512,384],pivotTopLeft=[256,352],directions=['left','center','right'],framesAreDirections=True,animationFps=0,demoMuzzles=[i['demoMuzzle'] for i in infos[:3]],gameConnected=False);dump(HERE/'cannon_aim3_grid.json',direction)
 assets=[]
 for info in infos:
  ident=info['id'];folder=HERE/ident
  note='동일 대포의 정적 조준 방향. 설치점 기준 배치와 크기는 HTML에서 비교합니다.' if ident.startswith('cannon') else {'wall_a':'규칙적인 큰 사각석, 밝은 회색 기본 성벽.','wall_b':'거친 큰 돌과 짙은 회색, 무거운 성벽.','wall_c':'두꺼운 지지대와 넓은 통로, 밝은 요새벽.'}[ident]
  assets.append(dict(id=ident,label=info['label'],image=data(folder/'static_grid.png','image/png'),source=data(folder/'source.png','image/png'),json=data(folder/'asset.json','application/json'),rect=info['packedRect'],muzzle=info['demoMuzzle'],note=note))
 extras=dict(sheet=data(HERE/'cannon_aim3_grid.png','image/png'),json=data(HERE/'cannon_aim3_grid.json','application/json'),ball=data(OLD/'cannonball/static_grid.png','image/png'),ground=data(OLD/'ground/static_grid.png','image/png'))
 t=(HERE/'template.html').read_text(encoding='utf-8').replace('__MD__',html.escape((HERE/'README.md').read_text(encoding='utf-8'))).replace('__ASSETS__',json.dumps(assets,ensure_ascii=False)).replace('__EXTRAS__',json.dumps(extras,ensure_ascii=False))
 t=t.replace('</style>','.wallstage{height:auto;aspect-ratio:3/1;position:relative}.wallstage img{position:absolute;inset:0;object-fit:cover}</style>',1)
 (HERE/'preview.html').write_text(t,encoding='utf-8');(ROOT/'_HTML/codex_대포3방향_성벽3안_v2.html').write_text(t,encoding='utf-8')
 dump(HERE/'manifest.json',dict(items=infos,directionSheet=direction));dump(HERE/'qa.json',dict(status='PASS',newStaticAssets=6,threeDirectionSheet=[1536,384],rgba=True,noPackedClipping=True,nearestOnly=True,unityPlayVerified=False,gameConnected=False))
 print('PASS: cannon 3 directions + walls 3 variants + standalone HTML')
