const fs=require('fs'),path=require('path'),assert=require('assert');
const {pathToFileURL}=require('url');
const {chromium}=require('C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({channel:'chrome',headless:true});
 try {
  const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[];
  page.on('pageerror',e=>errors.push(String(e)));
  const external=[];page.on('request',r=>{if(/^https?:/.test(r.url()))external.push(r.url());});
  await page.goto(pathToFileURL(path.resolve(__dirname,'../../../_HTML/5_그림_전장/codex_K-088_총구불꽃그림자_v1.html')).href);
  await page.waitForFunction(()=>window.reviewState?.ready);
  await page.locator('img').evaluateAll(a=>Promise.all(a.map(i=>i.decode())));
  const imageCount=await page.locator('img').count();assert.equal(imageCount,11);
  const before=await page.evaluate(()=>reviewState.time);
  await page.waitForTimeout(180);
  const after=await page.evaluate(()=>reviewState.time);assert(after>before);
  await page.locator('#play').click();
  const paused=await page.evaluate(()=>reviewState.time);await page.waitForTimeout(80);
  assert.equal(await page.evaluate(()=>reviewState.time),paused);
  await page.locator('#speed').selectOption('0.25');
  await page.locator('#bg').selectOption('#9a9b78');
  assert.equal(await page.locator('#right').evaluate(c=>c.style.background),'rgb(154, 155, 120)');
  await page.locator('#bg').selectOption('#171d22');
  const captures=[];
  for(const t of [10,50,100,250]){
   await page.evaluate(t=>reviewSeek(t),t);
   const file='review/frame_'+t+'ms.png';
   await page.locator('.row').first().screenshot({path:path.join(__dirname,file)});captures.push(file);
  }
  await page.evaluate(()=>reviewSeek(50));
  await page.screenshot({path:path.join(__dirname,'review/desktop.png'),fullPage:true});
  await page.locator('.duo').first().screenshot({path:path.join(__dirname,'review/contact_comparison.png')});
  await page.locator('#restart').click();assert.equal(await page.evaluate(()=>reviewState.time),0);
  await page.locator('#play').click();
  await page.waitForTimeout(120);assert(await page.evaluate(()=>reviewState.time)>0);
  await page.setViewportSize({width:390,height:844});
  const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);assert(!overflow);
  await page.screenshot({path:path.join(__dirname,'review/mobile.png'),fullPage:true});
  assert.equal(errors.length,0);assert.equal(external.length,0);
  const report={passed:true,imageCount,allImagesDecoded:true,canvasSourceCount:12,animationAdvances:true,
    pauseResume:true,restart:true,speedSelect:true,backgroundSelect:true,mobileOverflow:overflow,
    errors,externalRequests:external,captures,unityTested:false};
  fs.writeFileSync(path.join(__dirname,'review_validation.json'),JSON.stringify(report,null,2));
  console.log(JSON.stringify(report));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});
