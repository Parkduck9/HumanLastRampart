const fs = require('fs'), vm = require('vm'), assert = require('assert'), path = require('path');
const root = path.resolve(__dirname, '../../..');
const html = fs.readFileSync(path.join(root, '_HTML/6_그림_화면/codex_K-090_분대명령원형_v1.html'), 'utf8');
const script = html.match(/<script>([\s\S]*)<\/script>/)[1];
new vm.Script(script);
const elements = {};
const ctx2d = new Proxy({}, {get(o,k) {return k in o ? o[k] : () => {}}, set(o,k,v) {o[k]=v;return true}});
function el(id) {return elements[id] ||= {id,style:{},dataset:{},width:id==='direction'?222:360,height:360,clientWidth:1920,textContent:'',children:[],setAttribute(k,v){this[k]=String(v)},append(e){this.children.push(e)},setPointerCapture(){},getBoundingClientRect(){return {left:parseFloat(this.style.left)||0,top:parseFloat(this.style.top)||0,width:this.id==='stage'?1920:this.width,height:this.id==='stage'?1080:this.height}},getContext(){return ctx2d},set innerHTML(v){this.firstChild={src:'',alt:''}},get innerHTML(){return ''}}}
const document={getElementById:el,createElement:()=>el('new'+Math.random()),querySelectorAll:()=>[],addEventListener(k,f){this[k]=f},documentElement:{dataset:{}}};
class Image {set src(v){this.value=v;const bytes=Buffer.from(v.split(',')[1],'base64');this.width=bytes.readUInt32BE(16);this.height=bytes.readUInt32BE(20);queueMicrotask(()=>this.onload())}}
const sandbox={document,Image,ResizeObserver:class {constructor(fn){this.fn=fn}observe(){this.fn()}},setInterval(){},console,Math,Promise};
for(const id of ['animate','disabled','oldMenu','flowA','flowB','reset','mockup','shotCaption','shots'])sandbox[id]=el(id);
vm.createContext(sandbox);vm.runInContext(script,sandbox);
(async()=>{await vm.runInContext('ready',sandbox);await new Promise(setImmediate);
assert(!document.documentElement.dataset.imageError);assert.equal(Number(document.documentElement.dataset.imagesLoaded),67);
assert.equal(el('oldMenu').children.length,3);assert.equal(el('shots').children.length,6);
el('flowB').onclick();assert.equal(vm.runInContext('mode',sandbox),'amount');
const pop=el('pop'), r=pop.getBoundingClientRect();pop.onclick({clientX:r.left+180,clientY:r.top+65,stopPropagation(){}});
assert.equal(vm.runInContext('mode',sandbox),'direction');assert(el('status').textContent.includes('1분대 보내기'));
let q=pop.getBoundingClientRect();pop.onclick({clientX:q.left+190,clientY:q.top+111,stopPropagation(){}});
assert.equal(vm.runInContext('mode',sandbox),'');assert(el('status').textContent.includes('동쪽 보내기'));
el('flowA').onclick();q=pop.getBoundingClientRect();pop.onclick({clientX:q.left+111,clientY:q.top+111,stopPropagation(){}});assert.equal(vm.runInContext('mode',sandbox),'direction');
el('disabled').onclick();pop.onclick({clientX:q.left+111,clientY:q.top+40,stopPropagation(){}});assert.equal(vm.runInContext('mode',sandbox),'direction');el('disabled').onclick();
const stage=el('stage');stage.onpointerdown({button:0,target:stage,shiftKey:true,clientX:500,clientY:250,pointerId:1});stage.onpointermove({clientX:800,clientY:350});stage.onpointerup({button:0,target:stage,shiftKey:true,clientX:800,clientY:350});assert.equal(vm.runInContext('mode',sandbox),'direction');assert.equal(vm.runInContext('anchor[0]',sandbox),800);assert.equal(vm.runInContext('anchor[1]',sandbox),350);
document.keydown({key:'Escape'});assert.equal(vm.runInContext('mode',sandbox),'');
vm.runInContext("open('amount',[1919,1079])",sandbox);assert.equal(pop.style.left,'1560px');assert.equal(pop.style.top,'720px');
const out={script_syntax:true,embedded_images_requested:Number(document.documentElement.dataset.imagesLoaded),old_menu_rows:3,mockup_choices:6,amount_then_direction:true,north_east_mapping:true,hub_ignored:true,disabled_input_ignored:true,shift_release_at_cursor:true,escape_closes:true,edge_clamp:true,method:'Node VM with DOM/canvas stubs; not a browser render'};
fs.writeFileSync(path.join(__dirname,'review_verification.json'),JSON.stringify(out,null,2));console.log('리뷰 스크립트 구문과 조작 흐름 검사 통과');
})().catch(e=>{console.error(e);process.exitCode=1});
