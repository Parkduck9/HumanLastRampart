$ErrorActionPreference='Stop'
. "$PSScriptRoot/prepare.ps1"
$fp=[K052]::FlamePalette($crew,@('#FFF49A','#FFB52A','#E85508'))
$pal=@($sp)+@($fp)
$src=[Drawing.Bitmap]::new("$PSScriptRoot/crew_ignite_source.png")
$sheet=[Drawing.Bitmap]::new(2304,384);$info=@()
for($i=0;$i -lt 6;$i++){
 $cell=[K052]::Crop($src,$i*($src.Width/6),0,$src.Width/6,$src.Height);[K052]::Clean($cell)
 $top=[K052]::BodyTop($cell);$r=[K052]::Bounds($cell);$scale=210.0/($r.Bottom-$top)
 $hot=[K052]::Hot($cell,$top)
 $crop=[K052]::Crop($cell,$r.X,$r.Y,$r.Width,$r.Height)
 $body=[K052]::Quant([K052]::Resize($crop,[int][Math]::Round($r.Width*$scale),[int][Math]::Round($r.Height*$scale)),$pal)
 $dy=352-$body.Height;$dx=192-[K052]::Sole($body)
 $frame=[Drawing.Bitmap]::new(384,384);[K052]::Paste($frame,$body,$dx,$dy)
 [K052]::ThinOutline($frame,3)
 [K052]::Save($frame,"$PSScriptRoot/crew_ignite_$($i+1).png");[K052]::Paste($sheet,$frame,$i*384,0)
 $info+=@{frame=$i;hotX=[int][Math]::Round(($hot.X-$r.X)*$scale)+$dx;hotY=[int][Math]::Round(($hot.Y-$r.Y)*$scale)+$dy;bodyHeight=210;footY=352;offset=$dx}
}
[K052]::Save($sheet,"$PSScriptRoot/crew_ignite6.png")
@{flamePalette=$fp;frames=$info;fuse=@(269,$info[3].hotY)}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 "$PSScriptRoot/ignite_measurements.json"



# 점화는 발을 옮기지 않는다. 하체를 첫 칸과 같은 픽셀로 유지한다.
$first=[K052]::LoadCopy("$PSScriptRoot/crew_ignite_1.png")
$fixed=[Drawing.Bitmap]::new(2304,384)
for($i=0;$i -lt 6;$i++){
 $cell=[K052]::LoadCopy("$PSScriptRoot/crew_ignite_$($i+1).png")
 [K052]::Feet($cell,$first)
 [K052]::Save($cell,"$PSScriptRoot/crew_ignite_$($i+1).png")
 [K052]::Paste($fixed,$cell,384*$i,0)
}
[K052]::Save($fixed,"$PSScriptRoot/crew_ignite6.png")


