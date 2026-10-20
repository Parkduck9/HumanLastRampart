const fs = require('fs');
const path = require('path');
const {pathToFileURL} = require('url');
const {chromium} = require('C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
// 로컬 HTML의 이미지·재생·정지·한 칸 이동·좁은 화면을 확인한다.
(async()=>{
  const browser = await chromium.launch({channel:'chrome',headless:true});
  const page = await browser.newPage({viewport:{width:1400,height:1000},deviceScaleFactor:1});
  const errors=[]; page.on('pageerror',e=>errors.push(String(e)));
  await page.goto(pathToFileURL(path.resolve(__dirname,'../../../_HTML/4_그림_캐릭터/codex_K-078_사다리고블린동작_v1.html')).href);
  await page.waitForFunction(()=>window.reviewReady===true);
  const imageCount = await page.locator('img').count();
  const canvasCount = await page.locator('canvas').count();
  const imagesLoaded = await page.locator('img').evaluateAll(xs=>xs.every(x=>x.complete&&x.naturalWidth>0&&x.src.startsWith('data:')));
  await page.locator('#toggle').click();
  const stopped=await page.locator('#frame').textContent();
  await page.waitForTimeout(250);
  const pauseWorks=stopped===await page.locator('#frame').textContent();
  const before=await page.locator('canvas').nth(3).evaluate(c=>c.toDataURL());
  await page.locator('#step').click();
  const after=await page.locator('canvas').nth(3).evaluate(c=>c.toDataURL());
  const stepWorks=before!==after;
  await page.locator('#toggle').click();
  await page.waitForTimeout(250);
  const playbackWorks=after!==await page.locator('canvas').nth(3).evaluate(c=>c.toDataURL());
  await page.locator('#toggle').click();
  const animationFrames=[];
  for(let i=0;i<4;i++){
    animationFrames.push(await page.locator('canvas').evaluateAll(xs=>[3,5,7,9].map(k=>xs[k].toDataURL())));
    await page.locator('#step').click();
  }
  const allMotionsFourDistinct=Array.from({length:4},(_,k)=>new Set(animationFrames.map(f=>f[k])).size).every(n=>n===4);
  await page.screenshot({path:path.join(__dirname,'review_desktop.png'),fullPage:true});
  await page.locator('section').nth(4).screenshot({path:path.join(__dirname,'review_climb.png')});
  await page.setViewportSize({width:390,height:844});
  const mobileNoOverflow=await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth);
  await page.screenshot({path:path.join(__dirname,'review_mobile.png'),fullPage:true});
  const result={imageCount,canvasCount,imagesLoaded,pauseWorks,stepWorks,playbackWorks,allMotionsFourDistinct,mobileNoOverflow,errors};
  fs.writeFileSync(path.join(__dirname,'review_validation.json'),JSON.stringify(result,null,2));
  await browser.close();
  if(!imagesLoaded||!pauseWorks||!stepWorks||!playbackWorks||!allMotionsFourDistinct||!mobileNoOverflow||errors.length||canvasCount!==14)throw Error(JSON.stringify(result));
  console.log(JSON.stringify(result));
})();
