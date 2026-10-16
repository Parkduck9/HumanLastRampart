const fs=require('fs'),vm=require('vm'),path=require('path');
const elements={};
function element(){return {style:{setProperty(){}},attrs:{},hidden:false,children:[],setAttribute(k,v){this.attrs[k]=v},appendChild(n){this.children.push(n)}};}
const listeners={};let frame;
const context={document:{getElementById(id){return elements[id]??=element()},createElement:element,documentElement:element()},window:{addEventListener(k,fn){listeners[k]=fn}},requestAnimationFrame(fn){frame=fn}};
vm.createContext(context);
vm.runInContext(fs.readFileSync(path.join(__dirname,'../qa/codex_page_script.js'),'utf8'),context);
let count=0;function check(v,name){if(!v)throw Error(name);count++}
check(elements.yard.style.backgroundImage.includes('data:image/png;base64,'),'낮 배경');
elements.evening.onclick();check(elements.evening.attrs['aria-pressed']===true,'저녁 선택');
check(elements.places.children.length===4,'장소 네 개');
elements.places.children[3].onclick();check(elements.feedback.textContent.includes('다음 전투'),'계단 상호작용');
elements.defeat.onclick();check(elements.ribbon.alt==='패배','패배 띠');
elements.confirm.onclick();check(elements.resultFeedback.textContent.includes('화면 전환·저장은 실행하지 않습니다'),'확인 예시');
listeners.keydown({target:{matches:()=>false},key:'a',preventDefault(){}});frame(50);check(parseFloat(elements.hero.style.left)<85,'A 이동');
listeners.keyup({key:'a'});listeners.blur();elements.victory.onclick();check(elements.ribbon.alt==='승리','승리 복귀');
console.log(JSON.stringify({검사수:count,결과:'모의 DOM 통과',주의:'실제 브라우저 렌더 검사는 아님'}));
