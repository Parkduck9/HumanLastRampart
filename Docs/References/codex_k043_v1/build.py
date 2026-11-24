"""원안의 칸 분리·최근접 크기 정리·발 정렬과 검토 자료 포장."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json, base64, shutil, hashlib
P=Path(__file__).resolve().parent
ROOT=P.parents[2]
assets={}
REAR_ANCHORS=[330,325,330,330,325,315]
REAR_SCALE=.483
IGNITE_ANCHORS=[280,280,310,305,305,310]
IGNITE_SCALE=.54
def save_json(name,obj):
    (P/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
def sheet(name,source,anchors,scale):
    im=Image.open(P/source).convert('RGBA'); out=Image.new('RGBA',(1536,384)); frames=[]
    for i,ax in enumerate(anchors):
        x=i%3*(im.width//3); y=i//3*(im.height//2)
        cell=im.crop((x,y,x+im.width//3,y+im.height//2))
        box=cell.getchannel('A').point(lambda a:a if a>128 else 0).getbbox()
        sprite=cell.crop(box); w=round(sprite.width*scale); h=round(sprite.height*scale)
        dx=round(128+(box[0]-ax)*scale); dy=352-h
        assert dx>=1 and dx+w<255,(name,i,dx,w)
        out.alpha_composite(sprite.resize((w,h),Image.Resampling.NEAREST),(i*256+dx,dy))
        frames.append(dict(index=i,sourceRect=[x+box[0],y+box[1],x+box[2],y+box[3]],sourceFootX=ax,alphaHeight=h,placed=[dx,dy,w,h]))
    out.save(P/(name+'.png'))
    assets[name]=dict(file=name+'.png',cell=[256,384],frames=6,pivot=[128,352],scale=scale,source=source,frameDetails=frames)
sheet('crew_pull6','crew_pull_source.png',[250,250,260,260,250,255],.5)
if (P/'crew_rear_source.png').exists():
    sheet('crew_rear6','crew_rear_source.png',REAR_ANCHORS,REAR_SCALE)
if (P/'crew_ignite_short_source.png').exists():
    sheet('crew_ignite6','crew_ignite_short_source.png',IGNITE_ANCHORS,IGNITE_SCALE)
im=Image.open(P/'cannon_source.png').convert('RGBA'); box=im.getchannel('A').point(lambda a:a if a>128 else 0).getbbox()
s=im.crop(box); scale=.205; size=(round(s.width*scale),round(s.height*scale)); dx=(384-size[0])//2;dy=352-size[1]
out=Image.new('RGBA',(384,384));out.alpha_composite(s.resize(size,Image.Resampling.NEAREST),(dx,dy));out.save(P/'cannon_body.png')
def point(x,y): return [round(dx+(x-box[0])*scale),round(dy+(y-box[1])*scale)]
assets['cannon_body']=dict(file='cannon_body.png',cell=[384,384],frames=1,pivot=[192,352],source='cannon_source.png',sourceRect=list(box),scale=scale,wheelCenter=point(970,705),wheelRadius=39,frontHandle=point(248,573),rearHandle=point(1332,729),fuse=point(1250,344),fuseContact=point(1175,432),muzzle=point(367,440))
assets['crew_ignite6']['contact']=[round(128+(163-305)*.54),round(352-(449-273)*.54)]
for name,src in [('reference_crew','Game/Assets/_Project/Art/Characters/Run/cannon_crew_run.png'),('muzzle_smoke','Docs/References/codex_weapons_fire_v1/muzzle_smoke_sheet.png')]:
    shutil.copyfile(ROOT/src,P/(name+'.png'))
save_json('manifest.json',dict(status='사용자 확정 전 시안 · 게임 미연결',coordinates='좌상단 원점, 픽셀 좌표',assets=assets,sequence=dict(contactSeconds=.5,fireSeconds=1.5,delaySeconds=1,smokeFps=10,cycleSeconds=3.2)))
keys=list(assets)+['reference_crew','muzzle_smoke']
data={k:'data:image/png;base64,'+base64.b64encode((P/(k+'.png')).read_bytes()).decode() for k in keys}
html=(P/'review_template.html').read_text(encoding='utf-8').replace('__DATA__',json.dumps(data)).replace('__META__',json.dumps(assets,ensure_ascii=False))
target=ROOT/'_HTML/4_그림_캐릭터/codex_K-043_대포병분리_불붙이기_v1.html';target.write_text(html,encoding='utf-8')
qa=[]
for k in keys:
    im=Image.open(P/(k+'.png')).convert('RGBA');a=im.getchannel('A');cw=assets.get(k,{}).get('cell',[256,384])[0]
    frames=assets.get(k,{}).get('frames',4 if k=='reference_crew' else 6)
    corners=[];bounds=[]
    for i in range(frames):
        c=im.crop((i*cw,0,(i+1)*cw,im.height)); corners.append([c.getpixel(q)[3] for q in [(0,0),(cw-1,0),(0,im.height-1),(cw-1,im.height-1)]]);bounds.append(c.getchannel('A').getbbox())
    assert all(v==0 for arr in corners for v in arr),(k,corners)
    qa.append(dict(file=k+'.png',size=im.size,cornersAlpha=corners,alphaBounds=bounds,sha256=hashlib.sha256((P/(k+'.png')).read_bytes()).hexdigest()))
save_json('qa_assets.json',qa)
print('시트·내장 HTML·알파 검사 저장 완료')
