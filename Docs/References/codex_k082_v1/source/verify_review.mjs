import fs from 'node:fs/promises';
import path from 'node:path';
import vm from 'node:vm';
import zlib from 'node:zlib';
export async function verify(root){
 const out=path.join(root,'Docs/References/codex_k082_v1');
 const html=await fs.readFile(path.join(root,'_HTML/6_그림_화면/codex_K-082_정비결과6판_v1.html'),'utf8');
 const matches=[...html.matchAll(/data:image\/png;base64,([A-Za-z0-9+/=]+)/g)];
 const decoded=[];
 function decode(s){
  const b=Buffer.from(s.replace(/^data:image\/png;base64,/,''),'base64');
  if(b.subarray(0,8).toString('hex')!=='89504e470d0a1a0a')throw Error('PNG 서명 오류');
  let offset=8,w,h,depth,type,ended=false;const chunks=[];
  while(offset<b.length){const size=b.readUInt32BE(offset),name=b.toString('ascii',offset+4,offset+8),data=b.subarray(offset+8,offset+8+size);if(name==='IHDR'){w=data.readUInt32BE(0);h=data.readUInt32BE(4);depth=data[8];type=data[9];}if(name==='IDAT')chunks.push(data);if(name==='IEND')ended=true;offset+=size+12;}
  const raw=zlib.inflateSync(Buffer.concat(chunks));const channels={0:1,2:3,3:1,4:2,6:4}[type];
  if(!w||!h||!ended||raw.length!==(Math.ceil(w*channels*depth/8)+1)*h)throw Error('PNG 해독 오류');
  return {width:w,height:h};
 }
 for(const m of matches)decoded.push(decode(m[1]));
 const draws=[];const controls={play:{},frame:{value:0},speed:{value:'6'},bg:{},status:{}};
 const canvases=[...html.matchAll(/<canvas\b([^>]+)>/g)].map(m=>{
  const attrs=Object.fromEntries([...m[1].matchAll(/([\w-]+)="([^"]*)"/g)].map(m=>[m[1],m[2]]));
  return {width:+attrs.width,height:+attrs.height,dataset:{sheet:attrs['data-sheet']},classList:{contains:n=>attrs.class===n},getContext:()=>({clearRect(){},drawImage(...args){draws.push(args.slice(1));}})};
 });
 const images=[...html.matchAll(/<img\b[^>]*src="([^"]+)"/g)].map(m=>({complete:true,naturalWidth:decode(m[1]).width}));
 let raf;const sandbox={document:{querySelectorAll:s=>s==='canvas'?canvases:images,getElementById:id=>controls[id],body:{dataset:{}}},window:{},requestAnimationFrame:fn=>raf=fn,Image:class{set src(s){decode(s);this.onload();}}};
 const script=html.match(/<script>([\s\S]*?)<\/script>/)[1];
 new vm.Script(script).runInNewContext(sandbox);
 const initial=sandbox.window.reviewState();if(initial.loaded!==17||initial.total!==17||initial.broken!==0)throw Error('이미지 로딩 모의 검사 실패');
 raf(200);const animated=sandbox.window.reviewState();if(animated.frame!==1)throw Error('동작 진행 실패');
 controls.frame.oninput({target:{value:'3'}});const manual=sandbox.window.reviewState();if(manual.frame!==3||manual.playing)throw Error('동작 칸 선택 실패');
 controls.play.onclick();if(!sandbox.window.reviewState().playing)throw Error('재생 재개 실패');
 controls.bg.onchange({target:{value:'light'}});if(sandbox.document.body.dataset.bg!=='light')throw Error('배경 전환 실패');
 if(/(?:src|href)="https?:|<link|<script\s+src=/.test(html))throw Error('외부 의존성 발견');
 const result={pass:true,embedded_pngs_decoded:decoded.length,canvases_loaded_in_mock:17,motion_canvases:12,static_game_canvases:5,animation_tick:true,frame_selection:true,pause_resume:true,background_switch:true,external_dependencies:0,browser_render_verified:false,browser_blocker:'No browsers in CUA inventory; iab unavailable. PNG decoding and mock DOM checks are not a browser render.'};
 await fs.writeFile(path.join(out,'review_validation.json'),JSON.stringify(result,null,2));return result;
}
