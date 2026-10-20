const fs=require('fs'),path=require('path'),cp=require('child_process');
const out=__dirname,root=path.resolve(out,'../../..'),qa=path.join(out,'qa');
const wait=ms=>new Promise(r=>setTimeout(r,ms));
const chrome=cp.spawn('C:/Program Files/Google/Chrome/Application/chrome.exe',['--headless','--disable-gpu','--no-sandbox','--no-first-run','--remote-debugging-port=9494','--window-size=1440,1100','--user-data-dir='+path.join(qa,'browser-profile'),'about:blank'],{stdio:'ignore',windowsHide:true});
let ws,seq=0,pending=new Map();
function send(method,params={}){return new Promise((resolve,reject)=>{const id=++seq;pending.set(id,{resolve,reject});ws.send(JSON.stringify({id,method,params}));});}
async function js(expression){const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw Error(r.exceptionDetails.text);return r.result.value;}
const checks=[];function check(value,label){if(!value)throw Error(label);checks.push(label);}
(async()=>{try{
 let tabs;for(let i=0;i<40;i++){try{tabs=await (await fetch('http://127.0.0.1:9494/json/list')).json();break;}catch{await wait(100);}}
 ws=new WebSocket(tabs.find(t=>t.type==='page').webSocketDebuggerUrl);await new Promise(r=>ws.onopen=r);ws.onmessage=e=>{const data=JSON.parse(e.data);if(data.id){const p=pending.get(data.id);pending.delete(data.id);if(data.error)p.reject(Error(data.error.message));else p.resolve(data.result);}};
 await send('Page.enable');const file='file:///'+root.replaceAll('\\','/')+'/_HTML/'+encodeURIComponent('6_그림_화면')+'/'+encodeURIComponent('codex_K-094_함정자유설치_v1.html');await send('Page.navigate',{url:file});
 let state;for(let i=0;i<80;i++){state=await js('({...document.documentElement.dataset})');if(state.qa==='성공')break;await wait(100);}
 check(state.qa==='성공'&&state.qaImages==='12','내장 그림 12종 로드');check(state.qaErrors==='0','페이지 자바스크립트 오류 0');check(state.qaRing==='true','반경 128화소 재생성 선: 가장자리 불투명, 반경 오차 0.6화소 이내');
 check(await js('[...document.images].every(i=>i.complete&&i.naturalWidth>0)'),'페이지의 모든 이미지 요소 로드');
 const rect=await js('(()=>{const r=el("map").getBoundingClientRect();return {x:r.x,y:r.y}})()');
 await send('Input.dispatchMouseEvent',{type:'mouseMoved',x:rect.x+213.25,y:rect.y+87.75});
 check(await js('Math.abs(hover.x-213.25)<.02&&Math.abs(hover.y-87.75)<.02'),'포인터 자유 좌표: 소수 좌표 유지, 칸 중심으로 스냅하지 않음');
 const count=await js('traps.length');await send('Input.dispatchMouseEvent',{type:'mousePressed',button:'left',clickCount:1,x:rect.x+213.25,y:rect.y+87.75});await send('Input.dispatchMouseEvent',{type:'mouseReleased',button:'left',clickCount:1,x:rect.x+213.25,y:rect.y+87.75});
 check(await js(`traps.length===${count+1}&&traps.at(-1).x===hover.x`),'허용 위치 클릭: 같은 자유 좌표에 함정 추가');
 await js('el("invalid").checked=true');await send('Input.dispatchMouseEvent',{type:'mousePressed',button:'left',clickCount:1,x:rect.x+213.25,y:rect.y+87.75});await send('Input.dispatchMouseEvent',{type:'mouseReleased',button:'left',clickCount:1,x:rect.x+213.25,y:rect.y+87.75});check(await js(`traps.length===${count+1}&&invalidAt(hover)`),'불가 상태: 빨강 미리보기, 배치 개수 유지');
 await js('el("zoom").value="2";el("zoom").onchange();draw(performance.now())');check(await js('el("map").width===1536&&el("map").height===1152&&radius()===128'),'2배 지도: 화면 반경 128화소로 재생성');
 await send('Input.dispatchMouseEvent',{type:'mouseMoved',x:rect.x+426,y:rect.y+174});await js('hover={x:213,y:87};draw(performance.now())');
 const crop=await js('(()=>{const r=el("map").getBoundingClientRect();return {x:r.x,y:r.y+scrollY,width:768,height:410,scale:1}})()');let shot=await send('Page.captureScreenshot',{format:'png',captureBeyondViewport:true,clip:crop});fs.writeFileSync(path.join(qa,'radius_invalid_zoom2.png'),Buffer.from(shot.data,'base64'));
 await js('el("invalid").checked=false;el("zoom").value="1";el("zoom").onchange();initial();el("map").focus()');await send('Input.dispatchKeyEvent',{type:'keyDown',key:'ArrowRight',code:'ArrowRight',windowsVirtualKeyCode:39});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'ArrowRight',code:'ArrowRight',windowsVirtualKeyCode:39});check(await js('hover.x===524&&hover.y===111'),'방향키: 지도 좌표 한 화소 이동');
 await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Enter',code:'Enter',windowsVirtualKeyCode:13});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Enter',code:'Enter',windowsVirtualKeyCode:13});check(await js('traps.length===5'),'엔터: 임시 매설 실행');
 await js('el("pause").onclick()');const t=await js('dustTime');await wait(140);check(await js(`dustTime===${t}`),'먼지 일시 정지: 애니메이션 시계 유지');await js('el("pause").onclick()');await wait(140);check(await js(`dustTime>${t}`),'먼지 재생: 애니메이션 시계 진행');
 await js('initial();window.scrollTo(0,0);draw(performance.now())');await wait(120);const metrics=await send('Page.getLayoutMetrics');shot=await send('Page.captureScreenshot',{format:'png',captureBeyondViewport:true,clip:{x:0,y:0,width:1440,height:metrics.cssContentSize.height,scale:1}});fs.writeFileSync(path.join(qa,'review_browser_full.png'),Buffer.from(shot.data,'base64'));
 check(await js('document.documentElement.dataset.qaErrors==="0"'),'전체 조작 후 자바스크립트 오류 0');
 fs.writeFileSync(path.join(qa,'browser_check.json'),JSON.stringify({result:'통과',browser:'Chrome 헤드리스',checks,imagesLoaded:12,screenshots:['review_browser_full.png','radius_invalid_zoom2.png'],actualUnityCapture:false},null,2));console.log(JSON.stringify({result:'통과',checks:checks.length}));
 }finally{try{if(ws)await send('Browser.close');}catch{}if(ws)ws.close();chrome.kill();}})().catch(e=>{console.error(e);process.exitCode=1;});
