# K-026 아이콘 시안 원본. 외부 도구 없이 도트 원본·PNG·검토 페이지를 다시 만든다.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$repo = [IO.Path]::GetFullPath((Join-Path $root '../../..'))
$utf8 = New-Object Text.UTF8Encoding($false)
foreach ($dir in @('png/24','png/32','png/48','svg','sheets')) {
    [IO.Directory]::CreateDirectory((Join-Path $root $dir)) | Out-Null
}
$palette = [ordered]@{ ink='#101613'; shade='#786344'; brass='#d5b16b'; light='#eee4cf'; blue='#88b9c7'; red='#c37e70'; green='#91ad8b' }
$script:pen = 'brass'
function Color($key) { [Drawing.ColorTranslator]::FromHtml($palette[$key]) }
function Rect([int]$x,[int]$y,[int]$w,[int]$h,[string]$color='brass') {
    $brush = New-Object Drawing.SolidBrush (Color $color)
    $script:g.FillRectangle($brush,$x,$y,$w,$h)
    $brush.Dispose()
}
function L([int]$x,[int]$y,[int]$xx,[int]$yy,[string]$color='brass',[int]$width=2) {
    $p = New-Object Drawing.Pen ((Color $color),$width)
    $script:g.DrawLine($p,$x,$y,$xx,$yy)
    $p.Dispose()
}
function P($points,[string]$color='brass') {
    $ps = @(); for($i=0;$i -lt $points.Count;$i+=2){$ps += New-Object Drawing.Point($points[$i],$points[$i+1])}
    $brush = New-Object Drawing.SolidBrush (Color $color)
    $script:g.FillPolygon($brush,[Drawing.Point[]]$ps)
    $brush.Dispose()
}
function Head([int]$x,[int]$y,[string]$color='light') { Rect $x $y 4 4 $color; Rect ($x-1) ($y+5) 6 4 $color }
function Cloud([int]$y=7) {
    Rect 3 ($y+3) 18 5 'light'; Rect 5 ($y+1) 5 7 'light'; Rect 9 $y 7 8 'light'; Rect 16 ($y+2) 3 6 'light'
    Rect 4 ($y+7) 16 1 'brass'
}
function Sun([int]$x=12,[int]$y=12,[int]$r=4) {
    Rect ($x-$r) ($y-$r+1) ($r*2) ($r*2-2); Rect ($x-$r+1) ($y-$r) ($r*2-2) ($r*2)
    L $x ($y-$r-4) $x ($y-$r-2) 'light' 1; L $x ($y+$r+1) $x ($y+$r+3) 'light' 1
    L ($x-$r-4) $y ($x-$r-2) $y 'light' 1; L ($x+$r+1) $y ($x+$r+3) $y 'light' 1
    L ($x-7) ($y-7) ($x-6) ($y-6) 'light' 1; L ($x+6) ($y-6) ($x+7) ($y-7) 'light' 1
    L ($x-7) ($y+7) ($x-6) ($y+6) 'light' 1; L ($x+6) ($y+6) ($x+7) ($y+7) 'light' 1
}
function Heart {
    P @(4,7,7,4,10,4,12,6,14,4,17,4,20,7,20,11,12,19,4,11) 'light'
}
function Wall {
    Rect 3 10 18 11 'light'; Rect 3 6 4 5 'light'; Rect 10 6 4 5 'light'; Rect 17 6 4 5 'light'
    L 3 15 20 15 'shade' 1; L 9 11 9 14 'shade' 1; L 15 16 15 20 'shade' 1
}
$items = @(
    @{id='unit_archer'; name='궁수'; group='병종'; meaning='활과 화살'; draw={
        L 5 3 9 5; L 9 5 12 9; L 12 9 12 14; L 12 14 9 18; L 9 18 5 20
        L 5 3 5 20 'light' 1; L 3 12 20 12 'light' 1; P @(17,9,22,12,17,15) 'light'; L 3 10 5 12 'light' 1; L 3 14 5 12 'light' 1
    }},
    @{id='unit_gunner'; name='총병'; group='병종'; meaning='긴 총열과 개머리판'; draw={
        P @(3,17,6,13,9,14,17,6,19,8,11,16,9,16,6,21) 'brass'
        L 10 13 18 5 'light' 2; Rect 18 3 3 3 'light'; L 12 15 14 17 'shade' 1; L 14 17 16 15 'shade' 1
    }},
    @{id='unit_cannon'; name='대포병'; group='병종'; meaning='바퀴 달린 대포'; draw={
        P @(4,9,17,4,20,11,8,15) 'brass'; L 17 4 20 11 'light' 2; Rect 4 12 10 3 'shade'; Rect 2 18 19 2 'light'
        Rect 6 14 7 7 'light'; Rect 8 16 3 3 'ink'; L 14 14 19 18 'brass' 2
    }},
    @{id='unit_wizard'; name='마법사'; group='병종'; meaning='푸른 수정 지팡이'; draw={
        L 6 20 15 5 'brass' 2; P @(15,2,20,7,16,12,11,7) 'blue'; P @(15,3,17,7,15,10,13,7) 'light'
        Rect 4 4 3 1 'brass'; Rect 5 3 1 3 'brass'; Rect 19 15 3 1 'brass'; Rect 20 14 1 3 'brass'
    }},
    @{id='unit_hero'; name='주인공'; group='병종'; meaning='장교 제복의 견장과 훈장'; draw={
        P @(5,8,9,6,15,6,19,8,19,20,5,20) 'brass'; P @(9,6,12,11,15,6) 'light'; L 12 12 12 20 'ink' 1
        Rect 3 8 6 3 'light'; Rect 16 8 5 3 'light'; Rect 4 11 3 2 'shade'; Rect 17 11 3 2 'shade'
        P @(16,12,17,14,19,15,17,16,16,18,15,16,13,15,15,14) 'light'; Rect 10 3 4 2 'light'
    }},
    @{id='stat_gold'; name='골드'; group='자원·전황'; meaning='겹친 금화'; draw={
        Rect 3 13 12 7 'shade'; Rect 3 12 12 3; Rect 4 18 10 2; Rect 9 5 12 12; Rect 11 3 8 16; Rect 10 6 2 9 'light'; Rect 15 7 2 7 'shade'; Rect 13 9 6 2 'shade'
    }},
    @{id='stat_troops'; name='병력'; group='자원·전황'; meaning='세 사람의 묶음'; draw={ Head 4 7 'brass'; Head 16 7 'brass'; Rect 2 15 7 5; Rect 15 15 7 5; Rect 7 11 10 11 'ink'; Head 10 4 'light'; Rect 7 13 10 8 'light' }},
    @{id='stat_wall_hp'; name='성벽 체력'; group='자원·전황'; meaning='성벽과 작은 체력 십자'; draw={ Wall; Rect 9 12 7 9 'ink'; Rect 11 13 3 7 'brass'; Rect 9 15 7 3 'brass' }},
    @{id='stat_kills'; name='처치 수'; group='자원·전황'; meaning='쓰러진 적의 해골'; draw={
        Rect 5 4 14 11 'light'; Rect 7 2 10 14 'light'; Rect 7 14 10 6 'light'; Rect 7 7 4 4 'ink'; Rect 14 7 4 4 'ink'; P @(12,10,10,14,14,14) 'ink'; Rect 9 17 1 4 'ink'; Rect 12 17 1 4 'ink'; Rect 15 17 1 4 'ink'
    }},
    @{id='weather_clear'; name='맑음'; group='날씨·밤낮'; meaning='사방으로 빛나는 해'; draw={ Sun }},
    @{id='weather_cloudy'; name='흐림'; group='날씨·밤낮'; meaning='구름 두 겹'; draw={ Rect 9 4 10 5 'shade'; Rect 12 2 5 6 'shade'; Cloud 9 }},
    @{id='weather_rain'; name='비'; group='날씨·밤낮'; meaning='구름 아래 성긴 빗줄기'; draw={ Cloud 4; L 7 15 5 18 'blue' 1; L 13 15 11 18 'blue' 1; L 19 15 17 18 'blue' 1 }},
    @{id='weather_shower'; name='소나기'; group='날씨·밤낮'; meaning='구름 아래 굵고 촘촘한 빗줄기'; draw={ Cloud 3; L 6 13 3 19 'blue' 2; L 11 13 8 19 'blue' 2; L 16 13 13 19 'blue' 2; L 21 13 18 19 'blue' 2; Rect 4 21 2 1 'blue'; Rect 14 21 2 1 'blue' }},
    @{id='weather_fog'; name='안개'; group='날씨·밤낮'; meaning='엇갈린 수평 안개층'; draw={ Rect 6 4 12 2 'shade'; Rect 3 8 14 2 'light'; Rect 19 8 2 2 'light'; Rect 5 12 16 2 'brass'; Rect 3 16 5 2 'light'; Rect 10 16 11 2 'light'; Rect 6 20 12 1 'shade' }},
    @{id='time_day'; name='낮'; group='날씨·밤낮'; meaning='지평선 위의 해'; draw={ Sun 12 10 3; Rect 2 16 20 6 'ink'; Rect 3 17 18 2 'light'; Rect 6 21 12 1 'brass' }},
    @{id='time_night'; name='밤'; group='날씨·밤낮'; meaning='초승달과 별'; draw={
        P @(10,2,5,5,3,10,4,16,8,20,14,21,19,18,12,18,8,14,7,9) 'light'
        Rect 17 5 5 1 'brass'; Rect 19 3 1 5 'brass'; Rect 14 10 3 1 'brass'; Rect 15 9 1 3 'brass'
    }},
    @{id='priority_low_hp'; name='체력 낮은 적부터'; group='우선순위 명령'; meaning='빈 체력 모양과 아래 화살표'; api='LowestHpRatio'; draw={
        Heart; Rect 6 7 12 5 'ink'; P @(6,10,12,16,18,10) 'ink'; Rect 6 10 5 2 'red'; Rect 11 2 3 6 'ink'; L 18 14 18 18 'brass' 2; P @(14,17,22,17,18,22) 'brass'
    }},
    @{id='priority_high_hp'; name='체력 높은 적부터'; group='우선순위 명령'; meaning='채운 체력 모양과 위 화살표'; api='HighestHpRatio'; draw={
        Heart; L 18 15 18 20 'brass' 2; P @(14,16,22,16,18,11) 'brass'; L 5 8 7 6 'brass' 1
    }},
    @{id='priority_nearest'; name='가까운 적부터'; group='우선순위 명령'; meaning='성벽 가까이 향하는 화살표'; api='Nearest'; draw={
        Rect 3 4 4 16 'light'; Rect 3 4 6 3 'light'; Rect 3 17 6 3 'light'; L 13 12 21 12 'brass' 2; P @(9,12,15,7,15,17) 'brass'; Rect 19 4 3 3 'shade'
    }},
    @{id='priority_climbing'; name='오르는 적부터'; group='우선순위 명령'; meaning='사다리와 위쪽 이동'; api='ClimbingFirst'; draw={
        L 5 3 5 21 'light' 2; L 12 3 12 21 'light' 2; foreach($y in @(6,10,14,18)){L 5 $y 12 $y 'brass' 1}; L 18 19 18 8 'brass' 2; P @(15,9,21,9,18,4) 'brass'
    }},
    @{id='priority_special'; name='특수 적 먼저'; group='우선순위 명령'; meaning='뿔 달린 큰 적과 집중 표시'; api='SpecialFirst'; draw={
        P @(5,4,9,7,15,7,19,4,19,14,16,19,8,19,5,14) 'light'; Rect 8 10 3 2 'ink'; Rect 14 10 3 2 'ink'; Rect 10 16 4 1 'shade'
        L 2 5 2 2 'brass' 1; L 2 2 6 2 'brass' 1; L 21 17 21 21 'brass' 1; L 17 21 21 21 'brass' 1
    }},
    @{id='priority_dense'; name='밀집한 곳 쏘기'; group='우선순위 명령'; meaning='조준 괄호 안의 밀집한 세 적'; api='DensestArea'; draw={
        Head 6 8 'light'; Head 14 8 'light'; Rect 9 3 6 5 'ink'; Head 10 3 'brass'
        L 3 7 3 3 'brass' 1; L 3 3 7 3 'brass' 1; L 17 3 21 3 'brass' 1; L 21 3 21 7 'brass' 1
        L 3 17 3 21 'brass' 1; L 3 21 7 21 'brass' 1; L 17 21 21 21 'brass' 1; L 21 21 21 17 'brass' 1
    }}
)
$manifest = @()
foreach($item in $items) {
    $script:bmp = New-Object Drawing.Bitmap(24,24)
    $script:g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::None
    & $item.draw
    $g.Dispose()
    # 배경이 바뀌어도 형태가 남도록 한 화소의 어두운 외곽선을 붙인다.
    $outlined = New-Object Drawing.Bitmap(24,24)
    for($y=0;$y -lt 24;$y++){for($x=0;$x -lt 24;$x++){
        if($bmp.GetPixel($x,$y).A -gt 0){
            foreach($offset in @(@(-1,0),@(1,0),@(0,-1),@(0,1))){
                $nx=$x+$offset[0]; $ny=$y+$offset[1]
                if($nx -ge 0 -and $nx -lt 24 -and $ny -ge 0 -and $ny -lt 24){$outlined.SetPixel($nx,$ny,(Color 'ink'))}
            }
        }
    }}
    for($y=0;$y -lt 24;$y++){for($x=0;$x -lt 24;$x++){if($bmp.GetPixel($x,$y).A -gt 0){$outlined.SetPixel($x,$y,$bmp.GetPixel($x,$y))}}}
    $bmp.Dispose(); $bmp=$outlined
    $rects = New-Object Text.StringBuilder
    for($y=0;$y -lt 24;$y++){for($x=0;$x -lt 24;$x++){
        $c=$bmp.GetPixel($x,$y)
        if($c.A -gt 0){[void]$rects.Append("<rect x='$x' y='$y' width='1' height='1' fill='#$($c.R.ToString('X2'))$($c.G.ToString('X2'))$($c.B.ToString('X2'))'/>")}
    }}
    $svg="<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' width='24' height='24' shape-rendering='crispEdges' role='img'><title>$($item.name)</title>$rects</svg>"
    [IO.File]::WriteAllText((Join-Path $root "svg/$($item.id).svg"),$svg,$utf8)
    $paths = [ordered]@{}
    $data = [ordered]@{}
    foreach($size in @(24,32,48)) {
        $out = New-Object Drawing.Bitmap($size,$size)
        # 정수 좌표 대응으로 알파와 팔레트를 그대로 보존한다.
        for($y=0;$y -lt $size;$y++){for($x=0;$x -lt $size;$x++){
            $sx=[int][Math]::Floor(($x+0.5)*24/$size); $sy=[int][Math]::Floor(($y+0.5)*24/$size)
            $out.SetPixel($x,$y,$bmp.GetPixel($sx,$sy))
        }}
        $rel="png/$size/$($item.id).png"
        $out.Save((Join-Path $root $rel),[Drawing.Imaging.ImageFormat]::Png); $out.Dispose()
        $paths["$size"]=$rel
        $data["$size"]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $root $rel)))
    }
    $bmp.Dispose()
    $manifest += [ordered]@{ id=$item.id; name=$item.name; group=$item.group; meaning=$item.meaning; api=$item.api; files=$paths; images=$data }
}
$publicManifest = $manifest | ForEach-Object { [ordered]@{id=$_.id; name=$_.name; group=$_.group; meaning=$_.meaning; api=$_.api; files=$_.files} }
[IO.File]::WriteAllText((Join-Path $root 'manifest.json'),(ConvertTo-Json -Depth 8 -InputObject @{card='K-026'; version='v1'; status='사용자 확정 전 시안'; baseSize=24; sizes=@(24,32,48); palette=$palette; icons=@($publicManifest)}),$utf8)
$json=ConvertTo-Json -Depth 8 -Compress -InputObject @($manifest)
$template=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'review.template.html'))
$html=$template.Replace('__ICON_DATA__',$json)
[IO.File]::WriteAllText((Join-Path $repo '_HTML/codex_K-026_UI아이콘_v1.html'),$html,$utf8)
# 한 장에서 전체 아이콘과 세 크기를 비교하는 검토 그림.
$sheet=New-Object Drawing.Bitmap(1200,1110)
$sg=[Drawing.Graphics]::FromImage($sheet); $sg.Clear((Color 'ink'))
$titleFont=New-Object Drawing.Font('Malgun Gothic',23,[Drawing.FontStyle]::Bold)
$font=New-Object Drawing.Font('Malgun Gothic',10)
$small=New-Object Drawing.Font('Malgun Gothic',9)
$lightBrush=New-Object Drawing.SolidBrush (Color 'light'); $goldBrush=New-Object Drawing.SolidBrush (Color 'brass')
$sg.DrawString('마지막 성벽 · UI 아이콘',$titleFont,$goldBrush,30,22)
$sg.DrawString('K-026  /  사용자 확정 전 시안  /  각 칸 왼쪽부터 24 · 32 · 48px',$font,$lightBrush,32,69)
for($i=0;$i -lt $manifest.Count;$i++) {
    $entry=$manifest[$i]; $col=$i%4; $row=[int][Math]::Floor($i/4); $x=24+$col*294; $y=116+$row*160
    $panel=New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml('#26352d'))
    $sg.FillRectangle($panel,$x,$y,282,148); $panel.Dispose()
    $sg.DrawString($entry.name,$font,$lightBrush,$x+14,$y+12)
    $sg.DrawString($entry.group,$small,$goldBrush,$x+14,$y+37)
    $xx=$x+24
    foreach($size in @(24,32,48)) {
        $im=[Drawing.Image]::FromFile((Join-Path $root $entry.files["$size"]))
        $sg.DrawImageUnscaled($im,$xx,($y+78+(48-$size)/2)); $im.Dispose(); $xx+=76
    }
}
$sheet.Save((Join-Path $root 'sheets/codex_k026_contact_v1.png'),[Drawing.Imaging.ImageFormat]::Png)
$sg.Dispose(); $sheet.Dispose(); $titleFont.Dispose(); $font.Dispose(); $small.Dispose(); $lightBrush.Dispose(); $goldBrush.Dispose()
Write-Output "아이콘 $($manifest.Count)종, PNG $($manifest.Count*3)장, SVG $($manifest.Count)장과 검토 HTML 생성 완료"
