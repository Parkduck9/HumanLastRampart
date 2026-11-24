from pathlib import Path
from PIL import Image
import numpy as np
import base64, hashlib, html, json, re, subprocess, shutil
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
JOBS=json.loads((HERE/'jobs.json').read_text(encoding='utf-8'))
records=[]; checks=[]
def check(name,value):
    assert value,name
    checks.append({'name':name,'passed':True})
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
for job in JOBS:
    ident=job['id']; src=HERE/(ident+'_source.png')
    im=Image.open(src).convert('RGBA'); sw,sh=im.size
    alpha=im.getchannel('A'); bbox=alpha.point(lambda a:255 if a>=32 else 0).getbbox()
    check(ident+' 투명 배경 존재',alpha.getextrema()[0]==0)
    check(ident+' 효과 내용 존재',bbox is not None)
    x0,y0,x1,y1=bbox
    strong=(np.array(alpha)>=128).any(axis=0)
    ranges=[]; start=None
    for x,v in enumerate(strong):
        if v and start is None: start=x
        if not v and start is not None: ranges.append((start,x));start=None
    if start is not None:ranges.append((start,sw))
    gaps=[(ranges[i+1][0]-ranges[i][1],(ranges[i+1][0]+ranges[i][1])//2) for i in range(len(ranges)-1)]
    separators=sorted(mid for gap,mid in sorted(gaps,reverse=True)[:5])
    check(ident+' 가로6칸 공백 분리',len(separators)==5)
    bounds=[0]+separators+[sw]
    widths=[bounds[i+1]-bounds[i] for i in range(6)]
    cellw=max(widths)
    # All six cells share one scale and one vertical crop: do not normalize growth/fade frames independently.
    scale=min(1.0,232/cellw,job.get('targetMaxExtent',232)/max(1,y1-y0))
    out=Image.new('RGBA',(1536,256)); frames=[]
    for i in range(6):
        left=bounds[i]; right=bounds[i+1]
        panel=im.crop((left,y0,right,y1))
        size=(max(1,round((right-left)*scale)),max(1,round((y1-y0)*scale)))
        panel=panel.resize(size,Image.Resampling.NEAREST)
        # common lower-center anchor, content ending immediately above cell-bottom pivot
        dest=(i*256+128-size[0]//2,256-size[1])
        out.alpha_composite(panel,dest)
        frame=out.crop((i*256,0,(i+1)*256,256))
        pixels=frame.tobytes()
        frames.append({'index':i,'rect':[i*256,0,256,256],'pivotPixels':[128,256],'pivotNormalized':[0.5,0.0],'rgbaSha256':hashlib.sha256(pixels).hexdigest(),'alphaBBox':frame.getchannel('A').getbbox()})
    dest=HERE/(ident+'_sheet.png');out.save(dest)
    check(ident+' 최종 규격',out.size==(1536,256))
    check(ident+' 프레임6개 서로 다름',len({f['rgbaSha256'] for f in frames})==6)
    check(ident+' 모든 프레임 비어있지 않음',all(f['alphaBBox'] is not None for f in frames))
    check(ident+' 위/좌우 여유',all(f['alphaBBox'][0]>0 and f['alphaBBox'][2]<256 and f['alphaBBox'][1]>0 for f in frames))
    info={**job,'source':src.name,'sourceSize':[sw,sh],'sourceSha256':digest(src),'sheet':dest.name,'sheetSize':[1536,256],'sheetSha256':digest(dest),'cellSize':[256,256],'columns':6,'rows':1,'fps':10,'pivotPixels':[128,256],'pivotNormalized':[0.5,0],'sourceCommonCropY':[y0,y1],'sourceColumnBounds':bounds,'sourceColumnWidth':cellw,'commonScale':scale,'processing':'이미지 원안의 가로6칸을 같은 배율·같은 세로 범위로 최근접 정렬. 내용 덧칠/복제 프레임 없음. 아래 경계는 의도한 접지 피벗.','frames':frames,'approval':'외형 사용자 검토 대기 / 게임 미연결'}
    (HERE/(ident+'.json')).write_text(json.dumps(info,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    records.append(info)
manifest={'card':'K-012','task':'T-CX-055','status':'외형 사용자 검토 대기','cellSize':[256,256],'pivotPixels':[128,256],'fps':10,'assets':records,'notPerformed':['Unity 실행','게임 연결','실제 전투 크기/빛 확인'],'limits':['AI 원안의 프레임간 도트·실루엣 일관성은 육안 검토 대상','루프 두 종의6→1 연결은 사용자 확인 필요','효과에는 노멀맵/실시간 발광/빛 데이터가 포함되지 않음']}
from PIL import ImageDraw
contact=Image.new('RGB',(1536,6*284),(209,211,204)); draw=ImageDraw.Draw(contact)
for row,r in enumerate(records):
    sheet=Image.open(HERE/r['sheet']);contact.paste(sheet,(0,row*284+28),sheet)
    draw.text((8,row*284+5),r['id'],fill=(30,30,30))
contact.save(HERE/'contact_review.png')
(HERE/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
data=[]
for r in records:
    data.append({'id':r['id'],'title':r['title'],'loop':r['loop'],'src':'data:image/png;base64,'+base64.b64encode((HERE/r['sheet']).read_bytes()).decode('ascii')})
md=(HERE/'README.md').read_text(encoding='utf-8')
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-012 효과 도트 6종</title><style>:root{color-scheme:dark;font-family:Malgun Gothic,system-ui;background:#171d25;color:#eee8d8}*{box-sizing:border-box}body{margin:0}main{max-width:1160px;margin:auto;padding:28px}h1{font-size:27px}p,li{line-height:1.7;color:#c5cdd2}.note{padding:16px;border-left:4px solid #baa062;background:#29271f}.controls{display:flex;gap:10px;flex-wrap:wrap;margin:20px 0}button,select{font:inherit;padding:10px;border:1px solid #917f54;background:#2b3745;color:#eee;border-radius:6px}button{cursor:pointer}.stage{min-height:320px;display:flex;align-items:flex-end;justify-content:center;overflow:auto;background-color:#29323c;background-image:conic-gradient(#303a46 25%,transparent 0 50%,#303a46 0 75%,transparent 0);background-size:32px 32px;border:1px solid #58616a;padding:22px}.stage.light{background-color:#d8d6ca;background-image:conic-gradient(#bcbcad 25%,transparent 0 50%,#bcbcad 0 75%,transparent 0)}canvas{width:256px;height:256px;image-rendering:pixelated;flex-shrink:0}.stage.big canvas{width:512px;height:512px}.readout{padding:12px;color:#dcc58b}.sheets{display:grid;gap:16px;margin-top:24px}figure{margin:0;border:1px solid #46515c;border-radius:8px;overflow:hidden;background:#202a34}figcaption{padding:12px}figure img{display:block;width:100%;image-rendering:pixelated;background:#303a46}a{color:#e2c989}pre{white-space:pre-wrap;line-height:1.7;overflow-wrap:anywhere}summary{cursor:pointer;padding:14px}@media(max-width:600px){main{padding:14px}.stage.big canvas{width:384px;height:384px}}</style><main><h1>効果 도트 6종 · 6프레임</h1><div class="note">사용자 외형 검토용 새 PNG입니다. 실제 게임 연결·폭발 빛·피해 판정은 포함하지 않습니다.<br>각 시트 1536×256, 6칸, 칸 256×256, 피벗 (128,256), 기본 10fps.</div><div class="controls"><select id="effect" aria-label="효과 종류"></select><button id="play">정지</button><button id="prev">이전 칸</button><button id="next">다음 칸</button><button id="once">한 번 재생</button><select id="fps" aria-label="재생 속도"><option>8</option><option selected>10</option><option>12</option></select><button id="scale">2배 보기</button><button id="background">밝은 배경</button></div><div class="stage" id="stage"><canvas width="256" height="256" aria-label="선택 효과 애니메이션"></canvas></div><div class="readout" id="readout"></div><p>자동 미리보기는 반복합니다. 폭발·명중 효과는 게임에서 한 번 재생하는 원샷이고, 불바다·횃불만 루프 제안입니다. 이전/다음 칸을 누르면 정지합니다.</p><div class="sheets">@@SHEETS@@</div><details><summary>제작 문서·한계</summary><pre>@@MD@@</pre></details></main><script>
const assets=@@DATA@@;const stage=document.getElementById('stage'),canvas=document.querySelector('canvas'),ctx=canvas.getContext('2d'),select=document.getElementById('effect');let current=0,frame=0,running=true,once=false,last=0,acc=0;const images=assets.map(a=>{const im=new Image();im.onload=draw;im.src=a.src;return im;});assets.forEach((a,i)=>{const o=document.createElement('option');o.value=i;o.textContent=a.title+(a.loop?' · 루프':' · 원샷');select.append(o);});function draw(){ctx.clearRect(0,0,256,256);ctx.imageSmoothingEnabled=false;if(images[current]&&images[current].complete)ctx.drawImage(images[current],frame*256,0,256,256,0,0,256,256);document.getElementById('readout').textContent=assets[current].title+' · 프레임 '+(frame+1)+'/6 · '+document.getElementById('fps').value+'fps · '+(running?'재생 중':'정지');document.getElementById('play').textContent=running?'정지':'재생';}function reset(){frame=0;acc=0;last=performance.now();draw();}select.onchange=()=>{current=Number(select.value);once=false;running=true;reset();};document.getElementById('play').onclick=()=>{running=!running;once=false;acc=0;last=performance.now();draw();};document.getElementById('prev').onclick=()=>{running=false;frame=(frame+5)%6;draw();};document.getElementById('next').onclick=()=>{running=false;frame=(frame+1)%6;draw();};document.getElementById('once').onclick=()=>{once=true;running=true;reset();};document.getElementById('scale').onclick=function(){const big=stage.classList.toggle('big');this.textContent=big?'1배 보기':'2배 보기';};document.getElementById('background').onclick=function(){const light=stage.classList.toggle('light');this.textContent=light?'어두운 배경':'밝은 배경';};document.getElementById('fps').onchange=()=>{acc=0;draw();};function tick(t){if(!last)last=t;const delta=Math.min(t-last,250);last=t;if(running){acc+=delta;const interval=1000/Number(document.getElementById('fps').value);while(acc>=interval&&running){acc-=interval;if(once&&frame===5){running=false;}else frame=(frame+1)%6;}draw();}requestAnimationFrame(tick);}draw();requestAnimationFrame(tick);
</script></html>'''
sheets=''.join('<figure><figcaption>'+html.escape(a['title'])+' · '+('루프' if a['loop'] else '원샷')+' · <a download="'+a['id']+'_sheet.png" href="'+a['src']+'">PNG 저장</a></figcaption><img alt="'+html.escape(a['title'])+' 가로 6칸 시트" src="'+a['src']+'"></figure>' for a in data)
page=page.replace('効果','효과').replace('@@SHEETS@@',sheets).replace('@@MD@@',html.escape(md)).replace('@@DATA@@',json.dumps(data,ensure_ascii=False))
output=ROOT/'_HTML/codex_K012_효과도트_v1.html';output.parent.mkdir(parents=True,exist_ok=True)
(HERE/'preview.html').write_text(page,encoding='utf-8');output.write_text(page,encoding='utf-8')
check('독립 HTML 사본일치',(HERE/'preview.html').read_bytes()==output.read_bytes())
check('README 원문 내장',html.escape(md) in page)
check('PNG6종 데이터 내장',len(data)==6 and all(a['src'].startswith('data:image/png;base64,') for a in data))
check('외부 리소스 없음','src="http' not in page)
script=re.search('<script>(.*?)</script>',page,re.S).group(1)
node=shutil.which('node') or str(Path.home()/'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe')
syntax=subprocess.run([node,'--check'],input=script,text=True,capture_output=True)
check('HTML JavaScript 구문',syntax.returncode==0)
player=subprocess.run([node,str(HERE/'verify_player.cjs')],capture_output=True,text=True,encoding="utf-8")
check('플레이어 자동동작 검사',player.returncode==0)
player_result=json.loads(player.stdout)
checks.extend(player_result['checks'])
qa={'card':'K-012','checks':checks,'count':len(checks),'result':'passed','browserQA':'미수행: 부모 추가확인 예정','unityQA':'미수행','visualQA':'원안/최종 시트 직접 확인은 CARD_RESULT 참고'}
(HERE/'qa.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'result':'passed','checks':len(checks),'sheets':len(records),'html':str(output)},ensure_ascii=False))