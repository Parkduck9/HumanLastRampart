const fs=require('fs'),path=require('path'),cp=require('child_process');
const out=__dirname,root=path.resolve(out,'../../..');
const page=path.join(root,'_HTML/6_그림_화면/codex_K-126_성벽부서짐_v1.html');
async function run(){
const proc=cp.spawn('C:/Program Files/Google/Chrome/Application/chrome.exe',['--headless','--disable-gpu','--no-sandbox','--no-first-run','--disable-extensions','--remote-debugging-port=9336','--allow-file-access-from-files','--user-data-dir='+path.join(out,'_browserqa_cdp'),require('url').pathToFileURL(page).href],{stdio:'ignore',windowsHide:true});
let target;for(let i=0;i<40;i++){try{const list=await(await fetch('http://127.0.0.1:9336/json')).json();target=list.find(x=>x.type==='page');if(target)break;}catch{}await new Promise(r=>setTimeout(r,250));}
if(!target)throw Error('검토 브라우저 연결 실패');
const ws=new WebSocket(target.webSocketDebuggerUrl);await new Promise(r=>ws.onopen=r);let id=0;const calls=new Map();ws.onmessage=e=>{const d=JSON.parse(e.data);if(d.id){calls.get(d.id)?.(d);calls.delete(d.id)}};const call=(method,params={})=>new Promise(resolve=>{const n=++id;calls.set(n,resolve);ws.send(JSON.stringify({id:n,method,params}));});
await new Promise(r=>setTimeout(r,1200));
await call('Runtime.evaluate',{expression:'paused=true;tick=0;draw(performance.now())'});
const evalResult=await call('Runtime.evaluate',{expression:`Promise.all([...document.images].map(i=>i.decode().then(()=>({ok:true,w:i.naturalWidth})).catch(()=>({ok:false})))).then(images=>({embedded_images_decoded:images.every(x=>x.ok),image_count:images.length,canvases:document.querySelectorAll('canvas').length,title:document.title,animation_frames_visible:[...document.querySelectorAll('canvas')].map(c=>({effect:c.dataset.anim,visible:c.getContext('2d').getImageData(0,0,c.width,c.height).data.some((x,i)=>i%4===3&&x>0)}))}))`,awaitPromise:true,returnByValue:true});
const result=evalResult.result?.result?.value||evalResult;
await call('Emulation.setDeviceMetricsOverride',{width:1440,height:1000,deviceScaleFactor:1,mobile:false});
const shot=await call('Page.captureScreenshot',{format:'png'});if(shot.result?.data)fs.writeFileSync(path.join(out,'review_page_qa.png'),Buffer.from(shot.result.data,'base64'));
fs.writeFileSync(path.join(out,'browser_verification.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));await call('Browser.close');ws.close();proc.kill();
}run().catch(e=>{console.error(e.message);process.exitCode=1;});
