$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'BitmapTools.cs') -ReferencedAssemblies System.Drawing
$p=$PSScriptRoot;$m=Get-Content -Raw -Encoding UTF8 (Join-Path $p 'manifest.json')|ConvertFrom-Json
$images=@{};foreach($key in @('crew_pull6','crew_push6','crew_ignite6','cannon_body','installed_crew','installed_aim3')){$images[$key]=[Drawing.Bitmap]::new((Join-Path $p ($key+'.png')))}
$font=[Drawing.Font]::new('Malgun Gothic',17);$small=[Drawing.Font]::new('Malgun Gothic',13)
function Surface{ $b=[Drawing.Bitmap]::new(1400,420);$script:g=[Drawing.Graphics]::FromImage($b);$g.Clear([Drawing.Color]::FromArgb(30,45,37));$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor;return $b }
function Text-At($text,$x,$y){$g.DrawString([string]$text,$font,[Drawing.Brushes]::Wheat,[single]$x,[single]$y)}
function Draw-Line($x,$y,$a,$b,$color,$width=2){$pen=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($color),$width);$g.DrawLine($pen,[single]$x,[single]$y,[single]$a,[single]$b);$pen.Dispose()}
function Ground($offset=0){Draw-Line 0 352 1400 352 '#89b29c' 1;for($x=$offset%80;$x -lt 1400;$x+=80){Draw-Line $x 352 $x 371 '#679a83' 1}}
function Sprite($key,$f,$x,$y,$w=256,$h=384){$g.DrawImage($images[$key],[Drawing.Rectangle]::new($x,$y,$w,$h),$f*$w,0,$w,$h,[Drawing.GraphicsUnit]::Pixel)}
function Draw-Cannon($x,$distance=0){
 Sprite 'cannon_body' 0 $x 0 512 384
 if($distance -ne 0){$a=$m.cannon;$cx=$a.wheelCenter[0];$cy=$a.wheelCenter[1];$r=$a.wheelRadius;$state=$g.Save();$path=[Drawing.Drawing2D.GraphicsPath]::new();$path.AddEllipse(($x+$cx-$r),($cy-$r),(2*$r),(2*$r));$g.SetClip($path);$g.TranslateTransform(($x+$cx),$cy);$g.RotateTransform([single](-$distance/$r*180/[Math]::PI));$g.DrawImage($images.cannon_body,[Drawing.Rectangle]::new(-$r,-$r,2*$r,2*$r),($cx-$r),($cy-$r),(2*$r),(2*$r),[Drawing.GraphicsUnit]::Pixel);$g.Restore($state);$path.Dispose()}
}
function Draw-Group($front,$cx,$rear,$f,$d=0){
 $a=$m.cannon;$left=$m.crew.crew_pull6.frames[$f];$right=$m.crew.crew_push6.frames[$f];$px=$front-128;$rx=$rear-128;$bx=$cx-256
 Draw-Line ($px+$left.hand[0]) $left.hand[1] ($bx+$a.frontHandle[0]) $a.frontHandle[1] '#b98a50' 3
 Draw-Line ($rx+$right.hand[0]) $right.hand[1] ($bx+$a.rearHandle[0]) $a.rearHandle[1] '#92633c' 7
 Sprite 'crew_push6' $f $rx 0;Draw-Cannon $bx $d;Sprite 'crew_pull6' $f $px 0
 foreach($x in @(($px+$left.stanceFoot[0]),($rx+$right.stanceFoot[0]))){Draw-Line ($x-6) 352 ($x+6) 352 '#efc66e' 3}
 Text-At "$($f+1) / 6칸 · $($left.stanceLeg) 디딤 · 이동 $($d)px" 35 25
}
function Save-Surface($b,$name){$g.Dispose();$b.Save((Join-Path $p $name),[Drawing.Imaging.ImageFormat]::Png);$b.Dispose()}
$b=Surface;Ground
$g.DrawImage($images.installed_crew,[Drawing.Rectangle]::new(80,142,130,210),47,52,186,300,[Drawing.GraphicsUnit]::Pixel)
$g.DrawImage($images.installed_aim3,[Drawing.Rectangle]::new(240,128,242,224),595,32,345,320,[Drawing.GraphicsUnit]::Pixel)
Draw-Group 670 920 1120 0
$g.FillRectangle([Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(30,45,37)),0,0,1400,55)
Text-At '설치 원본 · 병사와 대포 함께 0.7배' 60 25;Text-At '새 운반 · 병사210px / 대포242×224px' 620 25
Save-Surface $b 'scale_review.png'
for($f=0;$f -lt 6;$f++){$b=Surface;Ground ($f*40);Draw-Group 420 720 920 $f ($f*40);Save-Surface $b "haul_$($f+1).png"}
$b=Surface;Ground;Draw-Cannon 380
$ix=380+$m.cannon.fuse[0]-$m.ignite.contacts[3][0];Sprite 'crew_ignite6' 3 $ix 0
Draw-Line (380+$m.cannon.fuse[0]-5) $m.cannon.fuse[1] (380+$m.cannon.fuse[0]+5) $m.cannon.fuse[1] '#ffe0a0' 2
Text-At '0.50초 도화선 접촉 → 1.50초 발사' 40 25;Save-Surface $b 'ignite_contact_review.png'
$b=[Drawing.Bitmap]::new(1400,1470);$g=[Drawing.Graphics]::FromImage($b);$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$im=[Drawing.Bitmap]::new((Join-Path $p 'scale_review.png'));$g.DrawImageUnscaled($im,0,0);$im.Dispose()
for($f=0;$f -lt 6;$f++){$im=[Drawing.Bitmap]::new((Join-Path $p "haul_$($f+1).png"));$g.DrawImage($im,[Drawing.Rectangle]::new(($f%2*700),(420+[int][Math]::Floor($f/2)*210),700,210));$im.Dispose()}
$im=[Drawing.Bitmap]::new((Join-Path $p 'ignite_contact_review.png'));$g.DrawImageUnscaled($im,0,1050);$im.Dispose();$g.Dispose();$b.Save((Join-Path $p 'contact_review.png'));$b.Dispose()
foreach($im in $images.Values){$im.Dispose()};$font.Dispose();$small.Dispose()
Write-Output '정적 비교·보행 여섯 칸·점화 접점 합성 저장 완료'
