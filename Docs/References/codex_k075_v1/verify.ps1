$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -Path "$PSScriptRoot/PixelTools.cs"
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$m=Get-Content -Raw -Encoding UTF8 "$PSScriptRoot/manifest.json" | ConvertFrom-Json
$qa=Get-Content -Raw -Encoding UTF8 "$PSScriptRoot/validation.json" | ConvertFrom-Json
$checks=@();$rows=@();$all=[Drawing.Bitmap]::new(576,768);$row=0
function Check($name,$ok){if(!$ok){throw "검사 실패: $name"};$script:checks+=@{check=$name;passed=$true}}
foreach($s in $m.sheets){
 $n=[K075Pixels]::Load("$PSScriptRoot/$($s.native)");$g=[K075Pixels]::Load("$PSScriptRoot/$($s.game)");$p=[K075Pixels]::Load("$PSScriptRoot/$($s.preview_4x)")
 Check "$($s.kind) 원본 크기" ($n.Width -eq 96*$s.frame_count -and $n.Height -eq 128)
 Check "$($s.kind) 게임 크기" ($g.Width -eq 288*$s.frame_count -and $g.Height -eq 384)
 Check "$($s.kind) 3배 최근접 픽셀 일치" ([K075Pixels]::Equal($g,[K075Pixels]::Resize($n,$n.Width*3,384)))
 Check "$($s.kind) 4배 미리보기 픽셀 일치" ([K075Pixels]::Equal($p,[K075Pixels]::Resize($n,$n.Width*4,512)))
 Check "$($s.kind) 이진 알파" ([K075Pixels]::BinaryAlpha($n))
 Check "$($s.kind) 원본 게임 파일 해시 유지" ((Get-FileHash (Join-Path $root $s.target)).Hash -eq $s.current_source_sha256)
 $first=$null;$hashes=@()
 for($i=0;$i -lt $s.frame_count;$i++){
  $f=[K075Pixels]::Crop($n,[Drawing.Rectangle]::new(96*$i,0,96,128));$b=[K075Pixels]::Bounds($f)
  Check "$($s.kind) $i 발선" ($b.Bottom -eq 120)
  Check "$($s.kind) $i 칸 가장자리 여백" ($b.Left -gt 0 -and $b.Right -lt 96 -and $b.Top -gt 0)
  Check "$($s.kind) $i 고립 잔여 픽셀 없음" (@([K075Pixels]::Components($f) | Where-Object Count -lt 3).Count -eq 0)
  $ms=[IO.MemoryStream]::new();$f.Save($ms,[Drawing.Imaging.ImageFormat]::Png);$sha=[Security.Cryptography.SHA256]::Create();$hash=[Convert]::ToBase64String($sha.ComputeHash($ms.ToArray()));$hashes+=,$hash
  if($s.kind -eq 'ignite'){
   if($i -eq 0){$first=$f}else{Check "점화 $i 하체 고정" ([K075Pixels]::SameFeet($first,$f))}
   $hot=[K075Pixels]::Hot($f);$s.frames[$i].flame_center_native=@($hot.X,$hot.Y)
   if($i -eq 3){$contactFrame=$f}
  }
  if($s.kind -in @('push','pull')){
   $sole=[K075Pixels]::PlantSole($f,($i%3 -eq 2));$expected=36+12*($i%3)
   Check "$($s.kind) $i 실제 접지 x" ($sole -eq $expected)
   Check "$($s.kind) $i 지면 고정" (3*$sole-36*$i -eq $(if($i -lt 3){108}else{0}))
  }
  $rows+=@{kind=$s.kind;frame=$i;bounds=@($b.X,$b.Y,$b.Width,$b.Height);sole_x=[K075Pixels]::PlantSole($f,($i%3 -eq 2));sha256_base64=$hash}
 }
 Check "$($s.kind) 프레임별 그림 구분" ((@($hashes | Select-Object -Unique).Count) -eq $s.frame_count)
 [K075Pixels]::Paste($all,$n,0,$row*128);$row++
}
# 현재 대포의 불투명 도화선과 같은 높이에서 실제 불씨 픽셀을 찾는다.
$xs=@();for($x=0;$x -lt 96;$x++){if([K075Pixels]::IsFire($contactFrame.GetPixel($x,46))){$xs+=,$x}}
Check '점화 접촉 높이 140에 불씨 픽셀 존재' ($xs.Count -gt 0)
$contactX=$xs[[int][Math]::Floor($xs.Count/2)];$dx=269-3*$contactX
$m.ignite_contact.crew_draw_offset_game=@($dx,0)
$m.ignite_contact | Add-Member -Force -NotePropertyName flame_contact_native -NotePropertyValue @($contactX,(140.0/3))
$m.ignite_contact | Add-Member -Force -NotePropertyName flame_contact_game -NotePropertyValue @((3*$contactX),140)
$m.ignite_contact.note='현재 대포를 y=8에 놓고 병사를 지정한 x에 놓으면 접촉 칸의 실제 불씨 픽셀이 도화선 (269,140)에 겹친다.'
$cannon=[K075Pixels]::Load((Join-Path $root $m.ignite_contact.cannon_source))
Check '대포 원본 해시 유지' ((Get-FileHash (Join-Path $root $m.ignite_contact.cannon_source)).Hash -eq $qa.source_hashes.cannon)
$composite=[Drawing.Bitmap]::new(960,416);[K075Pixels]::Paste($composite,$cannon,70,8);[K075Pixels]::Paste($composite,[K075Pixels]::Resize($contactFrame,288,384),(70+$dx),0)
for($x=0;$x -lt 960;$x++){$composite.SetPixel($x,360,[Drawing.Color]::FromArgb(255,90,120,150))}
[K075Pixels]::Save($composite,"$PSScriptRoot/ignite_contact_review.png")
[K075Pixels]::Save([K075Pixels]::Resize($all,1152,1536),"$PSScriptRoot/overview_2x.png")
$m | ConvertTo-Json -Depth 14 | Set-Content -Encoding UTF8 "$PSScriptRoot/manifest.json"
$report=@{checks=$checks;passed=$checks.Count;frames=$rows;source_hashes=$qa.source_hashes;performed=@('PNG 실제 픽셀·크기·접지 검사','최근접 3배·4배 대조','Game 원본 해시 대조');not_performed=@('Unity 실행 / Play / 실제 게임 캡처','실제 게임의 연속 이동 접지','노멀맵 제작')}
$report | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 "$PSScriptRoot/validation.json"
Write-Output "$($checks.Count) 항목 통과 / 점화 병사 x=$dx / 도화선 접점=(269,140)"
