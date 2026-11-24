// 실제 브라우저가 없는 환경에서 내장 재생 코드를 DOM 모형으로 검사한다.
const fs=require('fs'),vm=require('vm'),path=require('path');
const root=path.resolve(__dirname,'../../..');
const html=fs.readFileSync(path.join(root,'_HTML/codex_K-023_아군보강_v1.html'),'utf8');
const code=html.match(/<script>([\s\S]*?)<\/script>/)[1];
new vm.Script(code);
const elements=new Map(),checks=[];
function element(id){return {id,value:({fps:'6',zoom:'1',background:'#33443a',azimuth:'-45',elevation:'45',normalAsset:'0',asset:'0'})[id]||'',checked:id==='guides',style:{},children:[],disabled:false,textContent:'',classList:{toggle(){}},add(option){this.children.push(option);},append(child){this.children.push(child);},setAttribute(){},replaceChildren(){this.children=[];},getContext(){return {clearRect(){},drawImage(){},beginPath(){},moveTo(){},lineTo(){},stroke(){},putImageData(){},getImageData(){return {data:new Uint8ClampedArray(256*384*4).fill(128)};}};}};}
const document={getElementById(id){if(!elements.has(id))elements.set(id,element(id));return elements.get(id);},createElement(tag){return element(tag);},querySelectorAll(){return ['reference','animation','color','normal','lit'].map(id=>this.getElementById(id));}};
class TestImage{set src(v){this.source=v;queueMicrotask(()=>this.onload?.());}}
const context=vm.createContext({document,Image:TestImage,Option:function(label,value){this.label=label;this.value=value;},ImageData:class {constructor(data,w,h){this.data=data;this.width=w;this.height=h;}},Uint8ClampedArray,Map,Promise,Math,Number,Array,Error,window:{addEventListener(){}},requestAnimationFrame(){}});
vm.runInContext(code,context);
const flush=async()=>{for(let i=0;i<8;i++)await Promise.resolve();};
function check(name,condition){if(!condition)throw Error(name);checks.push(name);}
(async()=>{
await flush();check('선택 동작 5종',elements.get('asset').children.length===5);check('노멀 선택 3종',elements.get('normalAsset').children.length===3);
for(let i=0;i<5;i++){
 elements.get('asset').value=String(i);elements.get('asset').onchange();await flush();
 const count=i===4?3:4;
 check(`${i+1} 동작 첫 칸·미리보기`,elements.get('state').textContent.includes('1/'+count)&&elements.get('strip').children.length===count);
 check(`${i+1} 첫 칸 이전 비활성`,elements.get('prev').disabled);
 for(let f=1;f<count;f++){elements.get('next').onclick();await flush();check(`${i+1} 수동 ${f+1}칸`,elements.get('state').textContent.includes((f+1)+'/'+count));}
 check(`${i+1} 끝에서 다음 비활성`,elements.get('next').disabled);
 elements.get('play').onclick();await flush();check(`${i+1} 처음부터 재생`,elements.get('state').textContent.includes('재생 중'));
 vm.runInContext('previous=0; elapsed=0;',context);
 for(let t=200;t<=1200;t+=200)vm.runInContext(`tick(${t})`,context);
 await flush();check(`${i+1} 재생 후 마지막 정지`,elements.get('state').textContent.includes('마지막 칸 유지')&&elements.get('pause').disabled);
 elements.get('prev').onclick();await flush();check(`${i+1} 이전 이동`,elements.get('state').textContent.includes((count-1)+'/'+count));
}
elements.get('zoom').value='2';elements.get('zoom').onchange();await flush();check('2배 표시',elements.get('animation').style.width==='512px');
elements.get('background').value='#101015';elements.get('background').onchange();await flush();check('배경 변경',elements.get('animation').style.backgroundColor==='#101015');
for(let i=0;i<3;i++){elements.get('normalAsset').value=String(i);elements.get('normalAsset').oninput();await flush();elements.get('flat').checked=true;elements.get('flat').oninput();await flush();check(`${i+1} 노멀·평면 비교 실행`,true);}
fs.writeFileSync(path.join(__dirname,'qa_runtime.json'),JSON.stringify({status:'통과',environment:'Node VM·DOM 모형, 실제 브라우저 아님',count:checks.length,checks},null,2));
console.log(`재생 코드 검사 ${checks.length}개 통과 / 실제 브라우저 아님`);
})().catch(e=>{console.error(e);process.exitCode=1;});
