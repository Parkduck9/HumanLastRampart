$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$artRoot = Split-Path $PSScriptRoot -Parent
$paths = Get-Content (Join-Path $PSScriptRoot 'codex_generated_paths.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$bounds = Get-Content (Join-Path $PSScriptRoot 'codex_bounds.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($item in $paths.PSObject.Properties) {
    $id = $item.Name
    $original = Join-Path $artRoot ('originals/codex_' + $id + '_original.png')
    if (!(Test-Path $original)) { Copy-Item -LiteralPath $item.Value -Destination $original }
    $image = [System.Drawing.Image]::FromFile($original)
    try {
        if ($id.StartsWith('yard_')) {
            $canvas = New-Object System.Drawing.Bitmap 3840,1080
            $graphics = [System.Drawing.Graphics]::FromImage($canvas)
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            # 하늘과 가까운 바닥을 일부 덜어내고 같은 비율로 확대한다.
            $cropH = $image.Width * 1080.0 / 3840.0
            $src = New-Object System.Drawing.RectangleF 0,($image.Height * 0.18),$image.Width,$cropH
            $dst = New-Object System.Drawing.RectangleF 0,0,3840,1080
            $graphics.DrawImage($image,$dst,$src,[System.Drawing.GraphicsUnit]::Pixel)
            $destination = Join-Path $artRoot ('backgrounds/codex_' + $id + '_3840x1080.png')
        } else {
            $isPortrait = $id.StartsWith('portrait_')
            if ($isPortrait) { $cw=512; $ch=512; $targetH=440; $anchorX=256; $anchorY=480 } else {
                $cw=1536; $ch=1024; $anchorX=768; $anchorY=928
                $targetH = @{repair=640;barracks=704;workshop=544;stairs=800}[$id]
            }
            $canvas = New-Object System.Drawing.Bitmap $cw,$ch
            $graphics = [System.Drawing.Graphics]::FromImage($canvas)
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $box = $bounds.$id
            $scale = $targetH / ($box[3] - $box[1])
            $dx = $anchorX - (($box[0]+$box[2])/2.0)*$scale
            $dy = $anchorY - $box[3]*$scale
            $dst = New-Object System.Drawing.RectangleF $dx,$dy,($image.Width*$scale),($image.Height*$scale)
            $graphics.DrawImage($image,$dst)
            $folder = if($isPortrait){'result'}else{'stations'}
            $destination = Join-Path $artRoot ($folder+'/codex_'+$id+'.png')
        }
        $canvas.Save($destination,[System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose()
        $canvas.Dispose()
    } finally { $image.Dispose() }
}
Write-Output '배경·장소·초상 PNG 재출력 완료'
