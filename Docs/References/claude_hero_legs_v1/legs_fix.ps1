param([string]$Sheet,[string]$Idle,[string]$Dst)
# rows 0 (back walk) and 3 (front walk): replace legs below the coat with the idle sprite's two legs, one leg lifted per step
Add-Type -AssemblyName System.Drawing
$s=New-Object Drawing.Bitmap $Sheet; $i=New-Object Drawing.Bitmap $Idle; $o=New-Object Drawing.Bitmap $s
$clear=[Drawing.Color]::FromArgb(0,0,0,0)
$cut=@{0=321;3=312}            # first leg row under the coat hem (measured)
$lift=@(@(6,0),@(0,0),@(0,6),@(0,0))   # (left, right) lift in px per frame, 2 art pixels
foreach($r in 0,3){ for($c=0;$c -lt 4;$c++){
  $x0=$c*288;$y0=$r*384;$cy=$cut[$r]
  for($y=$cy;$y -lt 384;$y++){ for($x=0;$x -lt 288;$x++){ $o.SetPixel($x0+$x,$y0+$y,$clear) } }
  for($x=0;$x -lt 288;$x++){
    $L= if($x -lt 144){$lift[$c][0]}else{$lift[$c][1]}
    for($y=$cy;$y -lt 384;$y++){ $sy=$y+$L; if($sy -ge 384){continue}
      $p=$i.GetPixel($x,$sy); if($p.A -gt 0 -and $sy -ge 312){ $o.SetPixel($x0+$x,$y0+$y,$p) } } }
} }
$o.Save($Dst)
