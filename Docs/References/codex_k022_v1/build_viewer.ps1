# 필요한 PNG를 데이터 URI로 내장하여 서버 없는 검토 파일을 만든다.
$ErrorActionPreference='Stop'
$base=$PSScriptRoot
$data=@{}
foreach($n in @('goblin_fall4','ogre_attack4','ogre_fall4','goblin_normal','ogre_normal','ladder_goblin_normal')){$data[$n]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes("$base/assets/$n.png"))}
foreach($n in @('carry_run4','deploy4','climb4','ladder_front','ladder_side')){$data[$n]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes("$base/references/k017_$n.png"))}
foreach($n in @('goblin','ogre','ladder_goblin')){$data[$n+'_albedo']='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes("$base/references/${n}_albedo.png"))}
$template=[IO.File]::ReadAllText("$base/viewer.template.html")
$html=$template.Replace('__DATA__',($data|ConvertTo-Json -Compress))
[IO.File]::WriteAllText("$base/preview.html",$html,[Text.UTF8Encoding]::new($false))
$dest=[IO.Path]::GetFullPath("$base/../../../_HTML/codex_K-022_적동작_v1.html")
[IO.File]::WriteAllText($dest,$html,[Text.UTF8Encoding]::new($false))
Write-Output '검토 HTML 생성 완료'
