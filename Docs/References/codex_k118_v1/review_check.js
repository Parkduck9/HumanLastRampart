const fs=require('fs'),path=require('path'),vm=require('vm'),zlib=require('zlib');
const root=__dirname,repo=path.resolve(root,'../../..');
const html=fs.readFileSync(path.join(repo,'_HTML/6_그림_화면/codex_K-118_안마당건물_v1.html'),'utf8');
const embedded=[...html.matchAll(/(?:src|data-day|data-night)="data:image\/png;base64,([A-Za-z0-9+/=]+)"/g)].map(m=>Buffer.from(m[1],'base64'));
function png(b){if(b.readUInt32BE(0)!==0x89504e47)throw Error('PNG 형식 오류');let o=8,idat=[];while(o<b.length){const n=b.readUInt32BE(o),type=b.toString('ascii',o+4,o+8);if(o+n+12>b.length)throw Error('PNG 잘림');if(type==='IDAT')idat.push(b.subarray(o+8,o+8+n));o+=n+12;}zlib.inflateSync(Buffer.concat(idat));return [b.readUInt32BE(16),b.readUInt32BE(20)];}
embedded.forEach(png);
if(/<script[^>]+src=|<link[^>]+href=|<img[^>]+src="(?!data:)/.test(html))throw Error('외부 리소스 참조');
const manifest=JSON.parse(fs.readFileSync(path.join(root,'manifest.json'),'utf8'));
for(const a of [...manifest.assets,...manifest.emission_masks]){let s=png(fs.readFileSync(path.join(root,a.file)));if(s.join()!=a.size_px.join())throw Error('명세 크기 불일치 '+a.file);if(a.game_file){s=png(fs.readFileSync(path.join(root,a.game_file)));if(s.join()!=a.game_size_px.join())throw Error('게임용 크기 불일치');}}
const nodes={play:{textContent:''},day:{},night:{},state:{textContent:''},load:{textContent:''}};
let tick;const motions=[...html.matchAll(/<img class="motion"[^>]*data-day="([^"]+)" data-night="([^"]+)"/g)].map(m=>({src:m[1],dataset:{day:m[1],night:m[2]}}));
const document={getElementById:id=>nodes[id],querySelectorAll:()=>motions,images:embedded.map(()=>({decode:()=>Promise.resolve()}))};
const script=html.match(/<script>([\s\S]*?)<\/script>/)[1];
vm.runInNewContext(script,{document,setInterval:fn=>{tick=fn},Promise});
if(motions.length!==4)throw Error('앞면 개수 오류');
nodes.night.onclick();if(nodes.state.textContent!=='밤'||motions.some(n=>n.src!==n.dataset.night))throw Error('밤 전환 오류');
nodes.day.onclick();if(nodes.state.textContent!=='낮')throw Error('낮 전환 오류');
tick();if(nodes.state.textContent!=='밤')throw Error('자동 전환 오류');
nodes.play.onclick();tick();if(nodes.state.textContent!=='밤')throw Error('정지 오류');
nodes.play.onclick();tick();if(nodes.state.textContent!=='낮')throw Error('재개 오류');
setImmediate(()=>{if(!nodes.load.textContent.includes('모두 불러왔습니다'))throw Error('로딩 표시 오류');const result={result:'pass',method:'PNG IDAT decode, manifest dimensions, Node VM with minimal DOM stubs',embedded_png_references:embedded.length,external_resource_references:0,facade_switches:4,controls_checked:['day','night','automatic tick','pause','resume'],browser_render_checked:false,browser_blocker:'cua.getState returned apps=[] and browsers=[]; iab unavailable',limitation:'DOM stubs validate script behavior, not browser rendering, image.decode behavior, layout or real mouse interaction.'};fs.writeFileSync(path.join(root,'review_verification.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));});
