$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$out=$PSScriptRoot
$root=(Resolve-Path "$out/../../..").Path
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Collections.Generic;
public static class ArtCheck {
 public static int[] Scan(Bitmap b,int x0,int w,int h){int l=w,t=h,r=-1,bot=-1,bad=0,col=0;HashSet<int> colors=new HashSet<int>();for(int y=0;y<h;y++)for(int x=0;x<w;x++){Color c=b.GetPixel(x0+x,y);if(c.A!=0&&c.A!=255)bad++;if(c.A>0){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);bot=Math.Max(bot,y);colors.Add(c.ToArgb());}}col=colors.Count;return new int[]{l,t,r+1,bot+1,bad,col};}
 public static bool Nearest(Bitmap a,Bitmap b,int n){if(b.Width!=a.Width*n||b.Height!=a.Height*n)return false;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(a.GetPixel(x/n,y/n).ToArgb()!=b.GetPixel(x,y).ToArgb())return false;return true;}
 public static int[] Components(Bitmap b,int x0,int w,int h){bool[,] seen=new bool[w,h];int count=0,smallest=100000;for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(!seen[x,y]&&b.GetPixel(x0+x,y).A>0){int size=0;count++;Queue<Point> q=new Queue<Point>();q.Enqueue(new Point(x,y));seen[x,y]=true;while(q.Count>0){Point p=q.Dequeue();size++;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int xx=p.X+dx,yy=p.Y+dy;if(xx>=0&&yy>=0&&xx<w&&yy<h&&!seen[xx,yy]&&b.GetPixel(x0+xx,yy).A>0){seen[xx,yy]=true;q.Enqueue(new Point(xx,yy));}}}smallest=Math.Min(smallest,size);}return new int[]{count,smallest};}
}
'@
$keys=@('mouse_left','mouse_right','mouse_middle','mouse_wheel','shift','e','a','d','ad','1','2','3','4','5','6','7','8','9','0','tab')
$keyLabels=@('왼쪽 버튼','오른쪽 버튼','가운데 버튼','휠 굴리기','Shift','E','A','D','A / D','1','2','3','4','5','6','7','8','9','0','Tab')
$specs=@(
 @{name='instruction_box';label='명령서 말풍선';w=96;h=48;n=1},
 @{name='officer_bust';label='장교 흉상';w=48;h=48;n=1},
 @{name='next_tick';label='다음 표시';w=16;h=16;n=1},
 @{name='highlight_ring';label='황동 강조 고리';w=64;h=64;n=6},
 @{name='pointer_up';label='위쪽 화살표';w=32;h=32;n=6},
 @{name='pointer_right';label='오른쪽 화살표';w=32;h=32;n=6},
 @{name='pointer_down';label='아래쪽 화살표';w=32;h=32;n=6},
 @{name='pointer_left';label='왼쪽 화살표';w=32;h=32;n=6},
 @{name='input_chips_24';label='입력칩 24점';w=24;h=24;n=20},
 @{name='input_chips_32';label='입력칩 32점';w=32;h=32;n=20},
 @{name='locked_chain';label='잠금 사슬';w=64;h=24;n=1},
 @{name='locked_seal';label='잠금 봉인';w=32;h=32;n=1},
 @{name='complete_stamp';label='완료 도장';w=32;h=32;n=1}
)
$assets=@(); $checks=@()
foreach($s in $specs){
 $native=[Drawing.Bitmap]::FromFile("$out/$($s.name)_native.png")
 $game=[Drawing.Bitmap]::FromFile("$out/$($s.name).png")
 $preview=[Drawing.Bitmap]::FromFile("$out/$($s.name)_4x.png")
 if($native.Width -ne $s.w*$s.n -or $native.Height -ne $s.h){throw "규격 오류: $($s.name)"}
 $ok3=[ArtCheck]::Nearest($native,$game,3);$ok4=[ArtCheck]::Nearest($native,$preview,4)
 if(!$ok3 -or !$ok4){throw "배율 오류: $($s.name)"}
 $frames=@()
 for($i=0;$i -lt $s.n;$i++){
  $a=[ArtCheck]::Scan($native,$i*$s.w,$s.w,$s.h)
  if($a[4] -ne 0){throw "알파 오류: $($s.name)"}
  $cc=[ArtCheck]::Components($native,$i*$s.w,$s.w,$s.h)
  if(($s.name -eq 'officer_bust' -or $s.n -eq 6) -and $cc[0] -ne 1){throw "고립 영역: $($s.name) $i"}
  $frames+=@{index=$i;alpha_bbox_top_left=@($a[0],$a[1],$a[2],$a[3]);opaque_colors=$a[5];connected_components=$cc[0];smallest_component_pixels=$cc[1]}
 }
 $order=@('기본');if($s.n -eq 6){$order=@('정지','접근 1','접근 2','최대','복귀 2','복귀 1')};if($s.n -eq 20){$order=$keys}
 $asset=[ordered]@{id=$s.name;label_ko=$s.label;native_file="$($s.name)_native.png";game_file="$($s.name).png";preview_4x_file="$($s.name)_4x.png";native_cell=@($s.w,$s.h);game_cell=@(($s.w*3),($s.h*3));native_sheet=@($native.Width,$native.Height);game_sheet=@($game.Width,$game.Height);columns=$s.n;rows=1;frame_count=$s.n;frame_order=$order;pivot_top_left_native=@(($s.w/2),($s.h/2));pivot_top_left_game=@(($s.w*3/2),($s.h*3/2));fps=0;loop=$false;frames=$frames}
 if($s.n -eq 6){$asset.fps=6;$asset.loop=$true}
 if($s.name -eq 'instruction_box'){$asset.nine_slice_border_native=@{left=8;right=8;top=8;bottom=8};$asset.nine_slice_border_game=@{left=24;right=24;top=24;bottom=24};$asset.center_mode='tile';$asset.edge_mode='tile';$asset.text_safe_inset_native=@(11,10,11,10);$asset.example_size_game=@(900,144)}
 if($s.name -like 'pointer_*'){$asset.tip_top_left_native=switch($s.name){'pointer_up'{@(15,3)}'pointer_right'{@(28,15)}'pointer_down'{@(16,28)}'pointer_left'{@(3,16)}};$asset.motion_offsets_native=@(0,1,2,3,2,1)}
 if($s.name -eq 'highlight_ring'){$asset.radii_native=@(22,23,24,25,24,23);$asset.motion_center_native=@(31.5,31.5)}
 $assets+=$asset
 $checks+=@{id=$s.name;native_dimensions=$true;binary_alpha=$true;exact_3x_nearest=$ok3;exact_4x_preview=$ok4;per_frame_bounds_recorded=$true;foot_baseline='해당 없음: 인물 동작이 아닌 화면 안내 그림'}
 $native.Dispose();$game.Dispose();$preview.Dispose()
}
$sourceNames=@('Docs/References/codex_k071_v1/hero_neutral_generated.png','Docs/References/codex_hero_octopath_review_v6/generated.png','Docs/References/ui_redesign_v2/now_battle.png','Docs/References/ui_redesign_v2/now_yard.png','Game/Assets/_Project/Art/UI/Resources/HudArt/Command/command_circle.png','Game/Assets/_Project/Art/UI/Resources/HudArt/Command/command_number_1.png')
$sources=@();foreach($p in $sourceNames){$sources+=@{path=$p;sha256=(Get-FileHash -LiteralPath "$root/$p" -Algorithm SHA256).Hash}}
$mockNames=@('01_telescope','02_squad','03_magic','04_trap','05_repair','06_lens')
$mockLabels=@('망원경을 꾹 누르기','분대를 골라 보내기','마법 직접 사용','함정 직접 발동','성벽 먼저 수리','렌즈 구매와 장착')
$mocks=@();for($i=0;$i -lt 6;$i++){$mocks+=@{file="mockup_$($mockNames[$i]).png";label_ko=$mockLabels[$i];size=@(1920,1080);source=if($i -lt 4){'Docs/References/ui_redesign_v2/now_battle.png'}else{'Docs/References/ui_redesign_v2/now_yard.png'};actual_tutorial_capture=$false}}
$flow=@(
 @{step='걷기';line='A와 D로 성벽 위를 걸어 보자.';completion='이동 확인';mode='직접 수행'},
 @{step='망원경';line='오른쪽 버튼을 꾹 눌러 정찰하자.';completion='오른쪽 누르기 유지 확인';mode='직접 수행'},
 @{step='지도';line='Tab을 눌러 지도를 펼치자.';completion='지도 열림 확인';mode='직접 수행'},
 @{step='분대';line='분대를 골라 북쪽으로 보내자.';completion='선택이 아니라 실제 이동 완료';mode='직접 수행'},
 @{step='대포·집중 공격';line='대포는 범위를, 집중 공격은 표적을 정한다.';completion='설명 확인';mode='설명만'},
 @{step='마법';line='마법을 골라 들판에 사용하자.';completion='실제 마법 사용';mode='직접 수행'},
 @{step='함정';line='함정을 직접 발동해 보자.';completion='실제 함정 발동';mode='직접 수행'},
 @{step='성벽 수리';line='먼저 성벽을 수리하자.';completion='수리 완료';mode='정비 강제 순서 1'},
 @{step='함정 공방';line='함정 공방에서 다음 전투를 준비하자.';completion='공방 작업 완료';mode='정비 강제 순서 2'},
 @{step='병영';line='병영에서 병사 열 명을 모집하자.';completion='병력 +10';mode='정비 강제 순서 3'},
 @{step='상점';line='망원경 렌즈를 사고 장착하자.';completion='렌즈 구매와 영웅 장착 모두 확인';mode='정비 강제 순서 4'},
 @{step='계단';line='계단으로 올라가 다음 전투를 준비하자.';completion='2단계 진입';mode='정비 강제 순서 5'}
)
$manifest=[ordered]@{card='K-093';version=1;coordinate_origin='top_left';alpha='RGBA, binary 0/255 for delivery UI sheets';filter='Point';compression_recommendation='None';mipmaps=$false;native_to_game_scale=3;preview_scale=4;preview_scale_basis='native';figure_sheet_contract='D-081 인물 동작 시트는 없음. 화면 안내 전용 칸이며 발 기준선은 적용하지 않음.';unity_integration='Claude 담당. 이 폴더는 제작 자료이며 Game 파일을 바꾸지 않음.';assets=$assets;input_chip_order=$keys;input_chip_labels_ko=$keyLabels;tutorial_scope='1단계 · 북쪽 · 낮';tutorial_flow=$flow;mockups=$mocks;contact_sheet=@{file='mockup_sheet_6steps.png';size=@(3840,3240);layout='2열 3행, 행 우선'};sources=$sources;limitations=@('목업 배경은 현재 캡처를 사용했으며 새 튜토리얼의 낮·1단계·지도 화면을 증명하지 않음.','장소·마법·함정 표식 좌표는 배치 예시. 실제 대상 RectTransform/월드 위치에 Claude가 연결.','오른쪽 누르기 지속, 분대 이동, 마법·함정 수행 판정과 단계 잠금은 Claude가 구현.','렌즈 구매·장착을 확인한 뒤 계단을 열 것. 초상/입력칩 텍스처에는 게임 글꼴을 포함하지 않음.')}
$manifest|ConvertTo-Json -Depth 12|Set-Content -Encoding UTF8 "$out/manifest.json"
$r=0x21;$g=0x1e;$b=0x26
function Lum($r,$g,$b){$v=@($r,$g,$b)|ForEach-Object{$x=$_/255;if($x -le 0.04045){$x/12.92}else{[Math]::Pow(($x+0.055)/1.055,2.4)}};return 0.2126*$v[0]+0.7152*$v[1]+0.0722*$v[2]}
$contrast=((Lum 199 173 112)+0.05)/((Lum 33 30 38)+0.05)
$verification=[ordered]@{assets=$checks;parchment_text_contrast=[Math]::Round($contrast,2);parchment_text_contrast_pass=($contrast -ge 4.5);delivery_sheets=39;motion_frames_checked=30;mockup_dimensions_checked=$true;image_decode_checked=$false;browser_render_checked=$false;unity_run=$false;git_writes=$false;notes_ko=@('고리·화살표·흉상은 각 칸의 연결 영역 1개를 검사. 입력 글자·봉인 문양은 의도한 분리 영역이므로 최소 영역 수를 강제하지 않음.','발 기준선 검사는 화면 안내 그림에 해당하지 않음. 고리는 중심 고정, 화살표 이동은 명시한 바운스만 사용.','생성 원안의 부드러운 알파는 보관용이며 납품용 흉상은 이진 알파로 정리.','브라우저 실행과 Unity Play는 수행하지 않음.')}
foreach($m in $mocks){$im=[Drawing.Image]::FromFile("$out/$($m.file)");if($im.Width -ne 1920 -or $im.Height -ne 1080){throw '목업 크기 오류'};$im.Dispose()}
function DataUri([string]$p){return 'data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes($p))}
$htmlPath="$root/_HTML/6_그림_화면/codex_K-093_튜토리얼안내_v1.html"
$sb=New-Object Text.StringBuilder
[void]$sb.Append(@'
<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-093 · 튜토리얼 안내 그림</title><style>
*{box-sizing:border-box}body{margin:0;background:#17191c;color:#e8e4da;font:17px/1.65 'Malgun Gothic',sans-serif}main{max-width:1260px;margin:auto;padding:42px 28px 80px}h1{font-size:38px;margin:0 0 10px}h2{font-size:26px;margin:46px 0 18px;color:#e2bd68}h3{font-size:19px}p{max-width:960px}button,select{background:#36312b;color:#f4e7c6;border:1px solid #ba873f;padding:10px 18px;font:inherit;cursor:pointer}button:focus-visible,select:focus-visible{outline:3px solid #e2bd68}button[aria-pressed=true]{background:#725036}small,.muted{color:#b9b7b0}figure{margin:0}figcaption{padding:8px 0 16px}img,canvas{image-rendering:pixelated;max-width:100%}canvas{display:block}.toolbar{display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin:18px 0}.compare{display:grid;grid-template-columns:1fr 1fr;gap:24px}.motion{display:flex;gap:28px;align-items:end;flex-wrap:wrap;background:#25272b;padding:24px}.motion figure{min-width:124px}.assetrow{display:flex;gap:26px;align-items:center;flex-wrap:wrap;background:#25272b;padding:24px}.chips{display:flex;gap:16px;flex-wrap:wrap}.chips figure{width:104px}.chips figcaption{font-size:14px}.screen{width:100%;height:auto;display:block}.sources{display:none}table{border-collapse:collapse;width:100%;font-size:16px}th,td{text-align:left;border-bottom:1px solid #45423d;padding:10px}th{color:#e2bd68}.status{padding:10px 0;color:#e2bd68}.warning{border-left:3px solid #ba873f;padding-left:16px}.frame{font-variant-numeric:tabular-nums}@media(max-width:750px){.compare{grid-template-columns:1fr}main{padding:24px 16px}h1{font-size:29px}table{font-size:13px}}
</style><main><h1>튜토리얼 안내 그림</h1><p>K-093 · 장교의 짧은 한마디와 황동 표식으로 다음 행동 한 곳을 짚습니다. 1단계는 북쪽 성벽·낮이며, 대포와 집중 공격은 설명으로만 안내합니다.</p><p class="warning">아래 장면은 기존 캡처 위에 그림을 얹은 <b>합성 목업</b>입니다. 새 튜토리얼을 실행한 캡처가 아닙니다. 좌표와 단계 잠금은 게임 연결 때 맞춥니다.</p>
<h2>한 줄 안내와 작은 장교 초상</h2><div class="assetrow" id="bubbleExample"></div><p>낡은 종이 9칸 나눔 · 모서리 원본 8점 / 게임 24px · 가운데와 가장자리는 반복합니다. 검토 화면 글은 맑은 고딕이며 게임 글꼴은 별도 연결합니다.</p>
<h2>강조 표식 · 6프레임</h2><div class="toolbar"><button id="play" aria-pressed="true">자동 재생 멈추기</button><button id="next">한 칸 넘기기</button><span id="frame" class="frame">1 / 6</span></div><div class="motion" id="motions"></div><p>고리는 중심을 고정한 채 반경만 22→25→22점으로 변합니다. 화살표는 방향을 유지하며 0→3→0점 왕복합니다. 공통 재생 속도는 초당 6칸입니다.</p>
<h2>현재 HUD와 비교</h2><div class="compare"><figure><div id="currentMark" class="assetrow"></div><figcaption>현재 게임의 명령 원·숫자 표식. 기존 튜토리얼 시트는 없습니다.</figcaption></figure><figure><div id="newMark" class="assetrow"></div><figcaption>새 황동 강조 고리·입력칩. 아래 현재 캡처와 합성 목업을 같은 크기로 비교합니다.</figcaption></figure></div>
<h2>입력 힌트</h2><div class="toolbar"><label for="chipSize">원본 규격 </label><select id="chipSize"><option value="24">24점 · 화면 72px</option><option value="32">32점 · 화면 96px</option></select></div><div id="chips" class="chips"></div><p>마우스 버튼은 누를 부위만 황동으로 표시합니다. 휠 굴리기는 위아래 화살표로 가운데 누르기와 구분합니다. 숫자 1~0과 A/D는 조작 표기용이며 특정 능력 배정을 확정하지 않습니다.</p>
<h2>잠금과 완료</h2><div id="states" class="assetrow"></div><p>아직 열리지 않은 장소는 작은 봉인 또는 사슬로 표시합니다. 완료는 모양이 다른 체크 도장으로 구분합니다. 화면 전체를 가리는 잠금 판은 사용하지 않습니다.</p>
<h2>전투 4장 · 정비 마당 2장</h2><div class="toolbar"><label for="scene">확인할 장면 </label><select id="scene"></select></div><div class="compare"><figure><img id="before" class="screen" alt="현재 게임 캡처"><figcaption>현재 게임 캡처</figcaption></figure><figure><img id="after" class="screen" alt="튜토리얼 안내 합성 목업"><figcaption id="sceneCaption">새 안내 배치</figcaption></figure></div><p>분대는 Shift+왼쪽 끌기로 고른 뒤 버튼을 놓는 순간 방향 선택을 띄우는 흐름입니다. 마법·함정 위치 표식은 배치 예시입니다. 렌즈는 구매와 장착을 모두 확인한 뒤 계단으로 안내합니다.</p>
<details><summary>여섯 장 전체 펼치기</summary><div id="allScenes"></div></details>
<h2>안내 순서와 끝 조건</h2><table><thead><tr><th>순서</th><th>한 줄 안내</th><th>완료 기준</th></tr></thead><tbody id="flow"></tbody></table><p>수리 → 함정 공방 → 병영(+10) → 상점(렌즈 구매·장착) → 2단계 계단 순서입니다. 직접 수행 단계에서는 다음 표시만 눌러 완료할 수 없도록 Claude가 실제 행동 결과와 연결해야 합니다.</p>
<h2>검사와 연결 메모</h2><p id="validation"></p><p>납품용 화면 안내 칸은 용도에 맞는 규격을 씁니다. 인물 동작 시트가 아니므로 96×128 인물 칸과 발 기준선은 적용하지 않습니다. 원본·3배 게임용·원본 기준 4배 확대를 함께 제공합니다. Unity 연결과 Play 확인은 남아 있습니다.</p><p id="loadStatus" class="status">내장 그림을 확인하는 중입니다.</p></main><div class="sources" id="sources">
'@)
$imageMap=[ordered]@{}
foreach($s in $specs){$imageMap[$s.name]="$out/$($s.name)_native.png"}
$imageMap['current_circle']="$root/Game/Assets/_Project/Art/UI/Resources/HudArt/Command/command_circle.png"
$imageMap['current_number']="$root/Game/Assets/_Project/Art/UI/Resources/HudArt/Command/command_number_1.png"
$imageMap['before_battle']="$root/Docs/References/ui_redesign_v2/now_battle.png"
$imageMap['before_yard']="$root/Docs/References/ui_redesign_v2/now_yard.png"
foreach($m in $mocks){$imageMap[$m.file]="$out/$($m.file)"}
$decodeCount=0
foreach($id in $imageMap.Keys){$uri=DataUri $imageMap[$id];$bytes=[Convert]::FromBase64String($uri.Split(',')[1]);$mem=New-Object IO.MemoryStream(,$bytes);$im=[Drawing.Image]::FromStream($mem);$decodeCount++;$im.Dispose();$mem.Dispose();[void]$sb.Append('<img id="src_'+$id+'" alt="" src="'+$uri+'">')}
[void]$sb.Append('</div><script>const specs='+($specs|ConvertTo-Json -Compress)+';const keyLabels='+($keyLabels|ConvertTo-Json -Compress)+';const mockups='+($mocks|ConvertTo-Json -Compress)+';const flow='+($flow|ConvertTo-Json -Compress)+';const contrast='+[string]::Format([Globalization.CultureInfo]::InvariantCulture,'{0:0.00}',$contrast)+';')
[void]$sb.Append(@'
const src=id=>document.getElementById('src_'+id);let playing=!matchMedia('(prefers-reduced-motion: reduce)').matches,frame=0;
function canvas(id,w,h,n=0,scale=3){let c=document.createElement('canvas');c.width=w*scale;c.height=h*scale;c.dataset.id=id;c.dataset.w=w;c.dataset.h=h;c.dataset.n=n;paint(c,n);return c}
function paint(c,n){let w=+c.dataset.w,h=+c.dataset.h,g=c.getContext('2d');g.imageSmoothingEnabled=false;g.clearRect(0,0,c.width,c.height);g.drawImage(src(c.dataset.id),n*w,0,w,h,0,0,c.width,c.height)}
function addFigure(parent,node,label){let f=document.createElement('figure'),cap=document.createElement('figcaption');cap.textContent=label;f.append(node,cap);document.getElementById(parent).append(f)}
function update(){document.querySelectorAll('[data-motion]').forEach(c=>paint(c,frame));document.getElementById('frame').textContent=(frame+1)+' / 6';document.getElementById('play').textContent=playing?'자동 재생 멈추기':'자동 재생 시작';document.getElementById('play').setAttribute('aria-pressed',playing)}
function chips(){let size=+document.getElementById('chipSize').value;document.getElementById('chips').replaceChildren();keyLabels.forEach((s,i)=>addFigure('chips',canvas('input_chips_'+size,size,size,i),s))}
function scene(){let i=+document.getElementById('scene').value;document.getElementById('before').src=src(i<4?'before_battle':'before_yard').src;document.getElementById('after').src=src(mockups[i].file).src;document.getElementById('sceneCaption').textContent=mockups[i].label_ko+' · 합성 목업'}
function boot(){
 let b=document.getElementById('bubbleExample');b.append(canvas('officer_bust',48,48));let box=document.createElement('div');box.style.cssText='position:relative;min-width:270px;padding:24px 30px;color:#211e26;font-weight:bold;border:24px solid transparent;border-image:url('+src('instruction_box').src+') 8 fill repeat;';box.textContent='오른쪽 버튼을 꾹 눌러 정찰하자.';b.append(box,canvas('next_tick',16,16));
 specs.filter(s=>s.n===6).forEach(s=>{let c=canvas(s.name,s.w,s.h);c.dataset.motion='1';addFigure('motions',c,s.label)});
 ['current_circle','current_number'].forEach(id=>{let im=src(id),w=im.naturalWidth,h=im.naturalHeight,scale=Math.min(3,192/w,192/h),c=document.createElement('canvas');c.width=w*scale;c.height=h*scale;let g=c.getContext('2d');g.imageSmoothingEnabled=false;g.drawImage(im,0,0,c.width,c.height);document.getElementById('currentMark').append(c)});
 document.getElementById('newMark').append(canvas('highlight_ring',64,64,0),canvas('input_chips_24',24,24,0));
 ['locked_chain','locked_seal','complete_stamp'].forEach(id=>{let s=specs.find(s=>s.name===id);addFigure('states',canvas(id,s.w,s.h),s.label)});
 chips();mockups.forEach((m,i)=>{let o=new Option(m.label_ko,i);document.getElementById('scene').add(o);let f=document.createElement('figure'),im=new Image(),cap=document.createElement('figcaption');im.src=src(m.file).src;im.alt=m.label_ko+' 합성 목업';im.className='screen';cap.textContent=m.label_ko;f.append(im,cap);document.getElementById('allScenes').append(f)});scene();
 flow.forEach((s,i)=>{let row=document.createElement('tr');[(i+1)+'. '+s.step,s.line,s.mode+' · '+s.completion].forEach(s=>{let td=document.createElement('td');td.textContent=s;row.append(td)});document.getElementById('flow').append(row)});
 document.getElementById('validation').textContent='납품 시트 39장: 규격·이진 알파·3배 최근접 확대·4배 미리보기 일치 확인. 고리·화살표 총 30칸의 경계와 연결 영역을 검사했습니다. 종이 위 먹 글 대비 '+contrast.toFixed(2)+':1. 화면과 입력 동작은 Unity 연결 뒤 확인해야 합니다.';
 document.getElementById('play').onclick=()=>{playing=!playing;update()};document.getElementById('next').onclick=()=>{playing=false;frame=(frame+1)%6;update()};document.getElementById('chipSize').onchange=chips;document.getElementById('scene').onchange=scene;update();setInterval(()=>{if(playing){frame=(frame+1)%6;update()}},1000/6);
 document.getElementById('loadStatus').textContent='내장 그림 '+document.querySelectorAll('#sources img').length+'개를 불러왔습니다. 서버와 외부 파일 없이 열립니다.';
}
Promise.all([...document.querySelectorAll('#sources img')].map(i=>i.decode())).then(boot).catch(e=>{document.getElementById('loadStatus').textContent='그림을 읽지 못했습니다: '+e.message});
</script></html>
'@)
New-Item -ItemType Directory -Force (Split-Path $htmlPath)|Out-Null
[IO.File]::WriteAllText($htmlPath,$sb.ToString(),(New-Object Text.UTF8Encoding($true)))
$verification.image_decode_checked=$true;$verification.embedded_png_count=$decodeCount
$verification|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 "$out/validation.json"
Write-Output "납품 시트 39장 · 동작 30칸 · 내장 그림 $decodeCount 개 · 글 대비 $([Math]::Round($contrast,2)):1 검사 완료"
