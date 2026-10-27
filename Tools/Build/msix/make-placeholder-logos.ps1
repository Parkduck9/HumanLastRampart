# 임시 로고 생성기 (K-130 최종 그림이 오기 전까지 쓰는 자리표시). 앱 아이콘 app_icon_1024.png 로 Images 폴더를 채운다.
# 파일 이름은 최종 로고와 똑같이 두었으니, 그림만 같은 이름으로 덮어쓰면 된다.
# 사용: powershell -File make-placeholder-logos.ps1 [-Source <png>] [-Out <폴더>]
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\..\..\Game\Assets\_Project\Art\AppIcon\app_icon_1024.png'),
    [string]$Out = (Join-Path $PSScriptRoot 'Images')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$Source = (Resolve-Path $Source).Path
New-Item -ItemType Directory -Force $Out | Out-Null
$src = [Drawing.Image]::FromFile($Source)
$bg = [Drawing.ColorTranslator]::FromHtml('#101820')

# 가로 w, 세로 h 캔버스에 아이콘을 가운데 맞춰 그린다. 배경색을 주면 칠하고, 없으면 투명.
function New-Logo([string]$name, [int]$w, [int]$h, [double]$fill, [bool]$solid) {
    $bmp = New-Object Drawing.Bitmap $w, $h, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'; $g.SmoothingMode = 'HighQuality'; $g.PixelOffsetMode = 'HighQuality'
    if ($solid) { $g.Clear($bg) } else { $g.Clear([Drawing.Color]::Transparent) }
    $side = [int]([math]::Min($w, $h) * $fill)
    $g.DrawImage($src, [int](($w - $side) / 2), [int](($h - $side) / 2), $side, $side)
    $g.Dispose()
    $bmp.Save((Join-Path $Out $name), [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

foreach ($sc in 100, 200) {
    $k = $sc / 100
    New-Logo "Square44x44Logo.scale-$sc.png" (44 * $k) (44 * $k) 1.0 $false
    New-Logo "Square150x150Logo.scale-$sc.png" (150 * $k) (150 * $k) 1.0 $true
    New-Logo "Wide310x150Logo.scale-$sc.png" (310 * $k) (150 * $k) 0.9 $true
    New-Logo "StoreLogo.scale-$sc.png" (50 * $k) (50 * $k) 1.0 $false
    New-Logo "SplashScreen.scale-$sc.png" (620 * $k) (300 * $k) 0.6 $true
}
foreach ($t in 16, 24, 32, 48, 256) {
    New-Logo "Square44x44Logo.targetsize-$t.png" $t $t 1.0 $false
    New-Logo "Square44x44Logo.targetsize-${t}_altform-unplated.png" $t $t 1.0 $false
}
$src.Dispose()
Write-Output "임시 로고 $((Get-ChildItem $Out -Filter *.png).Count)장 생성: $Out"