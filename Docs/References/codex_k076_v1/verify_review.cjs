const fs=require('fs'),vm=require('vm'),assert=require('assert'),path=require('path');
const page=fs.readFileSync(path.resolve(__dirname,'../../../_HTML/4_그림_캐릭터/codex_K-076_마법사동작_v1.html'),'utf8');
const configs=[...page.matchAll(/data-config='([^']+)'/g)].map(m=>JSON.parse(m[1]));
const controls=Object.fromEntries(['toggle','step','speed','fps','status'].map(k=>['#'+k,{value:'4',textContent:''}]));
let drawCalls=0,next;
const canvas=()=>({getContext:()=>({clearRect(){},drawImage(img,sx,sy,sw,sh){assert(sx>=0&&sx+sw<=img.width);assert(sy===0&&sh<=img.height);drawCalls++},beginPath(){},moveTo(){},lineTo(){},stroke(){}})});
const els=configs.map(c=>{const elements={'.new':canvas(),'.old':c.old?canvas():null};return {dataset:{config:JSON.stringify(c)},querySelector:s=>elements[s],parentElement:{querySelector:()=>({textContent:''})}}});
class Image {set src(s){const b=Buffer.from(s.split(',')[1],'base64');assert(b.subarray(1,4).toString()==='PNG');this.width=b.readUInt32BE(16);this.height=b.readUInt32BE(20);queueMicrotask(()=>this.onload())}}
const scope={Image,Promise,Error,Number,JSON,document:{querySelector:s=>controls[s],querySelectorAll:()=>els},requestAnimationFrame:cb=>{next=cb}};scope.window=scope;vm.createContext(scope);
vm.runInContext(page.match(/<script>([\s\S]*?)<\/script>/)[1],scope);
setImmediate(()=>{try{assert.equal(scope.reviewCheck.loaded,7);assert.equal(scope.reviewCheck.errors.length,0);for(let t=300;t<=1800;t+=300)next(t);controls['#toggle'].onclick({target:controls['#toggle']});controls['#step'].onclick();assert(drawCalls>=50);assert(controls['#status'].textContent.includes('7개'));fs.writeFileSync(path.join(__dirname,'review_validation.json'),JSON.stringify({embeddedAnimationImages:7,drawCalls,frameStepping:true,pauseAndStep:true,mode:'모의 DOM 실행: 실제 브라우저 렌더링 아님'},null,2));console.log('내장 이미지와 재생·정지·단계 이동 코드 검증 통과')}catch(e){console.error(e);process.exitCode=1}});
