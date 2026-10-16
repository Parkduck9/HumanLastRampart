// 실제 브라우저 대신 최소 화면 모형에서 선택과 재생 로직을 확인한다.
const fs=require('fs'),vm=require('vm'),path=require('path');
const html=fs.readFileSync(path.resolve(__dirname,'../../../_HTML/4_그림_캐릭터/codex_K-056_3등신시험_v1.html'),'utf8');
const code=html.match(/<script>([\s\S]*?)<\/script>/)[1];
const elements={};let callback;const passed=[];
function element(id){return elements[id]??=( {value:id==='person'?'archer':id==='fps'?'6':'',checked:true,width:960,height:540,style:{},children:[],textContent:'',replaceChildren(){this.children=[];this.value=''},append(...a){this.children.push(...a)},add(o){this.children.push(o);if(!this.value)this.value=o.value},getContext(){return {drawImage(){},fillRect(){},fillText(){},strokeRect(){},beginPath(){},moveTo(){},lineTo(){},stroke(){},setLineDash(){},getImageData(x,y,w,h){const p=new Uint8ClampedArray(w*h*4);for(let y=1;y<h-1;y++)for(let x=1;x<w-1;x++)p[(y*w+x)*4+3]=255;return {data:p}}}}} );}
class Image{set src(v){this._src=v;const b=Buffer.from(v.split(',')[1],'base64');this.width=b.readUInt32BE(16);this.height=b.readUInt32BE(20);queueMicrotask(()=>this.onload())}get src(){return this._src}}
const context={document:{getElementById:element,createElement:()=>element('anon'+Math.random())},Image,Option:class{constructor(text,value){this.text=text;this.value=value}},requestAnimationFrame:f=>callback=f,window:{},console,Uint8ClampedArray,Map,WeakMap,Promise};
function check(name,v){if(!v)throw Error(name);passed.push(name)}
vm.runInNewContext(code,context);
setImmediate(()=>{
 check('내장 이미지 19장 로드',context.window.reviewState.loaded===19);
 elements.action.value='walk';elements.action.onchange();check('걷기 4칸 표시',elements.frames.children.length===4);
 callback(1000);check('자동 진행',context.window.reviewState.frame===1);
 elements.play.onclick();callback(2000);check('정지 유지',context.window.reviewState.frame===1);
 elements.next.onclick();check('다음 칸',context.window.reviewState.frame===2);
 elements.prev.onclick();check('이전 칸',context.window.reviewState.frame===1);
 elements.person.value='hero';elements.person.onchange();elements.action.value='run';elements.action.onchange();check('주인공 달리기 4칸',elements.frames.children.length===4);
 elements.prev.onclick();check('첫 칸에서 마지막 칸',context.window.reviewState.frame===3);
 elements.next.onclick();check('마지막 칸에서 첫 칸',context.window.reviewState.frame===0);
 elements.play.onclick();callback(3000);check('재생 재개',context.window.reviewState.frame===1);
 elements.action.value='front';elements.action.onchange();check('정면 서기 한 칸',elements.frames.children.length===1);
 elements.lines.checked=false;elements.lines.onchange();check('비율선 전환',context.window.reviewState.loaded===19);
 check('외부 이미지나 외부 스크립트 없음',!/<(?:img|script)[^>]+src=["']https?:/i.test(html));
 fs.writeFileSync(path.join(__dirname,'qa_review_logic.json'),JSON.stringify({검증:'Node 화면 모형. 실제 브라우저 검증 아님',통과:passed.length,검사:passed},null,2));
 console.log(passed.length+'개 검토 페이지 로직 검사 통과');
});
