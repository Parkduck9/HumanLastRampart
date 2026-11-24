const fs=require('fs'),path=require('path'),crypto=require('crypto');
const sharp=require('C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const P=__dirname,R=path.resolve(P,'../../..');
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const png=async(data,w,h)=>sharp(data,{raw:{width:w,height:h,channels:4}}).png().toBuffer();
// 재질마다 약 세 단계의 명암을 쓰는 공통 팔레트로 생성 잡색을 정리한다.
const palette=['100e13','23212a','37353f','514c56','8e8997','bcb8c4','dedbe0','fff1e6','6c351e','a35b32','d78a52','ffd09a','f9b56f','7e4b12','bc8015','eebc36','ffe481','352018','68351f','995233','c17b4c','a6927c','d6c5b3','fff7e9','153754','285b83','4c8ab0'].map(s=>[0,2,4].map(i=>parseInt(s.slice(i,i+2),16)));
function recolor(data){for(let i=0;i<data.length;i+=4)if(data[i+3]){let best=Infinity,pick;for(const p of palette){const d=(p[0]-data[i])**2*2+(p[1]-data[i+1])**2*3+(p[2]-data[i+2])**2*2;if(d<best){best=d;pick=p;}}for(let c=0;c<3;c++)data[i+c]=pick[c];}}
// 알파를 이진화하고 본체와 연결되지 않은 잔점을 제거한다.
function clean(data,w,h){
 for(let i=0;i<w*h;i++){if(data[i*4+3]<128)data.fill(0,i*4,i*4+4);else data[i*4+3]=255;}
 const seen=new Uint8Array(w*h),groups=[];
 for(let i=0;i<w*h;i++)if(data[i*4+3]&&!seen[i]){
  const q=[i];seen[i]=1;
  for(let k=0;k<q.length;k++){let n=q[k],x=n%w,y=Math.floor(n/w);
   for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){let xx=x+dx,yy=y+dy,j=yy*w+xx;if(xx>=0&&xx<w&&yy>=0&&yy<h&&!seen[j]&&data[j*4+3]){seen[j]=1;q.push(j);}}
  }groups.push(q);
 }
 groups.sort((a,b)=>b.length-a.length);for(const g of groups.slice(1))for(const i of g)data.fill(0,i*4,i*4+4);
 return {components:groups.map(g=>g.length),removed:groups.slice(1).reduce((a,b)=>a+b.length,0)};
}
function bbox(data,w,h){let x0=w,y0=h,x1=0,y1=0;for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(data[(y*w+x)*4+3]){x0=Math.min(x0,x);x1=Math.max(x1,x+1);y0=Math.min(y0,y);y1=Math.max(y1,y+1);}return [x0,y0,x1,y1];}
async function normalize(file,cols,rows){
 const meta=await sharp(file).metadata(),cw=Math.floor(meta.width/cols),ch=Math.floor(meta.height/rows),frames=[],records=[];
 for(let r=0;r<rows;r++)for(let c=0;c<cols;c++){
  let {data,info}=await sharp(file).extract({left:c*cw,top:r*ch,width:cw,height:ch}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const sourceClean=clean(data,info.width,info.height),b=bbox(data,info.width,info.height);
  let ww=Math.round((b[2]-b[0])*73/(b[3]-b[1]));
  let raw=await sharp(data,{raw:{width:info.width,height:info.height,channels:4}}).extract({left:b[0],top:b[1],width:b[2]-b[0],height:b[3]-b[1]}).resize(ww,73,{kernel:'nearest'}).raw().toBuffer();
  const resultClean=clean(raw,ww,73),bb=bbox(raw,ww,73);recolor(raw);
  // 머리의 좌우 끝 중간을 몸 중심축으로 사용해 뻗은 팔 때문에 위치가 흔들리지 않게 한다.
  let hx0=ww,hx1=0;for(let y=0;y<25;y++)for(let x=0;x<ww;x++)if(raw[(y*ww+x)*4+3]){hx0=Math.min(hx0,x);hx1=Math.max(hx1,x+1);}
  const left=48-Math.round((hx0+hx1)/2),top=120-bb[3];
  if(left<0||left+ww>96)throw Error('칸을 벗어난 프레임 '+r+','+c);
  const out=Buffer.alloc(96*128*4);for(let y=0;y<73;y++)raw.copy(out,((y+top)*96+left)*4,y*ww*4,(y+1)*ww*4);
  const frame=await png(out,96,128);frames.push(frame);
  const box=bbox(out,96,128);records.push({index:r*cols+c,row:r,column:c,bbox:box,head_axis_x:48,foot_bottom_exclusive:box[3],source_bbox:b,source_removed_pixels:sourceClean.removed,native_removed_pixels:resultClean.removed});
 }
 return {frames,records};
}
async function sheet(name,frames,cols,rows,records){
 const w=cols*96,h=rows*128;
 const native=await sharp({create:{width:w,height:h,channels:4,background:'#00000000'}}).composite(frames.map((input,i)=>({input,left:(i%cols)*96,top:Math.floor(i/cols)*128}))).png().toBuffer();
 fs.writeFileSync(path.join(P,name+'_native.png'),native);
 await sharp(native).resize(w*3,h*3,{kernel:'nearest'}).png().toFile(path.join(P,name+'.png'));
 await sharp(native).resize(w*4,h*4,{kernel:'nearest'}).png().toFile(path.join(P,name+'_4x.png'));
 return {file:name+'.png',native:name+'_native.png',preview:name+'_4x.png',columns:cols,rows,frame_count:frames.length,native_size:[w,h],game_size:[w*3,h*3],frames:records};
}
async function main(){
 const grid=await normalize(path.join(P,'hero_walk_run_grid_generated.png'),4,4);
 const cmd=await normalize(path.join(P,'hero_command_generated.png'),3,1);
 const idle={frames:[cmd.frames[0]],records:[{...cmd.records[0],index:0,column:0}]};
 const sheets=[];sheets.push(await sheet('hero_back_idle',idle.frames,1,1,idle.records));
 sheets.push(await sheet('hero_walk_run_grid',grid.frames,4,4,grid.records));
 sheets.push(await sheet('hero_command',cmd.frames,3,1,cmd.records));
 const sourceNames=['hero_back_idle','hero_walk_run_grid','hero_command'];
 const sourceHashes=Object.fromEntries(sourceNames.map(n=>[n+'.png',hash(path.join(R,'Game/Assets/_Project/Art/Characters/Hero',n+'.png'))]));
 const manifest={card:'K-080',status:'제작 완료 · 게임 연결 및 사용자 검토 대기',decision:'D-080 정정 · D-081',native_cell:[96,128],native_foot_pivot:[48,120],game_cell:[288,384],game_foot_pivot:[144,360],pivot_normalized_bottom_left:[0.5,0.0625],figure_height:73,head_height_target_approx:24,palette:palette.map(p=>'#'+p.map(v=>v.toString(16).padStart(2,'0')).join('')),scaling:'nearest-neighbor 3x',baseline_convention:'발바닥 아래 경계 y=120, 마지막 불투명 행 y=119',body_center_method:'상단 25행 머리 외곽 중간을 x=48에 맞춤',frame_order:{hero_back_idle:['후면 대기'],hero_walk_run_grid:[['후면 걷기 1','후면 걷기 2','후면 걷기 3','후면 걷기 4'],['왼쪽 걷기 1','왼쪽 걷기 2','왼쪽 걷기 3','왼쪽 걷기 4'],['왼쪽 달리기 1','왼쪽 달리기 2','왼쪽 달리기 3','왼쪽 달리기 4'],['정면 걷기 1','정면 걷기 2','정면 걷기 3','정면 걷기 4']],hero_command:['후면 대기','손 올리기','팔 뻗어 지휘']},source_hashes:sourceHashes,sheets};
 fs.writeFileSync(path.join(P,'manifest.json'),JSON.stringify(manifest,null,2)+'\n');
 await review(manifest);
}
async function review(m){
 const inline=p=>'data:image/png;base64,'+fs.readFileSync(p).toString('base64');
 const sections=[];
 for(const [title,name,row,count] of [['후면 대기','hero_back_idle',0,1],['후면 걷기','hero_walk_run_grid',0,4],['왼쪽 걷기','hero_walk_run_grid',1,4],['왼쪽 달리기','hero_walk_run_grid',2,4],['정면 걷기','hero_walk_run_grid',3,4],['지휘 동작','hero_command',0,3]]){
  const s=m.sheets.find(s=>s.file===name+'.png');
  const old=inline(path.join(R,'Game/Assets/_Project/Art/Characters/Hero',name+'.png')),fresh=inline(path.join(P,name+'_native.png'));
  sections.push(`<section><h2>${title}</h2><div class="pair"><figure><figcaption>현재 게임 시트 · 256×384칸</figcaption><canvas width="288" height="384" data-src="${old}" data-cw="256" data-ch="384" data-row="${row}" data-count="${count}"></canvas></figure><figure><figcaption>새 그림 · 원본 96×128의 3배</figcaption><canvas width="288" height="384" data-src="${fresh}" data-cw="96" data-ch="128" data-row="${row}" data-count="${count}"></canvas></figure></div><p>왼쪽부터 ${m.frame_order[name][row] instanceof Array?m.frame_order[name][row].join(' → '):m.frame_order[name].join(' → ')}</p></section>`);
 }
 const page=`<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-080 주인공 동작 검토</title><style>*{box-sizing:border-box}body{margin:0;background:#11151c;color:#e8e4da;font:16px/1.7 system-ui}main{max-width:1100px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:21px;color:#e9c573}p{color:#bdc5d0}button,input{font:inherit}button{background:#e4c370;border:0;border-radius:5px;padding:8px 18px;cursor:pointer}nav{position:sticky;top:0;background:#11151cf5;padding:12px 0;z-index:1;display:flex;flex-wrap:wrap;gap:15px;align-items:center}section{border-top:1px solid #39404b;padding:20px 0}.pair{display:flex;flex-wrap:wrap;gap:24px}figure{margin:0;max-width:100%}figcaption{padding:8px 0}canvas{display:block;max-width:100%;height:auto;image-rendering:pixelated;background:repeating-conic-gradient(#222a35 0% 25%,#2d3643 0% 50%) 0/24px 24px;border:1px solid #495262}.sheet{width:100%;image-rendering:pixelated}details{margin:20px 0}a{color:#edce83}.note{border-left:3px solid #c3a55e;padding:8px 16px}#state{color:#edce83}</style><main><h1>K-080 · 흰 머리 장교 주인공</h1><p>선택한 6판 원본의 흰 머리·파란 눈·금색 견장·짙은 장교복을 기준으로 제작했습니다. 인물 약 73px, 머리 약 24px. 게임 연결 전 검토 자료입니다.</p><p class="note">기존 시트와 새 시트를 동일한 발선에 놓았습니다. 새 원본 칸은 96×128, 게임용은 288×384입니다. 발 기준 (48,120) → (144,360). 가로선은 발바닥 아래 경계입니다. 실제 게임 캡처가 아닙니다.</p><nav><button id="toggle">일시 정지</button><label>속도 <input id="fps" type="range" min="2" max="12" value="6"> <span id="speed">6</span>장/초</label><button id="step">한 장씩</button><span id="state">그림 불러오는 중</span></nav>${sections.join('')}<section><h2>선택 원본과 전체 시트</h2><p>아래 선택 원본은 generated.png입니다. 보정본은 사용하지 않았습니다.</p><img alt="사용자가 선택한 6판 원본" src="${inline(path.join(R,'Docs/References/codex_hero_octopath_review_v6/generated.png'))}" style="width:256px;max-width:100%">${m.sheets.map(s=>`<details><summary>${s.file} · 전체 프레임</summary><img class="sheet" alt="${s.file} 전체 프레임" src="${inline(path.join(P,s.preview))}"></details>`).join('')}</section><p>제작: 내장 이미지 생성 도구 · 원본/3배 시트/4배 미리보기 제공. Unity 실행과 실제 화면 검증은 수행하지 않았습니다.</p></main><script>let tick=0,playing=true,last=0;const items=[...document.querySelectorAll('canvas')].map(c=>({c,img:new Image()}));window.ready=Promise.all(items.map(o=>new Promise((resolve,reject)=>{o.img.onload=resolve;o.img.onerror=reject;o.img.src=o.c.dataset.src}))).then(()=>{window.loaded=true;draw();document.querySelector('#state').textContent='모든 그림 로드 완료';});function draw(){for(const {c,img} of items){const d=c.dataset,w=+d.cw,h=+d.ch,n=+d.count,f=tick%n,x=c.getContext('2d');x.clearRect(0,0,288,384);x.imageSmoothingEnabled=false;if(w===96)x.drawImage(img,f*w,+d.row*h,w,h,0,0,288,384);else x.drawImage(img,f*w,+d.row*h,w,h,16,8,w,h);x.strokeStyle='#81bcaa';x.beginPath();x.moveTo(0,360.5);x.lineTo(288,360.5);x.stroke();}window.frameTick=tick;}function loop(t){if(playing&&window.loaded&&t-last>=1000/+document.querySelector('#fps').value){tick++;draw();last=t;}requestAnimationFrame(loop)}requestAnimationFrame(loop);document.querySelector('#toggle').onclick=()=>{playing=!playing;document.querySelector('#toggle').textContent=playing?'일시 정지':'재생';};document.querySelector('#step').onclick=()=>{playing=false;document.querySelector('#toggle').textContent='재생';tick++;draw();};document.querySelector('#fps').oninput=e=>document.querySelector('#speed').textContent=e.target.value;</script></html>`;
 fs.writeFileSync(path.join(R,'_HTML/4_그림_캐릭터/codex_K-080_주인공동작_v1.html'),page);
}
main().catch(e=>{console.error(e);process.exit(1)});
