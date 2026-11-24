const payload=JSON.parse(document.getElementById('payload').textContent);
const $=id=>document.getElementById(id),detail=$('detail'),ctx=detail.getContext('2d'),stage=$('stage'),world=stage.getContext('2d');
const state={clip:'carry_run',frame:0,playing:true,fps:6,elapsed:0,total:0,last:null};
const images={};
function bg(c,w,h){
 c.clearRect(0,0,w,h);const mode=$('background').value;
 if(mode==='checker'){for(let y=0;y<h;y+=16)for(let x=0;x<w;x+=16){c.fillStyle=((x+y)/16)%2?'#27312e':'#36413b';c.fillRect(x,y,16,16);}}
 else{c.fillStyle=mode==='light'?'#ded8c6':'#101613';c.fillRect(0,0,w,h);}
 c.imageSmoothingEnabled=false;
}
function frameTo(c,img,frame,x,y,scale){c.drawImage(img,frame*256,0,256,384,x-128*scale,y-352*scale,256*scale,384*scale);}
function drawWorld(){
 bg(world,960,640);const ground=560,top=112,wallX=650;
 world.fillStyle='#565d58';world.fillRect(wallX,top,290,ground-top);
 world.strokeStyle='#858b80';world.lineWidth=2;for(let y=top;y<ground;y+=44){world.beginPath();world.moveTo(wallX,y);world.lineTo(940,y);world.stroke();}
 world.strokeStyle='#d5b16b';world.beginPath();world.moveTo(38,ground);world.lineTo(940,ground);world.stroke();
 world.font='18px sans-serif';world.fillStyle=$('background').value==='light'?'#1b2921':'#eee4cf';world.fillText('성벽 4m / 병사 키 0.35m 기준 · 배치 설명용',32,36);
 world.fillText('실제 게임 캡처 아님 · 가로대/손 정렬은 후속 검토',32,63);
 const key=$('propView').value,prop=payload.assets[key],im=images[key],body=prop.body;
 const propScale=4.3*112/body[3],lean=Math.acos(4/4.3),footX=wallX-Math.sin(lean)*4.3*112;
 let x=footX,y=ground,angle=lean;
 if(state.clip==='carry_run'){x=265;y=ground;angle=0;}
 else if(state.clip==='deploy'){const p=state.frame/3;y=ground+(1-p)*420;angle=lean*p;}
 world.save();world.beginPath();world.rect(0,top-32,960,ground-top+32);world.clip();
 world.translate(x,y);world.rotate(angle);
 world.drawImage(im,-prop.pivot[0]*propScale,-prop.pivot[1]*propScale,im.width*propScale,im.height*propScale);
 world.restore();
 const clip=payload.assets[state.clip],characterScale=0.35*112/clip.maxBodyHeight;
 let charX=footX-15,charY=ground;
 if(state.clip==='carry_run'){charX=252;charY=ground;}
 else if(state.clip==='climb'){const p=(state.total%32)/31;charX=footX+(wallX-footX)*p-5;charY=ground-448*p;}
 frameTo(world,images[state.clip],state.frame,charX,charY,characterScale);
 world.fillStyle='#d5b16b';world.fillText(payload.labels[state.clip]+' · '+(state.frame+1)+'/4',32,612);
}
function draw(){
 if(!images[state.clip])return;
 bg(ctx,256,384);frameTo(ctx,images[state.clip],state.frame,128,352,1);
 ctx.strokeStyle='#d5b16b';ctx.beginPath();ctx.moveTo(118,352);ctx.lineTo(138,352);ctx.moveTo(128,342);ctx.lineTo(128,362);ctx.stroke();
 $('frameText').textContent=(state.frame+1)+' / 4';$('frame').value=state.frame;
 $('play').textContent=state.playing?'정지':'재생';$('play').setAttribute('aria-pressed',String(state.playing));
 $('loading').textContent='PNG 5개 로딩 완료 · '+(state.playing?'재생 중':'정지 중');
 $('phaseNote').textContent=state.clip==='deploy'?'세우기는 한 번 재생하고 4번에서 멈춥니다. 다시 재생하면 1번부터 시작합니다.':'1→2→3→4 순환 · 사다리 소품은 별도 레이어입니다.';
 drawWorld();
}
function step(delta){
 state.frame=(state.frame+delta+4)%4;state.total+=delta>0?1:0;draw();
}
function selectClip(value){state.clip=value;state.frame=0;state.total=0;state.elapsed=0;state.last=null;draw();}
$('clip').onchange=()=>selectClip($('clip').value);
$('play').onclick=()=>{if(!state.playing&&state.clip==='deploy'&&state.frame===3){state.frame=0;state.elapsed=0;}state.playing=!state.playing;state.last=null;draw();};
$('prev').onclick=()=>{state.playing=false;step(-1);};
$('next').onclick=()=>{state.playing=false;step(1);};
$('frame').oninput=()=>{state.playing=false;state.frame=+$('frame').value;draw();};
$('fps').onchange=()=>{state.fps=+$('fps').value;state.elapsed=0;};
$('scale').onchange=()=>{const scale=+$('scale').value;detail.style.width=256*scale+'px';detail.style.height=384*scale+'px';$('inspection').classList.toggle('zoomed',scale>1);};
$('background').onchange=()=>{draw();document.body.dataset.background=$('background').value;};
$('propView').onchange=draw;
function tick(now){
 if(state.last!==null&&state.playing){state.elapsed+=Math.min(now-state.last,250);const interval=1000/state.fps;while(state.elapsed>=interval&&state.playing){state.elapsed-=interval;if(state.clip==='deploy'&&state.frame===3){state.playing=false;break;}step(1);}draw();}
 state.last=now;requestAnimationFrame(tick);
}
Promise.all(Object.entries(payload.assets).map(([key,item])=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{images[key]=im;resolve();};im.onerror=()=>reject(new Error('이미지 읽기 실패: '+key));im.src=item.uri;}))).then(()=>{
 for(const key of ['carry_run','deploy','climb']){
  const area=document.createElement('section');area.innerHTML='<h3>'+payload.labels[key]+' · 4칸</h3>';const grid=document.createElement('div');grid.className='frames';
  for(let f=0;f<4;f++){const figure=document.createElement('figure'),c=document.createElement('canvas');c.width=256;c.height=384;const t=c.getContext('2d');bg(t,256,384);frameTo(t,images[key],f,128,352,1);const cap=document.createElement('figcaption');cap.textContent=f+1;figure.append(c,cap);grid.append(figure);}area.append(grid);$('sheets').append(area);
 }
 $('loading').textContent='PNG 5개 로딩 완료 · 자동 재생 중';draw();requestAnimationFrame(tick);
}).catch(error=>{$('loading').textContent=error.message;state.playing=false;});
