$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$out=$PSScriptRoot
New-Item -ItemType Directory -Force "$out/mockups" | Out-Null
$labels=@('정상','모서리 손상','반파','파공 · 잔단')
$states=@('intact','chipped','half_broken','stub')
function Draw-Png($g,$name,$x,$y,$w,$h){$b=[Drawing.Bitmap]::new("$out/native/$name.png");$g.DrawImage($b,[Drawing.Rectangle]::new($x,$y,$w,$h));$b.Dispose()}
foreach($view in @('walltop','courtyard')){
 $ref=if($view -eq 'walltop'){"$root/Docs/References/tower_20261023/238_tower_tall_night_4_beam_field_from_wall.png"}else{"$root/Docs/References/courtyard_20261023/02_bldg_day_1_topdown.png"}
 $board=[Drawing.Bitmap]::new(3072,464);$bg=[Drawing.Graphics]::FromImage($board);$bg.Clear([Drawing.Color]::FromArgb(24,27,30))
 for($s=0;$s -lt 4;$s++){
  $b=[Drawing.Bitmap]::new($ref);$g=[Drawing.Graphics]::FromImage($b);$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor;$g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
  if($view -eq 'walltop'){
   # 여장 하나의 기존 실루엣을 인접한 실제 바깥 들판으로 지운 뒤 같은 위치에 손상 단계를 얹는다.
   $patch=$b.Clone([Drawing.Rectangle]::new(437,460,126,104),$b.PixelFormat);$g.DrawImageUnscaled($patch,437,568);$patch.Dispose()
   $m=[Drawing.Bitmap]::new("$out/native/merlon_$($states[$s]).png");$attrs=[Drawing.Imaging.ImageAttributes]::new();$mat=[Drawing.Imaging.ColorMatrix]::new();$mat.Matrix00=0.42;$mat.Matrix11=0.48;$mat.Matrix22=0.70;$attrs.SetColorMatrix($mat)
   $g.DrawImage($m,[Drawing.Rectangle]::new(380,348,240,320),0,0,96,128,[Drawing.GraphicsUnit]::Pixel,$attrs);$m.Dispose();$attrs.Dispose()
   if($s -gt 0){Draw-Png $g "rubble_walkway_$(@('small','medium','large')[$s-1])_2" 414 455 192 256}
   if($s -eq 3){$fl=[Drawing.Bitmap]::new("$out/native/fire_6f.png");$g.DrawImage($fl,[Drawing.Rectangle]::new(425,420,192,256),[Drawing.Rectangle]::new(0,0,96,128),[Drawing.GraphicsUnit]::Pixel);$fl.Dispose()}
  }else{
   # 내려다본 성벽의 같은 구간에 균열·소실·잔해를 합성한다. 화면 전체와 카메라는 보존한다.
   if($s -eq 1){Draw-Png $g 'wall_stage1_cracks' 84 390 40 120}
   if($s -eq 2){Draw-Png $g 'wall_stage2_gouges' 84 390 40 120}
   if($s -eq 3){$patch=$b.Clone([Drawing.Rectangle]::new(42,410,34,76),$b.PixelFormat);$g.DrawImageUnscaled($patch,85,410);$patch.Dispose();Draw-Png $g 'wall_stage3_breach_rim' 78 390 58 120}
   if($s -gt 0){Draw-Png $g "rubble_walkway_$(@('small','medium','large')[$s-1])_1" 67 379 110 146;Draw-Png $g "rubble_field_$(@('small','medium','large')[$s-1])_3" 24 377 100 134}
  }
  $g.Dispose();$b.Save("$out/mockups/$($view)_stage$s.png");$bg.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor;$bg.DrawImage($b,[Drawing.Rectangle]::new(($s*768),32,768,432));$crop=if($view -eq 'walltop'){[Drawing.Rectangle]::new(408,536,184,184)}else{[Drawing.Rectangle]::new(30,370,160,160)};$bg.FillRectangle([Drawing.Brushes]::Black,($s*768+542),238,220,220);$bg.DrawImage($b,[Drawing.Rectangle]::new(($s*768+546),242,212,212),$crop,[Drawing.GraphicsUnit]::Pixel);$b.Dispose()
  $font=[Drawing.Font]::new('Malgun Gothic',16);$bg.DrawString("$s · $($labels[$s])",$font,[Drawing.Brushes]::White,($s*768+12),2);$font.Dispose()
 }
 $bg.Dispose();$board.Save("$out/mockups/$($view)_four_stages.png");$board.Dispose()
}
