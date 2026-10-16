$ErrorActionPreference='Stop'
. "$PSScriptRoot/prepare.ps1"
# 세 칸씩 같은 발을 딛도록 원안의 칸을 재배열한다.
$order=@(0,1,5,3,4,2)
$rows=@()
foreach($kind in @('pull','push')){
 $source=[Drawing.Bitmap]::new("$PSScriptRoot/crew_${kind}_source.png")
 $sheet=[Drawing.Bitmap]::new(2304,384)
 $frames=@()
 for($i=0;$i -lt 6;$i++){
  $cell=[K052]::Crop($source,$order[$i]*($source.Width/6),0,($source.Width/6),$source.Height)
  [K052]::Clean($cell);$r=[K052]::Bounds($cell)
  $body=[K052]::Resize([K052]::Crop($cell,$r.X,$r.Y,$r.Width,$r.Height),[int][Math]::Round($r.Width*210.0/$r.Height),210)
  $body=[K052]::Quant($body,$sp)
  if($kind -eq 'pull'){$body=[K052]::Eye($body)}
  $frame=[Drawing.Bitmap]::new(384,384)
  $sole=[K052]::Sole($body)
  $target=128+32*($i%3)
  $dx=$target-$sole
  [K052]::Paste($frame,$body,$dx,142)
  [K052]::ThinOutline($frame,3)
  $delta=$target-[K052]::Sole($frame);if($delta -ne 0){$aligned=[Drawing.Bitmap]::new(384,384);[K052]::Paste($aligned,$frame,$delta,0);$frame=$aligned;$dx+=$delta}
  [K052]::Save($frame,"$PSScriptRoot/crew_${kind}_$($i+1).png")
  [K052]::Paste($sheet,$frame,384*$i,0)
  $hand=[K052]::Hand($frame,($kind -eq 'pull'));
  $frames+=@{handX=$hand.X;handY=$hand.Y;frame=$i;sole=$target;footY=352;worldX=$target-32*$i;offset=$dx;sourceColumn=$order[$i];bounds=[K052]::Bounds($frame).ToString();outline=[K052]::Outline($frame)}
 }
 [K052]::Save($sheet,"$PSScriptRoot/crew_${kind}6.png")
 $rows+=@{kind=$kind;frames=$frames}
}
$rows|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 "$PSScriptRoot/walk_measurements.json"





