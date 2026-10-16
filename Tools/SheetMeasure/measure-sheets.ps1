# 캐릭터 시트 인물 키 재기 (K-034)
# 칸 256×384 격자 시트마다, 칸마다 알파 128 이상(셰이더 _Cutoff 0.5와 같음)인 픽셀의 맨 위·맨 아래 줄과 넓이를 잰다.
# 결과
#  - sheet-heights.md: 시트별 인물 키 표 (사람이 보는 표)
#  - PerfLabSheetHeights.cs: 시트 이름 → 기준 키(px) 표 (게임이 읽는 표, PerfLabArt.FigurePx)
# 기준 키 = 그 시트 인물을 '똑바로 선 키'로 환산한 값
#  - 서 있기·걷기·사격·시전·공격: 0번 칸 키 (0번 칸은 서 있는 자세)
#  - 쓰러짐(Fall/): 자세가 낮아져 키로는 못 잰다 → 짝 시트(같은 인물 서 있기) 키 × √(0번 칸 넓이 / 짝 0번 칸 넓이)
#  - 달리기(Run/): 같은 방법 + 옆모습은 뒷모습보다 좁다(주인공 시트로 잰 0.92)는 보정
# 사용: powershell -ExecutionPolicy Bypass -File Tools/SheetMeasure/measure-sheets.ps1 [-Cells]
param(
    [string]$Root = "$PSScriptRoot/../../Game/Assets/_Project/Art/Characters",
    [string]$Out = "$PSScriptRoot/sheet-heights.md",
    [string]$CsOut = "$PSScriptRoot/../../Game/Assets/_Project/Combat/PerfLab/PerfLabSheetHeights.cs",
    [switch]$Cells   # 칸마다 값도 적는다
)

Add-Type -AssemblyName System.Drawing
$CellW = 256; $CellH = 384; $AlphaCut = 128
$SideFactor = 0.92   # 옆모습 넓이 / 뒷모습 넓이 (주인공 시트: 옆 달리기 행 ÷ 뒷모습 걷기 행)

# 짝 시트 (쓰러짐·달리기 환산 기준): 이름 앞부분 → 같은 인물이 서 있는 시트
$Pairs = [ordered]@{
    'archer_' = 'archer_static'; 'gunner_' = 'gunner_static'; 'cannon_crew_' = 'cannon_crew_static'
    'ladder_goblin_' = 'ladder_goblin_walk'; 'goblin_' = 'goblin_walk_attack4'; 'ogre_' = 'ogre_walk'
    'wizard_' = 'wizard_back_static'; 'hero_' = 'hero_walk_run_grid'
}

function Read-Sheet($file) {
    $bmp = [System.Drawing.Bitmap]::FromFile($file.FullName)
    try {
        if ($bmp.Width % $CellW -ne 0 -or $bmp.Height % $CellH -ne 0) { return $null }   # 칸 규격이 아닌 그림
        $rect = New-Object System.Drawing.Rectangle 0, 0, $bmp.Width, $bmp.Height
        $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $px = New-Object byte[] ($data.Stride * $bmp.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $px, 0, $px.Length)
        $bmp.UnlockBits($data)
        $cols = $bmp.Width / $CellW; $rows = $bmp.Height / $CellH
        $cellsOut = @()
        for ($r = 0; $r -lt $rows; $r++) {
            for ($c = 0; $c -lt $cols; $c++) {
                $top = -1; $bottom = -1; $area = 0
                for ($y = 0; $y -lt $CellH; $y++) {
                    $o = ($r * $CellH + $y) * $data.Stride + $c * $CellW * 4 + 3
                    $hit = $false
                    for ($x = 0; $x -lt $CellW; $x++) { if ($px[$o + $x * 4] -ge $AlphaCut) { $area++; $hit = $true } }
                    if ($hit) { if ($top -lt 0) { $top = $y }; $bottom = $y }
                }
                $cellsOut += [pscustomobject]@{ Index = $r * $cols + $c; Top = $top; Bottom = $bottom; Height = $(if ($top -ge 0) { $bottom - $top + 1 } else { 0 }); Area = $area }
            }
        }
        return [pscustomobject]@{ Name = [IO.Path]::GetFileNameWithoutExtension($file.Name); Rel = $file.FullName.Substring((Resolve-Path $Root).Path.Length + 1).Replace('\', '/'); Cells = $cellsOut }
    }
    finally { $bmp.Dispose() }
}

function Median($v) { $s = @($v | Sort-Object); if ($s.Count -eq 0) { return 0 }; return $s[[int][math]::Floor(($s.Count - 1) / 2)] }

$sheets = @{}
Get-ChildItem -Recurse -Path $Root -Filter *.png | Where-Object { $_.Name -notmatch '_normal' } | Sort-Object FullName | ForEach-Object {
    $s = Read-Sheet $_
    if ($s -ne $null) { $sheets[$s.Name] = $s }
}

$lines = @(
    "# 캐릭터 시트 인물 키 표 (자동 생성: Tools/SheetMeasure/measure-sheets.ps1)", "",
    "칸 256×384 기준, 알파 128 이상. 머리·발 = 칸 위에서 잰 줄(px). 키 = 발 − 머리 + 1. 넓이는 √(불투명 픽셀 수).",
    "기준 키 = 똑바로 선 키로 환산한 값 (서 있기류: 0번 칸 키 / 쓰러짐·달리기: 짝 시트 키 × 넓이 비, 달리기는 옆모습 보정 $SideFactor).",
    "병사·주인공·마법사 그리기 배율 = 384 ÷ 기준 키 (인물 키 = 그리기 크기). 적은 자기 기본 시트 기준 키 ÷ 이 시트 기준 키.", "",
    "| 시트 | 칸 수 | 0번 칸 키 | 키 가운데값 | 키 최대 | 머리(0번) | 발(0번) | 발 최대 | √넓이(0번) | 짝 시트 | 기준 키 | 병사 기준 배율 |",
    "|---|---|---|---|---|---|---|---|---|---|---|---|")
$detail = @()
$table = [ordered]@{}

foreach ($name in ($sheets.Keys | Sort-Object)) {
    $s = $sheets[$name]
    $c0 = $s.Cells | Where-Object { $_.Height -gt 0 } | Select-Object -First 1
    if ($c0 -eq $null) { continue }
    $kind = if ($s.Rel -match '^Fall/') { 'fall' } elseif ($s.Rel -match '^Run/') { 'run' } elseif ($s.Rel -match '^Ladder/ladder_(front|side)') { 'prop' } else { 'upright' }
    if ($kind -eq 'prop') { continue }   # 사다리 소품 (인물 아님)
    $pair = ''
    foreach ($k in $Pairs.Keys) { if ($name.StartsWith($k)) { $pair = $Pairs[$k]; break } }
    $figure = [double]$c0.Height
    if ($kind -ne 'upright' -and $pair -ne '' -and $sheets.ContainsKey($pair)) {
        $p0 = $sheets[$pair].Cells | Where-Object { $_.Height -gt 0 } | Select-Object -First 1
        $figure = $p0.Height * [math]::Sqrt($c0.Area / [double]$p0.Area)
        if ($kind -eq 'run') { $figure /= $SideFactor }
    }
    $figure = [math]::Round($figure, 1)
    $table[$name] = $figure
    $heights = $s.Cells | ForEach-Object { $_.Height }
    $lines += "| $($s.Rel) | $($s.Cells.Count) | $($c0.Height) | $(Median $heights) | $(($heights | Measure-Object -Maximum).Maximum) | $($c0.Top) | $($c0.Bottom) | $(($s.Cells | ForEach-Object { $_.Bottom } | Measure-Object -Maximum).Maximum) | $([int][math]::Sqrt($c0.Area)) | $(if ($kind -ne 'upright') { $pair } else { '-' }) | $figure | $([math]::Round($CellH / $figure, 3)) |"
    if ($Cells) { foreach ($c in $s.Cells) { $detail += "| $name | $($c.Index) | $($c.Top) | $($c.Bottom) | $($c.Height) | $([int][math]::Sqrt($c.Area)) |" } }
}

if ($Cells) {
    $lines += ""; $lines += "## 칸마다"; $lines += ""
    $lines += "| 시트 | 칸 | 머리 | 발 | 키 | √넓이 |"; $lines += "|---|---|---|---|---|---|"
    $lines += $detail
}

$utf8Bom = New-Object System.Text.UTF8Encoding $true
[IO.File]::WriteAllLines([IO.Path]::GetFullPath($Out), [string[]]$lines, $utf8Bom)

# 게임이 읽는 표 (C#)
$cs = @(
    "using System.Collections.Generic;", "",
    "namespace HumanLastRampart.Combat.PerfLab", "{",
    "    // 자동 생성 — 손으로 고치지 말고 Tools/SheetMeasure/measure-sheets.ps1을 다시 돌린다 (K-034)",
    "    // 시트(텍스처 이름) → 칸 384px 안 인물 기준 키(px, 똑바로 선 키로 환산). 발 줄은 모두 칸 위에서 352 (표: Tools/SheetMeasure/sheet-heights.md)",
    "    public static class PerfLabSheetHeights", "    {",
    "        public static readonly Dictionary<string, float> FigurePx = new Dictionary<string, float>", "        {")
foreach ($k in $table.Keys) { $cs += ('            {{ "{0}", {1}f }},' -f $k, $table[$k].ToString([Globalization.CultureInfo]::InvariantCulture)) }
$cs += @("        };", "    }", "}")
$utf8 = New-Object System.Text.UTF8Encoding $false
[IO.File]::WriteAllText([IO.Path]::GetFullPath($CsOut), (($cs -join "`n") + "`n"), $utf8)

$lines | ForEach-Object { $_ }
