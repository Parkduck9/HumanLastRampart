"""HTML과 같은 좌표로 합성한 정적 검토 그림. 브라우저 캡처가 아니다."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json
P=Path(__file__).resolve().parent
meta=json.loads((P/'manifest.json').read_text(encoding='utf-8'))['assets']
ims={k:Image.open(P/(k+'.png')).convert('RGBA') for k in list(meta)+['reference_crew','muzzle_smoke']}
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',16)
def canvas():
    im=Image.new('RGBA',(1024,400),'#c3bfae');d=ImageDraw.Draw(im);d.line((15,352,1009,352),fill='#506e61',width=1);return im
def cell(im,k,f,x,y=0):
    w,h=meta[k]['cell'] if k in meta else (256,256 if k=='muzzle_smoke' else 384)
    im.alpha_composite(ims[k].crop((f*w,0,(f+1)*w,h)),(round(x),round(y)))
def cross(im,x,y,label):
    d=ImageDraw.Draw(im);d.line((x-5,y,x+5,y),fill='#176f5e');d.line((x,y-5,x,y+5),fill='#176f5e');d.text((x+8,y+6),label,font=font,fill='#1b352b')
a=meta['cannon_body'];cx=330;front=245;rear=740
im=canvas();d=ImageDraw.Draw(im);d.line((front+46,266,cx+a['frontHandle'][0],a['frontHandle'][1]),fill='#6a4a29',width=3);d.line((cx+a['rearHandle'][0],a['rearHandle'][1],rear-63,271),fill='#6a4a29',width=3)
cell(im,'crew_rear6',0,rear-128);cell(im,'cannon_body',0,cx);cell(im,'crew_pull6',0,front-128)
for x,label in [(front,'앞 병사'),(cx+192,'대포 기준'),(rear,'뒤 병사')]:cross(im,x,352,label)
ImageDraw.Draw(im).text((24,24),'앞 끌기 6칸 + 대포 본체 1칸 + 뒤 운반 보조 6칸',font=font,fill='#20362c');im.save(P/'haul_review.png')
rows=[im]
tip=meta['crew_ignite6']['contact'];contact=a['fuseContact'];cx=288;foot=cx+contact[0]-(tip[0]-128)
for f,title,filename in [(3,'0.50초 · 도화선 접촉·점화','ignite_contact_review.png'),(5,'1.50초 · 점화 1초 뒤 발사','fire_review.png')]:
    im=canvas();cell(im,'cannon_body',0,cx);cell(im,'crew_ignite6',f,foot-128)
    d=ImageDraw.Draw(im);d.text((24,24),title,font=font,fill='#20362c')
    if f==3:d.rectangle((cx+contact[0]-1,contact[1]-1,cx+contact[0]+1,contact[1]+1),fill='#ffc969')
    else:
        smoke=ims['muzzle_smoke'].crop((256,0,512,256)).resize((128,128),Image.Resampling.NEAREST)
        layer=Image.new('RGBA',(300,300));layer.alpha_composite(smoke,(150-64,150-120));layer=layer.rotate(90,resample=Image.Resampling.NEAREST)
        im.alpha_composite(layer,(cx+a['muzzle'][0]-150,a['muzzle'][1]-150))
    cross(im,foot,352,'점화 병사 발');im.save(P/filename);rows.append(im)
im=canvas();cell(im,'reference_crew',0,0);cell(im,'crew_pull6',0,256);cell(im,'crew_ignite6',3,512);ImageDraw.Draw(im).text((24,24),'기존 달리기 / 새 끌기 / 새 불붙이기 · 동일 배율',font=font,fill='#20362c');im.save(P/'scale_review.png');rows.append(im)
out=Image.new('RGB',(1024,len(rows)*400))
for i,im in enumerate(rows):out.paste(im.convert('RGB'),(0,i*400))
out.save(P/'contact_review.png')
print('동일 좌표 정적 합성 5장 저장')
