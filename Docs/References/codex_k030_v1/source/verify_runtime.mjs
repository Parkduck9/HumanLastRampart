// 최소 DOM 모형 검사다. 실제 브라우저의 화면 배치나 렌더링 검사가 아니다.
export async function verify(input) {
 const els={},draws=[];let raf;
 const ctx=new Proxy({drawImage:(...a)=>draws.push(a),createPattern:()=>({})},{get:(t,k)=>t[k]||(()=>{})});
 const defaults={zoom:'1',who:'archer',action:'dig',fps:'5',frame:'0',background:''};
 function element(id){return els[id]??= {id,value:defaults[id]??'',children:[],dataset:{},append(x){this.children.push(x)},setAttribute(k,v){this[k]=v},getContext(){return ctx}}}
 const imgs=input.assets.filter(a=>a.view!=='action').map(a=>({dataset:{id:a.id}}));
 const document={getElementById:element,createElement:tag=>({tag,children:[],append(x){this.children.push(x)}}),querySelectorAll:()=>imgs};
 class Image{set src(v){this._src=v;this.onload?.()}get src(){return this._src}}
 const packed=Object.fromEntries([...input.assets.map(a=>[a.id,'mock:'+a.id]),['reference_archer','ref:archer'],['reference_gunner','ref:gunner'],['k029_grass','ref:grass']]);
 const code=input.script.replace('__PACKED__',JSON.stringify(packed)).replace('__MANIFEST__',JSON.stringify({assets:input.assets}));
 new Function('document','Image','requestAnimationFrame',code)(document,Image,fn=>{raf=fn});
 await Promise.resolve();await Promise.resolve();await Promise.resolve();
 const checks=[];const ck=(name,b)=>checks.push({name,pass:!!b});
 ck('그림 25종 로드',els.loading.textContent.includes('25종'));
 ck('갤러리 8종',els.gallery.children.length===8);ck('규격표 22행',els.specs.children.length===22);ck('최초 프레임',els.frameLabel.textContent==='1 / 4');
 els.next.onclick();ck('다음 프레임과 정지',els.frameLabel.textContent==='2 / 4'&&els.play['aria-pressed']==='false');
 els.frame.value='3';els.frame.oninput();ck('수동 마지막 프레임',els.frameLabel.textContent==='4 / 4');els.next.onclick();ck('마지막 다음은 첫칸',els.frameLabel.textContent==='1 / 4');
 for(const who of ['archer','gunner'])for(const act of ['dig','earth','log']){els.who.value=who;els.action.value=act;els.action.onchange();ck(who+'_'+act+' 선택',els.strip.src==='mock:'+who+'_'+act&&els.reference.src==='ref:'+who&&els.frameLabel.textContent==='1 / 4')}
 els.zoom.value='4';els.zoom.onchange();ck('4배 확대',imgs.every(i=>i.width===input.assets.find(a=>a.id===i.dataset.id).width*4));
 els.background.value='light';els.background.onchange();ck('밝은 배경 선택',els.gallery.className==='grid light');
 els.play.onclick();raf(1000);ck('재생 프레임 진행',els.frameLabel.textContent==='2 / 4');
 ck('지도 9회와 프레임 11회 그리기',draws.length===20);
 return {environment:'V8 내 최소 DOM/Canvas 모형. 실제 브라우저 렌더링 아님.',drawCount:draws.length,checks};
}
