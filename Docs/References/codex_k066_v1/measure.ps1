Add-Type -AssemblyName System.Drawing
$src=[Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot '../codex_hero_octopath_review_v6/generated.png'))
$b=New-Object Drawing.Bitmap 64,96
for($y=0;$y -lt 96;$y++){for($x=0;$x -lt 64;$x++){$b.SetPixel($x,$y,[Drawing.Color]::Black)}}
for($y=0;$y -lt 52;$y++){for($x=0;$x -lt 22;$x++){$sx=266+[int][Math]::Floor(($x+0.5)*502/22);$sy=224+[int][Math]::Floor(($y+0.5)*1168/52);$b.SetPixel(21+$x,36+$y,$src.GetPixel($sx,$sy))}}
$b.Save((Join-Path $PSScriptRoot 'hero_generated_grid.png'),[Drawing.Imaging.ImageFormat]::Png)
$big=New-Object Drawing.Bitmap 256,384
for($y=0;$y -lt 384;$y++){for($x=0;$x -lt 256;$x++){$big.SetPixel($x,$y,$b.GetPixel([int][Math]::Floor($x/4),[int][Math]::Floor($y/4)))}}
$big.Save((Join-Path $PSScriptRoot 'hero_generated_grid_4x.png'),[Drawing.Imaging.ImageFormat]::Png)
$src.Dispose();$b.Dispose();$big.Dispose()
