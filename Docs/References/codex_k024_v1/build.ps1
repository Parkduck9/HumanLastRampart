$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = $PSScriptRoot
$world = Join-Path $root '../../../Game/Assets/_Project/Art/Battle/Resources/BattleArt/World'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$assets = New-Object System.Collections.Generic.List[object]

# 생성 원안을 자르고 최근접 보간으로 출력한다. 새 그림을 그리는 단계는 아니다.
function Resize-Part($image, $rect, $width, $height) {
    $output = New-Object Drawing.Bitmap($width, $height)
    $graphics = [Drawing.Graphics]::FromImage($output)
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.DrawImage($image, [Drawing.Rectangle]::new(0,0,$width,$height), $rect, [Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()
    return ,$output
}
function Save-Png($bitmap, $relative) {
    $bitmap.Save((Join-Path $root $relative), [Drawing.Imaging.ImageFormat]::Png)
}
$atlas = [Drawing.Bitmap]::FromFile((Join-Path $root 'sources/stone_damage_source.png'))
foreach ($surface in @('wall_stone','parapet_side','parapet_top')) {
    $base = [Drawing.Bitmap]::FromFile((Join-Path $world ($surface + '_albedo.png')))
    $name = $surface + '_albedo.png'
    Copy-Item -LiteralPath (Join-Path $world $name) -Destination (Join-Path $root ('references/' + $name))
    $assets.Add([ordered]@{id=$surface; label=(@{wall_stone='성벽 바깥면';parapet_side='난간 옆면';parapet_top='난간 윗면'})[$surface]; kind='texture'; stages=@()})
    $item = $assets[$assets.Count-1]
    $item.stages += [ordered]@{label='멀쩡'; path='references/'+$name; width=$base.Width; height=$base.Height; source='기존 승인 그림의 동일 사본'}
    foreach ($stage in 1,2) {
        $half = [int]($atlas.Width/2)
        if ($surface -eq 'wall_stone') { $rect = [Drawing.Rectangle]::new(($stage-1)*$half,0,$half,570) }
        elseif ($stage -eq 1) { $rect = [Drawing.Rectangle]::new(0,630,$half,320) }
        else { $rect = [Drawing.Rectangle]::new($half,760,$half,320) }
        $out = Resize-Part $atlas $rect $base.Width $base.Height
        # 타일 바깥 네 화소 띠를 기존 그림 그대로 유지해 혼합 배치 경계를 보존한다.
        for ($y=0; $y -lt $out.Height; $y++) { for ($x=0; $x -lt $out.Width; $x++) {
            if ($x -lt 4 -or $y -lt 4 -or $x -ge $out.Width-4 -or $y -ge $out.Height-4) { $out.SetPixel($x,$y,$base.GetPixel($x,$y)) }
        }}
        $relative = 'textures/'+$surface+'_damage'+$stage+'_albedo.png'
        Save-Png $out $relative
        $item.stages += [ordered]@{label=(@{1='금감';2='부서짐'})[$stage]; path=$relative; width=$out.Width; height=$out.Height; source='sources/stone_damage_source.png'; crop=@($rect.X,$rect.Y,$rect.Width,$rect.Height); borderPixels=4}
        $out.Dispose()
    }
    $base.Dispose()
}
$atlas.Dispose()

# 셀 안의 불투명 영역을 찾아 같은 캔버스와 지면 접점에 맞춘다.
foreach ($spec in @(
    @{file='corpse_pile_source.png'; count=3; id='corpse_pile'; label='시체 더미'; names=@('낮음','중간','높음'); width=48},
    @{file='trap_source.png'; count=2; id='trap'; label='설치된 함정'; names=@('묻힌 표시','터진 구덩이'); width=56}
)) {
    $sheet = [Drawing.Bitmap]::FromFile((Join-Path $root ('sources/'+$spec.file)))
    $cellWidth = [int]($sheet.Width/$spec.count)
    $item = [ordered]@{id=$spec.id;label=$spec.label;kind='sprite';stages=@()}
    for ($i=0; $i -lt $spec.count; $i++) {
        $left=$cellWidth; $top=$sheet.Height; $right=-1; $bottom=-1
        for ($y=0; $y -lt $sheet.Height; $y++) { for ($x=0; $x -lt $cellWidth; $x++) {
            if ($sheet.GetPixel($i*$cellWidth+$x,$y).A -ge 128) {
                $left=[Math]::Min($left,$x); $right=[Math]::Max($right,$x)
                $top=[Math]::Min($top,$y); $bottom=[Math]::Max($bottom,$y)
            }
        }}
        if ($right -lt $left) { throw '투명 셀만 발견됨' }
        $rect=[Drawing.Rectangle]::new($i*$cellWidth+$left,$top,$right-$left+1,$bottom-$top+1)
        $w=$spec.width; $h=[int][Math]::Round($rect.Height*$w/$rect.Width)
        if ($h -gt 48) { $w=[int][Math]::Round($w*48/$h); $h=48 }
        $part = Resize-Part $sheet $rect $w $h
        $out = New-Object Drawing.Bitmap(64,64)
        $offset=[int][Math]::Floor((64-$w)/2)
        for ($y=0; $y -lt $h; $y++) { for ($x=0; $x -lt $w; $x++) {
            $color=$part.GetPixel($x,$y)
            if ($color.A -ge 128) { $out.SetPixel($offset+$x,56-$h+$y,[Drawing.Color]::FromArgb(255,$color.R,$color.G,$color.B)) }
        }}
        $relative='sprites/'+$spec.id+'_'+($i+1)+'.png'
        Save-Png $out $relative
        $item.stages += [ordered]@{label=$spec.names[$i];path=$relative;width=64;height=64;contentWidth=$w;contentHeight=$h;pivotPixels=@(32,56);source='sources/'+$spec.file;crop=@($rect.X,$rect.Y,$rect.Width,$rect.Height)}
        $out.Dispose();$part.Dispose()
    }
    $assets.Add($item); $sheet.Dispose()
}
$manifest=[ordered]@{card='K-024';version=1;status='사용자 확정 전';pixelsPerMeter=32;assets=$assets}
[IO.File]::WriteAllText((Join-Path $root 'manifest.json'),($manifest|ConvertTo-Json -Depth 14),$utf8)

# 독립 HTML에는 최종 PNG와 원안을 모두 내장한다.
$embedded=@{}
foreach ($asset in $assets) { foreach ($stage in $asset.stages) { $embedded[$stage.path]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $root $stage.path))) }}
$sources=@('stone_damage_source.png','corpse_pile_source.png','trap_source.png') | ForEach-Object { @{name=$_;url='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $root ('sources/'+$_))))} }
$template=[IO.File]::ReadAllText((Join-Path $root 'review.template.html'))
$html=$template.Replace('__MANIFEST__',($manifest|ConvertTo-Json -Compress -Depth 14)).Replace('__IMAGES__',($embedded|ConvertTo-Json -Compress -Depth 3)).Replace('__SOURCES__',($sources|ConvertTo-Json -Compress -Depth 3))
$htmlPath=Join-Path $root '../../../_HTML/codex_K-024_성벽손상_전장흔적_v1.html'
[IO.File]::WriteAllText($htmlPath,$html,$utf8)
# 세 단계씩 나란히 놓은 정적 검토판도 보관한다.
$board=New-Object Drawing.Bitmap(1200,1290)
$g=[Drawing.Graphics]::FromImage($board)
$g.Clear([Drawing.ColorTranslator]::FromHtml('#101613'))
$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
$font=New-Object Drawing.Font('맑은 고딕',15)
$g.DrawString('K-024 성벽 손상·전장 흔적 — 사용자 확정 전', $font,[Drawing.Brushes]::Wheat,24,16)
$row=0
foreach($asset in $assets){
    $y=68+$row*235
    $g.DrawString($asset.label,$font,[Drawing.Brushes]::Wheat,24,$y)
    for($j=0;$j -lt $asset.stages.Count;$j++){
        $stage=$asset.stages[$j];$x=24+$j*390
        $g.DrawString($stage.label,$font,[Drawing.Brushes]::LightGray,$x,$y+30)
        $picture=[Drawing.Image]::FromFile((Join-Path $root $stage.path))
        $factor=1.25
        if($asset.kind -eq 'sprite'){$factor=2}
        $g.DrawImage($picture,[Drawing.Rectangle]::new($x,$y+65,[int]($picture.Width*$factor),[int]($picture.Height*$factor)))
        $picture.Dispose()
    }
    $row++
}
$g.DrawString('질감 32px/m · 부서짐은 표면 표현 · Unity 실제 화면 미검증',$font,[Drawing.Brushes]::LightGray,24,1250)
$g.Dispose();$font.Dispose();Save-Png $board 'contact_sheet.png';$board.Dispose()
Write-Output '그림 14장, 원안 3장, 독립 HTML 출력 완료'
