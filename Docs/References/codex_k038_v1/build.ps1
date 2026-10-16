$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$dest=$PSScriptRoot
$root=Split-Path (Split-Path (Split-Path $dest))
$gen='C:/Users/User/.codex/generated_images/01a142ce-0612-76a3-9536-2c49b4ab49c5'
if(!(Test-Path "$dest/cannon_pull_source.png")){Copy-Item -LiteralPath "$gen/exec-242fddb7-5a26-4184-b69e-491cbe9fd3b2.png" "$dest/cannon_pull_source.png"}
if(!(Test-Path "$dest/burn_source.png")){Copy-Item -LiteralPath "$gen/exec-8e6414f9-f9e0-4002-bcdb-4ac24145e909.png" "$dest/burn_source.png"}
function New-Canvas($w,$h) { return [Drawing.Bitmap]::new([int]$w,[int]$h,[Drawing.Imaging.PixelFormat]::Format32bppArgb) }
function Get-Bounds($im,$x0,$y0,$w,$h) {
 $l=$w;$t=$h;$r=-1;$b=-1
 for($y=0;$y -lt $h;$y++){for($x=0;$x -lt $w;$x++){if($im.GetPixel(($x0+$x),($y0+$y)).A -gt 128){$l=[Math]::Min($l,$x);$t=[Math]::Min($t,$y);$r=[Math]::Max($r,$x);$b=[Math]::Max($b,$y)}}}
 return ,@(($x0+$l),($y0+$t),($r-$l+1),($b-$t+1))
}
$assets=@()
function Build-Strip($name,$source,$cols,$rows,$cw,$ch,$maxw,$maxh,$bottom,$fps) {
 $im=[Drawing.Bitmap]::FromFile("$dest/$source");$sw=[int]($im.Width/$cols);$sh=[int]($im.Height/$rows)
 $bounds=@();for($i=0;$i -lt 6;$i++){$bounds+=,(Get-Bounds $im (($i%$cols)*$sw) ([Math]::Floor($i/$cols)*$sh) $sw $sh)}
 $mw=($bounds|ForEach-Object{$_[2]}|Measure-Object -Maximum).Maximum;$mh=($bounds|ForEach-Object{$_[3]}|Measure-Object -Maximum).Maximum
 $scale=[Math]::Min($maxw/$mw,$maxh/$mh)
 $sheet=New-Canvas ($cw*6) $ch;$g=[Drawing.Graphics]::FromImage($sheet);$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor;$g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
 $rects=@()
 for($i=0;$i -lt 6;$i++){$b=$bounds[$i];$w=[int][Math]::Round($b[2]*$scale);$h=[int][Math]::Round($b[3]*$scale);$x=$i*$cw+[int](($cw-$w)/2);$y=$bottom-$h
 $g.DrawImage($im,[Drawing.Rectangle]::new($x,$y,$w,$h),[Drawing.Rectangle]::new($b[0],$b[1],$b[2],$b[3]),[Drawing.GraphicsUnit]::Pixel)
 $rects+=,@(($i*$cw),0,$cw,$ch)
 };$g.Dispose();$sheet.Save("$dest/$name.png",[Drawing.Imaging.ImageFormat]::Png);$sheet.Dispose();$im.Dispose()
 $script:assets+=@{id=$name;file="$name.png";frames=6;cell=@($cw,$ch);pivot=@([int]($cw/2),$bottom);fps=$fps;rects=$rects;source=$source;sourceRects=$bounds;scale=$scale}
}
Build-Strip 'cannon_pull6' 'cannon_pull_source.png' 3 2 256 384 232 224 352 6
Build-Strip 'burn_small6' 'burn_source.png' 6 1 256 256 92 100 224 10
Build-Strip 'burn_large6' 'burn_source.png' 6 1 256 256 158 120 240 10
foreach($type in @('burst','field')){foreach($state in @('in','out')){
 $name="range_${type}_${state}";$im=New-Canvas 512 512;$g=[Drawing.Graphics]::FromImage($im);$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
 if($state -eq 'in'){$rgb=@(96,224,186)}else{$rgb=@(245,115,104)}
 $pen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(195,$rgb[0],$rgb[1],$rgb[2]),4)
 $fine=[Drawing.Pen]::new([Drawing.Color]::FromArgb(100,$rgb[0],$rgb[1],$rgb[2]),2)
 $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(14,$rgb[0],$rgb[1],$rgb[2]));$g.FillEllipse($brush,36,36,440,440);$g.DrawEllipse($pen,36,36,440,440);$g.DrawEllipse($fine,48,48,416,416)
 if($type -eq 'burst'){$n=8}else{$n=12}
 for($i=0;$i -lt $n;$i++){$g.ResetTransform();$g.TranslateTransform(256,256);$g.RotateTransform($i*360/$n)
 if($type -eq 'burst'){$g.DrawLine($pen,-9,-228,0,-240);$g.DrawLine($pen,0,-240,9,-228);$g.DrawLine($fine,0,-226,0,-211)}else{$g.DrawArc($pen,-8,-241,16,24,5,280);$g.DrawLine($pen,0,-244,0,-226);$g.DrawLine($fine,-7,-211,7,-211)}
 };$g.Dispose();$pen.Dispose();$fine.Dispose();$brush.Dispose();$im.Save("$dest/$name.png",[Drawing.Imaging.ImageFormat]::Png);$im.Dispose()
 $assets+=@{id=$name;file="$name.png";frames=1;cell=@(512,512);pivot=@(256,256);radius=220;state=$state;kind=$type}
}}
$assets|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 "$dest/manifest.json"
Copy-Item -LiteralPath "$root/Game/Assets/_Project/Art/Characters/Goblin/goblin_walk_attack4.png" "$dest/reference_goblin.png"
Copy-Item -LiteralPath "$root/Game/Assets/_Project/Art/Characters/Static/ogre_static.png" "$dest/reference_ogre.png"
Copy-Item -LiteralPath "$root/Game/Assets/_Project/Art/Characters/Run/cannon_crew_run.png" "$dest/reference_crew.png"
Write-Output '시트 3종과 범위 원 4종 생성 완료'
