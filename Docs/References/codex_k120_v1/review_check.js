const fs=require('fs'),path=require('path'),crypto=require('crypto');
const {pathToFileURL}=require('url');
const {chromium}=require('C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=__dirname,repo=path.resolve(root,'../../..');
const assert=(value,message)=>{if(!value)throw Error(message);};
(async()=>{
 const manifest=JSON.parse(fs.readFileSync(path.join(root,'manifest.json'),'utf8'));
 const hs=JSON.parse(fs.readFileSync(path.join(root,'hotspots.json'),'utf8'));
 const pixel=JSON.parse(fs.readFileSync(path.join(root,'pixel_verification.json'),'utf8').replace(/^\uFEFF/,''));
 for(const a of manifest.assets){const b=fs.readFileSync(path.join(root,a.file));assert(b.readUInt32BE(16)===a.size_px[0]&&b.readUInt32BE(20)===a.size_px[1],'크기 불일치');}
 for(const s of hs.stations){for(const r of [s.bbox_px,s.suggested_approach_rect_px])assert(r[0]>=0&&r[1]>=0&&r[2]>0&&r[3]>0&&r[0]+r[2]<=3840&&r[1]+r[3]<=1080,'경계 범위 오류');assert(s.bbox_px.every((v,i)=>v===s.bbox_native_px[i]*3),'원본 좌표 배율 오류');const [x,y,w,h]=s.bbox_px;assert(s.door_ground_px[0]>=x&&s.door_ground_px[0]<x+w&&s.door_ground_px[1]>=y&&s.door_ground_px[1]<y+h,'문 바닥 범위 오류');}
 for(const f of manifest.read_only_source_hashes)assert(crypto.createHash('sha256').update(fs.readFileSync(path.join(repo,f.path))).digest('hex').toUpperCase()===f.sha256,'읽기 전용 원본 변경');
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 const page=await browser.newPage({viewport:{width:1500,height:1100},deviceScaleFactor:1});
 const errors=[],network=[];page.on('pageerror',e=>errors.push(String(e)));page.on('request',r=>{if(/^https?:/.test(r.url()))network.push(r.url());});
 await page.goto(pathToFileURL(path.join(repo,'_HTML/6_그림_화면/codex_K-120_정비마당_v1.html')).href);
 await page.waitForFunction(()=>document.body.dataset.loaded==='true');
 const dimensions=await page.locator('img').evaluateAll(images=>images.map(im=>[im.naturalWidth,im.naturalHeight]));
 assert(JSON.stringify(dimensions)===JSON.stringify([[3840,1080],[3840,1080],[1920,1080]]),'브라우저 이미지 크기 오류');
 assert(await page.locator('.building').count()===5,'건물 경계 수 오류');
 await page.locator('#boxes').uncheck();assert(await page.locator('#overlay').evaluate(el=>el.classList.contains('hide-boxes')),'경계 숨기기 오류');
 await page.locator('#boxes').check();await page.locator('#approaches').check();assert(await page.locator('#overlay').evaluate(el=>el.classList.contains('show-approach')),'접근 영역 오류');
 await page.locator('#approaches').uncheck();
 await page.locator('#crop').click();
 await page.locator('#pan').fill('0');await page.locator('#pan').dispatchEvent('input');assert(await page.locator('.world').first().evaluate(el=>el.style.left)==='0%','왼쪽 보기 오류');
 await page.locator('#pan').fill('100');await page.locator('#pan').dispatchEvent('input');assert(await page.locator('.world').first().evaluate(el=>el.style.left)==='-100%','오른쪽 보기 오류');
 for(let i=0;i<5;i++){await page.locator('#jump').selectOption(String(i));assert(await page.locator('#compare').evaluate(el=>el.classList.contains('cropped')),'장소 보기 오류');}
 await page.locator('#full').click();
 await page.screenshot({path:path.join(root,'review_desktop.png'),fullPage:true});
 await page.locator('#crop').click();await page.locator('#jump').selectOption('4');await page.screenshot({path:path.join(root,'review_viewport.png'),fullPage:false});
 await page.setViewportSize({width:390,height:844});await page.locator('#full').click();
 assert(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),'휴대폰 가로 넘침');
 await page.screenshot({path:path.join(root,'review_mobile.png'),fullPage:true});
 assert(errors.length===0,'브라우저 스크립트 오류');assert(network.length===0,'외부 리소스 요청');
 const result={result:'pass',browser:'Google Chrome, headless Playwright',loaded_image_sizes:dimensions,building_boxes:5,controls_checked:['hide/show boxes','approach toggle','1920px crop','pan left/right','five place jumps','full panorama'],viewports:[[1500,1100],[390,844]],mobile_horizontal_overflow:false,external_network_requests:network.length,page_errors:errors,source_sha256_unchanged:manifest.read_only_source_hashes.map(f=>f.path),pixel_verification:pixel,unity_run:false,game_integration_checked:false,foot_baseline_check:'Not applicable to an opaque static background; five separate door-ground coordinates checked within building bounds.',stray_alpha_check:'Fully opaque; zero nonopaque pixels. Visual inspection for floating props and padding artifacts is separate.'};
 fs.writeFileSync(path.join(root,'review_verification.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result,null,2));await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
