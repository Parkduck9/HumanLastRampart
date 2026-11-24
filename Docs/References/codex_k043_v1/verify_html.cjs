const fs=require('fs'),vm=require('vm'),assert=require('assert'),path=require('path');
const html=fs.readFileSync(path.join(__dirname,'../../../_HTML/4_그림_캐릭터/codex_K-043_대포병분리_불붙이기_v1.html'),'utf8');
const code=html.match(/<script>([\s\S]*?)<\/script>/)[1];new vm.Script(code);
const checks=[];function check(name,fn){fn();checks.push({name,result:'통과'});}
let calls=0,raf;
const context=new Proxy({},{get:(o,k)=>o[k]||(()=>{calls++}),set:(o,k,v)=>(o[k]=v,true)});
const elements={};
for(const id of [...html.matchAll(/id="([^"]+)"/g)].map(m=>m[1]))elements[id]={checked:['guides','roll'].includes(id),value:id==='bg'?'grid':0,style:{},getContext:()=>context,width:1024,height:400};
const images=[...html.matchAll(/data-asset="([^"]+)"/g)].map(m=>({dataset:{asset:m[1]}}));
class ImageMock{set src(s){assert(s.startsWith('data:image/png;base64,'));this.onload();}}
const sandbox={document:{getElementById:id=>{assert(elements[id],id);return elements[id];},querySelectorAll:s=>s==='canvas'?['haul','ignite','compare','anchors'].map(id=>elements[id]):images},Image:ImageMock,requestAnimationFrame:fn=>{raf=fn;},console};vm.createContext(sandbox);vm.runInContext(code,sandbox);
(async()=>{await new Promise(setImmediate);const run=s=>vm.runInContext(s,sandbox);
check('그림 6종 내장 읽기 완료',()=>assert.equal(elements.status.textContent,'그림 준비 완료'));
check('외부 리소스 경로 없음',()=>assert(!/(?:src|href)="(?:https?:|\.\.\/)/.test(html)));
check('초기 캔버스 그리기',()=>{raf(0);assert(calls>20);});
check('점화 전에는 불씨·연기 없음',()=>assert(run('!sequence(.499).lit && sequence(.499).smoke===-1')));
check('0.5초 접촉·점화',()=>assert(run('sequence(.5).lit && sequence(.5).frame===3')));
check('점화 후 0.999초까지 발사 없음',()=>assert(run('!sequence(1.499).fired && sequence(1.499).smoke===-1')));
check('점화 정확히 1초 뒤 발사·첫 연기',()=>assert(run('sequence(1.5).fired && sequence(1.5).smoke===0')));
check('연기 여섯째 칸',()=>assert.equal(run('sequence(2.05).smoke'),5));
check('연기 종료와 반복',()=>assert(run('sequence(2.11).smoke===-1 && !sequence(3.2).fired')));
check('정지 버튼',()=>{elements.play.onclick();assert.equal(run('playing'),false);});
check('이전·다음 여섯 칸 순환',()=>{elements.restart.onclick();elements.prev.onclick();assert.equal(run('Math.floor(haulTime*6)%6'),5);elements.next.onclick();assert.equal(run('Math.floor(haulTime*6)%6'),0);});
check('시간 직접 이동',()=>{elements.timeline.value=1500;elements.timeline.oninput();assert.equal(run('sequence(time).smoke'),0);assert.equal(run('playing'),false);});
check('처음부터·자동 시간 진행',()=>{elements.restart.onclick();raf(0);raf(100);assert(run('time>0 && haulTime>0'));});
check('배경·기준·이동·바퀴 전환',()=>{elements.bg.value='#bdbaa8';elements.bg.onchange();for(let id of ['guides','travel','roll']){elements[id].checked=!elements[id].checked;elements[id].onchange();}assert(calls>100);});
check('원본 시트 표시 내장',()=>assert(images.every(x=>x.src.startsWith('data:image/png;base64,'))));
fs.writeFileSync(path.join(__dirname,'qa_html.json'),JSON.stringify({method:'Node VM과 DOM·캔버스 모형. 실제 브라우저 렌더링 시험 아님.',checks,canvasCalls:calls},null,2));console.log(`${checks.length}개 검사 통과`);
})().catch(e=>{console.error(e);process.exitCode=1;});
