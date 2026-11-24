// 실제 브라우저 대신 작은 DOM 모형에서 완성 HTML의 스크립트를 실행한다.
// Node.js 실행 예: node ./Docs/References/codex_k026_v1/source/verify-ui.mjs
import {readFile, writeFile} from 'node:fs/promises';
import vm from 'node:vm';
const root=new URL('../',import.meta.url);
const html=await readFile(new URL('../../../../_HTML/codex_K-026_UI아이콘_v1.html',import.meta.url),'utf8');
let checks=0;
function check(value,message){if(!value)throw Error(message);checks++;}
class Element{
 constructor(tag){this.tag=tag;this.children=[];this.dataset={};this.attributes={};this.className='';this.textContent='';this.classList={toggle:(name,on)=>{const set=new Set(this.className.split(' ').filter(Boolean));if(on)set.add(name);else set.delete(name);this.className=[...set].join(' ');}};}
 append(...nodes){this.children.push(...nodes);}
 replaceChildren(...nodes){this.children=[...nodes];}
 setAttribute(name,value){this.attributes[name]=value;}
 querySelector(tag){return this.all().find(e=>e.tag===tag);}
 all(){return this.children.flatMap(e=>e instanceof Element?[e,...e.all()]:[]);}
}
const ids=Object.fromEntries([...html.matchAll(/id="([^"]+)"/g)].map(m=>[m[1],new Element('div')]));
const document={body:new Element('body'),getElementById:id=>ids[id],createElement:tag=>new Element(tag),createTextNode:text=>String(text),querySelectorAll:cls=>ids.catalog.all().filter(e=>e.className.split(' ').includes(cls.slice(1)))};
const script=html.match(/<script>([\s\S]*?)<\/script>/)[1];
vm.runInNewContext(script,{document},{timeout:1000});
const data=JSON.parse(script.match(/const icons=(.*?);\r?\nconst groups=/s)[1]);
const cards=document.querySelectorAll('.card');
check(cards.length===22,'목록이 22종이어야 합니다.');
check(ids.catalog.children.length===4,'범주가 네 개여야 합니다.');
for(const icon of data){
 const card=cards.find(c=>c.dataset.id===icon.id);card.onclick();
 check(ids.name.textContent===icon.name,'선택 이름 동기화 실패');
 check(cards.filter(c=>c.attributes['aria-pressed']==='true').length===1,'선택 표시 중복');
 for(const size of [24,32,48]){
  ids.size.onchange({target:{value:String(size)}});
  check(card.querySelector('img').src===icon.images[size],'목록 크기 이미지 불일치');
  check(card.querySelector('img').width===size,'목록 표시 크기 불일치');
  check(ids.large.width===size*4,'확대 보기 크기 불일치');
  check(ids.download.href===icon.images[size],'저장 링크 PNG 불일치');
  check(ids.download.download===icon.id+'_'+size+'.png','저장 파일 이름 불일치');
 }
 check(ids.sizes.children.map(e=>e.children[0].width).join(',')==='24,32,48','실제 크기 비교 불일치');
}
for(const bg of ['panel','dark','light','checker']){
 ids.bg.onchange({target:{value:bg}});
 check(document.body.dataset.bg===bg,'배경 선택 불일치');
 check(document.body.className.includes('checker')===(bg==='checker'),'격자 배경 상태 불일치');
}
ids.scale.onclick();check(cards.every(c=>c.querySelector('img').width===96),'목록 2배 확대 실패');
check(ids.scale.attributes['aria-pressed']==='true','확대 선택 상태 실패');
ids.scale.onclick();check(cards.every(c=>c.querySelector('img').width===48),'원래 크기 복원 실패');
check(ids.hud.children.length===6,'상태판 예시 여섯 항목 불일치');
const result={checks,passed:true,method:'완성 HTML 스크립트를 DOM 모형에서 실행',limitations:'실제 브라우저 렌더링·다운로드·CSS 배치·반응형·접근성 검증은 포함하지 않음'};
await writeFile(new URL('qa-ui.json',root),JSON.stringify(result,null,2),'utf8');
console.log(JSON.stringify(result));
