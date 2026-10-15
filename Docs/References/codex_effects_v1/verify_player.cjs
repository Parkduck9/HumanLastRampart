const fs=require('fs'),vm=require('vm');
const source=fs.readFileSync(__dirname+'/preview.html','utf8').match(/<script>([\s\S]*?)<\/script>/)[1];
let clock=0,nextTick=null;const pending=[];const elements={};
function el(id){return elements[id]??={value:id==='fps'?'10':'0',textContent:'',append(){},classList:{set:new Set(),toggle(n){if(this.set.has(n)){this.set.delete(n);return false;}this.set.add(n);return true;}}};}
const canvas={getContext(){return {clearRect(){},drawImage(){},imageSmoothingEnabled:false};}};
class FakeImage{constructor(){this.complete=false;}set src(v){this.complete=true;pending.push(()=>this.onload?.());}}
const context={Image:FakeImage,document:{getElementById:el,querySelector(){return canvas;},createElement(){return {};}},performance:{now(){return clock;}},requestAnimationFrame(fn){nextTick=fn;},console};
vm.createContext(context);vm.runInContext(source,context);pending.forEach(fn=>fn());
let checks=[];function test(name,value){if(!value)throw Error(name);checks.push({name,passed:true});}
function step(ms){clock+=ms;const fn=nextTick;fn(clock);}
const before=el('readout').textContent;step(110);step(110);test('재생 시 프레임 자동 진행',el('readout').textContent!==before);
el('play').onclick();const paused=el('readout').textContent;step(240);test('정지 시 프레임 유지',el('readout').textContent===paused);
el('next').onclick();test('다음 칸 수동 이동',el('readout').textContent!==paused);
el('prev').onclick();test('이전 칸 되돌리기',el('readout').textContent===paused);
el('play').onclick();step(110);test('재생 재개',el('readout').textContent.includes('재생 중'));
for(let i=0;i<6;i++){el('effect').value=String(i);el('effect').onchange();test('효과'+i+' 전환 시 첫칸',el('readout').textContent.includes('프레임 1/6'));}
el('once').onclick();for(let i=0;i<10;i++)step(110);test('한번 재생 마지막칸 정지',el('readout').textContent.includes('프레임 6/6')&&el('readout').textContent.includes('정지'));
el('fps').value='8';el('fps').onchange();test('속도 표시 갱신',el('readout').textContent.includes('8fps'));
el('background').onclick();test('밝은 배경 전환',el('stage').classList.set.has('light'));
el('scale').onclick();test('확대 전환',el('stage').classList.set.has('big'));
console.log(JSON.stringify({result:'passed',checks}));