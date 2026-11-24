$ErrorActionPreference='Stop'
$dest=$PSScriptRoot
$root=Split-Path (Split-Path (Split-Path $dest))
$embedded=@{}
foreach($p in Get-ChildItem $dest -Filter '*.png'){if($p.BaseName -notmatch '_source$'){$embedded[$p.BaseName]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes($p.FullName))}}
$template=[IO.File]::ReadAllText("$dest/review_template.html",[Text.Encoding]::UTF8)
$html=$template.Replace('__DATA__',($embedded|ConvertTo-Json -Compress))
$folder="$root/_HTML/4_그림_캐릭터"
[IO.Directory]::CreateDirectory($folder)|Out-Null
[IO.File]::WriteAllText("$folder/codex_K-038_대포끌기_화상_v1.html",$html,[Text.UTF8Encoding]::new($false))
