const fs=require('fs'),vm=require('vm'),path=require('path');
const root=__dirname;
const file=path.resolve(root,'../../../_HTML/codex_K-029_함정지도_v1.html');
const html=fs.readFileSync(file,'utf8'),script=html.match(/<script>([\s\S]*?)<\/script>/)[1];
new vm.Script(script);
const context=new Proxy({},{get:(obj,k)=>obj[k]??(()=>{})});
const nodes={};
function node(id){return nodes[id]??=( {value:id==='tileSelect'?'grass_a':id==='back'?'#4a5738':'1',checked:true,children:[],style:{},append(...n){this.children.push(...n)},getContext(){return context},getBoundingClientRect(){return {left:0,top:0,width:1024,height:768}}});}
const sandbox={document:{getElementById:node,createElement:()=>({append(){}})},Image:class{set src(v){this._src=v;if(this.onload)this.onload()}},console};
vm.createContext(sandbox);vm.runInContext(script,sandbox);
setImmediate(()=>{
 let count=0;const check=(s,c)=>{if(!c)throw Error(s);count++};
 check('로드',nodes.status.textContent.includes('94장 준비'));
 check('격자와 표시 모음',nodes.states.children.length===7);
 check('타일 선택',nodes.tileSelect.children.length===24);
 const before=vm.runInContext('JSON.stringify(cells)',sandbox);nodes.new.onclick();check('새 조합',before!==vm.runInContext('JSON.stringify(cells)',sandbox));
 nodes.map.onclick({clientX:1,clientY:1});check('클릭 칸',vm.runInContext('chosen.join()',sandbox)==='0,0');
 nodes.map.onkeydown({key:'ArrowLeft',preventDefault(){}});check('왼쪽 범위',vm.runInContext('chosen.join()',sandbox)==='0,0');
 nodes.map.onkeydown({key:'ArrowRight',preventDefault(){}});check('방향키',vm.runInContext('chosen.join()',sandbox)==='1,0');
 nodes.zoom.value='2';nodes.zoom.onchange();check('확대',nodes.map.style.width==='2048px');
 nodes.grid.checked=false;nodes.grid.onchange();nodes.icons.checked=false;nodes.icons.onchange();check('겉칠 표시 전환',true);
 nodes.tileSelect.value='path_15';nodes.tileSelect.onchange();nodes.back.value='#ded7c3';nodes.back.onchange();check('길 반복과 배경',true);
 fs.writeFileSync(path.join(root,'qa/html_model.json'),JSON.stringify({passed:count,syntax:true,scope:'최소 DOM 모형 확인이며 실제 브라우저 렌더링 아님'},null,2));console.log(count+'개 DOM 모형 검사 통과');
});
