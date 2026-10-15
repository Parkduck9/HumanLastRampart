"""AI 노멀 원안의 좌표/단위벡터/빈 영역 인코딩을 규격 출력. 색 원본은 그대로 복사."""
from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib,shutil
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
jobs=json.loads((HERE/'generation.json').read_text(encoding='utf-8'))['requests'];items=[]
for j in jobs:
 key=j['id'];src=Path(j['sourcePath']);raw=HERE/(key+'_normal_source.png');shutil.copyfile(src,raw)
 color=Path(j['ref']);before=sha(color);dest=HERE/(key+'_albedo.png');shutil.copyfile(color,dest)
 rgba=np.array(Image.open(color).convert('RGBA'));h,w=rgba.shape[:2]
 ai=Image.open(raw).convert('RGB').resize((w,h),Image.Resampling.NEAREST);a=np.array(ai,dtype=np.float32)
 mask=rgba[:,:,3]>32;assert mask.any();background=np.median(a[~mask],axis=0)
 # AI 원안의 몇 픽셀 확대/이동을 칸 전체로 정렬한다. 팔다리/머리를 따로 움직이지 않는다.
 registered=np.empty_like(a);registered[:]=background;registration=[]
 for cy in range(h//384):
  for cx in range(w//256):
   x,y=cx*256,cy*384;patch=a[y:y+384,x:x+256];target=mask[y:y+384,x:x+256]
   colorbox=Image.fromarray(target.astype(np.uint8)*255).getbbox()
   foreground=np.linalg.norm(patch-background,axis=2)>30
   normalbox=Image.fromarray(foreground.astype(np.uint8)*255).getbbox();assert colorbox and normalbox
   l,t,r,b=colorbox;chunk=Image.fromarray(patch.astype(np.uint8)).crop(normalbox).resize((r-l,b-t),Image.Resampling.NEAREST)
   registered[y+t:y+b,x+l:x+r]=np.array(chunk);registration.append(dict(column=cx,row=cy,colorBounds=list(colorbox),normalSourceBounds=list(normalbox)))
 a=registered
 n=np.zeros((h,w,3),np.float32);n[:,:,0]=(a[:,:,0]-background[0])/127.5;n[:,:,1]=(a[:,:,1]-background[1])/127.5;n[:,:,2]=np.maximum((a[:,:,2]-127.5)/127.5,.25)
 # 과도한 원안 굴곡을 낮춰 견본에서 작은 캐릭터의 빛 번쩍임을 줄인다.
 n[:,:,:2]*=.55;n/=np.linalg.norm(n,axis=2,keepdims=True);n[~mask]=[0,0,1]
 encoded=np.rint((n*.5+.5)*255).clip(0,255).astype(np.uint8);encoded[~mask]=[128,128,255]
 normal=HERE/(key+'_normal.png');Image.fromarray(encoded).save(normal)
 check=np.array(Image.open(normal));assert check.shape==rgba[:,:,:3].shape;assert np.all(check[~mask]==[128,128,255]);assert sha(color)==before
 decoded=check.astype(float)/255*2-1;length=np.linalg.norm(decoded,axis=2);error=float(np.max(abs(length[mask]-1)));assert error<.012
 cols,rows=(4,4) if key=='hero' else (1,1)
 info=dict(id=key,label='주인공 4행 시트' if key=='hero' else '궁수 뒷모습',albedo=dest.name,normal=normal.name,size=[w,h],cell=[256,384],columns=cols,rows=rows,footPivotTopLeft=[128,352],unityPivotBottomLeft=[.5,32/384],rowOrder=['후면 걷기','왼쪽 걷기','왼쪽 달리기','정면 걷기'] if key=='hero' else ['후면 정지'],normalSpace='tangent OpenGL Y+',neutral=[128,128,255],normalAlpha='opaque RGB; diffuse alpha controls visibility',aiSource=raw.name,sourceColorSha256=before,colorCopyExact=True,normalSha256=sha(normal),normalSourceSha256=sha(raw),normalSourceSize=list(Image.open(raw).size),backgroundMedian=background.tolist(),xyStrength=.55,blankMaskThreshold=32,maxUnitLengthError=error,geometry='source grid unchanged; AI internal surface alignment needs user/Unity review',status='sample-user-review-pending',unityPlayVerified=False)
 info['cellRegistration']=registration
 (HERE/(key+'_normal.json')).write_text(json.dumps(info,ensure_ascii=False,indent=2),encoding='utf-8');items.append(info)
(HERE/'manifest.json').write_text(json.dumps(items,ensure_ascii=False,indent=2),encoding='utf-8')
(HERE/'qa.json').write_text(json.dumps(dict(status='PASS',colorSourcesUnchanged=True,sizesExact=True,gridAndPivotMetadataExact=True,blankPixelsExactNeutral=True,unitNormals=True,aiInteriorPixelAlignment='visual review only, not baked geometry',unityPlayVerified=False,browser='pending'),ensure_ascii=False,indent=2),encoding='utf-8')
print('2 normal samples: color exact, same grid/dimensions, blank flat normals, unit vector check PASS')
