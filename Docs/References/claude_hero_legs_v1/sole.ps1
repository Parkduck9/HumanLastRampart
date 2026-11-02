param([string]$Src,[string]$Dst,[switch]$Normal)
Add-Type -AssemblyName System.Drawing
$b=New-Object Drawing.Bitmap $Src; $o=New-Object Drawing.Bitmap $b
$outline=[Drawing.Color]::FromArgb(255,26,18,22)
foreach($r in 1,2){ for($c=0;$c -lt 4;$c++){
  $x0=$c*288;$y0=$r*384
  # shift the cell content up by 3 px (one art pixel)
  for($y=0;$y -lt 381;$y++){ for($x=0;$x -lt 288;$x++){ $o.SetPixel($x0+$x,$y0+$y,$b.GetPixel($x0+$x,$y0+$y+3)) } }
  for($y=381;$y -lt 384;$y++){ for($x=0;$x -lt 288;$x++){ $o.SetPixel($x0+$x,$y0+$y,[Drawing.Color]::FromArgb(0,0,0,0)) } }
  # new bottom art row y=357..359: outline under every opaque column of y=354..356, widened by one art pixel toward the toe/heel
  $cols=@{}
  for($x=0;$x -lt 288;$x+=3){ if($o.GetPixel($x0+$x,$y0+355).A -gt 128){ $cols[$x]=1 } }
  $fill=@{}; $xs=@($cols.Keys | Sort-Object); $i=0; while($i -lt $xs.Count){ $j=$i; while($j+1 -lt $xs.Count -and $xs[$j+1] -eq $xs[$j]+3){$j++}; if(($j-$i+1) -ge 3){ for($k=$i;$k -le $j;$k++){ $fill[$xs[$k]]=1 } } else { for($k=$i;$k -le $j;$k++){ for($dx=0;$dx -lt 3;$dx++){ for($dy=354;$dy -le 356;$dy++){ if(-not $Normal -and $o.GetPixel($x0+$xs[$k]+$dx,$y0+$dy).A -gt 128){ $o.SetPixel($x0+$xs[$k]+$dx,$y0+$dy,$outline) } } } } }; $i=$j+1 }
  foreach($x in @($cols.Keys)){ if(-not $cols.ContainsKey($x-3) -and -not $cols.ContainsKey($x+3)){ } }
  foreach($x in $fill.Keys){ for($dx=0;$dx -lt 3;$dx++){ for($dy=357;$dy -le 359;$dy++){
     if($Normal){ $o.SetPixel($x0+$x+$dx,$y0+$dy,$o.GetPixel($x0+$x+$dx,$y0+354)) } else { $o.SetPixel($x0+$x+$dx,$y0+$dy,$outline) } } } }
  # darken the old bottom art row into a sole band where it was boot colour (not already outline)
  if(-not $Normal){ foreach($x in $fill.Keys){ for($dx=0;$dx -lt 3;$dx++){ for($dy=354;$dy -le 356;$dy++){ $p=$o.GetPixel($x0+$x+$dx,$y0+$dy); if(($p.R+$p.G+$p.B) -ge 150){ $o.SetPixel($x0+$x+$dx,$y0+$dy,[Drawing.Color]::FromArgb(255,[int]($p.R*0.62),[int]($p.G*0.6),[int]($p.B*0.62))) } } } } }
} }
$o.Save($Dst); $b.Dispose()
