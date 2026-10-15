// 검토 페이지의 그림 로드와 조작을 실제 브라우저에서 확인한다.
const { chromium } = require('C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const path = require('path');
const fs = require('fs');
(async()=>{
 const root=path.resolve(__dirname,'../../../..');
 const browser=await chromium.launch({headless:true,channel:'msedge'});
 const page=await browser.newPage({viewport:{width:1280,height:960}});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('file:///'+path.join(root,'_HTML/codex_K-019_commands_v1.html').replaceAll('\\','/'));
 await page.waitForFunction(()=>[...document.images].every(i=>i.complete&&i.naturalWidth>0));
 const checks=[];const check=(name,pass)=>{checks.push({확인:name,통과:pass});if(!pass)throw Error(name)};
 check('명령 여섯 개',await page.locator('.choice').count()===6);
 await page.keyboard.press('3');check('숫자 3 강조',await page.locator('.choice.active .label').textContent()==='가까운 적부터');
 await page.locator('#bg').selectOption('light');check('밝은 배경',await page.locator('.scene.light').count()===1);
 await page.locator('.choice').nth(1).hover();check('마우스 올림',await page.locator('.choice.active .label').textContent()==='체력 높은 적부터');
 await page.locator('#bg').selectOption('night');
 await page.screenshot({path:path.join(root,'Docs/References/codex_k019_v1/review_capture.png'),fullPage:true});
 await page.locator('#mode').selectOption('three');check('처음 메뉴 세 개',await page.locator('.choice').count()===3);
 await page.keyboard.press('2');check('처음 메뉴 번호 선택',await page.locator('.choice.active .label').textContent()==='전령 부르기');
 await page.locator('#reset').click();check('선택 해제',await page.locator('.choice.active').count()===0);
 check('내장 그림 전체 로드',await page.evaluate(()=>[...document.images].every(i=>i.complete&&i.naturalWidth>0)));
 check('외부 리소스 없음',await page.evaluate(()=>[...document.images].every(i=>i.src.startsWith('data:'))));
 check('기본 화면 가로 넘침 없음',await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
 check('스크립트 오류 없음',errors.length===0);
 fs.writeFileSync(path.join(root,'Docs/References/codex_k019_v1/verification.json'),JSON.stringify({검사:checks,오류:errors,Unity실행:false},null,2));
 console.log('브라우저 검사',checks.length,'개 통과');await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
