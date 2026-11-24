from pathlib import Path
from PIL import Image, ImageDraw
import base64, hashlib, html, json, re, subprocess, shutil
import numpy as np
HERE=Path(__file__).resolve().parent; ROOT=HERE.parents[2]
JOBS=json.loads((HERE/'jobs.json').read_text(encoding='utf-8'));records=[];checks=[]
def check(name,value):
    assert value,name
    checks.append({'name':name,'passed':True})
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
for job in JOBS:
    ident=job['id'];src=HERE/(ident+'_source.png');im=Image.open(src).convert('RGBA');sw,sh=im.size
    a=np.array(im)[:,:,3];bbox=im.getchannel('A').point(lambda v:255 if v>=32 else 0).getbbox();x0,y0,x1,y1=bbox
    n=job['frames']; w=job['w'];h=job['h'];px,py=job['pivot'];
    check(ident+' 실제투명 알파',a.min()==0)
    if n>1:
        projection=(a>=128).any(axis=0);ranges=[];start=None
        for x,v in enumerate(projection):
            if v and start is None:start=x
            if not v and start is not None:ranges.append((start,x));start=None
        if start is not None:ranges.append((start,sw))
        gaps=[(ranges[i+1][0]-ranges[i][1],(ranges[i+1][0]+ranges[i][1])//2) for i in range(len(ranges)-1)]
        cuts=sorted(mid for gap,mid in sorted(gaps,reverse=True)[:n-1]);check(ident+' 프레임공백분리',len(cuts)==n-1)
        bounds=[0]+cuts+[sw]
    else:bounds=[0,sw]
    anchors=[];rects=[];leftExtent=0;rightExtent=0
    for i in range(n):
        left,right=bounds[i:i+2];panel=a[y0:y1,left:right]>=32
        ys,xs=np.where(panel);check(ident+' 프레임'+str(i)+' 존재',len(xs)>0)
        # For soldiers/cannon, anchor to boot/wheel contact band so arm motion does not slide the body.
        if job['kind'] in ['character','cannon']:
            foot=(a[max(y0,y1-max(8,round((y1-y0)*0.07))):y1,left:right]>=96)
            fy,fx=np.where(foot);cx=(int(fx.min())+int(fx.max()))/2 if len(fx) else (xs.min()+xs.max())/2
        else:cx=(xs.min()+xs.max())/2
        cx+=left;anchors.append(cx);rects.append([left,y0,right,y1]);leftExtent=max(leftExtent,cx-(left+xs.min()));rightExtent=max(rightExtent,(left+xs.max()+1)-cx)
    if job['kind']=='projectile':
        scale=min(1,180/max(1,x1-x0),72/max(1,y1-y0))
    else:scale=min(1,(px-12)/max(1,leftExtent),(w-px-12)/max(1,rightExtent),(py-32)/max(1,y1-y0))
    sheet=Image.new('RGBA',(w*n,h));frames=[]
    for i,rect in enumerate(rects):
        left,top,right,bottom=rect;panel=im.crop(tuple(rect));size=(max(1,round(panel.width*scale)),max(1,round(panel.height*scale)))
        panel=panel.resize(size,Image.Resampling.NEAREST)
        if job['kind']=='projectile':
            dest=(i*w+px-round((anchors[i]-left)*scale),py-size[1]//2)
        else:dest=(i*w+px-round((anchors[i]-left)*scale),py-size[1])
        sheet.alpha_composite(panel,dest)
        frame=sheet.crop((i*w,0,(i+1)*w,h));ab=frame.getchannel('A').getbbox()
        frames.append({'index':i,'rect':[i*w,0,w,h],'pivotPixels':[px,py],'alphaBBox':ab,'rgbaSha256':hashlib.sha256(frame.tobytes()).hexdigest(),'sourceRect':rect,'sourceAnchorX':anchors[i]})
        check(ident+' 프레임'+str(i)+' 상하좌우칸내',ab is not None and ab[0]>0 and ab[1]>0 and ab[2]<w and ab[3]<h)
    dest=HERE/(ident+'_sheet.png');sheet.save(dest)
    check(ident+' 규격',sheet.size==(w*n,h));check(ident+' 다른프레임',len({f['rgbaSha256'] for f in frames})==n)
    info={**job,'source':src.name,'sourceSize':[sw,sh],'sourceSha256':digest(src),'sheet':dest.name,'sheetSize':[w*n,h],'sheetSha256':digest(dest),'sourceColumnBounds':bounds,'commonScale':scale,'framesMeta':frames,'reviewStatus':'외형사용자검토대기·게임연결없음','processing':'원안분리·동일시트공통배율·최근접정렬만. 병사발/대포바퀴접지 기준. 내용덧칠/프레임복제없음.'}
    (HERE/(ident+'.json')).write_text(json.dumps(info,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');records.append(info)
manifest={'card':'K-016','task':'T-CX-057','approval':'사용자외형검토대기','assets':records,'existingCharacterSheetsChanged':False,'scopeCorrection':'M-CL-077: 석궁병과 석궁 볼트 제외, 궁수(활)·총병과 투사체3종만','unityRun':False}
(HERE/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
data=[]
for r in records:
    data.append({'id':r['id'],'title':r['title'],'frames':r['frames'],'w':r['w'],'h':r['h'],'pivot':r['pivot'],'fps':r['fps'],'kind':r['kind'],'src':'data:image/png;base64,'+base64.b64encode((HERE/r['sheet']).read_bytes()).decode('ascii')})
md=(HERE/'README.md').read_text(encoding='utf-8')
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-016 무기와 사격 시안</title><style>:root{color-scheme:dark;font-family:Malgun Gothic,system-ui;background:#171d25;color:#eee8d8}*{box-sizing:border-box}body{margin:0}main{max-width:1240px;margin:auto;padding:28px}h1{font-size:27px}p,li{line-height:1.7;color:#c5cdd2}.note{padding:16px;border-left:4px solid #baa062;background:#29271f}.controls{display:flex;gap:10px;flex-wrap:wrap;margin:20px 0}button,select,input{font:inherit;padding:10px;border:1px solid #917f54;background:#2b3745;color:#eee;border-radius:6px}button{cursor:pointer}.stage{min-height:420px;display:flex;align-items:center;justify-content:center;overflow:auto;background-color:#29323c;background-image:conic-gradient(#303a46 25%,transparent 0 50%,#303a46 0 75%,transparent 0);background-size:32px 32px;border:1px solid #58616a;padding:22px}.stage.light{background-color:#d8d6ca;background-image:conic-gradient(#bcbcad 25%,transparent 0 50%,#bcbcad 0 75%,transparent 0)}canvas{image-rendering:pixelated;flex-shrink:0}.readout{padding:12px;color:#dcc58b}.sheets{display:grid;gap:16px;margin-top:24px}figure{margin:0;border:1px solid #46515c;border-radius:8px;overflow:hidden;background:#202a34}figcaption{padding:12px}figure img{display:block;max-width:100%;height:auto;image-rendering:pixelated;background:#303a46}a{color:#e2c989}pre{white-space:pre-wrap;line-height:1.7;overflow-wrap:anywhere}summary{cursor:pointer;padding:14px}.refs{display:flex;gap:16px;flex-wrap:wrap}.refs img{height:250px;width:auto;max-width:100%;object-fit:contain}.refs figure{padding:14px}@media(max-width:600px){main{padding:14px}.stage{justify-content:flex-start}}</style><main><h1>무기와 사격 · K-016</h1><div class="note">궁수·총병의 기존 외형을 참고한 <strong>별도 사격 시트 제안</strong>입니다. 기존 4열×3행 그림은 바꾸지 않았습니다.<br>사용자 정정 M-CL-077에 따라 궁수(활)·총병만 포함합니다. 사용자 검토 후 게임 연결은 Claude가 진행합니다.</div><div class="controls"><select id="asset" aria-label="에셋 종류"></select><button id="play">정지</button><button id="prev">이전 칸</button><button id="next">다음 칸</button><button id="once">한 번 재생</button><select id="fps" aria-label="재생 속도"><option>8</option><option selected>10</option><option>12</option></select><button id="scale">2배 보기</button><button id="background">밝은 배경</button><label>투사체 회전 <input id="angle" type="range" min="0" max="360" value="0"></label></div><div class="stage" id="stage"><canvas width="256" height="384" aria-label="선택 에셋 미리보기"></canvas></div><div class="readout" id="readout"></div><p>방향: 병사와 대포는 화면 위쪽 바깥 들판을 향하는 후면입니다. 투사체는 오른쪽(+X)이 원본 방향이며 회전은 표시용 제안입니다. 실제 월드 방향과 포구 위치는 아직 연결하지 않았습니다.</p><p>미리보기는 반복하며, 한 번 재생을 누르면 마지막 칸에서 멈춥니다. 화살/총알/포탄은 실제 게임에서 훨씬 작게 보일 수 있습니다.</p><div class="sheets">@@SHEETS@@</div><h2>기존 외형 참고 · 변경하지 않음</h2><div class="refs">@@REFS@@</div><details><summary>제작 문서·규격·미확정 항목</summary><pre>@@MD@@</pre></details></main><script>
const assets=@@DATA@@;const stage=document.getElementById('stage'),canvas=document.querySelector('canvas'),ctx=canvas.getContext('2d'),select=document.getElementById('asset');let current=0,frame=0,running=true,once=false,last=0,acc=0,zoom=1;const images=assets.map(a=>{const im=new Image();im.onload=draw;im.src=a.src;return im;});assets.forEach((a,i)=>{const o=document.createElement('option');o.value=i;o.textContent=a.title;select.append(o);});document.getElementById('fps').value=String(assets[0].fps||10);function draw(){const a=assets[current];const side=a.kind==='projectile'?Math.ceil((Math.hypot(a.w,a.h)+16)/32)*32:0;const viewW=side||a.w,viewH=side||a.h;if(canvas.width!==viewW)canvas.width=viewW;if(canvas.height!==viewH)canvas.height=viewH;canvas.style.width=(viewW*zoom)+'px';canvas.style.height=(viewH*zoom)+'px';ctx.clearRect(0,0,viewW,viewH);ctx.imageSmoothingEnabled=false;if(images[current]&&images[current].complete){ctx.save();if(a.kind==='projectile'){ctx.translate(viewW/2,viewH/2);ctx.rotate(Number(document.getElementById('angle').value)*Math.PI/180);ctx.translate(-viewW/2,-viewH/2);}ctx.drawImage(images[current],frame*a.w,0,a.w,a.h,(viewW-a.w)/2,(viewH-a.h)/2,a.w,a.h);ctx.restore();}document.getElementById('readout').textContent=a.title+' · 프레임 '+(frame+1)+'/'+a.frames+' · 칸 '+a.w+'×'+a.h+' · 피벗 '+a.pivot.join(',')+' · '+document.getElementById('fps').value+'fps · '+(running&&a.frames>1?'재생 중':'정지/정적');document.getElementById('play').textContent=running&&a.frames>1?'정지':'재생';}function reset(){frame=0;acc=0;last=performance.now();draw();}select.onchange=()=>{current=Number(select.value);once=false;running=assets[current].frames>1;document.getElementById('fps').value=String(assets[current].fps||10);reset();};document.getElementById('play').onclick=()=>{running=assets[current].frames>1&&!running;once=false;acc=0;last=performance.now();draw();};document.getElementById('prev').onclick=()=>{running=false;const n=assets[current].frames;frame=(frame+n-1)%n;draw();};document.getElementById('next').onclick=()=>{running=false;frame=(frame+1)%assets[current].frames;draw();};document.getElementById('once').onclick=()=>{once=true;running=assets[current].frames>1;reset();};document.getElementById('scale').onclick=function(){zoom=zoom===1?2:1;this.textContent=zoom===1?'2배 보기':'1배 보기';draw();};document.getElementById('background').onclick=function(){const light=stage.classList.toggle('light');this.textContent=light?'어두운 배경':'밝은 배경';};document.getElementById('fps').onchange=()=>{acc=0;draw();};document.getElementById('angle').oninput=draw;function tick(t){if(!last)last=t;const delta=Math.min(t-last,250);last=t;if(running&&assets[current].frames>1){acc+=delta;const interval=1000/Number(document.getElementById('fps').value);while(acc>=interval&&running){acc-=interval;if(once&&frame===assets[current].frames-1)running=false;else frame=(frame+1)%assets[current].frames;}draw();}requestAnimationFrame(tick);}draw();requestAnimationFrame(tick);
</script></html>'''
sheets=''.join('<figure><figcaption>'+html.escape(a['title'])+' · '+str(a['frames'])+'칸 · <a download="'+a['id']+'_sheet.png" href="'+a['src']+'">PNG 저장</a></figcaption><img alt="'+html.escape(a['title'])+' 시트" src="'+a['src']+'"></figure>' for a in data)
refs=''
for title,name in [('기존 궁수 후면','archer_reference.png'),('기존 총병 후면','gunner_reference.png'),('기존 바깥향 대포','cannon_reference.png')]:
    refs+='<figure><figcaption>'+title+'</figcaption><img alt="'+title+'" src="data:image/png;base64,'+base64.b64encode((HERE/name).read_bytes()).decode('ascii')+'"></figure>'
page=page.replace('@@SHEETS@@',sheets).replace('@@REFS@@',refs).replace('@@MD@@',html.escape(md)).replace('@@DATA@@',json.dumps(data,ensure_ascii=False))
out=ROOT/'_HTML/codex_K016_무기와사격_v1.html';out.parent.mkdir(parents=True,exist_ok=True);(HERE/'preview.html').write_text(page,encoding='utf-8');out.write_text(page,encoding='utf-8')
check('독립 HTML 사본동일',(HERE/'preview.html').read_bytes()==out.read_bytes());check('README 내장',html.escape(md) in page);check('이미지7종 내장',len(data)==7);check('외부 의존 없음','src="http' not in page)
script=re.search('<script>(.*?)</script>',page,re.S).group(1);node=shutil.which('node') or str(Path.home()/'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe');syntax=subprocess.run([node,'--check'],input=script,text=True,capture_output=True,encoding='utf-8');check('JS 구문',syntax.returncode==0)
if (HERE/'verify_player.cjs').exists():
    r=subprocess.run([node,str(HERE/'verify_player.cjs')],capture_output=True,text=True,encoding='utf-8');check('플레이어 동작모형',r.returncode==0);checks.extend(json.loads(r.stdout)['checks'])
qa={'card':'K-016','checks':checks,'count':len(checks),'result':'passed','browserQA':'미수행·부모 추가확인','unityQA':'미수행'}
if (HERE/'browser_qa.json').exists():qa['browserQA']=json.loads((HERE/'browser_qa.json').read_text(encoding='utf-8'))
(HERE/'qa.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
contact=Image.new('RGB',(1536,2*412+284),(207,210,201));d=ImageDraw.Draw(contact)
for row,ident in enumerate(['archer_fire','gunner_fire']):
    r=next(a for a in records if a['id']==ident);im=Image.open(HERE/r['sheet']);contact.paste(im,(0,row*412+28),im);d.text((8,row*412+6),ident,fill=(20,20,20))
r=next(a for a in records if a['id']=='cannon_recoil');im=Image.open(HERE/r['sheet']);contact.paste(im,(0,2*412+28),im);d.text((8,2*412+6),'cannon_recoil',fill=(20,20,20));contact.save(HERE/'contact_review.png')
projectiles=Image.new('RGB',(768,156),(207,210,201));pd=ImageDraw.Draw(projectiles)
for i,name in enumerate(['arrow','bullet_trace','cannonball']):
    image=Image.open(HERE/(name+'_sheet.png'));projectiles.paste(image,(i*256,28),image);pd.text((i*256+8,6),name,fill=(20,20,20))
projectiles.save(HERE/'projectile_review.png')
print(json.dumps({'result':'passed','checks':len(checks),'assets':len(records)},ensure_ascii=False))