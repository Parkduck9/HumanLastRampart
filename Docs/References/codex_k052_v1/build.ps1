$ErrorActionPreference='Stop'
. "$PSScriptRoot/prepare.ps1"
$src=[Drawing.Bitmap]::new("$PSScriptRoot/cannon_source.png")
$barrel=[K052]::Poly($src,@(560,350,640,290,690,215,750,260,1880,135,1975,135,1983,325,1940,330,770,459,650,477,560,420))
$barrel=[K052]::Resize([K052]::Crop($barrel,560,135,1423,342),514,120)
$carriage=[K052]::Poly($src,@(20,590,710,467,745,425,855,366,1080,350,1080,385,1210,385,1210,720,20,720))
$carriage=[K052]::Crop($carriage,20,350,1190,370)
$carriage=[K052]::StretchX($carriage,810,125,117,130)
$c=[Drawing.Bitmap]::new(768,384)
[K052]::Paste($c,$carriage,160,232)
[K052]::Paste($c,$barrel,220,128)
$c=[K052]::Quant($c,$cp);[K052]::Clean($c)
[K052]::Save($c,"$PSScriptRoot/cannon_body.png")
$wheel=[K052]::Disc([K052]::Resize([K052]::Crop($src,848,392,324,306),101,101))
$wheel=[K052]::Quant($wheel,$cp)
[K052]::EraseDisc($c,337,302,54)
[K052]::Paste($c,$wheel,286,251)
[K052]::Line($c,269,162,269,132,'#4E3627',3)
[K052]::Line($c,269,162,269,132,'#B78154',1)
[K052]::Save($c,"$PSScriptRoot/cannon_body.png")
[K052]::Save($wheel,"$PSScriptRoot/cannon_wheel.png")
# 움직이는 바퀴를 얹기 위한 고정 몸체
[K052]::EraseDisc($c,336,301,51)
[K052]::Save($c,"$PSScriptRoot/cannon_chassis.png")




