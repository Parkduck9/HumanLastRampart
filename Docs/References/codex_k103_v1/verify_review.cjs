// 파일로 직접 연 검토 화면의 그림과 애니메이션, 선택 상태를 확인한다.
const fs=require('fs'),path=require('path'),{pathToFileURL}=require('url');
const {chromium}=require('C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 const page=await browser.newPage({viewport:{width:1440,height:1080}});let errors=[];
 page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(pathToFileURL(path.resolve(__dirname,'../../../_HTML/6_그림_화면/codex_K-103_성벽화로와안개_v1.html')).href);
 await page.locator('img').evaluateAll(xs=>Promise.all(xs.map(x=>x.decode())));
 const loaded=await page.locator('img').evaluateAll(xs=>xs.every(x=>x.complete&&x.naturalWidth>0&&x.src.startsWith('data:')));
 const backgrounds=await page.locator('[style*="background-image"]').evaluateAll(xs=>Promise.all(xs.map(x=>new Promise(r=>{let im=new Image();im.onload=()=>r({w:im.width,h:im.height});im.onerror=()=>r(null);im.src=x.style.backgroundImage.slice(5,-2)}))));
 const before=await page.locator('.anim').evaluateAll(xs=>xs.map(x=>x.style.backgroundPosition));await page.waitForTimeout(650);
 const after=await page.locator('.anim').evaluateAll(xs=>xs.map(x=>x.style.backgroundPosition));
 const animated=before.every((x,i)=>x!==after[i]);
 await page.screenshot({path:path.join(__dirname,'review_desktop.png'),fullPage:true});
 await page.locator('#pause').click();
 const stopBefore=await page.locator('.anim').evaluateAll(xs=>xs.map(x=>x.style.backgroundPosition));await page.waitForTimeout(600);
 const stopAfter=await page.locator('.anim').evaluateAll(xs=>xs.map(x=>x.style.backgroundPosition));
 const stopped=JSON.stringify(stopBefore)===JSON.stringify(stopAfter);
 let selections=[];
 for(let i=0;i<3;i++){await page.locator(`[data-level="${i}"]`).click();selections.push(await page.locator('[data-level]').evaluateAll((xs,i)=>xs.every((x,j)=>x.getAttribute('aria-pressed')===String(i===j)),i));}
 await page.locator('#night').click();
 await page.setViewportSize({width:390,height:844});await page.screenshot({path:path.join(__dirname,'review_mobile.png'),fullPage:true});
 const mobileOverflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);
 const result={loaded,backgrounds,animated,stopped,selections,mobileOverflow,errors};
 result.passed=loaded&&backgrounds.every(Boolean)&&animated&&stopped&&selections.every(Boolean)&&!mobileOverflow&&!errors.length;
 fs.writeFileSync(path.join(__dirname,'review_validation.json'),JSON.stringify(result,null,2));
 await browser.close();console.log(JSON.stringify(result));if(!result.passed)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1});
