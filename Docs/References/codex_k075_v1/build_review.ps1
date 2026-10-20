$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$m=Get-Content -Raw -Encoding UTF8 "$PSScriptRoot/manifest.json" | ConvertFrom-Json
$images=@{}
foreach($s in $m.sheets){
 foreach($pair in @(@('Native',"$PSScriptRoot/$($s.native)"),@('Game',"$PSScriptRoot/$($s.game)"),@('Old',(Join-Path $root $s.target)))){$images[$s.kind+$pair[0]]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes($pair[1]))}
 Add-Type -AssemblyName System.Drawing
 $b=[Drawing.Bitmap]::new((Join-Path $root $s.target));$s | Add-Member current_cell @([int]($b.Width/$s.frame_count),$b.Height);$b.Dispose()
}
$images.cannon='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $root $m.ignite_contact.cannon_source)))
$data=@{sheets=$m.sheets;ignite_contact=$m.ignite_contact;haul=$m.haul;images=$images}
$json=$data | ConvertTo-Json -Depth 14 -Compress
$template=Get-Content -Raw -Encoding UTF8 "$PSScriptRoot/review_template.html"
$page=Join-Path $root '_HTML/4_그림_캐릭터/codex_K-075_대포병동작_v1.html'
[IO.File]::WriteAllText($page,$template.Replace('__DATA__',$json),[Text.UTF8Encoding]::new($false))
Write-Output "내장 이미지 $($images.Count)개 저장"
