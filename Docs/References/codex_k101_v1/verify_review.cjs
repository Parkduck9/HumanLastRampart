// 브라우저 화면 검증과 구분되는 스크립트 실행 검사다. 이미지 바이트 검증은 verify.py에서 수행한다.
const fs=require('fs'),vm=require('vm'),assert=require('assert');
const html=fs.readFileSync(process.argv[2],'utf8');
const script=html.match(/<script>([\s\S]*?)<\/script>/)[1];
const nodes=new Map(),draws=[];
function canvas(){return {width:0,height:0,getContext(){return {drawImage(...args){draws.push(args)},getImageData(x,y,w,h){return {data:new Uint8ClampedArray(w*h*4)}},putImageData(){},clearRect(){},beginPath(){},moveTo(){},lineTo(){},stroke(){},fillRect(){}}}}}
function element(){return {textContent:'',value:'',checked:true,children:[],append(n){this.children.push(n)},set innerHTML(v){this.cs=[canvas(),canvas(),canvas()];this.caption={textContent:''}},querySelectorAll(){return this.cs},querySelector(){return this.caption}}}
for(const id of ['gallery','channel','baseline','play','next','fps','status','error'])nodes.set(id,element());
nodes.get('channel').value='rg';nodes.get('fps').value='6';
let callback;
class TestImage{set src(v){const b=Buffer.from(v.split(',')[1],'base64');this.width=b.readUInt32BE(16);this.height=b.readUInt32BE(20);queueMicrotask(()=>this.onload())}}
const ctx={console,Uint8ClampedArray,Image:TestImage,window:{},document:{getElementById:id=>nodes.get(id),createElement:tag=>tag==='canvas'?canvas():element()},requestAnimationFrame:fn=>{callback=fn}};
vm.createContext(ctx);vm.runInContext(script,ctx);
(async()=>{
  for(let i=0;i<30&&!ctx.window.__review.ready;i++)await new Promise(setImmediate);
  assert(ctx.window.__review.ready);
  assert.equal(ctx.window.__review.imagesLoaded,21);
  assert.equal(ctx.window.__review.errors.length,0);
  assert.equal(nodes.get('gallery').children.length,10);
  assert(draws.some(d=>d.length===9&&d[2]===1152));
  assert(draws.some(d=>d.length===9&&d[3]===432));
  callback(1000);const tick=ctx.window.__review.tick;assert(tick>0);
  nodes.get('play').onclick();callback(2000);assert.equal(ctx.window.__review.tick,tick);
  nodes.get('next').onclick();assert.equal(ctx.window.__review.tick,tick+1);
  for(const mode of ['r','g','rg']){nodes.get('channel').value=mode;nodes.get('channel').onchange()}
  nodes.get('baseline').checked=false;nodes.get('baseline').onchange();
  assert.equal(nodes.get('error').textContent,'');
  console.log(JSON.stringify({pass:true,animated_motion_cards:10,images_initialized:21,play_pause_step:true,channel_switches:true,baseline_toggle:true,actual_browser:false}));
})().catch(e=>{console.error(e);process.exitCode=1});
