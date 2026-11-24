import sys
sys.dont_write_bytecode=True
from build import ROOT, OUT, CELL, GROUND, CX, NN, uri
from PIL import Image, ImageDraw, ImageFont
import json

m=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
data=[]
for motion in m['motions']:
    data.append(dict(motion,albedo=uri(Image.open(OUT/motion['files']['native'])),normal=uri(Image.open(OUT/motion['files']['native_normal']))))
oldroot=ROOT/'Game/Assets/_Project/Art/Characters/Walk'
old={name:uri(Image.open(oldroot/file)) for name,file in [('walk','ogre_walk.png'),('attack','ogre_attack4.png'),('walk_normal','ogre_walk_normal.png'),('attack_normal','ogre_attack4_normal.png')]}
for name,file in [('walk','ogre_walk.png'),('attack','ogre_attack4.png')]:
    src=Image.open(oldroot/file).convert('RGBA');sheet=Image.new('RGBA',(1024,288))
    first=src.crop((0,0,256,384));b=first.getbbox();scale=96/(b[3]-b[1])
    for f in range(4):
        im=src.crop((f*256,0,(f+1)*256,384));box=im.getbbox();im=im.crop(box);im=im.resize((round(im.width*scale),round(im.height*scale)),NN)
        sheet.paste(im,(f*256+128-im.width//2,280-im.height))
    old[name+'_review']=uri(sheet)
ui=[dict(v,src=uri(Image.open(OUT/v['file'])),nrm=uri(Image.open(OUT/v['normal']))) for v in m['ui']]
head='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>성벽손 오우거 · 동작 검토</title>
<style>*{box-sizing:border-box}body{margin:0;background:#111713;color:#e8e2cd;font:16px/1.65 system-ui,sans-serif}header,main{max-width:1160px;margin:auto;padding:24px}h1{font-size:32px;margin:8px 0}h2{font-size:21px;margin:0 0 8px}h3{font-size:16px;color:#c6b68e;margin:8px 0}p{margin:8px 0 16px}.eyebrow{color:#c99d58;letter-spacing:.15em}.muted{color:#adb5a6}.bar{position:sticky;top:0;background:#202a24f5;padding:12px;z-index:2;display:flex;flex-wrap:wrap;gap:12px;align-items:center;border-block:1px solid #52604c}button,select{font:inherit;background:#344335;color:#f3ecd5;border:1px solid #69735b;padding:6px 12px;border-radius:5px}button{cursor:pointer}section{border:1px solid #46513f;border-radius:8px;padding:18px;margin:22px 0;background:#1b241e}.views{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}.views>div{min-width:0}.framebox{overflow:auto;background:repeating-conic-gradient(#19221b 0 25%,#1d281f 0 50%) 0/20px 20px;border:1px solid #384635}canvas{image-rendering:pixelated;display:block;margin:auto;width:256px;height:288px}.framebox.zoom canvas{width:512px;height:576px}.phase{min-height:3em;color:#c7cfbd;font-size:14px}.note{background:#2d3024;border-left:3px solid #c39a54;padding:12px}.ui{display:flex;gap:36px;flex-wrap:wrap}.ui img{image-rendering:pixelated;max-width:100%}.source{max-width:100%;image-rendering:pixelated}details{margin-top:12px}a{color:#dfba75}table{border-collapse:collapse;width:100%}td,th{text-align:left;padding:8px;border-bottom:1px solid #3d493b}footer{color:#adb5a6;padding:20px 0}@media(max-width:800px){header,main{padding:14px}.views{grid-template-columns:1fr}h1{font-size:27px}.bar{position:relative}section{padding:12px}}</style>
<header><div class="eyebrow">K-129 · 점화의 밤</div><h1>성벽손 오우거 — 손으로 오르고, 주먹으로 부순다</h1><p>선택한 B안의 긴 팔·쇠장갑·짧은 다리. 걷기부터 죽음까지 <b>17동작 · 120프레임</b>을 확인합니다.</p><p class="muted">왼쪽은 새 우두머리, 가운데는 현재 게임 오우거, 오른쪽은 4m 성벽과 같은 눈금의 크기 비교입니다. 현재 오우거의 없는 동작은 기존 공격·걷기를 비교용으로 보여줍니다.</p><div class="note">실제 전투 캡처가 아닌 투명 스프라이트 검토판입니다. 성벽은 높이 눈금용 도식입니다. 야간 광선·노멀 조명은 브라우저 근사이며 Unity 결과가 아닙니다.</div></header>
<main><div class="bar"><button id="play">일시정지</button><button id="step">한 칸 진행</button><label>표시 <select id="mode"><option value="color">기본 색</option><option value="night">밤의 광선</option><option value="silhouette">검은 실루엣</option><option value="normal">노멀 방향</option><option value="lit">노멀 조명</option></select></label><label>배율 <select id="zoom"><option value="1">원본 점 1배</option><option value="2">원본 점 2배</option></select></label><label><input type="checkbox" id="guides" checked>기준점</label><span id="loaded">그림 읽는 중…</span></div>
<section><h2>규격과 연결 기준</h2><table><tr><th>항목</th><th>원본</th><th>게임용</th></tr><tr><td>한 칸</td><td>256 × 288</td><td>768 × 864 · 정확히 3배</td></tr><tr><td>지면 피벗</td><td>(128, 280)</td><td>(384, 840)</td></tr><tr><td>기립 키 / 성벽</td><td>144 / 128점</td><td>4.5m / 4m</td></tr><tr><td>등반 기준</td><td>(128, 16) · 손 위 매달림 기준</td><td>(384, 48)</td></tr><tr><td>추락 기준</td><td>(128, 152) · 몸 중심</td><td>(384, 456)</td></tr></table><p class="muted">발이 뜨는 동작에는 지면 기준을 그대로 유지하고 몸의 자세가 변합니다. 등반·추락의 월드 이동은 연결 담당이 적용합니다. 기존 파일의 교체본이 아닌 별도 우두머리 자산입니다. 좌측 걷기는 우측 걷기의 반전이며 어깨 장식도 반전됩니다.</p></section>
<div id="motions"></div><section><h2>지도 아이콘 · 체력 막대 초상</h2><div class="ui" id="ui"></div></section>
<section><h2>현재 게임 시트 원본</h2><p>비교에는 읽기 전용 원본을 내장했습니다. 일반 오우거는 이 검토판에서 키96점으로 맞췄으며, 확정된 월드 키라는 뜻은 아닙니다.</p><details><summary>걷기 · 4칸과 노멀맵</summary><img class="source" id="oldwalk" alt="현재 오우거 걷기 전체 시트"><img class="source" id="oldnormal" alt="현재 오우거 걷기 노멀 전체 시트"></details><details><summary>공격 · 4칸</summary><img class="source" id="oldattack" alt="현재 오우거 공격 전체 시트"></details></section>
<footer>노멀맵은 색 윤곽에서 만든 얕은 부조입니다. 피부·갑옷의 실제 입체를 베이크한 자료가 아닙니다. 게임 임포트·광선·함정 연결·실제 전투 거리에서의 판독성은 Claude와 사용자 확인이 남아 있습니다.</footer></main>
'''
script=r'''
<script>
const motions=__MOTIONS__,old=__OLD__,ui=__UI__;
let playing=true, manual=0, tick=0, errors=[];
const container=document.getElementById('motions');
const makeImage=src=>{let im=new Image();im.src=src;return im};
const sourceCanvas=im=>{let c=document.createElement('canvas');c.width=im.width;c.height=im.height;c.getContext('2d').drawImage(im,0,0);return c};
for(const [i,m] of motions.entries()){
 let s=document.createElement('section');s.dataset.motion=m.name;
 s.innerHTML=`<h2>${String(i+1).padStart(2,'0')} · ${m.label}</h2><div class="phase"></div><div class="views"><div><h3>새 우두머리 · ${m.n}칸</h3><div class="framebox"><canvas width="256" height="288" class="boss"></canvas></div></div><div><h3>현재 오우거 · 비교용 ${m.name.includes('walk')?'걷기':'공격'}</h3><div class="framebox"><canvas width="256" height="288" class="current"></canvas></div></div><div><h3>동일 눈금 · 우두머리 4.5m / 성벽 4m</h3><div class="framebox"><canvas width="256" height="288" class="scale"></canvas></div></div></div><details><summary>동작 순서 · 기준</summary><p>${m.phases.map((x,i)=>`${i}: ${x}`).join(' → ')}</p><p>${m.mode==='grip'?'손 위 매달림 기준 고정 · 지면 이동 없음':m.mode==='pelvis'?'몸 중심 기준 고정 · 추락 이동 없음':'지면 기준 고정'} · ${m.loop?'반복':'마지막 자세를 유지한 뒤 검토용 재시작'} · ${m.fps}칸/초</p></details>`;
 container.append(s);m.section=s;m.im=makeImage(m.albedo);m.nim=makeImage(m.normal);
}
const oldImages=Object.fromEntries(Object.entries(old).map(([k,v])=>[k,makeImage(v)]));
document.getElementById('oldwalk').src=old.walk;document.getElementById('oldnormal').src=old.walk_normal;document.getElementById('oldattack').src=old.attack;
for(const u of ui){let d=document.createElement('div');d.innerHTML=`<h3>${u.label} · ${u.size[0]}×${u.size[1]}</h3><img src="${u.src}" alt="${u.label}" width="${u.size[0]*3}" height="${u.size[1]*3}"><details><summary>노멀맵</summary><img src="${u.nrm}" alt="${u.label} 노멀맵" width="${u.size[0]*2}"></details>`;document.getElementById('ui').append(d)}
function guide(c,anchor){let x=c.getContext('2d');x.strokeStyle='#ae9c6355';x.lineWidth=1;x.beginPath();x.moveTo(0,280.5);x.lineTo(256,280.5);x.moveTo(128.5,0);x.lineTo(128.5,288);x.stroke();x.fillStyle='#e8b958';x.fillRect(anchor[0]-2,anchor[1]-2,4,4)}
function wall(x){x.fillStyle='#666854';x.fillRect(212,152,32,128);x.strokeStyle='#30392a';x.lineWidth=1;for(let y=152;y<280;y+=8){x.beginPath();x.moveTo(212,y+.5);x.lineTo(244,y+.5);x.stroke();for(let xx=212+(y%16?8:16);xx<244;xx+=16){x.beginPath();x.moveTo(xx+.5,y);x.lineTo(xx+.5,y+8);x.stroke()}}x.fillStyle='#9a9879';x.fillRect(212,152,32,3)}
function drawBoss(m,f,c){let x=c.getContext('2d');x.clearRect(0,0,256,288);x.imageSmoothingEnabled=false;x.drawImage(m.im,f*256,0,256,288,0,0,256,288);let mode=document.getElementById('mode').value;
 if(mode!=='color'){
 let a=x.getImageData(0,0,256,288),n=m.normalPixels[f];
 for(let p=0;p<a.data.length;p+=4){if(!a.data[p+3])continue;
 if(mode==='silhouette'){a.data[p]=9;a.data[p+1]=13;a.data[p+2]=9}
 if(mode==='normal'){a.data[p]=n[p];a.data[p+1]=n[p+1];a.data[p+2]=n[p+2]}
 if(mode==='night'){let px=(p/4)%256,py=Math.floor(p/1024),beam=px>32+(280-py)*.13&&px<220;let k=beam?.78:.14;a.data[p]*=k;a.data[p+1]*=k*.94;a.data[p+2]*=k*.79}
 if(mode==='lit'){let nx=n[p]/127.5-1,ny=n[p+1]/127.5-1,nz=n[p+2]/127.5-1;let v=.12+.88*Math.max(0,nx*(-.56)+ny*.65+nz*.51);a.data[p]*=v;a.data[p+1]*=v*.96;a.data[p+2]*=v*.87}
 }x.putImageData(a,0,0)}
 if(document.getElementById('guides').checked)guide(c,m.frames[f].anchor_native);
}
function drawOld(m,t){let c=m.section.querySelector('.current'),x=c.getContext('2d');x.clearRect(0,0,256,288);x.imageSmoothingEnabled=false;let key=m.name.includes('walk')?'walk_review':'attack_review',im=oldImages[key],f=Math.floor(t*7)%4;x.drawImage(im,f*256,0,256,288,0,0,256,288);if(document.getElementById('guides').checked)guide(c,[128,280])}
function render(t){for(const m of motions){let hold=m.loop?0:m.fps*1.5;let v=playing?Math.floor(t*m.fps)%(m.n+hold):manual;let f=Math.min(m.n-1,Math.floor(v));drawBoss(m,f,m.section.querySelector('.boss'));drawOld(m,t);let c=m.section.querySelector('.scale'),x=c.getContext('2d');x.clearRect(0,0,256,288);x.imageSmoothingEnabled=false;x.drawImage(motions[0].im,0,0,256,288,0,0,256,288);wall(x);if(document.getElementById('guides').checked)guide(c,[128,280]);m.section.querySelector('.phase').textContent=`${f+1} / ${m.n} · ${m.phases[f]}`;m.section.dataset.frame=f}document.body.dataset.tick=++tick}
document.getElementById('play').onclick=()=>{playing=!playing;document.getElementById('play').textContent=playing?'일시정지':'재생';if(!playing)manual=0;render(performance.now()/1000)};
document.getElementById('step').onclick=()=>{playing=false;manual=(manual+1)%10;document.getElementById('play').textContent='재생';render(performance.now()/1000)};
document.getElementById('zoom').onchange=e=>document.querySelectorAll('.framebox').forEach(x=>x.classList.toggle('zoom',e.target.value==='2'));
document.getElementById('mode').onchange=()=>render(performance.now()/1000);document.getElementById('guides').onchange=()=>render(performance.now()/1000);
window.ready=Promise.all([...motions.flatMap(m=>[m.im.decode(),m.nim.decode()]),...Object.values(oldImages).map(im=>im.decode()),...Array.from(document.images).map(im=>im.decode())]).then(()=>{for(const m of motions){let sc=sourceCanvas(m.nim),x=sc.getContext('2d');m.normalPixels=Array.from({length:m.n},(_,f)=>x.getImageData(f*256,0,256,288).data)}document.getElementById('loaded').textContent='모든 그림 준비됨';window.assetsReady=true;render(0);setInterval(()=>{if(playing)render(performance.now()/1000)},100)}).catch(e=>{document.getElementById('loaded').textContent='그림 읽기 실패';throw e});
</script></html>'''
script=script.replace('__MOTIONS__',json.dumps(data,ensure_ascii=False)).replace('__OLD__',json.dumps(old)).replace('__UI__',json.dumps(ui,ensure_ascii=False))
page=ROOT/'_HTML/6_그림_화면/codex_K-129_우두머리동작_v1.html'
page.parent.mkdir(exist_ok=True,parents=True);page.write_text(head+script,encoding='utf-8')

# 동작 원본의 대표 칸을 투명 여백을 줄여 한 장에서 비교한다.
board=Image.new('RGB',(1600,17*180),(25,33,27));d=ImageDraw.Draw(board)
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',14)
for row,motion in enumerate(m['motions']):
    sh=Image.open(OUT/motion['files']['native'])
    for j,f in enumerate(range(motion['n'])):
        frame=sh.crop((f*256,0,(f+1)*256,288));box=frame.getbbox();frame=frame.crop(box)
        if max(frame.size)>150:frame.thumbnail((150,150),NN)
        board.paste(frame,(j*160+(160-frame.width)//2,row*180+18+(150-frame.height)//2),frame)
    d.text((4,row*180+2),motion['label'],fill='#c5b78d',font=font)
board.save(OUT/'contact_all.png')
print('검토 HTML 및 17동작 접촉판 생성')
