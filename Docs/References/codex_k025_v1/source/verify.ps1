# 실제 실행한 파일 검사와 알파 합성 확인용 이미지
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$repo=[IO.Path]::GetFullPath((Join-Path $root '../../..'))
$m=Get-Content -LiteralPath (Join-Path $root 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$checks=[Collections.Generic.List[object]]::new()
function Check($name,$passed,$detail){$checks.Add(@{name=$name;passed=[bool]$passed;detail=$detail})}
foreach($a in $m.assets){
 $b=[Drawing.Bitmap]::new((Join-Path $root $a.file));$nonzero=0;$opaque=0;$transparent=0
 for($y=0;$y -lt $b.Height;$y++){for($x=0;$x -lt $b.Width;$x++){ $v=$b.GetPixel($x,$y).A;if($v -gt 0){$nonzero++}else{$transparent++};if($v -ge 240){$opaque++} }}
 $corners=@($b.GetPixel(0,0).A,$b.GetPixel(($b.Width-1),0).A,$b.GetPixel(0,($b.Height-1)).A,$b.GetPixel(($b.Width-1),($b.Height-1)).A)
 Check ($a.id+' 크기·투명 여백·실체') ($b.Width -eq $a.width -and $b.Height -eq $a.height -and $nonzero -gt 0 -and $transparent -gt 0 -and $opaque -gt 0 -and ($corners | Measure-Object -Sum).Sum -eq 0) @{size=@($b.Width,$b.Height);corners=$corners;nonzero=$nonzero;opaque=$opaque;transparent=$transparent}
 $b.Dispose()
}
$before=Get-Content -LiteralPath (Join-Path $root 'qa/game_before.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$after=@(Get-ChildItem (Join-Path $repo 'Game') -Recurse -File | ForEach-Object {@{path=$_.FullName.Substring($repo.Length+1);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
$after | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $root 'qa/game_after.json') -Encoding UTF8
$map=@{};foreach($a in $after){$map[$a.path]=$a.sha256};$changed=@($before | Where-Object {$map[$_.path] -ne $_.sha256})
Check 'Game 전후 해시와 파일 수 일치' ($changed.Count -eq 0 -and $before.Count -eq $after.Count) @{before=$before.Count;after=$after.Count;changed=$changed.Count}
$gen=Get-Content (Join-Path $PSScriptRoot 'generation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($e in $gen.entries){$file=Join-Path $root ('originals/'+$e.id+'.png');$matches=[regex]::Matches($e.output_hint,'as (C:[^\r\n]+?\.png) by default');$from=$matches[0].Groups[1].Value;Check ($e.id+' 원안 보존') ((Get-FileHash $file).Hash -eq (Get-FileHash $from).Hash) $file.Substring($root.Length+1)}
foreach($name in @('horizon_day','horizon_night','sky_day','sky_night')){$b=[Drawing.Bitmap]::new((Join-Path $root ('backgrounds/'+$name+'.png')));$expected=if($name.StartsWith('horizon')){@(1024,352)}else{@(32,256)};Check ($name+' 크기') ($b.Width -eq $expected[0] -and $b.Height -eq $expected[1]) @($b.Width,$b.Height);$b.Dispose()}
$day=[Drawing.Bitmap]::new((Join-Path $root 'backgrounds/horizon_day.png'));$night=[Drawing.Bitmap]::new((Join-Path $root 'backgrounds/horizon_night.png'));$alphaDiff=0;$shapeDiff=0
for($y=0;$y -lt $day.Height;$y++){for($x=0;$x -lt $day.Width;$x++){ $da=$day.GetPixel($x,$y).A;$na=$night.GetPixel($x,$y).A;if($da -ne $na){$alphaDiff++};if(($da -ge 128) -ne ($na -ge 128)){$shapeDiff++} }}
$seamDiff=0;for($y=0;$y -lt $day.Height;$y++){if($day.GetPixel(0,$y).ToArgb() -ne $day.GetPixel(($day.Width-1),$y).ToArgb()){$seamDiff++}}
$limits=@{dayNightAlphaDifferentPixels=$alphaDiff;dayNightSilhouetteDifferentPixelsAt128=$shapeDiff;dayDirectSeamDifferentRows=$seamDiff;notes='생성 낮밤 형태 차이와 직접 반복 불일치. 무봉제·보간 전환 완료 아님.'};$day.Dispose();$night.Dispose()
$html=[IO.File]::ReadAllText((Join-Path $repo '_HTML/codex_K-025_field_background_v1.html'))
Check 'HTML 데이터 치환 완료' (!$html.Contains('__PACKED__') -and !$html.Contains('__MANIFEST__')) '내장 자료 15종'
Check 'HTML 외부 리소스 없음' (![regex]::IsMatch($html,'(?:src|href)\s*=\s*["''](?:https?:|\.\.?/)')) '서버·인터넷·외부 파일 의존 없음'
$packedMatch=[regex]::Match($html,'const packed=(.*?);\s*const manifest=','Singleline');$packed=$packedMatch.Groups[1].Value | ConvertFrom-Json
foreach($p in $packed.PSObject.Properties){$bytes=[Convert]::FromBase64String($p.Value.Substring('data:image/png;base64,'.Length));$stream=[IO.MemoryStream]::new($bytes,0,$bytes.Length);$b=[Drawing.Bitmap]::new($stream);Check ('HTML 그림 '+$p.Name) ($b.Width -gt 0 -and $b.Height -gt 0) @($b.Width,$b.Height);$b.Dispose();$stream.Dispose()}
# 원안은 변경하지 않고 확인용 한 장에 알파 합성한다.
$sheet=[Drawing.Bitmap]::new(1100,740);$g=[Drawing.Graphics]::FromImage($sheet);$g.Clear([Drawing.ColorTranslator]::FromHtml('#e3dccb'));$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor;$g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
$font=[Drawing.Font]::new('Malgun Gothic',12);$brush=[Drawing.Brushes]::Black
$baseAssets=@($m.assets | Where-Object {$_.view -eq 'oblique'})
for($i=0;$i -lt 5;$i++){$a=$baseAssets[$i];$x=$i*220;$g.DrawString($a.label,$font,$brush,($x+12),12);$b=[Drawing.Bitmap]::new((Join-Path $root $a.file));$g.DrawImage($b,([int]($x+110-$a.width/2)),([int](275-$a.height)),$a.width,$a.height);$b.Dispose();$t=$m.assets | Where-Object {$_.id -eq ($a.id+'_top')};$b=[Drawing.Bitmap]::new((Join-Path $root $t.file));$g.DrawImage($b,([int]($x+110-$t.width/2)),310,$t.width,$t.height);$b.Dispose()}
foreach($isNight in @($false,$true)){$x=if($isNight){550}else{0};$suffix=if($isNight){'night'}else{'day'};$b=[Drawing.Bitmap]::new((Join-Path $root ('backgrounds/sky_'+$suffix+'.png')));$g.DrawImage($b,$x,480,550,250);$b.Dispose();$b=[Drawing.Bitmap]::new((Join-Path $root ('backgrounds/horizon_'+$suffix+'.png')));$g.DrawImage($b,$x,530,550,189);$b.Dispose()}
$g.Dispose();$font.Dispose();$sheet.Save((Join-Path $root 'qa/alpha_contact_sheet.png'),[Drawing.Imaging.ImageFormat]::Png);$sheet.Dispose()
$result=@{card='K-025';checks=$checks;passed=@($checks | Where-Object {$_.passed}).Count;failed=@($checks | Where-Object {!$_.passed}).Count;limitations=$limits;browser='연결된 브라우저 없음. 실제 브라우저 동작·반응형·콘솔 미검증';unity='미실행';git='쓰기 없음'}
$result | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $root 'qa/verification.json') -Encoding UTF8
[pscustomobject]$result | Select-Object passed,failed,limitations | ConvertTo-Json -Depth 4
if($result.failed -gt 0){exit 1}
