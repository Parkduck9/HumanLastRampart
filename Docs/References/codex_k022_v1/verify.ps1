# 실제 파일·격자·투명도·노멀 인코딩과 허용 경로를 확인한다. Unity 시험이 아니다.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$base=$PSScriptRoot;$root=[IO.Path]::GetFullPath("$base/../../..")
$checks=@()
function Check($name,$ok){$script:checks+=@{name=$name;passed=[bool]$ok};if(!$ok){throw "검사 실패: $name"}}
$m=Get-Content -Encoding UTF8 "$base/manifest.json" -Raw|ConvertFrom-Json
foreach($a in $m.assets){
  $im=[Drawing.Bitmap]::FromFile("$base/$($a.file)")
  Check "$($a.name) 1024×384" ($im.Width -eq 1024 -and $im.Height -eq 384)
  $frameHashes=@();$transparent=0
  for($f=0;$f -lt 4;$f++){
    $count=0;$edge=0
    for($y=0;$y -lt 384;$y++){for($x=0;$x -lt 256;$x++){$p=$im.GetPixel($f*256+$x,$y);if($p.A -gt 32){$count++;if($x -eq 0 -or $x -eq 255 -or $y -eq 0 -or $y -eq 383){$edge++}}else{$transparent++}}}
    Check "$($a.name) $($f+1)번 실체·가장자리 여백" ($count -gt 500 -and $edge -eq 0)
    $crop=$im.Clone([Drawing.Rectangle]::new($f*256,0,256,384),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stream=[IO.MemoryStream]::new();$crop.Save($stream,[Drawing.Imaging.ImageFormat]::Png);$sha=[Security.Cryptography.SHA256]::Create();$frameHashes+=[Convert]::ToBase64String($sha.ComputeHash($stream.ToArray()));$sha.Dispose();$stream.Dispose();$crop.Dispose()
  }
  Check "$($a.name) 서로 다른 4칸" (($frameHashes|Select-Object -Unique).Count -eq 4)
  Check "$($a.name) 투명 배경" ($transparent -gt 180000)
  Check "$($a.name) 기록 해시" ((Get-FileHash "$base/$($a.file)").Hash -eq $a.sha256)
  $im.Dispose()
}
foreach($a in $m.normals){
  $n=[Drawing.Bitmap]::FromFile("$base/$($a.file)");$c=[Drawing.Bitmap]::FromFile("$base/$($a.albedo)")
  Check "$($a.name) 노멀 크기" ($n.Width -eq 256 -and $n.Height -eq 384)
  $bad=0;$maxErr=0
  for($y=0;$y -lt 384;$y++){for($x=0;$x -lt 256;$x++){$p=$n.GetPixel($x,$y);if($c.GetPixel($x,$y).A -le 32){if($p.R -ne 128 -or $p.G -ne 128 -or $p.B -ne 255){$bad++}}else{$len=[Math]::Sqrt([Math]::Pow($p.R/127.5-1,2)+[Math]::Pow($p.G/127.5-1,2)+[Math]::Pow($p.B/127.5-1,2));$maxErr=[Math]::Max($maxErr,[Math]::Abs($len-1))}}}
  Check "$($a.name) 투명 영역 평면 노멀" ($bad -eq 0)
  Check "$($a.name) 단위벡터 오차 0.012 이하" ($maxErr -lt 0.012)
  Check "$($a.name) 노멀 해시" ((Get-FileHash "$base/$($a.file)").Hash -eq $a.sha256)
  $n.Dispose();$c.Dispose()
}
foreach($name in @('carry_run4','deploy4','climb4','ladder_front','ladder_side')){Check "K-017 $name 바이트 동일" ((Get-FileHash "$base/references/k017_$name.png").Hash -eq (Get-FileHash "$base/../codex_ladder_v1/assets/$name.png").Hash)}
$html=[IO.File]::ReadAllText("$base/preview.html")
Check 'HTML 데이터 치환 완료' (!$html.Contains('__DATA__'))
Check 'HTML 외부 리소스 없음' ($html -notmatch '(src|href)="(?:https?:|\.\.?/)')
Check 'HTML PNG 14종 내장' (([regex]::Matches($html,'data:image/png;base64,')).Count -eq 14)
Check '검토 HTML과 자료 사본 동일' ((Get-FileHash "$base/preview.html").Hash -eq (Get-FileHash "$root/_HTML/codex_K-022_적동작_v1.html").Hash)
$before=Get-Content -Encoding UTF8 "$base/game_before.json" -Raw|ConvertFrom-Json
$different=@();foreach($a in $before){$p=Join-Path $root $a.path;if(!(Test-Path -LiteralPath $p) -or (Get-FileHash -LiteralPath $p).Hash -ne $a.sha256){$different+=$a.path}}
$now=@(Get-ChildItem "$root/Game" -Recurse -File)
Check 'Game 594개 파일 해시와 파일 수 유지' ($different.Count -eq 0 -and $now.Count -eq $before.Count)
$report=@{card='K-022';checks=$checks;passed=$checks.Count;failed=0;gameFiles=$before.Count;browser='미수행: CUA 브라우저 목록이 비어 있음';unity='미실행';gitWrites='없음';visual='생성 원안 6장과 최종 동작·노멀 6장을 직접 열어 확인';limits=@('HTML 상호작용 실제 브라우저 미검증','게임 타격점과 발 위치 미검증','노멀 내부 대응 근사')}
$report|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 "$base/qa.json"
Write-Output ("정적 검사 "+$checks.Count+"개 통과 / Game "+$before.Count+"개 동일")
