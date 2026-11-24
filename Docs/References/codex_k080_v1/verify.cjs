const fs=require('fs'),path=require('path'),assert=require('assert'),crypto=require('crypto');
const base='C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/';
const sharp=require(base+'sharp'),{chromium}=require(base+'playwright'),{pathToFileURL}=require('url');
const P=__dirname,R=path.resolve(P,'../../..');
// 시트 크기·발선·알파·연결성 및 실제 로컬 검토 페이지를 검사한다.
(async()=>{
 const m=JSON.parse(fs.readFileSync(path.join(P,'manifest.json'))),records=[];
 for(const s of m.sheets){
  const native=await sharp(path.join(P,s.native)).ensureAlpha().raw().toBuffer({resolveWithObject:true}),game=await sharp(path.join(P,s.file)).raw().toBuffer({resolveWithObject:true});
  assert.deepStrictEqual([native.info.width,native.info.height],s.native_size);assert.deepStrictEqual([game.info.width,game.info.height],s.game_size);
  const nearest=await sharp(path.join(P,s.native)).resize(s.game_size[0],s.game_size[1],{kernel:'nearest'}).raw().toBuffer();assert(nearest.equals(game.data));
  const preview=await sharp(path.join(P,s.preview)).raw().toBuffer();const previewExpected=await sharp(path.join(P,s.native)).resize(s.native_size[0]*4,s.native_size[1]*4,{kernel:'nearest'}).raw().toBuffer();assert(preview.equals(previewExpected));
  for(let f=0;f<s.frame_count;f++){
   const raw=await sharp(path.join(P,s.native)).extract({left:(f%s.columns)*96,top:Math.floor(f/s.columns)*128,width:96,height:128}).raw().toBuffer();
   let x0=96,x1=0,y0=128,y1=0,opaque=0,hx0=96,hx1=0;const seen=new Uint8Array(96*128),groups=[];
   for(let y=0;y<128;y++)for(let x=0;x<96;x++){let i=y*96+x,a=raw[i*4+3];assert(a===0||a===255);if(!a){assert.equal(raw[i*4]+raw[i*4+1]+raw[i*4+2],0);continue;}opaque++;x0=Math.min(x0,x);x1=Math.max(x1,x+1);y0=Math.min(y0,y);y1=Math.max(y1,y+1);}
   assert.equal(y1,120);assert.equal(y1-y0,73);assert(x0>0&&x1<96);
   for(let y=y0;y<y0+25;y++)for(let x=0;x<96;x++)if(raw[(y*96+x)*4+3]){hx0=Math.min(hx0,x);hx1=Math.max(hx1,x+1);}assert(Math.abs((hx0+hx1)/2-48)<=0.5);
   for(let i=0;i<seen.length;i++)if(raw[i*4+3]&&!seen[i]){const q=[i];seen[i]=1;for(let k=0;k<q.length;k++){let n=q[k],x=n%96,y=Math.floor(n/96);for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){let xx=x+dx,yy=y+dy,j=yy*96+xx;if(xx>=0&&xx<96&&yy>=0&&yy<128&&!seen[j]&&raw[j*4+3]){seen[j]=1;q.push(j);}}}groups.push(q.length);}
   assert.equal(groups.length,1);records.push({sheet:s.file,frame:f,bbox:[x0,y0,x1,y1],opaque_pixels:opaque,components:groups.length,head_axis:(hx0+hx1)/2});
  }
 }
 const expected={'hero_back_idle.png':'dc29add2be5beb8661b24e6ca9eb1152294fb54eff312218636f71284ce37067','hero_walk_run_grid.png':'3ac773e6922c1ae1a3281730fec4ed26679fff808f448e0d0e3e96550d085c15','hero_command.png':'1da7d9a82b63c0aabb2603cc48f1004ffa1a75ccaa2e8ca6ece97243496155d7'};
 for(const [name,h] of Object.entries(expected)){const actual=crypto.createHash('sha256').update(fs.readFileSync(path.join(R,'Game/Assets/_Project/Art/Characters/Hero',name))).digest('hex');assert.equal(actual,h);}
 const browser=await chromium.launch({channel:'chrome',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:1400,height:1000}}),errors=[];page.on('pageerror',e=>errors.push(String(e)));
  await page.goto(pathToFileURL(path.join(R,'_HTML/4_그림_캐릭터/codex_K-080_주인공동작_v1.html')).href);await page.evaluate(()=>window.ready);
  await page.locator('img').evaluateAll(xs=>Promise.all(xs.map(x=>x.decode())));assert.equal(await page.locator('canvas').count(),12);
  const t=await page.evaluate(()=>window.frameTick);await page.waitForTimeout(400);assert((await page.evaluate(()=>window.frameTick))>t);
  await page.locator('#toggle').click();const paused=await page.evaluate(()=>window.frameTick);await page.waitForTimeout(250);assert.equal(await page.evaluate(()=>window.frameTick),paused);
  await page.locator('#step').click();assert.equal(await page.evaluate(()=>window.frameTick),paused+1);
  await page.screenshot({path:path.join(P,'review_desktop.png')});await page.setViewportSize({width:390,height:844});
  assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));await page.screenshot({path:path.join(P,'review_mobile.png')});assert.deepStrictEqual(errors,[]);
  const result={passed:true,frames:records,game_sources_unchanged:true,native_alpha:'binary 0/255',nearest_3x_exact:true,nearest_4x_exact:true,review:{all_images_decoded:true,animated_canvases:12,play_pause_step:true,mobile_no_horizontal_overflow:true,console_errors:errors},unity_run:false};
  fs.writeFileSync(path.join(P,'validation.json'),JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify({passed:true,frames:records.length,review:result.review}));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});
