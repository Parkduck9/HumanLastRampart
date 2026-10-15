param([switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$pngDir = Join-Path $root 'png'
$svgDir = Join-Path $root 'source/svg'
$utf8 = New-Object Text.UTF8Encoding($false)
if (-not $VerifyOnly) {
    New-Item -ItemType Directory -Force $pngDir,$svgDir | Out-Null
}
$script:assets = @()
function Color($hex) { [Drawing.ColorTranslator]::FromHtml($hex) }
function BeginIcon($name,$label,$size,$pivot) {
    $script:name=$name; $script:label=$label; $script:size=$size; $script:pivot=$pivot
    $script:b = New-Object Drawing.Bitmap($size,$size)
    $script:g = [Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = 'None'
    $g.Clear([Drawing.Color]::Transparent)
}
function Rect($x,$y,$w,$h,$c) {
    $br=New-Object Drawing.SolidBrush((Color $c)); $g.FillRectangle($br,[int]$x,[int]$y,[int]$w,[int]$h); $br.Dispose()
}
function Line($x,$y,$xx,$yy,$c,$w=1) {
    $p=New-Object Drawing.Pen((Color $c),[single]$w); $g.DrawLine($p,[int]$x,[int]$y,[int]$xx,[int]$yy); $p.Dispose()
}
function Poly($coords,$c) {
    $pts = New-Object 'Drawing.Point[]' ($coords.Count / 2)
    for($i=0;$i -lt $pts.Length;$i++){$pts[$i]=New-Object Drawing.Point($coords[$i*2],$coords[$i*2+1])}
    $br=New-Object Drawing.SolidBrush((Color $c));$g.FillPolygon($br,$pts);$br.Dispose()
}
function Digit($n,$x,$y) {
    $rows = if($n -eq 1){@('010','110','010','010','111')}else{@('111','101','101','101','111')}
    for($j=0;$j -lt 5;$j++){for($i=0;$i -lt 3;$i++){if($rows[$j][$i] -eq '1'){Rect ($x+$i) ($y+$j) 1 1 '#fff1cf'}}}
}
function EndIcon {
    $g.Dispose()
    $b.Save((Join-Path $pngDir "$name.png"),[Drawing.Imaging.ImageFormat]::Png)
    # SVG는 PNG의 각 불투명 화소를 그대로 보존하는 편집 가능한 정수 격자 원본이다.
    $s=New-Object Text.StringBuilder
    [void]$s.AppendLine("<svg xmlns=`"http://www.w3.org/2000/svg`" width=`"$size`" height=`"$size`" viewBox=`"0 0 $size $size`" shape-rendering=`"crispEdges`"><title>$label</title>")
    for($y=0;$y -lt $size;$y++){for($x=0;$x -lt $size;$x++){$p=$b.GetPixel($x,$y);if($p.A -gt 0){[void]$s.AppendLine(('<rect x="{0}" y="{1}" width="1" height="1" fill="#{2:x2}{3:x2}{4:x2}"/>' -f $x,$y,$p.R,$p.G,$p.B))}}}
    [void]$s.Append('</svg>');[IO.File]::WriteAllText((Join-Path $svgDir "$name.svg"),$s.ToString(),$utf8)
    $script:assets += [ordered]@{file="$name.png";label=$label;width=$size;height=$size;pivotTopLeft=$pivot}
    $b.Dispose()
}
if (-not $VerifyOnly) {
    foreach($kind in @('archer','gunner')) {
        $label=if($kind -eq 'archer'){'궁수 10명 깃발'}else{'총병 10명 깃발'}
        BeginIcon "flag_$kind" $label 32 @(7,29)
        $main=if($kind -eq 'archer'){'#429eaf'}else{'#a96f99'}
        $shade=if($kind -eq 'archer'){'#245360'}else{'#59364f'}
        Rect 5 2 3 28 '#101b20';Rect 6 3 1 26 '#ead7a6';Rect 4 28 7 2 '#101b20'
        Poly @(8,3,28,5,28,21,19,18,8,20) '#101b20'
        Poly @(9,5,26,7,26,18,19,16,9,18) $main
        Poly @(9,14,19,13,26,15,26,18,19,16,9,18) $shade
        Line 10 5 24 7 '#c7e2d5'
        if($kind -eq 'archer') {
            Line 15 7 18 9 '#fff1cf';Line 18 9 19 11 '#fff1cf';Line 19 11 18 13 '#fff1cf';Line 18 13 15 15 '#fff1cf'
            Line 15 7 15 15 '#fff1cf';Line 12 11 23 11 '#fff1cf';Line 21 9 23 11 '#fff1cf';Line 21 13 23 11 '#fff1cf'
        } else {
            Line 13 14 22 7 '#fff1cf' 2;Rect 11 14 4 3 '#fff1cf';Line 17 13 20 15 '#fff1cf'
        }
        Rect 17 21 13 9 '#101b20';Rect 18 22 11 7 $shade;Digit 1 20 23;Digit 0 24 23
        EndIcon
    }
    foreach($dir in @('east','south','west','north')) {
        $label=@{east='동쪽 이동';south='남쪽 이동';west='서쪽 이동';north='북쪽 이동'}[$dir]
        BeginIcon "move_$dir" $label 24 @(12,12)
        Poly @(2,8,12,8,12,3,22,12,12,21,12,16,2,16) '#101b20'
        Poly @(4,10,14,10,14,7,19,12,14,17,14,14,4,14) '#f2dab0'
        Rect 4 13 10 1 '#b58d54'
        $g.Dispose()
        switch($dir){south{$b.RotateFlip('Rotate90FlipNone')}west{$b.RotateFlip('Rotate180FlipNone')}north{$b.RotateFlip('Rotate270FlipNone')}}
        $script:g=[Drawing.Graphics]::FromImage($b)
        EndIcon
    }
    BeginIcon 'compass_back' '방위 글자 받침' 64 @(32,32)
    Poly @(32,1,39,12,51,12,51,25,62,32,51,39,51,51,39,51,32,62,25,51,12,51,12,39,1,32,12,25,12,12,25,12) '#121e1c'
    Poly @(32,5,38,16,47,16,47,26,58,32,47,38,47,47,38,47,32,58,26,47,16,47,16,38,5,32,16,26,16,16,26,16) '#a68a55'
    Poly @(32,9,37,20,43,20,43,27,54,32,43,37,43,43,37,43,32,54,27,43,20,43,20,37,9,32,20,27,20,20,27,20) '#26352d'
    Rect 22 22 20 20 '#182721';Line 24 20 40 20 '#526053';Line 20 24 20 40 '#526053'
    EndIcon
    foreach($state in @('normal','active')) {
        BeginIcon "cannon_target_$state" $(if($state -eq 'active'){'선택한 대포 조준점'}else{'대포 조준점'}) 32 @(16,16)
        $ink=if($state -eq 'active'){'#ffdb92'}else{'#ede3cf'}
        foreach($p in @(@(4,4,11,4),@(4,4,4,11),@(21,4,27,4),@(27,4,27,11),@(4,21,4,27),@(4,27,11,27),@(21,27,27,27),@(27,21,27,27))){Line $p[0] $p[1] $p[2] $p[3] '#111c1d' 5;Line $p[0] $p[1] $p[2] $p[3] $ink 1}
        foreach($p in @(@(16,7,16,11),@(16,21,16,25),@(7,16,11,16),@(21,16,25,16))){Line $p[0] $p[1] $p[2] $p[3] '#111c1d' 3;Line $p[0] $p[1] $p[2] $p[3] $ink}
        Rect 14 14 5 5 '#111c1d';Rect 15 15 3 3 '#c37e70';Rect 16 16 1 1 '#fff1cf'
        if($state -eq 'active'){Rect 12 1 8 2 '#ffdb92';Rect 12 29 8 2 '#ffdb92'}
        EndIcon
    }
    BeginIcon 'group_selected' '병력 선택 테두리' 40 @(20,20)
    foreach($p in @(@(2,2,9,2),@(2,2,2,9),@(30,2,37,2),@(37,2,37,9),@(2,30,2,37),@(2,37,9,37),@(30,37,37,37),@(37,30,37,37))){Line $p[0] $p[1] $p[2] $p[3] '#101b20' 3;Line $p[0] $p[1] $p[2] $p[3] '#f2dab0'}
    EndIcon
    $manifest=[ordered]@{card='K-020';version=1;status='사용자 확정 전 시안';author='Codex';method='정수 화소 도형 직접 제작, 이미지 생성 모델 미사용';coordinate='좌상단 원점, 오른쪽 +X, 아래 +Y';assets=$assets}
    [IO.File]::WriteAllText((Join-Path $root 'manifest.json'),($manifest|ConvertTo-Json -Depth 8),$utf8)
}

# 투명 여백·크기·알파·방향을 실제 PNG에서 검사한다.
$m=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'manifest.json')|ConvertFrom-Json
$checks=@()
foreach($a in $m.assets){
    $im=[Drawing.Bitmap]::FromFile((Join-Path $pngDir $a.file));$count=0;$partial=0;$edge=0
    for($y=0;$y -lt $im.Height;$y++){for($x=0;$x -lt $im.Width;$x++){$alpha=$im.GetPixel($x,$y).A;if($alpha -gt 0){$count++;if($x -eq 0 -or $y -eq 0 -or $x -eq ($im.Width-1) -or $y -eq ($im.Height-1)){$edge++}};if($alpha -gt 0 -and $alpha -lt 255){$partial++}}}
    $ok=($im.Width -eq $a.width -and $im.Height -eq $a.height -and $count -gt 0 -and $count -lt ($im.Width*$im.Height) -and $partial -eq 0 -and $edge -eq 0)
    $checks += [ordered]@{file=$a.file;pass=$ok;visiblePixels=$count;partialAlpha=$partial;edgePixels=$edge;sha256=(Get-FileHash (Join-Path $pngDir $a.file) -Algorithm SHA256).Hash}
    $im.Dispose();if(-not $ok){throw "그림 검사 실패: $($a.file)"}
}
[IO.File]::WriteAllText((Join-Path $root 'verification.json'),($checks|ConvertTo-Json -Depth 5),$utf8)
Write-Output "PNG $($checks.Count)종 크기·투명도·화소 경계 검사 통과"
