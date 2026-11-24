$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$artRoot = Split-Path $PSScriptRoot -Parent
$repoRoot = (Resolve-Path (Join-Path $artRoot '../../..')).Path
$gold = [System.Drawing.ColorTranslator]::FromHtml('#d5b16b')
$ink = [System.Drawing.ColorTranslator]::FromHtml('#eee4cf')
$panel = [System.Drawing.ColorTranslator]::FromHtml('#26352d')
$danger = [System.Drawing.ColorTranslator]::FromHtml('#c37e70')
$ids = @('repair','barracks','workshop','stairs')
$names = @('성벽 수리 자리','병영','함정 공방','성벽 계단')
function Label($g,$value,$x,$y,$size,$color) {
    $font = New-Object System.Drawing.Font 'Malgun Gothic',$size,([System.Drawing.FontStyle]::Regular),([System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush $color
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString($value,$font,$brush,[single]$x,[single]$y,$format)
    $font.Dispose(); $brush.Dispose(); $format.Dispose()
}
function Picture($g,$path,$x,$y,$w,$h) {
    $im = [System.Drawing.Image]::FromFile($path)
    try {$g.DrawImage($im,[single]$x,[single]$y,[single]$w,[single]$h)}finally{$im.Dispose()}
}
foreach ($time in @('day','evening')) {
    $bmp = New-Object System.Drawing.Bitmap 3840,1080
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    Picture $g (Join-Path $artRoot "backgrounds/codex_yard_$($time)_3840x1080.png") 0 0 3840 1080
    for($i=0;$i -lt 4;$i++) {
        $center = 499.2 + 921.6*$i
        Picture $g (Join-Path $artRoot "stations/codex_$($ids[$i]).png") ($center-768) 12 1536 1024
        $height = @(640,704,544,800)[$i]
        $y = 940-$height-78
        $g.FillRectangle((New-Object System.Drawing.SolidBrush $panel),[single]($center-190),[single]$y,380,66)
        $g.DrawRectangle((New-Object System.Drawing.Pen $gold,2),[single]($center-190),[single]$y,380,66)
        Label $g $names[$i] $center ($y+10) 34 $ink
    }
    $hero = [System.Drawing.Image]::FromFile((Join-Path $repoRoot 'Game/Assets/_Project/Art/Characters/Hero/hero_walk_run_grid.png'))
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.DrawImage($hero,(New-Object System.Drawing.Rectangle 900,588,256,384),(New-Object System.Drawing.Rectangle 0,1152,256,384),[System.Drawing.GraphicsUnit]::Pixel)
    $hero.Dispose()
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $panel),933,500,80,70)
    Label $g 'E' 973 504 50 $gold
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $panel),0,1008,3840,72)
    Label $g '정비 마당 · A / D 이동    E 상호작용 · 배치 시안' 1920 1020 32 $ink
    $bmp.Save((Join-Path $artRoot "qa/codex_yard_$($time)_composite.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose();$bmp.Dispose()
}
foreach($ending in @('victory','defeat')) {
    $bmp=New-Object System.Drawing.Bitmap 1920,1080
    $g=[System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#18231e'))
    $g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $line=New-Object System.Drawing.Pen $gold,2
    $g.DrawRectangle($line,35,35,1850,1010)
    $c=if($ending -eq 'victory'){$gold}else{$danger}
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $panel),600,62,720,110)
    $g.DrawRectangle((New-Object System.Drawing.Pen $c,3),600,62,720,110)
    Label $g $(if($ending -eq 'victory'){'승리'}else{'패배'}) 960 69 62 $c
    Label $g '성벽 피해' 470 220 36 $gold
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $panel),150,360,640,410)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#394b3e'))),365,465,210,190)
    Label $g '성 안' 470 530 36 $ink
    for($side=0;$side -lt 4;$side++) {
        $state=@(1,0,0,1)[$side]
        if($side -eq 3 -and $ending -eq 'defeat'){$state=2}
        $col=@('#829b83','#d5b16b','#c37e70')[$state]
        $brush=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($col))
        for($j=0;$j -lt 10;$j++) {
            if($state -eq 2 -and $j -ge 4 -and $j -le 6){continue}
            switch($side){
                0 {$x=180+58*$j;$y=305;$w=54;$h=58}
                1 {$x=788;$y=310+46*$j;$w=58;$h=42}
                2 {$x=180+58*$j;$y=785;$w=54;$h=58}
                3 {$x=93;$y=310+46*$j;$w=58;$h=42}
            }
            $g.FillRectangle($brush,$x,$y,$w,$h)
        }
        $brush.Dispose()
    }
    $crack=New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#362c24')),9
    $g.DrawLines($crack,[System.Drawing.Point[]]@((New-Object System.Drawing.Point 485,305),(New-Object System.Drawing.Point 465,329),(New-Object System.Drawing.Point 484,343),(New-Object System.Drawing.Point 460,364)))
    Label $g '북 · 금감' 470 266 24 $ink
    Label $g $(if($ending -eq 'defeat'){'서 · 부서짐'}else{'서 · 금감'}) 120 850 24 $danger
    Label $g '동' 875 540 24 $ink
    Label $g '남' 470 854 24 $ink
    $units=@('archer','gunner','cannon','wizard','hero')
    $unitNames=@('궁수','총병','대포병','마법사','주인공')
    $states=@('−12명','−8명','−2명','양호','부상')
    for($i=0;$i -lt 5;$i++) {
        $x=1010+($i%3)*265;$y=270+[math]::Floor($i/3)*335
        $g.FillRectangle((New-Object System.Drawing.SolidBrush $panel),[single]$x,[single]$y,240,300)
        Picture $g (Join-Path $artRoot "result/codex_portrait_$($units[$i]).png") $x $y 240 240
        Label $g $unitNames[$i] ($x+120) ($y+228) 25 $gold
        Label $g $states[$i] ($x+120) ($y+264) 24 $ink
    }
    Label $g '병력 손실 22명 · 영웅 상태' 1390 219 36 $gold
    Label $g '배치 예시 · 실제 전투 데이터가 아닙니다' 960 944 24 $ink
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $panel),1590,945,220,65)
    $g.DrawRectangle($line,1590,945,220,65)
    Label $g '확인' 1700 953 32 $gold
    $bmp.Save((Join-Path $artRoot "result/codex_result_$($ending)_1920x1080.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose();$bmp.Dispose();$line.Dispose()
}
Write-Output '정비 마당·결과 화면 합성 그림 출력 완료'
