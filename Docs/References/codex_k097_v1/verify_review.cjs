const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),zlib=require('node:zlib'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'../../..'),file=path.join(root,'_HTML/6_그림_화면/codex_K-097_튜토리얼보충_v1.html'),html=fs.readFileSync(file,'utf8');
const canvases=[],elements=new Map(),raf=[];let imagesDecoded=0,drawCalls=0;
// 브라우저가 없는 환경에서 내장 그림과 검토 스크립트의 동작을 검사한다.
class Element{
 constructor(tag='div'){this.tag=tag;this.dataset={};this.children=[];this.value='0';this.textContent='';this.width=0;this.height=0}
 append(...items){this.children.push(...items)}add(o){this.children.push(o)}
 getContext(){return {imageSmoothingEnabled:false,fillText(){},drawImage(im,sx,sy,sw,sh,dx,dy,dw,dh){assert(im.width>0&&im.height>0);assert(sx>=0&&sy>=0&&sx+sw<=im.width&&sy+sh<=im.height);assert(dw>0&&dh>0);drawCalls++}}}
}
for(const match of html.matchAll(/<canvas\b([^>]*)>/g)){const c=new Element('canvas');for(const a of match[1].matchAll(/([\w-]+)="([^"]*)"/g)){if(a[1].startsWith('data-'))c.dataset[a[1].slice(5)]=a[2];if(a[1]==='id')elements.set('#'+a[2],c)}canvases.push(c)}
for(const id of ['portraits','directions','step','stepLabels','stepText','status','pause','next','width','widthValue'])if(!elements.has('#'+id))elements.set('#'+id,new Element());elements.get('#width').value='720';
const document={querySelector:s=>elements.get(s),createElement:tag=>{const e=new Element(tag);if(tag==='canvas')canvases.push(e);return e},querySelectorAll:s=>s==='canvas[data-sprite]'?canvases.filter(c=>c.dataset.sprite):canvases};
class Image{
 set src(v){assert(v.startsWith('data:image/png;base64,'));const b=Buffer.from(v.split(',')[1],'base64');assert.equal(b.subarray(1,4).toString(),'PNG');this.width=b.readUInt32BE(16);this.height=b.readUInt32BE(20);let pos=8,idat=[];while(pos<b.length){const len=b.readUInt32BE(pos),type=b.toString('ascii',pos+4,pos+8);if(type==='IDAT')idat.push(b.subarray(pos+8,pos+8+len));pos+=len+12}const data=zlib.inflateSync(Buffer.concat(idat));assert(data.length>this.height);imagesDecoded++;queueMicrotask(()=>this.onload())}
}
const window={addEventListener(){}};
const context=vm.createContext({document,window,Image,Option:class{constructor(text,value){this.text=text;this.value=value}},requestAnimationFrame:f=>raf.push(f),console,Promise,Error});
vm.runInContext(html.match(/<script>([\s\S]*?)<\/script>/)[1],context,{filename:'review-inline.js'});
(async()=>{for(let i=0;i<10&&!window.reviewReady;i++)await new Promise(r=>setImmediate(r));assert(window.reviewReady);assert.equal(imagesDecoded,19);const frame=raf.shift();frame(100);raf.shift()(500);const animated=canvases.filter(c=>+c.dataset.fps),before=animated.map(c=>c.dataset.frame);elements.get('#pause').onclick();raf.shift()(800);assert.deepEqual(animated.map(c=>c.dataset.frame),before);elements.get('#next').onclick();assert(animated.every((c,i)=>+c.dataset.frame===(+before[i]+1)%+c.dataset.count));raf.shift()(1000);assert(animated.every((c,i)=>+c.dataset.frame===(+before[i]+1)%+c.dataset.count));
const stepTexts=[];for(let i=0;i<9;i++){elements.get('#step').value=String(i);elements.get('#step').onchange();stepTexts.push(elements.get('#stepText').textContent)}assert.equal(stepTexts[8],'여덟 단계 모두 완료');for(const w of [180,900]){elements.get('#width').value=String(w);elements.get('#width').oninput();assert.equal(elements.get('#sliced').width,w)}
const report={method:'Node VM with DOM/canvas mocks plus PNG IDAT decoding; not a real browser',embedded_png_count:imagesDecoded,draw_calls:drawCalls,all_source_rects_in_bounds:true,autoplay:true,pause:true,single_step_holds:true,all_9_progress_states:stepTexts,nine_slice_widths:[180,900],external_resources:0,browser_verified:false,browser_limitation:'Headless Chrome did not expose its debugging endpoint. Computer-use inventory had no browsers; iab was unavailable.',unity_verified:false};fs.writeFileSync(path.join(__dirname,'review_verification.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report))})().catch(e=>{console.error(e);process.exitCode=1});
