const { chromium } = require('C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');
(async () => {
  const root = path.resolve(__dirname, '../../..');
  const browser = await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
  const page = await browser.newPage({viewport:{width:1440,height:1100},deviceScaleFactor:1});
  const errors=[]; page.on('pageerror',e=>errors.push(String(e)));
  const failed=[];page.on('requestfailed',r=>failed.push(r.url()));
  await page.goto(pathToFileURL(path.join(root,'_HTML/5_그림_전장/codex_K-092_반사마스크_v1.html')).href);
  await page.waitForFunction(()=>window.__review?.ready,{},{timeout:60000});
  await page.locator('#animate').uncheck();await page.locator('#moving').uncheck();
  const tested=[];
  const manifest=JSON.parse(fs.readFileSync(path.join(__dirname,'manifest.json'),'utf8'));
  let framesChecked=0,alphaMismatches=0;
  for(let i=0;i<41;i++){
    const before=await page.evaluate(()=>window.__review.renderCount);
    await page.selectOption('#sheet',String(i));
    await page.waitForFunction(n=>window.__review.renderCount>n,before);
    const result=await page.evaluate(()=>{
      const a=document.getElementById('diffuse'),b=document.getElementById('specular');
      const x=a.getContext('2d').getImageData(0,0,a.width,a.height).data,y=b.getContext('2d').getImageData(0,0,b.width,b.height).data;
      let different=0;for(let j=0;j<x.length;j+=4)if(x[j]!==y[j]||x[j+1]!==y[j+1]||x[j+2]!==y[j+2])different++;
      return {name:document.getElementById('sheet').selectedOptions[0].textContent,width:a.width,height:a.height,different};
    });tested.push(result);
    for(let f=0;f<manifest.sheets[i].frame_count;f++){
      await page.locator('#frame').fill(String(f));
      await page.waitForFunction(v=>window.__review.frame===v,f);
      const mismatch=await page.evaluate(async ({i,f})=>{
        const p=await window.__review.prepare(i),c=document.getElementById('specular');
        const data=c.getContext('2d').getImageData(0,0,c.width,c.height).data;
        const sheet=window.__reviewManifest[i],cols=sheet.grid[0],sw=sheet.size[0];let n=0;
        const xx=(f%cols)*c.width,yy=Math.floor(f/cols)*c.height;
        for(let y=0;y<c.height;y++)for(let x=0;x<c.width;x++){
          const dx=x-(c.width/2+window.__lightPosition.x*Math.min(c.width,c.height)*.5);
          const dy=y-(c.height*.58-window.__lightPosition.y*Math.min(c.width,c.height)*.5);
          if(dx*dx+dy*dy<25)continue;
          if(data[(y*c.width+x)*4+3]!==p.rgba[((yy+y)*sw+xx+x)*4+3])n++;
        }return n;
      },{i,f});
      alphaMismatches+=mismatch;framesChecked++;
    }
  }
  await page.selectOption('#sheet','34');
  await page.locator('#lightx').fill('0');await page.locator('#gain').fill('1');
  await page.screenshot({path:path.join(__dirname,'review_desktop.png'),fullPage:false});
  await page.locator('#gain').fill('0');
  const zeroGainEqual=await page.evaluate(()=>{
    const a=document.getElementById('diffuse'),b=document.getElementById('specular');
    const x=a.getContext('2d').getImageData(0,0,a.width,a.height).data,y=b.getContext('2d').getImageData(0,0,b.width,b.height).data;
    return x.every((v,i)=>v===y[i]);
  });
  await page.locator('#gain').fill('0.6');
  await page.selectOption('#sheet','7');
  await page.locator('#animate').check();await page.locator('#moving').check();
  const start=await page.evaluate(()=>window.__review.frame);await page.waitForTimeout(700);
  const end=await page.evaluate(()=>window.__review.frame);
  await page.setViewportSize({width:390,height:844});
  await page.screenshot({path:path.join(__dirname,'review_mobile.png'),fullPage:false});
  const report={page_errors:errors,failed_requests:failed,...await page.evaluate(()=>({ready:window.__review.ready,loaded_images:window.__review.loadedImages,card_count:document.querySelectorAll('.card').length,overflow:document.documentElement.scrollWidth>innerWidth})),frames_checked:framesChecked,preview_alpha_mismatches:alphaMismatches,zero_gain_equal:zeroGainEqual,animation_frame_changed:start!==end,sheets:tested};
  fs.writeFileSync(path.join(__dirname,'review_validation.json'),JSON.stringify(report,null,2));
  console.log(JSON.stringify(report));
  await browser.close();
  if(errors.length||failed.length||!report.ready||report.overflow||!report.animation_frame_changed||alphaMismatches||!zeroGainEqual||framesChecked!==142)process.exitCode=1;
})();
