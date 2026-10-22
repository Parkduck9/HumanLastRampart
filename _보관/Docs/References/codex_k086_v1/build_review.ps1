$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$m=Get-Content -Raw -Encoding UTF8 "$PSScriptRoot/manifest.json"|ConvertFrom-Json
$images=@{}
foreach($pair in @(@('proof',"$PSScriptRoot/previews/ignite_back_proof.png"),@('composite',"$PSScriptRoot/previews/composite6.png"),@('old',"$root/Game/Assets/_Project/Art/Characters/Fire/cannon_crew_ignite6.png"))){$images[$pair[0]]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes($pair[1]))}
$data=@{images=$images;crewFoot=$m.composite.crew_pivot[0];tipY=$m.frames[3].proof_tip[1];labels=@($m.frames|ForEach-Object {$_.label})}|ConvertTo-Json -Depth 5 -Compress
$html=(Get-Content -Raw -Encoding UTF8 "$PSScriptRoot/review_template.html").Replace('__DATA__',$data)
$path="$root/_HTML/4_그림_캐릭터/codex_K-086_대포불붙이기다시_v1.html"
[IO.File]::WriteAllText($path,$html,[Text.UTF8Encoding]::new($false))
Write-Output $path
