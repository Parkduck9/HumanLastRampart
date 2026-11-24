const fs=require('fs');
const path=require('path');
const {pathToFileURL}=require('url');
const {chromium}=require('C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({channel:'chrome',headless:true});
 const page=await browser.newPage({viewport:{width:1100,height:1000}});
 const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(pathToFileURL(path.resolve(__dirname,'../../../_HTML/4_그림_캐릭터/codex_K-077_고블린동작_v1.html')).href);
 const images=await page.evaluate(async()=>{
  const urls=[...document.images].map(i=>i.src).concat([...document.querySelectorAll('.sprite')].map(s=>getComputedStyle(s).backgroundImage.slice(5,-2)));
  return Promise.all(urls.map(async src=>{const i=new Image();i.src=src;await i.decode();return {width:i.naturalWidth,height:i.naturalHeight,embedded:src.startsWith('data:')};}));
 });
 await page.getByRole('button',{name:'일시 정지',exact:true}).click();
 const before=await page.locator('.sprite').first().evaluate(e=>e.style.backgroundPosition);
 await page.getByRole('button',{name:'한 장씩',exact:true}).click();
 const after=await page.locator('.sprite').first().evaluate(e=>e.style.backgroundPosition);
 await page.getByRole('button',{name:'처음부터',exact:true}).click();
 const reset=await page.locator('.sprite').first().evaluate(e=>e.style.backgroundPosition);
 await page.screenshot({path:path.join(__dirname,'review_desktop.png'),fullPage:true});
 await page.setViewportSize({width:390,height:844});
 await page.screenshot({path:path.join(__dirname,'review_mobile.png'),fullPage:true});
 const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);
 const result={imagesDecoded:images.length,allImagesEmbedded:images.every(i=>i.embedded),stepChangesFrame:before!==after,resetWorks:reset==='0px 0px',mobileOverflow:overflow,pageErrors:errors};
 fs.writeFileSync(path.join(__dirname,'review_validation.json'),JSON.stringify(result,null,2));
 console.log(result);await browser.close();
 if(errors.length||overflow||!result.stepChangesFrame||!result.resetWorks)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1});
