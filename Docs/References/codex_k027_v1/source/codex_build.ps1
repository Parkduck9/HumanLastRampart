# 생성 원본은 보존하고, 최종 규격 및 내장형 검토 페이지를 다시 만듭니다.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$base = Split-Path $PSScriptRoot -Parent
$root = (Resolve-Path (Join-Path $base '../../..')).Path
$utf8 = New-Object System.Text.UTF8Encoding($false)
$assets = [ordered]@{}
foreach ($name in @('main','victory','defeat','prep')) {
    $original = Join-Path $base "originals/codex_${name}_original.png"
    $target = Join-Path $base "backgrounds/codex_${name}_1920x1080.png"
    $image = [System.Drawing.Image]::FromFile($original)
    $bitmap = New-Object System.Drawing.Bitmap(1920,1080)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    # 구도와 가장자리 인물을 보존합니다. 원본의 미세한 비율 차이만 규격에 맞춥니다.
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($image,0,0,1920,1080)
    $bitmap.Save($target,[System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $bitmap.Dispose(); $image.Dispose()
    $assets[$name] = 'data:image/png;base64,' + [Convert]::ToBase64String([IO.File]::ReadAllBytes($target))
}
foreach ($variant in @('a','b')) {
    $subtitle = if ($variant -eq 'a') { '인류의 마지막 성벽' } else { '마지막 성벽을 지켜라' }
    $svg = @"
<svg xmlns="http://www.w3.org/2000/svg" width="960" height="300" viewBox="0 0 960 300" role="img" aria-label="Human Last Rampart · $subtitle">
<defs><linearGradient id="brass" x2="0" y2="1"><stop stop-color="#f4e2b8"/><stop offset="1" stop-color="#d5b16b"/></linearGradient><filter id="shadow" x="-10%" y="-20%" width="120%" height="140%"><feDropShadow dx="0" dy="3" stdDeviation="4" flood-color="#101613"/></filter></defs>
<g filter="url(#shadow)"><text x="6" y="93" fill="url(#brass)" font-family="Georgia,serif" font-size="80" letter-spacing="6">Human Last</text><text x="0" y="196" fill="url(#brass)" font-family="Georgia,serif" font-size="112" letter-spacing="5">Rampart</text><path d="M5 224 H700" stroke="#d5b16b" stroke-width="2"/><text x="6" y="277" fill="#eee4cf" font-family="Batang,serif" font-size="34" letter-spacing="7">$subtitle</text></g></svg>
"@
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot "codex_title_${variant}.svg"),$svg,$utf8)
    $assets["title_$variant"] = 'data:image/svg+xml;base64,' + [Convert]::ToBase64String($utf8.GetBytes($svg))
}
$template = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'codex_review.template.html'),$utf8)
$html = $template.Replace('__ASSETS__',($assets | ConvertTo-Json -Compress))
[IO.File]::WriteAllText((Join-Path $root '_HTML/codex_K-027_화면배경_v1.html'),$html,$utf8)
Write-Output '배경 4장·제목 SVG 2안·내장형 HTML 작성 완료'
