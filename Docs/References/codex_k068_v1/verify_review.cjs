const fs = require('fs');
const path = require('path');
const {pathToFileURL} = require('url');
const {chromium} = require('C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async () => {
  const browser = await chromium.launch({channel:'chrome',headless:true});
  try {
    const page = await browser.newPage({viewport:{width:1440,height:1000}});
    const errors=[];
    page.on('pageerror',e=>errors.push(String(e)));
    await page.goto(pathToFileURL(path.resolve(__dirname,'../../../_HTML/5_그림_전장/codex_K-068_들판꾸미기_v1.html')).href);
    await page.locator('img').evaluateAll(a=>Promise.all(a.map(i=>i.decode())));
    const imageCount=await page.locator('img').count();
    if(imageCount!==76) throw Error('내장 이미지 개수 오류');
    const controls=[];
    for(let i=0;i<2;i++) {
      await page.locator('.toolbar').nth(i).locator('button').first().click();
      controls.push(await page.locator('#after'+i).evaluate(e=>e.style.clipPath.includes('100%')));
      await page.locator('.toolbar').nth(i).locator('button').last().click();
      controls.push(await page.locator('#after'+i).evaluate(e=>e.style.clipPath.includes('0%')));
      await page.locator('input').nth(i).fill('50');
      controls.push(await page.locator('#after'+i).evaluate(e=>e.style.clipPath.includes('50%')));
    }
    const captures=[];
    for(const name of ['버려진 방패','토벽 · 온전함','토벽 · 균열','토벽 · 붕괴','토벽 잔해']) {
      const tile=page.locator('article').filter({has:page.getByRole('heading',{name,exact:true})});
      await tile.locator('summary').click();
      const file='review/browser_'+({ '버려진 방패':'shield','토벽 · 온전함':'intact','토벽 · 균열':'cracked','토벽 · 붕괴':'collapsed','토벽 잔해':'rubble'}[name])+'.png';
      await tile.screenshot({path:path.join(__dirname,file)});
      captures.push(file);
    }
    await page.setViewportSize({width:390,height:900});
    const mobileOverflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);
    if(errors.length||controls.some(x=>!x)||mobileOverflow) throw Error('검토 페이지 동작 또는 배치 오류');
    const report={imageCount,allImagesDecoded:true,controls,errors,mobileOverflow,captures};
    fs.writeFileSync(path.join(__dirname,'review_validation.json'),JSON.stringify(report,null,2));
    const verification=JSON.parse(fs.readFileSync(path.join(__dirname,'verification.json'),'utf8'));
    verification.browser_render_tested=true;
    fs.writeFileSync(path.join(__dirname,'verification.json'),JSON.stringify(verification,null,2));
    console.log(report);
  } finally { await browser.close(); }
})().catch(e=>{console.error(e);process.exit(1)});
