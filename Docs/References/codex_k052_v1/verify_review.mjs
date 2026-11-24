import fs from 'node:fs/promises';
import vm from 'node:vm';
import path from 'node:path';
export async function verify(root){
 const dir=path.join(root,'Docs/References/codex_k052_v1');
 const html=await fs.readFile(path.join(root,'_HTML/4_그림_캐릭터/codex_K-052_대포끌기_3차_v1.html'),'utf8');
 const code=html.match(/<script>([\s\S]*?)<\/script>/)[1];
 const checks=[];const check=(name,pass)=>{checks.push({name,pass:!!pass});if(!pass)throw Error(name)};
 let calls=0,queued;const drawn=new Set();
 const context={fillRect(){},fillText(){},beginPath(){},moveTo(){},lineTo(){},stroke(){},strokeRect(){},save(){},restore(){},translate(){},rotate(){},ellipse(){},fill(){},drawImage(im,...args){calls++;if(!im?.width)throw Error('그림 누락');if(args.length===8){const [x,y,w,h]=args;if(x<0||y<0||x+w>im.width||y+h>im.height)throw Error('원본 칸 경계 초과')}}};
 const els={};for(const [,id]of html.matchAll(/id="([^"]+)"/g))els[id]={value:'',checked:false,textContent:'',innerHTML:'',width:1350,height:410,getContext:()=>{drawn.add(id);return context}};
 for(const [id,value]of Object.entries({bg:'#23332c',scale:'0.7',fps:'6',camera:'follow',time:'.5'}))els[id].value=value;
 els.guides.checked=true;
 class MockImage{set src(s){this._src=s;const b=Buffer.from(s.split(',')[1],'base64');if(b.toString('hex',0,8)!=='89504e470d0a1a0a')throw Error('PNG 아님');this.width=b.readUInt32BE(16);this.height=b.readUInt32BE(20);queueMicrotask(()=>this.onload?.())}get src(){return this._src}}
 const sandbox={document:{getElementById:id=>els[id],body:{insertAdjacentHTML(){throw Error('그림 로드 오류')}}},Image:MockImage,requestAnimationFrame:fn=>{queued=fn}};
 vm.createContext(sandbox);const script=new vm.Script(code);script.runInContext(sandbox);await new Promise(resolve=>setImmediate(resolve));
 const get=s=>vm.runInContext(s,sandbox);
 check('스크립트 문법과 초기 내장 그림 로드',get('Object.keys(images).length')===13);
 check('서버 없는 내장 PNG 13개',get('Object.values(DATA.images).every(s=>s.startsWith("data:image/png;base64,"))'));
 check('팔레트 견본 생성',els.cannonSource.innerHTML.includes('#0C0A10')&&els.crewSource.innerHTML.includes('#161B2E'));
 check('외곽선 비교표 생성',els.outlineTable.innerHTML.includes('중앙값'));
 check('비교·걷기·점화·전후 초기 그리기',['comparison','faces','walking','ignition','beforeAfter'].every(id=>drawn.has(id))&&calls>=18);
 check('이전 칸 1→6 순환',(()=>{els.prev.onclick();return get('walkFrame')===5&&get('walkStep')===5})());
 check('다음 칸 6→1 순환',(()=>{els.next.onclick();return get('walkFrame')===0&&get('walkStep')===6})());
 check('칸 이동 시 재생 정지',get('walkPlaying')===false);
 for(let i=0;i<6;i++){els.next.onclick();check(`보행 ${i+1}칸 이동`,get('walkFrame')===(i+1)%6)}
 els.walkPlay.onclick();check('보행 재생 전환',get('walkPlaying')===true);
 queued(2000);check('자동 보행 다음 칸',get('walkFrame')===1);
 els.walkPlay.onclick();check('보행 정지 전환',get('walkPlaying')===false);
 for(const [t,f]of [[0,0],[.166,0],[1/6,1],[1/3,2],[.499,2],[.5,3],[.749,3],[.75,4],[1,5],[1.5,5]])check(`점화 ${t}초의 칸`,get(`ignitionFrame(${t})`)===f);
 for(const [t,label]of [[.49,'접촉 전'],[.5,'점화 완료'],[1.49,'점화 완료'],[1.5,'발사']]){els.time.value=String(t);els.time.oninput();check(`점화 ${t}초의 상태`,els.igniteReadout.textContent.includes(label))}
 els.time.value='.5';els.time.oninput();els.igniteNext.onclick();check('점화 다음 칸',get('igniteTime')===.75);els.ignitePrev.onclick();check('점화 이전 칸',get('igniteTime')===.5);
 els.ignitePlay.onclick();check('점화 재생',get('ignitePlaying'));queued(2100);check('점화 자동 진행',get('igniteTime')>.5);els.ignitePlay.onclick();check('점화 정지',!get('ignitePlaying'));
 for(const scale of ['1','0.7']){els.scale.value=scale;els.scale.onchange();check(`비교 배율 ${scale}`,true)}
 for(const bg of ['#888478','#11151b','#23332c']){els.bg.value=bg;els.bg.onchange();check(`배경 ${bg}`,true)}
 els.camera.value='fixed';els.camera.onchange();check('고정 시점 그리기',true);els.guides.checked=false;els.guides.onchange();check('접지 표시 끄기',!els.guides.checked);
 check('불씨 접점 좌표 일치',get('420+269-DATA.ignite.frames[3].hotX+DATA.ignite.frames[3].hotX')===689&&get('DATA.ignite.frames[3].hotY')===132);
 check('접촉→발사 간격 1초',1.5-.5===1);
 check('6칸 바퀴 회전은 192/50.5 라디안',get('32*6/50.5')===192/50.5);
 const report={kind:'Node VM 캔버스·DOM 모형 검사. 실제 브라우저 렌더·실재생 검사가 아님.',passed:checks.length,failed:0,drawCalls:calls,checks};
 await fs.writeFile(path.join(dir,'qa_html.json'),JSON.stringify(report,null,2));return report;
}
