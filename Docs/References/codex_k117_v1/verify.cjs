const fs=require('fs'),path=require('path'),{pathToFileURL}=require('url');
const deps='C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/';
const sharp=require(deps+'sharp'),{chromium}=require(deps+'playwright');
const root=__dirname,repo=path.resolve(root,'../../..');
(async()=>{
const checks=[];
for(const n of ['dead','relit']){
 const f=`story_${n}_beacon_1920x1080.png`,p=path.join(root,f),{data,info}=await sharp(p).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let opaque=true,bottomSum=0,bottomMax=0,bottomBright=0;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
  const i=(y*info.width+x)*4;if(data[i+3]!==255)opaque=false;
  if(y>=720){const l=.2126*data[i]+.7152*data[i+1]+.0722*data[i+2];bottomSum+=l;bottomMax=Math.max(bottomMax,l);if(l>100)bottomBright++;}
 }
 const preview=await sharp(path.join(root,'previews',f.replace('.png','_4x.png'))).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let exact=true;for(let y=0;y<preview.info.height;y++)for(let x=0;x<preview.info.width;x++)for(let c=0;c<4;c++)if(preview.data[(y*preview.info.width+x)*4+c]!==data[(Math.floor(y/4)*1920+Math.floor(x/4))*4+c])exact=false;
 checks.push({file:f,size:[info.width,info.height],size_ok:info.width===1920&&info.height===1080,fully_opaque:opaque,preview_size:[preview.info.width,preview.info.height],preview_exact_nearest_4x:exact,lower_third:{mean_luminance:bottomSum/(1920*360),max_luminance:bottomMax,bright_pixel_fraction:bottomBright/(1920*360)},foot_line:'해당 없음 · 불투명 정지 배경',stray_alpha_pixels:'완전 불투명 여부로 확인'});
}
const browser=await chromium.launch({channel:'msedge',headless:true}),page=await browser.newPage({viewport:{width:1440,height:1080}}),errors=[];
page.on('pageerror',e=>errors.push(String(e)));
await page.goto(pathToFileURL(path.join(repo,'_HTML/6_그림_화면/codex_K-117_이야기배경_등대탑_v1.html')).href);
await page.locator('img').evaluateAll(xs=>Promise.all(xs.map(x=>x.decode())));
const images=await page.locator('img').evaluateAll(xs=>xs.map(x=>({label:x.alt,loaded:x.complete&&x.naturalWidth>0,embedded:x.src.startsWith('data:')})));
const visibleOn=await page.locator('.dialogue').first().isVisible();await page.locator('#dialogue').uncheck();const hiddenOff=!(await page.locator('.dialogue').first().isVisible());await page.locator('#dialogue').check();
await page.screenshot({path:path.join(root,'review_desktop.png'),fullPage:true});
await page.locator('details').first().locator('summary').click();const expanded=await page.locator('details').first().getAttribute('open')!==null;
await page.setViewportSize({width:390,height:844});const mobileNoOverflow=await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth);await page.screenshot({path:path.join(root,'review_mobile.png'),fullPage:true});
await browser.close();
const result={passed:checks.every(x=>x.size_ok&&x.fully_opaque&&x.preview_exact_nearest_4x)&&images.every(x=>x.loaded&&x.embedded)&&visibleOn&&hiddenOff&&expanded&&mobileNoOverflow&&!errors.length,assets:checks,review:{images,dialogue_toggle:visibleOn&&hiddenOff,expand:expanded,mobileNoOverflow,errors},unity_executed:false};
fs.writeFileSync(path.join(root,'verification.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));if(!result.passed)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1});
