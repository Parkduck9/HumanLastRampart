$ErrorActionPreference='Stop'
. "$PSScriptRoot/prepare.ps1"
function Load($name){[Drawing.Bitmap]::new("$PSScriptRoot/$name")}
function Ref($name){[Drawing.Bitmap]::new((Join-Path (Get-Location) $name))}
$c=Load 'cannon_body.png';$chassis=Load 'cannon_chassis.png'
$pull=Load 'crew_pull6.png';$push=Load 'crew_push6.png';$ignite=Load 'crew_ignite6.png'
$walk=Get-Content -Raw -Encoding UTF8 "$PSScriptRoot/walk_measurements.json"|ConvertFrom-Json
$ign=Get-Content -Raw -Encoding UTF8 "$PSScriptRoot/ignite_measurements.json"|ConvertFrom-Json
$outlines=@{};$used=@{}
$outlines['기존 대포 정면 원본']=[K052]::Outline((Ref 'Game/Assets/_Project/Art/Battle/Resources/BattleArt/cannon_aim3.png'))
$outlines['기존 대포병 정지 원본']=[K052]::Outline((Ref 'Game/Assets/_Project/Art/Characters/Static/cannon_crew_static.png'))
$outlines['새 대포']=[K052]::Outline($c)
$checks=@()
foreach($kind in @('pull','push','ignite')){
 $b=Load "crew_${kind}6.png"
 $used[$kind]=[K052]::Used(@("$PSScriptRoot/crew_${kind}6.png"))
 $outlines["새 병사 $kind"]=[K052]::Outline($b)
 $allowed=@($sp);if($kind -eq 'ignite'){$allowed+=@($ign.flamePalette)}
 $bad=@($used[$kind]|Where-Object{$_ -notin $allowed})
 $checks+=@{name="$kind 팔레트 외 색상";pass=($bad.Count -eq 0);actual=$bad}
 $checks+=@{name="$kind 시트 규격";pass=($b.Width -eq 2304 -and $b.Height -eq 384)}
 for($i=0;$i -lt 6;$i++){
  $cell=[K052]::Crop($b,$i*384,0,384,384);$r=[K052]::Bounds($cell)
  $checks+=@{name="$kind $i 발 높이";pass=($r.Bottom -eq 352);actual=$r.Bottom}
  if($kind -eq 'ignite'){$checks+=@{name="점화 $i 하체 픽셀 고정";pass=[K052]::SameFeet([K052]::Crop($b,0,0,384,384),$cell)}}
  if($kind -ne 'ignite'){
   $sole=[K052]::Sole($cell)
   $checks+=@{name="$kind $i 접지 실측";pass=($sole -eq 128+32*($i%3));actual=$sole}
   $checks+=@{name="$kind $i 인물 높이";pass=($r.Height -eq 210);actual=$r.Height}
  }
 }
}
$used.cannon=[K052]::Used(@("$PSScriptRoot/cannon_body.png"))
$checks+=@{name='대포 팔레트 외 색상';pass=(@($used.cannon|Where-Object{$_ -notin $cp}).Count -eq 0)}
$wheel=Load 'cannon_wheel.png';$checks+=@{name='앞바퀴 101px';pass=($wheel.Width -eq 101 -and $wheel.Height -eq 101)}
$checks+=@{name='접촉 0.5초 후 1초 대기';pass=((1.5-.5) -eq 1)}
$checks+=@{name='불씨와 도화선 높이';pass=($ign.frames[3].hotY -eq 132);actual=$ign.frames[3].hotY}
foreach($h in (Get-Content -Raw -Encoding UTF8 "$PSScriptRoot/reference_hashes.json"|ConvertFrom-Json)){
 $checks+=@{name="원본 해시 $([IO.Path]::GetFileName($h.Path))";pass=((Get-FileHash -LiteralPath $h.Path).Hash -eq $h.Hash)}
}
$used|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 "$PSScriptRoot/used_palette.json"
$outlines|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 "$PSScriptRoot/outline_measurements.json"
@{passed=@($checks|Where-Object pass).Count;failed=@($checks|Where-Object{-not $_.pass}).Count;checks=$checks}|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 "$PSScriptRoot/qa_assets.json"
# HTML과 같은 배치의 정적 모아보기. 브라우저 캡처가 아니다.
$board=[K052]::Board(1350,1660)
[K052]::Label($board,'기존 설치 대포·대포병 0.7배 / 새 대포·병사 1배',20,15,19)
$aim=Ref 'Game/Assets/_Project/Art/Battle/Resources/BattleArt/cannon_aim3.png'
$crewRef=Ref 'Game/Assets/_Project/Art/Characters/Static/cannon_crew_static.png'
[K052]::Paste($board,[K052]::Resize([K052]::Crop($aim,512,0,512,384),358,269),0,106)
[K052]::Paste($board,[K052]::Resize($crewRef,179,269),310,106)
[K052]::Paste($board,$c,480,0)
[K052]::Paste($board,[K052]::Crop($push,0,0,384,384),970,0)
[K052]::Line($board,0,352,1350,352,'#77927D',1)
for($i=0;$i -lt 6;$i++){
 $scene=[K052]::Board(1350,384)
 $a=$walk[0].frames[$i];$b=$walk[1].frames[$i]
 [K052]::Line($scene,$a.handX,$a.handY,415,315,'#936341',2)
 [K052]::Line($scene,650,275,(580+$b.handX),$b.handY,'#754F38',5)
 [K052]::Paste($scene,$chassis,250,0)
 [K052]::Paste($scene,[K052]::Rotate($wheel,(-32*$i/50.5)),536,251)
 [K052]::Paste($scene,[K052]::Crop($pull,$i*384,0,384,384),0,0)
 [K052]::Paste($scene,[K052]::Crop($push,$i*384,0,384,384),580,0)
 [K052]::Line($scene,0,352,1350,352,'#77927D',1)
 [K052]::Label($scene,"$($i+1)/6칸 · 이동 $($i*32)px",20,14,18)
 [K052]::Save($scene,"$PSScriptRoot/haul_$($i+1).png")
 $mini=[K052]::Resize($scene,675,192)
 [K052]::Paste($board,$mini,($i%2)*675,(390+[int][Math]::Floor($i/2)*210))
}
$point=[K052]::Board(1350,384)
[K052]::Paste($point,$c,420,0)
[K052]::Paste($point,[K052]::Crop($ignite,3*384,0,384,384),(420+269-$ign.frames[3].hotX),0)
[K052]::Line($point,0,352,1350,352,'#77927D',1)
[K052]::Label($point,'0.50초 접촉 → 1.50초 발사 / 발높이 고정',20,15,20)
[K052]::Save($point,"$PSScriptRoot/ignite_contact_review.png")
[K052]::Paste($board,$point,0,1030)
[K052]::Label($board,'포신 중심선: K-048 약185px → K-052 약463px (2.50배)',20,1440,20)
[K052]::Label($board,'무쇠 3단계+반사광 / 저채도 갈색 목재 / 기존 원본 팔레트만 사용',20,1490,18)
[K052]::Label($board,'시안 · Unity 미연결 · 브라우저 캡처가 아닌 픽셀 합성 비교',20,1550,18)
[K052]::Save($board,"$PSScriptRoot/contact_review.png")
$failed=@($checks|Where-Object{-not $_.pass})
"PNG 검사 통과 $($checks.Count-$failed.Count)/$($checks.Count)"
if($failed.Count){$failed|ConvertTo-Json -Depth 5;throw 'PNG 검사 실패'}


