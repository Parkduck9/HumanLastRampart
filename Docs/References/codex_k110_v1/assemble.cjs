// 생성 원안을 정리하고 지정한 K-110 산출물만 만든다.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const u=require('../codex_k102_v1/build.cjs');
const {pngRead,blank,crop,resize,blit,bounds,trim}=u;
const OUT=__dirname,ROOT=path.resolve(OUT,'../../..');
const PNG=require('C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/pngjs').PNG;
const assets=[],framesByName={};
function save(file,im){fs.mkdirSync(path.dirname(path.join(OUT,file)),{recursive:true});fs.writeFileSync(path.join(OUT,file),PNG.sync.write({width:im.w,height:im.h,data:im.d}));}
function read(file){return pngRead(path.join(OUT,file));}
function groups(im,threshold=128){let seen=new Uint8Array(im.w*im.h),list=[];for(let i=0;i<seen.length;i++){if(seen[i]||im.d[i*4+3]<threshold)continue;let q=[i],l=im.w,t=im.h,r=0,b=0;seen[i]=1;for(let p=0;p<q.length;p++){let n=q[p],x=n%im.w,y=Math.floor(n/im.w);l=Math.min(l,x);r=Math.max(r,x);t=Math.min(t,y);b=Math.max(b,y);for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){let xx=x+dx,yy=y+dy,j=yy*im.w+xx;if(xx<0||xx>=im.w||yy<0||yy>=im.h||seen[j]||im.d[j*4+3]<threshold)continue;seen[j]=1;q.push(j);}}list.push({q,box:[l,t,r+1,b+1],n:q.length});}return list.sort((a,b)=>b.n-a.n);}
function isolate(im,g){let out=blank(im.w,im.h);for(let i of g.q)im.d.copy(out.d,i*4,i*4,i*4+4);return trim(out);}
function clean(im){let g=groups(im)[0],a=isolate(im,g);for(let i=0;i<a.d.length;i+=4){if(a.d[i+3]){a.d[i+3]=255;for(let k=0;k<3;k++)a.d[i+k]=Math.min(255,Math.round(a.d[i+k]/17)*17);}}return a;}
function deliver(name,im,meta){save(name,im);assets.push({file:name,size:[im.w,im.h],...meta});}
function sheets(name,frames,meta={}){let w=frames[0].w,h=frames[0].h,s=blank(w*frames.length,h);frames.forEach((f,i)=>blit(s,f,i*w,0));framesByName[name]=frames;
 const base={cell:[w,h],frames:frames.length,frame_order:frames.map((_,i)=>i),filter:'Point',alpha:'Straight RGBA',...meta};
 deliver('native/'+name+'.png',s,base);deliver('game/'+name+'.png',resize(s,s.w*3,s.h*3),{...base,cell:[w*3,h*3],pivot_px:base.pivot_px?.map(v=>v*3),baseline_y:base.baseline_y===undefined?undefined:base.baseline_y*3});
 deliver('previews/'+name+'_4x.png',resize(s,s.w*4,s.h*4),{role:'preview',source:'native/'+name+'.png'});
}
let extraction={};
for(let v of ['a','b']){let src=read('source/worker_'+v+'.png'),gs=groups(src).filter(g=>g.n>10000);extraction[v]={source:[src.w,src.h],components:gs.map(g=>({box:g.box,n:g.n}))};
 if(gs.length!==16)throw Error('일꾼 원안 연결 덩어리 수 '+v+': '+gs.length);
 let ordered=[];for(let row=0;row<4;row++){let rowg=gs.filter(g=>Math.floor(((g.box[1]+g.box[3])/2)/src.h*4)===row).sort((a,b)=>a.box[0]-b.box[0]);if(rowg.length!==4)throw Error('행 추출 실패');ordered.push(...rowg);}
 let scale=73/(ordered.slice(0,4).reduce((s,g)=>s+g.box[3]-g.box[1],0)/4);
 let frames=ordered.map((g,index)=>{let cut=isolate(src,g),small=resize(cut,Math.round(cut.w*scale),Math.round(cut.h*scale));small=clean(small);let cell=blank(96,128),bb=bounds(small),lo=small.w,hi=0;for(let y=small.h-7;y<small.h;y++)for(let x=0;x<small.w;x++)if(small.d[(y*small.w+x)*4+3]){lo=Math.min(lo,x);hi=Math.max(hi,x);}let dx=48-Math.round((lo+hi)/2),dy=121-small.h;blit(cell,small,dx,dy);return cell;});
 sheets('worker_'+v+'_idle',frames.slice(0,4),{role:'worker',variant:v,motion:'idle',fps:4,loop:true,pivot_px:[48,120],baseline_y:120,center_x:48,frame_labels:['숨 들이쉬기','가슴 펴기','숨 내쉬기','자세 회복']});
 sheets('worker_'+v+'_push',frames.slice(4,12),{role:'worker',variant:v,motion:'push',fps:8,loop:true,pivot_px:[48,120],baseline_y:120,center_x:48,frame_labels:['힘 받기','밀기 시작','왼발 지지','체중 이동','오른발 전진','버티기','밀어내기','다음 보폭 준비']});
 sheets('worker_'+v+'_rest',frames.slice(12),{role:'worker',variant:v,motion:'rest',fps:3,loop:false,pivot_px:[48,120],baseline_y:120,center_x:48,frame_labels:['숨 고르기','땀 닦기','주먹 환호','휴식']});
}
fs.writeFileSync(path.join(OUT,'extraction.json'),JSON.stringify(extraction,null,2));
let tower=clean(read('source/tower_base.png'));tower=resize(tower,344,244);tower=clean(tower);let towerCell=blank(384,320);blit(towerCell,tower,20,45);sheets('tower_base',[towerCell],{role:'tower_base',pivot_px:[240,288],baseline_y:288,attach_lamp_px:[250,72],door_px:[160,278],stair_top_px:[144,132],stair_bottom_px:[63,287]});
// 앞 기둥·문턱은 사람보다 앞에 겹치며 회전 손잡이는 별도 레이어로 제공한다.
let front=blank(384,320),capstan=blank(384,320);
for(let y=0;y<320;y++)for(let x=0;x<384;x++){let i=(y*384+x)*4;let pillar=(x>=183&&x<=210&&y>=144)||(x>=339&&y>=144);let sill=y>=263&&x>=132;if(pillar||sill)towerCell.d.copy(front.d,i,i,i+4);let bar=y>=207&&y<=246&&x>=211&&x<=337&&towerCell.d[i]>towerCell.d[i+2]*1.3&&towerCell.d[i]>towerCell.d[i+1]*1.1;if(bar)towerCell.d.copy(capstan.d,i,i,i+4);}
sheets('tower_foreground',[front],{role:'occlusion_overlay',pivot_px:[240,288]});sheets('capstan_contact_overlay',[capstan],{role:'capstan_overlay',pivot_px:[240,288],note:'기본 탑에 이미 장치가 포함되어 있다. 손·막대 접촉을 보정하는 앞쪽 복제 레이어.'});
let burstSrc=read('source/light_bursts.png'),burstGroups=groups(burstSrc,90).filter(g=>g.n>3000).slice(0,3).sort((a,b)=>a.box[0]-b.box[0]);if(burstGroups.length!==3)throw Error('빛 원안 추출 실패');
for(let i=0;i<3;i++){let c=isolate(burstSrc,burstGroups[i]),target=[76,96,116][i],s=resize(c,target,Math.round(c.h/c.w*target));let cell=blank(128,128);for(let p=0;p<s.d.length;p+=4){let a=s.d[p+3];if(a<90){s.d.fill(0,p,p+4);continue;}let lum=Math.max(...s.d.subarray(p,p+3));let rgb=lum>239?[255,248,204]:lum>208?[255,210,85]:[235,151,34];for(let k=0;k<3;k++)s.d[p+k]=rgb[k];s.d[p+3]=[110,180,245][i];}blit(cell,s,64-Math.floor(s.w/2),64-Math.floor(s.h/2));let keep=groups(cell,1)[0];let result=blank(128,128);for(let p of keep.q)cell.d.copy(result.d,p*4,p*4,p*4+4);sheets('light_burst_'+['low','normal','high'][i],[result],{role:'additive_burst',pivot_px:[64,64],blend:'SrcAlpha One',intensity:['low','normal','high'][i],attach_to:'tower_base lamp anchor',attach_to_native_px:[250,72]});}
// 같은 좌표의 참조 원본을 HTML과 목업에서 사용한다.
const referencePaths={battle:'Docs/References/beacon_game_20261022/05_pf_night_clean.png',day:'Docs/References/beacon_game_20261022/01_pf_day_clean.png',map:'Docs/References/beacon_game_20261022/08_pf_map_side.png',ally:'Game/Assets/_Project/Art/Characters/Fire/archer_fire.png',hero:'Docs/References/codex_k070_v1/hero_native_96x128.png',oldTower:'Docs/References/codex_k102_v1/native/beacon_tower_lit.png'};
const refs={};for(let [k,p] of Object.entries(referencePaths)){let b=fs.readFileSync(path.join(ROOT,p));refs[k]={path:p,sha256:crypto.createHash('sha256').update(b).digest('hex'),uri:'data:image/png;base64,'+b.toString('base64')};}
const manifest={card:'K-110',version:1,status:'assets_only_integration_pending',generation:'built-in image_gen; prompts.json; native extraction and nearest-neighbor packing',coordinate_system:'top-left pixels; Unity normalized pivot = [x/width,1-y/height]',worker_native_cell:[96,128],worker_game_cell:[288,384],worker_foot_native:[48,120],worker_foot_game:[144,360],worker_unity_pivot:[0.5,0.0625],human_standing_height:73,variants:2,scale:3,frame_direction:'left_to_right',worker_facing:'right; mirror on X for left-facing placement',replacement:'none; no existing tower worker sheets found',tower_native_cell:[384,320],tower_game_cell:[1152,960],tower_foot_native:[240,288],tower_unity_pivot:[0.625,0.1],render_order:['burst behind lamp','tower_base','workers','optional capstan_contact_overlay','tower_foreground','hero'],rest_playback:'play once then hold frame 3; review repeats only to inspect all frames',push_playback:'8 frames at 8fps; repeat for commanded six-second turn',references:Object.fromEntries(Object.entries(refs).map(([k,v])=>[k,{path:v.path,sha256:v.sha256}])),limitations:['Provided low battle camera does not show courtyard centre; battle mockup foreground placement is an illustrative cutaway, not validated world position.','Architecture enlarges K-102 bowl/base vocabulary to fit human-scale workers.','Contact overlay is static artwork; actual rotating capstan and collision/stair traversal belong to Claude integration.','No Unity import, Play, material or camera validation.'],assets};
fs.writeFileSync(path.join(OUT,'manifest.json'),JSON.stringify(manifest,null,2));
// 검토 HTML에는 모든 그림을 내장한다.
const uri=im=>'data:image/png;base64,'+PNG.sync.write({width:im.w,height:im.h,data:im.d}).toString('base64');
const atlasUris={};for(let name of Object.keys(framesByName))atlasUris[name]=uri(read('native/'+name+'.png'));
fs.writeFileSync(path.join(OUT,'review_data.json'),JSON.stringify({atlasUris,refs}));
console.log(JSON.stringify({workerComponents:extraction,assets:assets.length,towerBounds:bounds(towerCell)}));
