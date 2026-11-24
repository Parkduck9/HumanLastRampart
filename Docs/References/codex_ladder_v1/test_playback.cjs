const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path');
const root=__dirname,html=fs.readFileSync(path.join(root,'preview.html'),'utf8');
const payload=JSON.parse(html.match(/<script id="payload" type="application\/json">([\s\S]*?)<\/script>/)[1]);
const results={};let drawCalls=0,raf;
const ctx=new Proxy({},{get:(_t,name)=>name==='drawImage'?(..._args)=>{drawCalls++;}:()=>{} ,set:()=>true});
const elements={};function el(id){return elements[id]??={id,value:'',style:{},dataset:{},classList:{toggle(name,value){this[name]=value;}},textContent:'',width:id==='stage'?960:256,height:id==='stage'?640:384,getContext:()=>ctx,setAttribute(){},append(){}};}
el('payload').textContent=JSON.stringify(payload);
for(const [key,value] of Object.entries({clip:'carry_run',background:'checker',propView:'ladder_front',scale:'1',fps:'6',frame:'0'}))el(key).value=value;
class MockImage{set src(value){const b=Buffer.from(value.split(',')[1],'base64');this.width=b.readUInt32BE(16);this.height=b.readUInt32BE(20);setImmediate(()=>this.onload());}}
const sandbox={document:{getElementById:el,createElement:tag=>el('created'+Math.random()),body:{dataset:{}}},Image:MockImage,requestAnimationFrame:cb=>{raf=cb;},console,Promise,Math,JSON,String,Error};
vm.createContext(sandbox);vm.runInContext(fs.readFileSync(path.join(root,'viewer.js'),'utf8'),sandbox);
function state(){return vm.runInContext('({...state})',sandbox);}
function frame(t){const cb=raf;cb(t);}
(async()=>{
 await new Promise(setImmediate);await new Promise(setImmediate);
 results.images_loaded=Object.keys(vm.runInContext('images',sandbox)).length===5;
 results.initial_autoplay=state().playing===true&&drawCalls>0;
 frame(0);frame(200);
 results.raf_advances_frame=state().frame===1;
 el('play').onclick();const paused=state().frame;frame(400);frame(600);
 results.pause_stops_frame=state().frame===paused&&!state().playing;
 results.status_matches_pause=el('loading').textContent.includes('정지 중');
 el('next').onclick();results.manual_next_one_frame=state().frame===(paused+1)%4&&!state().playing;
 el('prev').onclick();results.manual_previous_one_frame=state().frame===paused&&!state().playing;
 el('play').onclick();frame(800);frame(1000);results.resume_advances=state().frame===(paused+1)%4&&state().playing;results.status_matches_resume=el('loading').textContent.includes('재생 중');
 el('clip').value='deploy';el('clip').onchange();frame(1200);for(let t=1400;t<=2400;t+=200)frame(t);
 results.deploy_stops_last_frame=state().frame===3&&!state().playing;
 el('play').onclick();results.deploy_replay_restarts=state().frame===0&&state().playing;
 el('clip').value='climb';el('clip').onchange();vm.runInContext('state.frame=3;state.playing=true;state.last=null',sandbox);frame(2600);frame(2800);
 results.climb_loops_to_first=state().frame===0&&state().playing;
 el('frame').value='2';el('frame').oninput();results.scrub_pauses_and_selects=state().frame===2&&!state().playing;
 el('scale').value='2';el('scale').onchange();results.double_scale=el('detail').style.width==='512px'&&el('detail').style.height==='768px';results.zoom_expands_layout=el('inspection').classList.zoomed===true;
 el('background').value='light';el('background').onchange();results.light_background_control=sandbox.document.body.dataset.background==='light';
 el('propView').value='ladder_side';el('propView').onchange();results.side_prop_control_no_error=true;
 el('fps').value='8';el('fps').onchange();results.fps_control=state().fps===8;
 const qa={method:'Node VM 재생 상태·타이머·이벤트 단위 검사; 실제 DOM/브라우저/캔버스 렌더 검증 아님',checks:results,passed:Object.values(results).filter(Boolean).length,total:Object.keys(results).length};
 fs.writeFileSync(path.join(root,'playback_qa.json'),JSON.stringify(qa,null,2)+'\n');console.log(JSON.stringify(qa));
 if(!Object.values(results).every(Boolean))process.exitCode=1;
})().catch(error=>{console.error(error);process.exitCode=1;});
