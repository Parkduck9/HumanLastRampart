const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
async function main(){
 const root=path.resolve(__dirname,'../../..'),page=path.join(root,'_HTML/4_그림_캐릭터/codex_K-086_대포불붙이기다시_v1.html');
 const html=fs.readFileSync(page,'utf8'),script=html.match(/<script>([\s\S]*?)<\/script>/)[1],checks=[];
 const check=(name,ok)=>{assert.ok(ok,name);checks.push({check:name,passed:true});};
 check('한국어 문서',html.includes('lang="ko"'));check('외부 이미지 주소 없음',!/(?:src|href)=["']https?:/i.test(html));
 const elements={},draws=[];let callback;
 const ctx={clearRect(){},strokeRect(){},beginPath(){},moveTo(){},lineTo(){},stroke(){},drawImage(im,sx,sy,sw,sh,...dst){assert.ok(sx>=0&&sy>=0&&sx+sw<=im.width&&sy+sh<=im.height);draws.push({sx,sy,sw,sh,dst});}};
 const get=id=>elements[id]||(elements[id]={width:288,height:160,checked:true,value:'4',getContext:()=>ctx,textContent:''});
 class Image{set src(url){check('내장 PNG 주소',url.startsWith('data:image/png;base64,'));const bytes=Buffer.from(url.split(',')[1],'base64');assert.equal(bytes.subarray(0,8).toString('hex'),'89504e470d0a1a0a');this.width=bytes.readUInt32BE(16);this.height=bytes.readUInt32BE(20);queueMicrotask(()=>this.onload());}}
 const context={document:{getElementById:get},Image,requestAnimationFrame:fn=>{callback=fn;}};vm.createContext(context);vm.runInContext(script,context);await new Promise(r=>setImmediate(r));
 const read=s=>vm.runInContext(s,context),data=read('DATA');
 check('내장 이미지 3개 준비',read('ready && Object.keys(imgs).length===3'));
 for(const [key,p] of Object.entries({proof:'Docs/References/codex_k086_v1/previews/ignite_back_proof.png',composite:'Docs/References/codex_k086_v1/previews/composite6.png',old:'Game/Assets/_Project/Art/Characters/Fire/cannon_crew_ignite6.png'}))check(key+' 원본 바이트 일치',data.images[key].split(',')[1]===fs.readFileSync(path.join(root,p)).toString('base64'));
 for(let i=1;i<=6;i++){get('next').onclick();check('다음 칸 '+i,read('frame')===i%6&&!read('playing'));}
 get('prev').onclick();check('이전 칸 순환',read('frame')===5);get('reset').onclick();check('처음으로',read('frame')===0);
 get('play').onclick();check('재생',read('playing'));callback(10);callback(500);check('시간에 따라 칸 전환',read('frame')===1);get('play').onclick();callback(1000);check('정지 유지',read('frame')===1);
 get('guides').checked=false;get('guides').onchange();check('안내선 끄기',read('frame')===1);get('speed').value='8';get('speed').oninput();check('속도 변경',get('rate').textContent==='8');
 check('합성 펼침 이미지 지정',get('strip').src===data.images.composite);check('교체 준비 완료로 표시하지 않음',html.includes('최종 교체 시트 아님'));
 fs.writeFileSync(path.join(__dirname,'review_validation.json'),JSON.stringify({checks,draw_calls:draws.length,method:'Node VM의 DOM·이미지 모형 검사. 실제 브라우저 디코딩·배치 검사는 미수행.'},null,2));
 console.log(JSON.stringify({checks:checks.length,draw_calls:draws.length,passed:true}));
}main().catch(e=>{console.error(e);process.exitCode=1;});
