$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = 'C:/Users/User/.codex/generated_images/01a156df-f8dc-7261-a5d6-4f1ed93d9638'
$assets = @(
    @('exec-ed92e578-11a7-4e06-a1c7-2c960f26b3ea.png','main_menu_keyart.png',1920,1080),
    @('exec-87d158b7-e511-43d5-9a92-6b5c26d844a6.png','cover.png',1920,1080),
    @('exec-b45408fd-46ac-4d64-a6ff-ae2507a9f569.png','title_logo.png',1774,887),
    @('exec-b2ab7236-d638-4e91-adff-60176b6a949f.png','title_logo_wide.png',2172,724)
)
function Export-Nearest($inputImage,$outputPath,$width,$height,$rect) {
    $canvas = New-Object Drawing.Bitmap($width,$height,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($canvas)
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.DrawImage($inputImage,[Drawing.Rectangle]::new(0,0,$width,$height),$rect,[Drawing.GraphicsUnit]::Pixel)
    $canvas.Save($outputPath,[Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $canvas.Dispose()
}
foreach ($asset in $assets) {
    $inputImage = [Drawing.Bitmap]::FromFile((Join-Path $source $asset[0]))
    $full = [Drawing.Rectangle]::new(0,0,$inputImage.Width,$inputImage.Height)
    Export-Nearest $inputImage (Join-Path $PSScriptRoot $asset[1]) $asset[2] $asset[3] $full
    if ($asset[1] -in @('main_menu_keyart.png','cover.png')) {
        Export-Nearest $inputImage (Join-Path $PSScriptRoot ($asset[1].Replace('.png','_960.png'))) 960 540 $full
    }
    $inputImage.Dispose()
}
$details = @(
    @('main_menu_keyart.png','hero_detail_4x.png',1450,360,256,320),
    @('title_logo.png','logo_detail_4x.png',700,170,256,192)
)
foreach ($detail in $details) {
    $inputImage = [Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot $detail[0]))
    Export-Nearest $inputImage (Join-Path $PSScriptRoot $detail[1]) ($detail[4]*4) ($detail[5]*4) ([Drawing.Rectangle]::new($detail[2],$detail[3],$detail[4],$detail[5]))
    $inputImage.Dispose()
}
