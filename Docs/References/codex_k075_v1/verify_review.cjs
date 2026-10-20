const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
async function verify(root){
 const dir=path.join(root,'Docs/References/codex_k075_v1'),file=path.join(root,'_HTML/4_그림_캐릭터/codex_K-075_대포병동작_v1.html');
 const html=fs.readFileSync(file,'utf8'),code=html.match(/<script>([\s\S]*?)<\/script>/)[1],checks=[];
 const check=(label,condition)=>{assert.ok(condition,label);checks.push({check:label,passed:true});};
 new vm.Script(code);check('스크립트 문법',true);check('한국어 문서',html.includes('lang="ko"'));check('외부 리소스 참조 없음',!/(?:src|href)=["']https?:/i.test(html));
 const elements={},allCanvas=[],calls=[];let requested;
 const ctx={clearRect(){},beginPath(){},moveTo(){},lineTo(){},stroke(){},setLineDash(){},arc(){},fillRect(){},fillText(){},drawImage(im,...args){assert.ok(im.width>0&&im.height>0);if(args.length===8){const [x,y,w,h]=args;assert.ok(x>=0&&y>=0&&x+w<=im.width&&y+h<=im.height,`그림 영역 초과: ${args}`);}calls.push(args);}};
 function element(id=''){return {id,value:id==='speed'?'6':id==='bg'?'check':'',checked:id==='guides',style:{},textContent:'',children:[],append(a){this.children.push(a);},getContext(){return ctx;},querySelectorAll(){return this.canvases||[];},set innerHTML(v){this.markup=v;this.canvases=[element(),element()];for(const c of this.canvases){c.width=384;c.height=416;allCanvas.push(c);}}};}
 function get(id){if(!elements[id]){elements[id]=element(id);if(id.endsWith('Scene')){elements[id].width=960;elements[id].height=id==='haulScene'?660:416;allCanvas.push(elements[id]);}}return elements[id];}
 class Image{set src(url){assert.ok(url.startsWith('data:image/png;base64,'));const bytes=Buffer.from(url.split(',')[1],'base64');assert.equal(bytes.subarray(0,8).toString('hex'),'89504e470d0a1a0a');this.width=bytes.readUInt32BE(16);this.height=bytes.readUInt32BE(20);queueMicrotask(()=>this.onload());}}
 const sandbox={document:{getElementById:get,createElement:()=>element(),querySelectorAll:()=>allCanvas},Image,requestAnimationFrame:fn=>{requested=fn;},console};
 vm.createContext(sandbox);vm.runInContext(code,sandbox);await new Promise(resolve=>setImmediate(resolve));
 const read=expr=>vm.runInContext(expr,sandbox);
 check('이미지 19개 로드 완료',read('ready && Object.keys(imgs).length===19'));
 check('동작 6종 비교판 생성',get('cards').children.length===6);check('비교판 12개와 합성판 2개',allCanvas.length===14);
 const data=read('DATA');
 for(const s of data.sheets){const old=fs.readFileSync(path.join(root,s.target)),game=fs.readFileSync(path.join(dir,s.game)),native=fs.readFileSync(path.join(dir,s.native));for(const [suffix,bytes] of [['Old',old],['Game',game],['Native',native]])check(`${s.kind} ${suffix} 내장 파일 일치`,data.images[s.kind+suffix].split(',')[1]===bytes.toString('base64'));}
 const before=JSON.stringify(read('states'));get('next').onclick();check('다음 칸과 정지',read('!playing && states.run===1 && states.ignite===1'));get('prev').onclick();check('이전 칸',JSON.stringify(read('states'))===before);
 get('prev').onclick();check('이전 칸 순환',read('states.run===3 && states.ignite===5'));get('reset').onclick();check('처음으로',read('Object.values(states).every(v=>v===0)'));
 get('play').onclick();check('재생 전환',read('playing'));requested(1000);requested(1200);check('시간에 따른 칸 진행',read('states.run===1 && states.ignite===1'));
 for(let i=0;i<5;i++)read('step(1)');check('4칸·6칸 각자 순환',read('states.run===2 && states.ignite===0'));
 get('guides').checked=false;get('guides').onchange();check('발선 표시 전환',true);get('bg').value='light';get('bg').onchange();check('밝은 배경',allCanvas.every(c=>c.style.background==='#d3cbbb'));
 get('bg').value='check';get('bg').onchange();check('격자 배경 복귀',allCanvas.every(c=>c.style.background===''));get('speed').value='3';get('speed').onchange();check('속도 변경',read('clock===0'));
 const c=data.ignite_contact;check('실제 도화선 수평·수직 접점',c.flame_contact_game[0]+c.crew_draw_offset_game[0]===269&&c.flame_contact_game[1]===140);
 check('견인 36게임 픽셀 이동',data.haul.step_game===36&&data.haul.planted_world_game.join(',')==='108,108,108,0,0,0');check('모든 캔버스 원본 칸 경계 유효',calls.length>50);
 const result={passed:checks.length,checks,canvas_draw_calls:calls.length,method:'실제 HTML 스크립트를 Node VM의 DOM·Canvas 모형에서 실행했다. 내장 PNG는 헤더와 파일 바이트를 검사했다.',not_performed:['실제 브라우저 레이아웃·마우스 조작·이미지 디코더 검사','Unity 실행 / Play']};fs.writeFileSync(path.join(dir,'review_validation.json'),JSON.stringify(result,null,2));return {passed:checks.length,draws:calls.length};
}
module.exports={verify};
if(require.main===module)verify(process.argv[2]||process.cwd()).then(console.log).catch(e=>{console.error(e);process.exitCode=1;});
