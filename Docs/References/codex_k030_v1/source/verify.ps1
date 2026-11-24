$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$repo=(Resolve-Path (Join-Path $root '../../..')).Path
$manifest=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'manifest.json')|ConvertFrom-Json
$html=Get-Content -Raw -Encoding UTF8 (Join-Path $repo '_HTML/codex_K-030_지형손질_v1.html')
$packed=([regex]::Match($html,'const packed=(.*?);const manifest=',[Text.RegularExpressions.RegexOptions]::Singleline)).Groups[1].Value|ConvertFrom-Json
$checks=@()
foreach($a in $manifest.assets){
 $raw=[IO.File]::ReadAllBytes((Join-Path $root $a.file));$embedded=[Convert]::FromBase64String(($packed.($a.id) -replace '^data:image/png;base64,',''))
 $equal=[Convert]::ToBase64String($raw) -ceq [Convert]::ToBase64String($embedded)
 $b=[Drawing.Bitmap]::new((Join-Path $root $a.file));$checks+=@{id=$a.id;embeddedBytesMatch=$equal;sizeMatch=($b.Width -eq $a.width -and $b.Height -eq $a.height)};$b.Dispose()
}
$before=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'qa/game_before.json')|ConvertFrom-Json
$after=[ordered]@{};foreach($f in Get-ChildItem (Join-Path $repo 'Game') -Recurse -File){$key=$f.FullName.Substring($repo.Length+1).Replace('\','/');$after[$key]=(Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
$diff=@();foreach($p in $before.PSObject.Properties){if(!$after.Contains($p.Name) -or $after[$p.Name] -cne $p.Value){$diff+=$p.Name}};foreach($key in $after.Keys){if(!$before.PSObject.Properties[$key]){$diff+=$key}}
$after|ConvertTo-Json -Depth 3|Set-Content -Encoding UTF8 (Join-Path $root 'qa/game_after.json')
$result=@{assetChecks=$checks;gameFiles=$after.Count;gameDifferences=$diff;gameUnchanged=($diff.Count -eq 0);externalResources=([regex]::Matches($html,'(?:src|href)\s*=\s*["''](?:https?:|//)').Count);unfilledPlaceholders=($html.Contains('__PACKED__') -or $html.Contains('__MANIFEST__'));browserVerified=$false;unityVerified=$false}
$result|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 (Join-Path $root 'qa/verification.json')
Write-Output ('내장 바이트/크기 검사 '+$checks.Count+'종, Game 파일 '+$after.Count+'개, 변경 '+$diff.Count+'개')
if($diff.Count -or @($checks|Where-Object{!$_.embeddedBytesMatch -or !$_.sizeMatch}).Count -or $result.externalResources -or $result.unfilledPlaceholders){throw '확인 실패 항목이 있습니다.'}
