import fs from 'node:fs/promises';
import vm from 'node:vm';
import path from 'node:path';
import crypto from 'node:crypto';
// 실제 브라우저 대신 문법·좌표·시간 경계·조작 상태를 검사한다.
export async function verify(folder,htmlFile){
 const html=await fs.readFile(htmlFile,'utf8'),meta=JSON.parse(await fs.readFile(path.join(folder,'manifest.json'),'utf8'));
 const checks=[];function check(name,condition){if(!condition)throw Error(name);checks.push(name);}
 check('외부 그림·스크립트·스타일 없음',!/(?:src|href)\s*=\s*["'](?:https?:|\/\/)/i.test(html));
 check('템플릿 자리표시자 없음',!/__DATA__|__META__/.test(html));
 const code=html.match(/<script>([\s\S]*?)<\/script>/)[1];new vm.Script(code);check('자바스크립트 문법',true);
 const elements={},calls=[],raf=[];
 const noop=()=>{};const g=new Proxy({drawImage(...a){if(a.length===9){let [im,sx,sy,sw,sh,dx,dy,dw,dh]=a;if(sx<0||sy<0||sw<=0||sh<=0||sx+sw>im.width+.01||sy+sh>im.height+.01||dw<=0||dh<=0)throw Error('그림 좌표 초과');}calls.push(a);}}, {get(o,k){return k in o?o[k]:noop;},set(o,k,v){o[k]=v;return true;}});
 function el(id){return elements[id]??=( {id,value:({zoom:'1',bg:'grid',aim:'1',fps:'6',timeline:'0'})[id]??'',checked:['guides','follow'].includes(id),width:1400,height:420,style:{},getContext:()=>g,textContent:''});}
 const doc={getElementById:el,querySelectorAll:s=>s==='canvas'?['compare','haul','ignite'].map(el):[]};
 class MockImage {set src(v){let b=Buffer.from(v.split(',')[1],'base64');this.width=b.readUInt32BE(16);this.height=b.readUInt32BE(20);queueMicrotask(()=>this.onload?.());}}
 const ctx=vm.createContext({document:doc,Image:MockImage,requestAnimationFrame:f=>raf.push(f),Math,Promise,console});vm.runInContext(code,ctx);await new Promise(r=>setImmediate(r));
 const run=s=>vm.runInContext(s,ctx);check('내장 이미지 아홉 장 읽기',run('Object.keys(imgs).length')===9);check('초기 합성',calls.length>0&&el('load').textContent==='그림 준비 완료');
 check('접촉 직전',run('seq(.499).lit')===false);check('0.5초 점화',run('seq(.5).lit')===true);check('1.499초 발사 전',run('seq(1.499).smoke')===-1);check('1.5초 발사',run('seq(1.5).smoke')===0);check('점화부터 발사까지 1초',meta.ignite.fireSeconds-meta.ignite.contactSeconds===1);
 for(let f=0;f<6;f++)check(`연기 ${f+1}칸`,run(`seq(${1.501+f*.1}).smoke`)===f);
 check('2.1초 연기 종료',run('seq(2.1).smoke')===-1);check('3.2초 반복',Math.abs(run('seq(3.2).t'))<1e-10);
 for(let key of ['crew_pull6','crew_push6']){let a=meta.crew[key];for(let start of [0,3]){let world=a.frames.slice(start,start+3).map((q,j)=>q.stanceFoot[0]-(start+j)*40);check(`${key} ${start===0?'왼발':'오른발'} 지면 고정`,new Set(world).size===1);}check(`${key} 210px`,a.frames.every(q=>q.placed[3]===210));check(`${key} 칸 안쪽`,a.frames.every(q=>q.placed[0]>0&&q.placed[0]+q.placed[2]<256));}
 for(let f=0;f<12;f++){el('next').onclick();check(`수동 전진 ${f+1}`,run('stepNo')===f+1);}
 check('6→1 순환 표시',el('walkState').textContent.startsWith('1/6'));el('prev').onclick();check('이전 칸',run('stepNo')===11);el('restart').onclick();check('처음으로',run('stepNo')===0&&!run('playing'));
 el('play').onclick();check('재생',run('playing'));run('tick(0);tick(200);tick(400)');check('자동 진행',run('stepNo')>0);el('play').onclick();check('정지',!run('playing'));
 el('timeline').value='1500';el('timeline').oninput();check('발사 시간 직접 이동',run('fireTime')===1.5&&!run('firePlaying'));el('firePlay').onclick();check('점화 재생',run('firePlaying'));el('fireStart').onclick();check('점화 초기화',run('fireTime')===0&&!run('firePlaying'));
 el('zoom').value='2';el('zoom').onchange();check('2배 표시',el('haul').style.width==='2800px');el('bg').value='#0c1514';el('bg').onchange();check('밤 배경',el('haul').style.backgroundColor==='#0c1514');el('follow').checked=false;el('follow').onchange();check('고정 시점 합성',true);el('guides').checked=false;el('guides').onchange();check('접지선 끄기',true);for(let i=0;i<3;i++){el('aim').value=String(i);el('aim').onchange();check(`설치 ${i+1}방향`,true);}
 const embedded=run('data');for(let [key,uri] of Object.entries(embedded)){const b=Buffer.from(uri.split(',')[1],'base64'),f=await fs.readFile(path.join(folder,key+'.png'));check(`${key} 내장 원본 일치`,b.equals(f));}
 const result={kind:'Node VM의 DOM·캔버스 모형 검사, 실제 브라우저 아님',passed:checks.length,checks,htmlSha256:crypto.createHash('sha256').update(html).digest('hex')};await fs.writeFile(path.join(folder,'qa_html.json'),JSON.stringify(result,null,2));return result;
}
