"""생성된 재질 원안을 자르고 규격·미러 반복·노멀 방향 데이터로 출력."""
from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib
HERE=Path(__file__).resolve().parent
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
items=[]
jobs=json.loads((HERE/'generation.json').read_text(encoding='utf-8'))['requests']
for j in jobs:
 key=j['id'];p=HERE/(key+'_source.png');im=Image.open(p).convert('RGB');w,h=im.size;assert w==h and w%2==0
 # 생성 원안은 윗줄 색/아랫줄 노멀, 왼쪽 타일을 사용한다. 반사 반복 내보내기용 절반 크기로 축소한다.
 cw,ch=j['size'];hw,hh=cw//2,ch//2
 color=np.array(im.crop((0,0,w//2,h//2)).resize((hw,hh),Image.Resampling.NEAREST))
 raw=np.array(im.crop((0,h//2,w//2,h)).resize((hw,hh),Image.Resampling.NEAREST),dtype=float)
 base=np.median(raw.reshape(-1,3),axis=0);n=np.zeros_like(raw);n[:,:,0]=(raw[:,:,0]-base[0])/127.5;n[:,:,1]=(raw[:,:,1]-base[1])/127.5;n[:,:,2]=np.maximum((raw[:,:,2]-127.5)/127.5,.3);n[:,:,:2]*=.55;n/=np.linalg.norm(n,axis=2,keepdims=True)
 # 반사축의 접선 기울기는 0. 반사할 때 X/Y 성분도 함께 뒤집어 빛 방향을 일치시킨다.
 n[:,[0,-1],0]=0;n[[0,-1],:,1]=0;n/=np.linalg.norm(n,axis=2,keepdims=True)
 colorwide=np.concatenate([color,color[:,::-1]],axis=1);albedo=np.concatenate([colorwide,colorwide[::-1]],axis=0)
 nr=n[:,::-1].copy();nr[:,:,0]*=-1;wide=np.concatenate([n,nr],axis=1);bottom=wide[::-1].copy();bottom[:,:,1]*=-1;normals=np.concatenate([wide,bottom],axis=0);encoded=np.rint((normals*.5+.5)*255).clip(0,255).astype(np.uint8)
 assert albedo.shape[:2]==(ch,cw);assert encoded.shape==albedo.shape
 assert np.array_equal(albedo[:,0],albedo[:,-1]) and np.array_equal(albedo[0],albedo[-1])
 assert np.array_equal(encoded[:,0],encoded[:,-1]) and np.array_equal(encoded[0],encoded[-1])
 dst=HERE/key;dst.mkdir(exist_ok=True);colorpath=dst/'albedo.png';normalpath=dst/'normal.png';Image.fromarray(albedo).save(colorpath);Image.fromarray(encoded).save(normalpath)
 check=encoded.astype(float)/255*2-1;err=float(np.max(abs(np.linalg.norm(check,axis=2)-1)));assert err<.012
 info=dict(id=key,label=j['label'],albedo=f'{key}/albedo.png',normal=f'{key}/normal.png',size=[cw,ch],worldMeters=j['worldMeters'],pixelsPerMeter=32,source=p.name,sourceSha256=sha(p),albedoSha256=sha(colorpath),normalSha256=sha(normalpath),normalSpace='tangent OpenGL Y+',normalStrength=.55,normalMaxUnitError=err,wrap='Repeat',filter='Point',seamMethod='mirrored periodization of AI material quarter; reflected normal XY corrected; reflection-axis tangent=0',edgesExact=True,normalEdgesExact=True,albedoColorSpace='sRGB',normalColorSpace='Linear',status='reference-sample-user-review-pending',unityPlayVerified=False)
 assert cw/j['worldMeters'][0]==ch/j['worldMeters'][1]==32
 (dst/'material.json').write_text(json.dumps(info,ensure_ascii=False,indent=2),encoding='utf-8');items.append(info)
(HERE/'manifest.json').write_text(json.dumps(items,ensure_ascii=False,indent=2),encoding='utf-8')
(HERE/'qa.json').write_text(json.dumps(dict(status='PASS',materials=len(items),mapPairs=len(items),pixelsPerMeter=32,exactOppositeAlbedoEdges=True,exactOppositeNormalEdges=True,normalVectors=True,sourceArtwork='AI reference + technical mirrored tiling export, not seamless hand-painted original',unityPlayVerified=False,browser='pending'),ensure_ascii=False,indent=2),encoding='utf-8')
print('9 materials: dimensions,32px/m,albedo/normal tile edges and unit vectors PASS')
