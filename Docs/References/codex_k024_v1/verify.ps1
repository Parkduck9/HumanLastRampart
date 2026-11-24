$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=$PSScriptRoot
$manifest=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'manifest.json')|ConvertFrom-Json
$checks=New-Object System.Collections.Generic.List[object]
function Check($name,$ok){$checks.Add(@{name=$name;passed=[bool]$ok});if(-not $ok){throw "검사 실패: $name"}}
$heights=@()
foreach($asset in $manifest.assets){
    foreach($stage in $asset.stages){
        $image=[Drawing.Bitmap]::FromFile((Join-Path $root $stage.path))
        Check ($stage.path+' 크기') ($image.Width -eq $stage.width -and $image.Height -eq $stage.height)
        if($asset.kind -eq 'texture'){
            $opaque=$true
            for($y=0;$y -lt $image.Height;$y++){for($x=0;$x -lt $image.Width;$x++){if($image.GetPixel($x,$y).A -ne 255){$opaque=$false}}}
            Check ($stage.path+' 불투명') $opaque
            $base=[Drawing.Bitmap]::FromFile((Join-Path $root $asset.stages[0].path))
            $edge=$true
            for($y=0;$y -lt $image.Height;$y++){for($x=0;$x -lt $image.Width;$x++){
                if(($x -lt 4 -or $y -lt 4 -or $x -ge $image.Width-4 -or $y -ge $image.Height-4) -and $image.GetPixel($x,$y).ToArgb() -ne $base.GetPixel($x,$y).ToArgb()){$edge=$false}
            }}
            Check ($stage.path+' 기존 경계 네 화소 일치') $edge
            $base.Dispose()
        }else{
            $transparent=0;$solid=0;$binary=$true;$edgeClear=$true;$maxY=-1
            for($y=0;$y -lt 64;$y++){for($x=0;$x -lt 64;$x++){
                $alpha=$image.GetPixel($x,$y).A
                if($alpha -eq 0){$transparent++}elseif($alpha -eq 255){$solid++;$maxY=[Math]::Max($maxY,$y)}else{$binary=$false}
                if(($x -eq 0 -or $y -eq 0 -or $x -eq 63 -or $y -eq 63) -and $alpha -ne 0){$edgeClear=$false}
            }}
            Check ($stage.path+' 투명 여백과 실체 존재') ($transparent -gt 0 -and $solid -gt 0)
            Check ($stage.path+' 알파 0 또는 255') $binary
            Check ($stage.path+' 외곽 잘림 없음') $edgeClear
            Check ($stage.path+' 접점 위에 그림') ($maxY -le 55)
            if($asset.id -eq 'corpse_pile'){$heights+= $stage.contentHeight}
        }
        $image.Dispose()
    }
    if($asset.kind -eq 'texture'){
        $source=Join-Path $root ('../../../Game/Assets/_Project/Art/Battle/Resources/BattleArt/World/'+$asset.id+'_albedo.png')
        Check ($asset.id+' 멀쩡 사본 SHA256') ((Get-FileHash -Algorithm SHA256 $source).Hash -eq (Get-FileHash -Algorithm SHA256 (Join-Path $root $asset.stages[0].path)).Hash)
    }
}
Check '시체 더미 높이 순서' ($heights[0] -lt $heights[1] -and $heights[1] -lt $heights[2])
$html=[IO.File]::ReadAllText((Join-Path $root '../../../_HTML/codex_K-024_성벽손상_전장흔적_v1.html'))
Check 'HTML 치환 완료' (-not ($html -match '__MANIFEST__|__IMAGES__|__SOURCES__'))
Check 'HTML 외부 리소스 참조 없음' (-not ($html -match '(src|href)=["'']https?://'))
Check 'HTML 그림 17장 내장' (([regex]::Matches($html,'data:image/png;base64,')).Count -eq 17)
$result=@{date='2026-10-16';checks=$checks;passed=$checks.Count;browser='사용 가능한 브라우저 없음: iab 열기 실패, 브라우저 목록 비어 있음. 실제 브라우저 조작 미검증';unity='실행하지 않음'}
[IO.File]::WriteAllText((Join-Path $root 'qa.json'),($result|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
Write-Output ("정적 검사 {0}개 통과, 시체 높이 {1}" -f $checks.Count,($heights -join '/'))
