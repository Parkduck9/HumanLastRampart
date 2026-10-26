# W45: 24점 배속 아이콘 (1/2/3 겹삼각형, 어두운 테두리 + 밝은 속). 결정적 System.Drawing 생성.
Add-Type -AssemblyName System.Drawing
$dir = Join-Path $PSScriptRoot "..\..\Game\Assets\_Project\Art\UI\Resources\UiKit"
$ink = [Drawing.Color]::FromArgb(255, 21, 14, 9)
$fill = [Drawing.Color]::FromArgb(255, 238, 228, 207)
$w = 8; $pitch = 7
function Make($name, $count) {
  $bmp = New-Object Drawing.Bitmap 24, 24, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
  for ($y = 0; $y -lt 24; $y++) { for ($x = 0; $x -lt 24; $x++) { $bmp.SetPixel($x, $y, [Drawing.Color]::FromArgb(0, 255, 255, 255)) } }
  $total = $w + $pitch * ($count - 1); $x0 = [int][math]::Floor((24 - $total) / 2)
  for ($i = 0; $i -lt $count; $i++) {
    $ox = $x0 + $i * $pitch
    $mask = @{}
    for ($c = 0; $c -lt $w; $c++) { $half = 7 - [int][math]::Round(7 * $c / ($w - 1)); for ($k = -$half; $k -le $half; $k++) { $mask["$c,$k"] = 1 } }
    foreach ($key in $mask.Keys) {
      $p = $key.Split(','); $c = [int]$p[0]; $k = [int]$p[1]
      $edge = -not ($mask.ContainsKey("$($c-1),$k") -and $mask.ContainsKey("$($c+1),$k") -and $mask.ContainsKey("$c,$($k-1)") -and $mask.ContainsKey("$c,$($k+1)"))
      $bmp.SetPixel($ox + $c, 12 + $k - 0, $(if ($edge) { $ink } else { $fill }))
    }
  }
  $path = Join-Path $dir ($name + ".png")
  $bmp.Save($path, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}
Make "icon_speed1" 1
Make "icon_speed2" 2
Make "icon_speed4" 3
