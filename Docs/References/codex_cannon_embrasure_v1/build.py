from pathlib import Path
from PIL import Image
import json,base64,hashlib,html
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2];REF=HERE.parent
dump=lambda p,v:p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
data=lambda p,m:'data:'+m+';base64,'+base64.b64encode(p.read_bytes()).decode()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
infos=[];assets={}
for ident,label in [('wall_cannon_bay','대포 전용 사격구 성벽'),('parapet_cannon_front','대포 앞난간 레이어')]:
 folder=HERE/ident;raw=Image.open(folder/'source.png').convert('RGBA');box=raw.getchannel('A').point(lambda v:255 if v>32 else 0).getbbox();patch=raw.crop(box);scale=min(992/patch.width,480/patch.height);wh=[round(patch.width*scale),round(patch.height*scale)];x=(1024-wh[0])//2;y=(512-wh[1])//2;out=Image.new('RGBA',(1024,512));out.paste(patch.resize(wh,Image.Resampling.NEAREST),(x,y));out.save(folder/'static_grid.png')
 assert x>0 and y>0 and x+wh[0]<1024 and y+wh[1]<512
 assert out.getchannel('A').getextrema()[0]==0 and Image.open(folder/'static_grid.png').convert('RGBA').tobytes()==out.tobytes()
 # 실제 중앙 사격구는 상부에서 지면까지가 아니라 낮은 턱 위로 열린 U형이다.
 centralProbe=out.getpixel((512,y+round(wh[1]*.1)))[3];assert centralProbe==0,(ident,'central opening not transparent')
 info=dict(id=ident,label=label,size=[1024,512],columns=1,rows=1,frames=1,fps=0,packedRect=[x,y,*wh],sourceCrop=list(box),sourceSha256=sha(folder/'source.png'),packedSha256=sha(folder/'static_grid.png'),centralOpeningAlpha=centralProbe,status='review-draft',gameConnected=False,unityPlayVerified=False)
 dump(folder/'asset.json',info);infos.append(info);assets[ident]=dict(image=data(folder/'static_grid.png','image/png'),source=data(folder/'source.png','image/png'),json=data(folder/'asset.json','application/json'),rect=info['packedRect'],label=label)
for ident in ['cannon_left','cannon_center','cannon_right']:
 p=REF/'codex_cannon_wall_v2'/ident/'static_grid.png';assets[ident]=dict(image=data(p,'image/png'))
assets['ball']=dict(image=data(REF/'codex_static_world_v1/cannonball/static_grid.png','image/png'));assets['ground']=dict(image=data(REF/'codex_static_world_v1/ground/static_grid.png','image/png'))
for ident in ['archer','gunner']:
 p=REF/'codex_soldier_back_v1'/f'{ident}_back.png';im=Image.open(p).convert('RGBA');b=im.getchannel('A').point(lambda v:255 if v>32 else 0).getbbox();assets[ident]=dict(image=data(p,'image/png'),rect=[b[0],b[1],b[2]-b[0],b[3]-b[1]])
t=(HERE/'template.html').read_text(encoding='utf-8').replace('__ASSETS__',json.dumps(assets,ensure_ascii=False)).replace('__MD__',html.escape((HERE/'README.md').read_text(encoding='utf-8')))
(HERE/'preview.html').write_text(t,encoding='utf-8');(ROOT/'_HTML/codex_대포사격구성벽_v1.html').write_text(t,encoding='utf-8')
dump(HERE/'manifest.json',dict(items=infos,reused=list(assets)[2:]));dump(HERE/'qa.json',dict(status='PASS',newStaticAssets=2,rgba=True,noPackedClipping=True,centralOpeningTransparent=True,nearestOnly=True,savedPixelsExact=True,gameConnected=False,unityPlayVerified=False))
print('PASS: 2 static modules, transparent gun gap, standalone HTML')
