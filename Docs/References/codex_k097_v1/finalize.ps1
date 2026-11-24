$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$out=$PSScriptRoot
$utf8=[System.Text.UTF8Encoding]::new($false)
function WriteUtf8($path,$value){[IO.File]::WriteAllText($path,$value,$utf8)}
$specs=@(
 @('instruction_box_9slice','명령서 판',96,48,1,0,@('기본')),
 @('hint_chip_9slice','힌트 판',48,24,1,0,@('기본')),
 @('officer_expressions','장교 표정',48,48,4,1,@('기본','설명 · 손들기','긴급','만족')),
 @('highlight_ring_pulse','강조 고리',64,64,6,8,@('수축','확장 1','확장 2','최대','수축 2','수축 1'))
)
foreach($d in @(@('up','위'),@('right','오른쪽'),@('down','아래'),@('left','왼쪽'))){$specs+=,@("pointer_bounce_$($d[0])","$($d[1]) 포인터",32,32,4,6,@('기준','접근','최대 접근','복귀'))}
$keys=@('Shift + 끌기','오른쪽 클릭','마우스 휠','E','Esc')
$steps=@('걷기','망원경','지도','분대','대포','마법','함정','망원경 업그레이드')
$specs+=,@('input_chips_24','입력 칩 24px',24,24,5,0,$keys)
$specs+=,@('input_chips_32','입력 칩 32px',32,32,5,0,$keys)
$specs+=,@('step_icons_24','단계 아이콘',24,24,8,0,$steps)
$pipOrder=@();foreach($s in $steps){foreach($state in @('대기','현재','완료')){$pipOrder+="$s · $state"}}
$specs+=,@('step_pips','단계 표시 부품',32,32,24,0,$pipOrder)
$specs+=,@('step_progress_strip','8단계 진행 띠',256,32,9,0,($steps+@('모두 완료')))
$check=@'
using System;
using System.Drawing;
using System.Collections.Generic;
public static class Check97 {
 public static string Frame(Bitmap b,int x0,int w,int h) {
  int minX=w,minY=h,maxX=-1,maxY=-1;bool[,] seen=new bool[w,h];List<int> sizes=new List<int>();
  for(int y=0;y<h;y++)for(int x=0;x<w;x++){int a=b.GetPixel(x0+x,y).A;if(a!=0&&a!=255)throw new Exception("중간 알파");if(a==255){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}}
  if(maxX<0)throw new Exception("빈 프레임");
  for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(!seen[x,y]&&b.GetPixel(x0+x,y).A>0){int count=0;Queue<Point> q=new Queue<Point>();q.Enqueue(new Point(x,y));seen[x,y]=true;while(q.Count>0){Point p=q.Dequeue();count++;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int xx=p.X+dx,yy=p.Y+dy;if(xx>=0&&xx<w&&yy>=0&&yy<h&&!seen[xx,yy]&&b.GetPixel(x0+xx,yy).A>0){seen[xx,yy]=true;q.Enqueue(new Point(xx,yy));}}}sizes.Add(count);}
  return "{\"bbox_top_left_exclusive\":["+minX+","+minY+","+(maxX+1)+","+(maxY+1)+"],\"component_pixels\":["+string.Join(",",sizes)+"]}";
 }
 public static void Scale(Bitmap n,Bitmap b,int scale){if(b.Width!=n.Width*scale||b.Height!=n.Height*scale)throw new Exception("배율 크기 불일치");for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(n.GetPixel(x/scale,y/scale).ToArgb()!=b.GetPixel(x,y).ToArgb())throw new Exception("최근접 불일치");}
 public static void Slice(Bitmap b,int border){for(int y=border;y<b.Height-border;y++)for(int x=border;x<b.Width-border;x++)if(b.GetPixel(x,y).ToArgb()!=b.GetPixel(border,border).ToArgb())throw new Exception("9-slice 중앙 불균일");}
}
'@
Add-Type -TypeDefinition $check -ReferencedAssemblies System.Drawing
$assets=@();$checks=@()
foreach($s in $specs){
 $id=$s[0];$w=[int]$s[2];$h=[int]$s[3];$n=[int]$s[4]
 $native=[Drawing.Bitmap]::new("$out/${id}_native.png");$game=[Drawing.Bitmap]::new("$out/$id.png");$preview=[Drawing.Bitmap]::new("$out/${id}_4x.png")
 try{
  if($native.Width -ne $w*$n -or $native.Height -ne $h){throw "시트 크기 오류 $id"}
  [Check97]::Scale($native,$game,3);[Check97]::Scale($native,$preview,4)
  $frames=@();for($i=0;$i -lt $n;$i++){$frames+=([Check97]::Frame($native,($i*$w),$w,$h)|ConvertFrom-Json)}
  if($id -eq 'officer_expressions'){foreach($f in $frames){if($f.bbox_top_left_exclusive[3] -ne 46){throw '흉상 밑선 오류'};if($f.bbox_top_left_exclusive[0] -le 0 -or $f.bbox_top_left_exclusive[2] -ge 48){throw '흉상 칸 경계 접촉'};if(($f.component_pixels|Measure-Object -Minimum).Minimum -lt 4){throw '흉상 고립점'}}}
  $a=[ordered]@{id=$id;label_ko=$s[1];native_file="${id}_native.png";game_file="$id.png";preview_4x_file="${id}_4x.png";native_cell=@($w,$h);game_cell=@(($w*3),($h*3));native_sheet=@(($w*$n),$h);game_sheet=@(($w*$n*3),($h*3));columns=$n;rows=1;frame_count=$n;frame_order=$s[6];pivot_top_left_native=@(($w/2),($h/2));pivot_top_left_game=@(($w*1.5),($h*1.5));unity_pivot_normalized=@(0.5,0.5);fps=$s[5];loop=($s[5] -gt 0);frames=$frames}
  if($id -match '9slice'){
   [Check97]::Slice($native,8)
   $a.nine_slice_border_native=[ordered]@{left=8;bottom=8;right=8;top=8}
   $a.nine_slice_border_game=[ordered]@{left=24;bottom=24;right=24;top=24}
   $a.unity_image_type='Sliced';$a.center_mode='stretch';$a.edge_mode='stretch';$a.minimum_native_size=@(17,17);$a.text_safe_inset_native=@(10,9,10,9)
  }
  if($id -eq 'officer_expressions'){$a.bust_bottom_y_exclusive_native=46;$a.figure_foot_baseline=$null}
  if($id -eq 'step_pips'){$a.index_formula='stepIndex * 3 + stateIndex';$a.state_order=@('empty','current','done');$a.icon_size_native=@(24,24)}
  if($id -eq 'highlight_ring_pulse'){$a.reused_from='codex_k093_v1/highlight_ring_native.png'}
  $assets+=$a;$checks+=[pscustomobject]@{id=$id;dimensions=$true;binary_alpha=$true;nearest_3x=$true;nearest_4x=$true;frames=$n}
 }finally{$native.Dispose();$game.Dispose();$preview.Dispose()}
}
$sources=@('Docs/References/codex_hero_octopath_review_v6/generated.png','Game/Assets/_Project/Art/Characters/Hero/hero_command.png','Docs/References/codex_k093_v1/build.ps1','Docs/References/codex_k093_v1/officer_bust_native.png','Docs/References/codex_k093_v1/highlight_ring_native.png','Docs/References/codex_k093_v1/input_chips_24_native.png','Docs/References/codex_k093_v1/input_chips_32_native.png')
$hashes=@();foreach($src in $sources){$hashes+=@{path=$src;sha256=(Get-FileHash "$root/$src" -Algorithm SHA256).Hash}}
$manifest=[ordered]@{card='K-097';version=1;coordinate_origin='top_left';alpha='RGBA binary 0/255';filter='Point';compression='None';mipmaps=$false;native_to_game_scale=3;preview_scale=4;figure_contract_note_ko='전부 화면 안내 부품과 흉상이다. D-081 전신 96×128 및 발 y=120은 해당하지 않는다. 흉상은 K-093과 같은 48×48, 중앙 피벗 (24,24), 밑선 y=46.';integration_ko='게임 연결·Sprite Editor 설정·Unity 확인은 Claude 담당. 기존 게임용 파일 교체 없음.';step_order=$steps;assets=$assets;sources=$hashes;generation=@{mode='built-in imagegen';source_file='officer_expressions_generated.png';prompt_file='generation_prompt.json';postprocess_ko='동일 축척 최근접 축소·머리 중심 정렬·알파 이진화·17단위 색 양자화·4점 미만 고립 영역 제거'}}
WriteUtf8 "$out/manifest.json" ($manifest|ConvertTo-Json -Depth 12)
WriteUtf8 "$out/validation.json" (@{asset_checks=$checks;asset_count=$assets.Count;frame_count=($checks|Measure-Object frames -Sum).Sum;foot_line='not applicable: UI/bust only';bust_bottom_y_exclusive=46;source_hashes=$hashes;browser='See review_verification.json. Browser unavailable; Node VM checks only. Unity not tested.'}|ConvertTo-Json -Depth 8)
$imgs=[ordered]@{}
foreach($a in $assets){$imgs[$a.id]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes("$out/$($a.native_file)"))}
foreach($pair in @(@('old_bust','officer_bust_native.png'),@('old_box','instruction_box_native.png'),@('old_ring','highlight_ring_native.png'),@('old_pointer','pointer_down_native.png'),@('old_chips','input_chips_24_native.png'))){$imgs[$pair[0]]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes("$root/Docs/References/codex_k093_v1/$($pair[1])"))}
$imgs['game_hero']='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes("$root/Game/Assets/_Project/Art/Characters/Hero/hero_command.png"))
$html=@'
<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-097 · 튜토리얼 보충</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#191b20;color:#eee4ca;font:16px/1.6 'Malgun Gothic',sans-serif}main{max-width:1220px;margin:auto;padding:36px 24px}h1{font-size:34px;margin:8px 0}h2{font-size:23px;color:#e2bd68}p{color:#c4c1b9}.tag{color:#e2bd68;font-size:13px;letter-spacing:2px}.toolbar{position:sticky;top:0;z-index:3;background:#25262cf5;border:1px solid #62523a;padding:12px;display:flex;gap:12px;flex-wrap:wrap;align-items:center}button,select{font:inherit;color:#eee4ca;background:#37342e;border:1px solid #9c7943;padding:6px 12px;cursor:pointer}section{margin:28px 0;border:1px solid #46423a;background:#24252b;padding:22px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:18px}.stage{background:repeating-conic-gradient(#303139 0% 25%,#383940 0% 50%) 50%/16px 16px;padding:16px;min-height:170px;display:flex;gap:15px;flex-wrap:wrap;align-items:center;justify-content:center}canvas,img{image-rendering:pixelated;max-width:100%;object-fit:contain}.portrait{display:grid;grid-template-columns:repeat(4,1fr);gap:10px;text-align:center}.portrait canvas{width:144px}.label{font-size:13px;color:#bab6a9}.wide{overflow:auto}.wide canvas{max-width:none}.piplabels{display:flex;gap:12px;flex-wrap:wrap}.piplabels span{width:104px;text-align:center;font-size:13px}.example{background:#303139;padding:16px;overflow:auto}.example canvas{max-width:none}.ok{color:#accca0}label{display:inline-flex;align-items:center;gap:8px}a{color:#e2bd68}@media(max-width:750px){.grid{grid-template-columns:1fr}.portrait{grid-template-columns:repeat(2,1fr)}main{padding:18px 12px}.toolbar{position:static}}
</style><main><div class="tag">HUMAN LAST RAMPART · K-097</div><h1>튜토리얼 보충 그림</h1><p>장교의 네 표정 · 늘어나는 안내 판 · 강조 동작 · 여덟 단계 진행 표시 · 입력 칩<br>그림 검토용입니다. 튜토리얼 판정과 게임 연결은 포함하지 않습니다.</p>
<div class="toolbar"><button id="pause">일시정지</button><button id="next">한 칸 넘기기</button><label>진행 단계 <select id="step"></select></label><span id="status">그림 불러오는 중</span></div>
<section><h2>장교 · 같은 인물, 네 표정</h2><div class="portrait" id="portraits"></div><div class="grid"><div><p>기존 K-093 흉상</p><div class="stage"><canvas data-sprite="old_bust" data-w="48" data-h="48" data-scale="3"></canvas></div></div><div><p>현재 게임 장교 동작 · 등 모습</p><div class="stage"><canvas data-sprite="game_hero" data-w="288" data-h="384" data-count="3" data-fps="3" data-scale="0.5"></canvas></div></div></div><p class="label">현재 게임 그림 폴더에는 튜토리얼 전용 시트가 없습니다. 기존 K-093 제작 자료와 실제 게임의 장교 동작을 나란히 표시했습니다. 새 흉상은 48×48칸, 중앙 피벗, 밑선 46px로 맞췄습니다.</p></section>
<section><h2>명령서와 힌트 · 9-slice</h2><label>안내 판 너비 <input id="width" type="range" min="180" max="900" value="720" step="12"><output id="widthValue">720px</output></label><div class="example"><canvas id="sliced"></canvas></div><p>게임용 Sprite Editor 테두리: 왼쪽·아래·오른쪽·위 모두 <strong>24px</strong>. 원본은 모두 <strong>8px</strong>. 모서리는 고정하고 안쪽을 늘립니다.</p><div class="grid"><div><p>기존 명령서 판</p><div class="stage"><canvas data-sprite="old_box" data-w="96" data-h="48" data-scale="3"></canvas></div></div><div><p>새 힌트 판 · 폭을 늘린 예</p><div class="stage"><canvas id="hint"></canvas></div></div></div><p class="label">글자는 그림에 넣지 않았습니다. 아래 시연 문장은 검토 페이지에서만 표시합니다.</p></section>
<section><h2>강조 고리와 포인터</h2><div class="grid"><div><p>기존 K-093 · 고리 6칸 / 포인터 6칸</p><div class="stage"><canvas data-sprite="old_ring" data-w="64" data-h="64" data-count="6" data-fps="8" data-scale="2"></canvas><canvas data-sprite="old_pointer" data-w="32" data-h="32" data-count="6" data-fps="6" data-scale="3"></canvas></div></div><div><p>K-097 · 고리 6칸 / 포인터 4칸</p><div class="stage"><canvas data-sprite="highlight_ring_pulse" data-w="64" data-h="64" data-count="6" data-fps="8" data-scale="2"></canvas><canvas data-sprite="pointer_bounce_down" data-w="32" data-h="32" data-count="4" data-fps="6" data-scale="3"></canvas></div></div></div><div class="stage" id="directions"></div><p class="label">고리는 검토된 K-093 원본을 그대로 재사용했습니다. 네 방향 포인터는 기준 → 접근 → 최대 접근 → 복귀의 4칸입니다.</p></section>
<section><h2>여덟 단계 진행 띠</h2><div class="stage wide"><canvas id="progress"></canvas></div><div class="piplabels" id="stepLabels"></div><p id="stepText"></p><p class="label">단계마다 대기(회색), 현재(금색), 완료(초록 테두리·확인 표시) 3상태를 제공합니다. 대포는 설명 단계입니다.</p><div class="stage wide"><canvas data-sprite="step_pips" data-w="768" data-h="32" data-scale="2"></canvas></div></section>
<section><h2>추가 입력 칩</h2><p>Shift + 끌기 / 오른쪽 클릭 / 마우스 휠 / E / Esc</p><div class="grid"><div><p>24px 원본 · 4배 확대</p><div class="stage"><canvas data-sprite="input_chips_24" data-w="120" data-h="24" data-scale="4"></canvas></div></div><div><p>32px 원본 · 3배 확대</p><div class="stage"><canvas data-sprite="input_chips_32" data-w="160" data-h="32" data-scale="3"></canvas></div></div></div><p>기존 K-093 입력 칩 · 앞쪽 일부</p><div class="stage"><canvas data-sprite="old_chips" data-w="168" data-h="24" data-scale="3"></canvas></div><p class="label">우클릭·휠·E는 기존 칩과 픽셀이 같습니다. Shift + 끌기와 Esc를 보충했습니다. 키 표기는 실제 키보드 표기를 따릅니다.</p></section>
<p>모든 그림과 스크립트를 내장했습니다. 서버와 인터넷 없이 이 파일을 직접 열 수 있습니다. Unity 실행·연결 검증은 수행하지 않았습니다.</p></main>
<script>
const sources=__IMAGES__, steps=__STEPS__, images={};let paused=false,ticks=0,loaded=0,errors=0;const spriteCanvases=[];
window.addEventListener('error',()=>{errors++;document.querySelector('#status').textContent='오류가 발생했습니다'});
function canvas(id,w,h,scale=3,count=1,fps=0,index=0){const c=document.createElement('canvas');Object.assign(c.dataset,{sprite:id,w,h,scale,count,fps,index});return c}
['기본','설명 · 손들기','긴급','만족'].forEach((name,i)=>{const div=document.createElement('div');div.append(canvas('officer_expressions',48,48,3,1,0,i),document.createElement('br'),name);document.querySelector('#portraits').append(div)});
['up','right','down','left'].forEach(d=>document.querySelector('#directions').append(canvas('pointer_bounce_'+d,32,32,3,4,6)));
const sel=document.querySelector('#step');[...steps,'모두 완료'].forEach((s,i)=>sel.add(new Option(s,i)));steps.forEach((s,i)=>{const span=document.createElement('span');span.textContent=(i+1)+'. '+s;document.querySelector('#stepLabels').append(span)});
function drawSprite(c){const {sprite,w,h,scale=3,count=1,fps=0,index=0}=c.dataset;const f=+fps?Math.floor(ticks*(+fps))%(+count):(+index);c.width=Math.round(w*scale);c.height=Math.round(h*scale);const ctx=c.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.drawImage(images[sprite],f*w,0,+w,+h,0,0,c.width,c.height);c.dataset.frame=f}
function slice(c,id,w,h,text){c.width=w;c.height=h;const im=images[id],ctx=c.getContext('2d'),b=24,x=[0,8,im.width-8,im.width],y=[0,8,im.height-8,im.height],dx=[0,b,w-b,w],dy=[0,b,h-b,h];ctx.imageSmoothingEnabled=false;for(let j=0;j<3;j++)for(let i=0;i<3;i++)ctx.drawImage(im,x[i],y[j],x[i+1]-x[i],y[j+1]-y[j],dx[i],dy[j],dx[i+1]-dx[i],dy[j+1]-dy[j]);ctx.fillStyle=id==='hint_chip_9slice'?'#dcc58d':'#211e26';ctx.font='bold 18px Malgun Gothic';ctx.fillText(text,30,Math.round(h/2+7))}
function drawUI(){const w=+document.querySelector('#width').value;document.querySelector('#widthValue').value=w+'px';slice(document.querySelector('#sliced'),'instruction_box_9slice',w,144,w<360?'북쪽으로 이동하자.':'북쪽으로 이동해 망원경을 확인하자.');slice(document.querySelector('#hint'),'hint_chip_9slice',360,72,'Shift + 끌기로 분대 선택');const c=document.querySelector('#progress');c.width=1024;c.height=128;const ctx=c.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.drawImage(images.step_progress_strip,+sel.value*256,0,256,32,0,0,1024,128);document.querySelector('#stepText').textContent=+sel.value===8?'여덟 단계 모두 완료':(+sel.value+1)+' / 8 · '+steps[+sel.value]}
async function init(){await Promise.all(Object.entries(sources).map(([id,src])=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{images[id]=im;loaded++;resolve()};im.onerror=()=>{errors++;reject(new Error(id))};im.src=src})));document.querySelectorAll('canvas[data-sprite]').forEach(c=>spriteCanvases.push(c));drawUI();spriteCanvases.forEach(drawSprite);document.querySelector('#status').textContent='그림 '+loaded+'개 로드 완료';document.querySelector('#status').className='ok';window.reviewReady=true;window.reviewMetrics={loaded,errors};let last=0;function frame(ts){if(!paused&&last)ticks+=(ts-last)/1000;last=ts;if(!paused)spriteCanvases.filter(c=>+c.dataset.fps).forEach(drawSprite);requestAnimationFrame(frame)}requestAnimationFrame(frame)}
document.querySelector('#pause').onclick=()=>{paused=!paused;document.querySelector('#pause').textContent=paused?'재생':'일시정지'};document.querySelector('#next').onclick=()=>{paused=true;document.querySelector('#pause').textContent='재생';ticks+=0.5;spriteCanvases.filter(c=>+c.dataset.fps).forEach(c=>{let next=(+c.dataset.frame+1)%+c.dataset.count;const fps=c.dataset.fps;c.dataset.fps=0;c.dataset.index=next;drawSprite(c);c.dataset.fps=fps})};sel.onchange=drawUI;document.querySelector('#width').oninput=drawUI;init().catch(e=>{document.querySelector('#status').textContent='그림 로드 실패: '+e.message});
</script></html>
'@
$html=$html.Replace('__IMAGES__',($imgs|ConvertTo-Json -Compress -Depth 4)).Replace('__STEPS__',($steps|ConvertTo-Json -Compress))
$review="$root/_HTML/6_그림_화면/codex_K-097_튜토리얼보충_v1.html"
WriteUtf8 $review $html
Write-Output "검사 통과: $($assets.Count)종, $(($checks|Measure-Object frames -Sum).Sum)칸. 검토 HTML 생성 완료."
