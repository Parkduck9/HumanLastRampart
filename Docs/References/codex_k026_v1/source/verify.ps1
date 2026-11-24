# 실제 PNG의 크기·알파·여백·팔레트·서로 다른 형태와 HTML 내장 데이터를 확인한다.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$repo=[IO.Path]::GetFullPath((Join-Path $root '../../..'))
$manifest=[IO.File]::ReadAllText((Join-Path $root 'manifest.json')) | ConvertFrom-Json
$checks=0
function Check($condition,$message){if(-not $condition){throw $message}; $script:checks++}
Check ($manifest.icons.Count -eq 22) '아이콘 수가 다릅니다.'
$allowed=@($manifest.palette.psobject.Properties | ForEach-Object {$_.Value.ToUpperInvariant()})
$hashes=@{}
foreach($item in $manifest.icons){
    Check (Test-Path (Join-Path $root "svg/$($item.id).svg")) 'SVG 원본이 없습니다.'
    foreach($size in @(24,32,48)){
        $path=Join-Path $root $item.files."$size"
        $b=[Drawing.Bitmap]::FromFile($path)
        try {
            Check ($b.Width -eq $size -and $b.Height -eq $size) 'PNG 크기가 다릅니다.'
            $opaque=0; $invalid=0; $edge=0
            for($y=0;$y -lt $size;$y++){for($x=0;$x -lt $size;$x++){
                $c=$b.GetPixel($x,$y)
                if($c.A -eq 0){continue}
                $opaque++
                $hex='#{0:X2}{1:X2}{2:X2}' -f $c.R,$c.G,$c.B
                if($c.A -ne 255 -or $allowed -notcontains $hex){$invalid++}
                if($x -eq 0 -or $y -eq 0 -or $x -eq ($size-1) -or $y -eq ($size-1)){$edge++}
            }}
            Check ($opaque -gt 20 -and $opaque -lt $size*$size) '그림이 비어 있거나 투명이 없습니다.'
            Check ($invalid -eq 0) '반투명 또는 팔레트 밖 색이 있습니다.'
            Check ($edge -eq 0) "가장자리에 그림이 닿습니다: $($item.id) / $size"
        } finally {$b.Dispose()}
        $hash=(Get-FileHash $path -Algorithm SHA256).Hash
        Check (-not $hashes.ContainsKey($hash)) '동일한 PNG가 중복되었습니다.'
        $hashes[$hash]=$path
    }
}
$html=[IO.File]::ReadAllText((Join-Path $repo '_HTML/codex_K-026_UI아이콘_v1.html'))
Check ($html -notmatch '__ICON_DATA__') '내장 데이터 치환이 빠졌습니다.'
Check ($html -notmatch '(?:src|href)=["'']https?://') '외부 리소스가 있습니다.'
$embedded=[regex]::Match($html,'const icons=(.*?);\r?\nconst groups=',[Text.RegularExpressions.RegexOptions]::Singleline).Groups[1].Value | ConvertFrom-Json
Check ($embedded.Count -eq 22) 'HTML에 22종이 없습니다.'
foreach($item in $embedded){foreach($size in @(24,32,48)){
    $bytes=[Convert]::FromBase64String(($item.images."$size" -split ',')[1])
    $fileBytes=[IO.File]::ReadAllBytes((Join-Path $root $item.files."$size"))
    Check ([Convert]::ToBase64String($bytes) -ceq [Convert]::ToBase64String($fileBytes)) '내장 PNG가 원본과 다릅니다.'
}}
$result="PNG·SVG·HTML 정적 검증: $checks 항목 통과. 실제 브라우저·Unity 검증은 포함하지 않음."
[IO.File]::WriteAllText((Join-Path $root 'qa-static.txt'),$result,(New-Object Text.UTF8Encoding($false)))
Write-Output $result
