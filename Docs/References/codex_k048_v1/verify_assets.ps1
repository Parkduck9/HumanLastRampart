$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'BitmapTools.cs') -ReferencedAssemblies System.Drawing
$p=$PSScriptRoot;$m=Get-Content -Raw -Encoding UTF8 (Join-Path $p 'manifest.json')|ConvertFrom-Json
$checks=@();$details=@()
function Assert-Asset($name,$condition){if(-not $condition){throw $name};$script:checks+=,$name}
foreach($key in @('crew_pull6','crew_push6','crew_ignite6','cannon_body','installed_crew','installed_aim3','installed_recoil3','previous_cannon','muzzle_smoke')){
 $file=Join-Path $p "$key.png";$im=[Drawing.Bitmap]::new($file);$cw=256;$count=6
 if($key -eq 'cannon_body'){$cw=512;$count=1};if($key -eq 'installed_crew'){$count=1};if($key -eq 'installed_aim3'){$cw=512;$count=3};if($key -eq 'installed_recoil3'){$count=3};if($key -eq 'previous_cannon'){$cw=384;$count=1}
 Assert-Asset "$key 시트 폭" ($im.Width -eq $cw*$count)
 $frames=@();$actual=@()
 for($i=0;$i -lt $count;$i++){
  $c=[BitmapTools]::Crop($im,[Drawing.Rectangle]::new($i*$cw,0,$cw,$im.Height));$b=[BitmapTools]::Bounds($c,128)
  $corners=@($c.GetPixel(0,0).A,$c.GetPixel(($cw-1),0).A,$c.GetPixel(0,($im.Height-1)).A,$c.GetPixel(($cw-1),($im.Height-1)).A)
  Assert-Asset "$key $i 모서리 투명" (($corners|Measure-Object -Sum).Sum -eq 0)
  if($key -in @('crew_pull6','crew_push6')){
   $sole=[BitmapTools]::Sole($c,($i%3 -le 1));$expected=$m.crew.$key.frames[$i].stanceFoot[0]
   Assert-Asset "$key $i 실측 인물210px" ($b.Height -eq 210)
   Assert-Asset "$key $i 실측 발 높이" ($b.Bottom -eq 352)
   Assert-Asset "$key $i 실측 접지 x" ($sole -eq $expected)
   $actual+=,$sole
  }
  if($key -eq 'cannon_body'){Assert-Asset '대포 실체242×224' ($b.Width -eq 242 -and $b.Height -eq 224);Assert-Asset '대포 접지352' ($b.Bottom -eq 352)}
  $frames+=@{index=$i;alphaBounds=@($b.X,$b.Y,$b.Width,$b.Height);cornersAlpha=$corners};$c.Dispose()
 }
 if($actual.Count -eq 6){foreach($start in @(0,3)){$world=@();for($j=0;$j -lt 3;$j++){$world+=($actual[$start+$j]-40*($start+$j))};Assert-Asset "$key $start 실측 부츠의 세계좌표 고정" (($world|Select-Object -Unique).Count -eq 1)}}
 $details+=@{file="$key.png";size=@($im.Width,$im.Height);frames=$frames;sha256=(Get-FileHash -Algorithm SHA256 $file).Hash};$im.Dispose()
}
$root=Split-Path (Split-Path (Split-Path $p))
foreach($pair in @(@('installed_crew.png','Game/Assets/_Project/Art/Characters/Static/cannon_crew_static.png'),@('installed_aim3.png','Game/Assets/_Project/Art/Battle/Resources/BattleArt/cannon_aim3.png'),@('installed_recoil3.png','Game/Assets/_Project/Art/Battle/Resources/BattleArt/Cannon/cannon_recoil.png'),@('muzzle_smoke.png','Docs/References/codex_k043_v1/muzzle_smoke.png'))){Assert-Asset "$($pair[0]) 읽기 원본과 일치" ((Get-FileHash (Join-Path $p $pair[0])).Hash -eq (Get-FileHash (Join-Path $root $pair[1])).Hash)}
Assert-Asset '점화4칸과 도화선의 높이 일치' ($m.ignite.contacts[3][1] -eq $m.cannon.fuse[1])
Assert-Asset '점화4칸 접점이 칸 안쪽' ($m.ignite.contacts[3][0] -gt 0 -and $m.ignite.contacts[3][1] -gt 0)
$result=@{kind='저장한 PNG 픽셀 직접 실측';passed=$checks.Count;checks=$checks;files=$details}
[IO.File]::WriteAllText((Join-Path $p 'qa_assets.json'),($result|ConvertTo-Json -Depth 15),[Text.UTF8Encoding]::new($false))
Write-Output "$($checks.Count)개 픽셀·투명도·원본 일치 검사 통과"
