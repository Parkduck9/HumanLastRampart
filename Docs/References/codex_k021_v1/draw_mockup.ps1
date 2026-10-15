# HTML 캡처가 아닌 별도 정지 목업입니다. 같은 위치·색·문구로 배치를 검토합니다.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$bmp=New-Object System.Drawing.Bitmap(1440,810)
$g=[System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode='AntiAlias'
$g.TextRenderingHint='AntiAliasGridFit'
$svg=New-Object System.Text.StringBuilder
[void]$svg.AppendLine('<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="810" viewBox="0 0 1440 810"><title>전투 준비 창 정지 목업 · 브라우저 캡처 아님</title>')
function Color($hex){[System.Drawing.ColorTranslator]::FromHtml($hex)}
function Rect($x,$y,$w,$h,$fill,$stroke=''){
 $brush=New-Object System.Drawing.SolidBrush((Color $fill));$g.FillRectangle($brush,$x,$y,$w,$h);$brush.Dispose()
 if($stroke){$pen=New-Object System.Drawing.Pen((Color $stroke),1);$g.DrawRectangle($pen,$x,$y,$w,$h);$pen.Dispose()}
 [void]$svg.AppendLine("<rect x='$x' y='$y' width='$w' height='$h' fill='$fill' stroke='$(if($stroke){$stroke}else{'none'})'/>")
}
function Text($text,$x,$y,$size=16,$color='#eee4cf',$serif=$false){
 $font=New-Object System.Drawing.Font($(if($serif){'Batang'}else{'Malgun Gothic'}),$size,[System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
 $brush=New-Object System.Drawing.SolidBrush((Color $color));$g.DrawString($text,$font,$brush,$x,$y);$brush.Dispose();$font.Dispose()
 $escaped=[System.Security.SecurityElement]::Escape($text)
 [void]$svg.AppendLine("<text x='$x' y='$($y+$size)' font-family='$(if($serif){'Batang'}else{'Malgun Gothic'}),sans-serif' font-size='$size' fill='$color'>$escaped</text>")
}
function Line($x1,$y1,$x2,$y2,$color,$width=1){$pen=New-Object System.Drawing.Pen((Color $color),$width);$g.DrawLine($pen,$x1,$y1,$x2,$y2);$pen.Dispose();[void]$svg.AppendLine("<path d='M$x1 $y1 L$x2 $y2' stroke='$color' stroke-width='$width'/>")}
function Ellipse($x,$y,$w,$h,$fill,$stroke=''){
 $brush=New-Object System.Drawing.SolidBrush((Color $fill));$g.FillEllipse($brush,$x,$y,$w,$h);$brush.Dispose()
 if($stroke){$pen=New-Object System.Drawing.Pen((Color $stroke),1);$g.DrawEllipse($pen,$x,$y,$w,$h);$pen.Dispose()}
 [void]$svg.AppendLine("<ellipse cx='$($x+$w/2)' cy='$($y+$h/2)' rx='$($w/2)' ry='$($h/2)' fill='$fill' stroke='$(if($stroke){$stroke}else{'none'})'/>")
}
function Panel($x,$y,$w,$h,$title,$fold=$true){Rect $x $y $w $h '#26352d' '#d5b16b';Rect ($x+4) ($y+4) ($w-8) ($h-8) '#213128';Text $title ($x+16) ($y+18) 21 '#eee4cf' $true;if($fold){Rect ($x+$w-60) ($y+15) 44 36 '#26352d' '#7d805b';Text '▲' ($x+$w-48) ($y+21) 14 '#d5b16b'};Line ($x+16) ($y+65) ($x+$w-16) ($y+65) '#747852'}
function Button($text,$x,$y,$w,$h,$active=$false){Rect $x $y $w $h $(if($active){'#675733'}else{'#314338'}) $(if($active){'#d5b16b'}else{'#727b5e'});Text $text ($x+10) ($y+($h-18)/2) 15}
Rect 0 0 1440 810 '#344935'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$grassPath=Join-Path $root 'Game/Assets/_Project/Art/Battle/Resources/BattleArt/World/field_grass_albedo.png'
$grass=[System.Drawing.Image]::FromFile($grassPath)
$texture=New-Object System.Drawing.TextureBrush($grass)
$g.FillRectangle($texture,0,0,1440,810)
$texture.Dispose();$grass.Dispose()
$tint=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(145,26,48,32));$g.FillRectangle($tint,0,0,1440,810);$tint.Dispose()
$data=[Convert]::ToBase64String([IO.File]::ReadAllBytes($grassPath))
[void]$svg.AppendLine("<defs><pattern id='grass' width='256' height='256' patternUnits='userSpaceOnUse'><image href='data:image/png;base64,$data' width='256' height='256'/></pattern></defs><rect width='1440' height='810' fill='url(#grass)'/><rect width='1440' height='810' fill='#1a3020' opacity='.57'/>")
for($x=0;$x -lt 1440;$x+=44){Line $x 0 $x 810 '#42583b'}
for($y=0;$y -lt 810;$y+=44){Line 0 $y 1440 $y '#42583b'}
Ellipse 350 200 160 67 '#445b3d' '#61744a';Ellipse 930 480 165 65 '#445b3d' '#61744a'
Rect 609 304 222 222 '#8d927e' '#b1b393';Rect 636 331 168 168 '#2d4431';Rect 686 375 68 58 '#54664c' '#9b9f7c';Rect 711 408 16 25 '#24382b'
foreach($xy in @(@(604,297),@(836,297),@(604,529),@(836,529))){Rect ($xy[0]-22) ($xy[1]-22) 44 44 '#586657' '#b9b799';Rect ($xy[0]-14) ($xy[1]-14) 28 28 '#354a39' '#879476'}
Line 611 300 829 300 '#e4c98b' 5
foreach($x in @(652,696,740,784)){Rect $x 286 9 21 '#c5ba8c' '#273b2d'}
Text '북' 707 225 27 '#eddaac';Text '동' 878 393 27 '#eddaac';Text '남' 707 546 27 '#eddaac';Text '서' 535 393 27 '#eddaac'
Text '▲ 북쪽 들판 · 정면 주 공격 방향' 600 137 12 '#e5c68b';Text '거리·축척은 설명용' 665 600 11 '#c9d1af'
foreach($xy in @(@(653,327),@(745,327),@(642,489),@(741,489))){Line $xy[0] ($xy[1]-8) $xy[0] ($xy[1]+19) '#d9d7b5' 2;Rect $xy[0] ($xy[1]-8) 18 13 '#76a69a' '#d0e3bb';Text '10' ($xy[0]+24) ($xy[1]-6) 12 '#e8e5ce'}
Text '성 안 마당' 687 442 11 '#c9d1af';Ellipse 713 456 14 14 '#b398cb' '#e0c6f6'
Rect 24 22 292 99 '#16251e';Rect 24 22 3 99 '#d5b16b';Text '전투 전 · 시간 정지' 43 32 12 '#d5b16b';Text '전투 준비' 43 51 25 '#eee4cf' $true;Text '명령과 대포 조준을 먼저 정하십시오.' 43 87 13 '#d4d2bc'
Rect 616 22 208 54 '#d5b16b' '#f5d99c';Text '전투 시작' 674 35 20 '#1e231b' $true;Text '준비가 끝나면 전장으로' 655 82 12 '#ded9bd'
Text '북쪽 관문' 1300 26 25 '#eee4cf' $true;Text '전체 지도 · 북쪽 고정' 1278 66 13 '#d1ceba';Text '병력 300명 · 배치 예시' 1270 87 13 '#d1ceba'
Panel 24 137 292 578 '병사 명령'
$names=@('전체','북쪽','동쪽','남쪽','서쪽')
for($i=0;$i -lt 5;$i++){Button $names[$i] (40+$i*53) 214 49 44 ($i -eq 0)}
Text '네 성벽에 같은 명령을 내립니다.' 40 266 12 '#bfc4b0'
$commands=@('체력 낮은 적부터','체력 높은 적부터','가까운 적부터','오르는 적부터','특수 적 먼저','밀집한 곳 쏘기')
for($i=0;$i -lt 6;$i++){$y=294+$i*49;Rect 40 $y 260 44 $(if($i -eq 2){'#675733'}else{'#293c30'}) $(if($i -eq 2){'#d5b16b'}else{'#727b5e'});Ellipse 50 ($y+11) 23 23 '#26352d' '#a28b5c';Text ($i+1).ToString() 57 ($y+12) 13 '#e2bf79';Text $commands[$i] 86 ($y+11) 16}
Text '준비 중 · 전령 없이 바로 적용' 40 658 12 '#c2c7b4'
Panel 1134 137 282 460 '대포 조준'
for($i=0;$i -lt 4;$i++){Button $names[$i+1] (1150+$i*63) 214 59 46 ($i -eq 0)}
for($i=0;$i -lt 4;$i++){$x=1150+($i%2)*128;$y=274+[Math]::Floor($i/2)*69;Rect $x $y 121 62 '#314338' '#727b5e';Text "$($i+1)번 대포" ($x+10) ($y+8) 14;Text '조준 미지정' ($x+10) ($y+33) 11 '#c5c9b6'}
Rect 1150 423 250 71 '#15251d';Rect 1150 423 2 71 '#d5b16b';Text '대포를 고르고 지도에서' 1162 436 13;Text '조준할 곳을 누르십시오.' 1162 458 13
Button '선택한 대포 조준 해제' 1150 504 250 46
Text '사거리·실제 발사는 게임 연결 후 확인' 1150 563 11 '#c2c7b4'
Rect 1134 637 282 112 '#26352d' '#d5b16b';Text '성벽별 현재 명령' 1150 646 16 '#eee4cf' $true
for($i=0;$i -lt 4;$i++){Text $names[$i+1] 1150 (677+$i*16) 11 '#bfc4b0';Text '가까운 적부터' 1311 (677+$i*16) 11}
Rect 400 185 640 39 '#18251e' '#7f805c';Text '명령할 성벽을 고른 뒤 우선순위를 정하십시오.' 540 196 14 '#ffe7ae'
Rect 410 638 620 108 '#26352d' '#d5b16b';Text '마법사' 428 649 18 '#eee4cf' $true;Text '마법 선택 → 지도에 위치 미리보기' 810 654 12 '#c2c7b4';Button 'Q    폭발' 428 685 286 48;Button 'W    불바다' 724 685 288 48
Text '별도 정지 목업 · 브라우저 캡처 아님' 24 735 11 '#ded2ae'
Rect 0 762 1440 48 '#101b16' '#687452';Text '준비 순서   ① 병사 명령  →  ② 대포 조준  →  ③ 전투 시작' 24 777 12 '#d4d1bc';Text 'Q · W  마법 선택    ·    Esc  선택 취소    ·    ▲ / ▼  접기·펼치기' 1002 777 12 '#d4d1bc'
[void]$svg.AppendLine('</svg>')
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ui_layout_mockup.source.svg'),$svg.ToString(),(New-Object System.Text.UTF8Encoding($false)))
$bmp.Save((Join-Path $PSScriptRoot 'ui_layout_mockup_1440.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()
Write-Output '별도 정지 목업 SVG·PNG 생성 완료'
