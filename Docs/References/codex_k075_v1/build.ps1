$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -Path "$PSScriptRoot/PixelTools.cs"
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$cfg=@(
 @{kind='static';source='static';path='Characters/Static/cannon_crew_static.png';count=1;order=@('뒷모습 대기')},
 @{kind='run';source='run_fixed';path='Characters/Run/cannon_crew_run.png';count=4;order=@('왼발 디딤','오른발 통과','오른발 디딤','왼발 통과')},
 @{kind='ignite';source='ignite_fixed';path='Characters/Fire/cannon_crew_ignite6.png';count=6;order=@('막대 내림','들기','도화선 접근','도화선 접촉','거두기','대기 복귀')},
 @{kind='fall';source='fall_back';path='Characters/Fall/cannon_crew_fall.png';count=4;order=@('무릎 꺾임','무릎 꿇음','옆으로 쓰러짐','옆으로 누움')},
 @{kind='push';source='push';path='Battle/Resources/BattleArt/Cannon/crew_push6.png';count=6;order=@('왼발 앞 디딤','왼발 가운데 디딤','왼발 뒤 디딤','오른발 앞 디딤','오른발 가운데 디딤','오른발 뒤 디딤')},
 @{kind='pull';source='pull';path='Battle/Resources/BattleArt/Cannon/crew_pull6.png';count=6;order=@('왼발 앞 디딤','왼발 가운데 디딤','왼발 뒤 디딤','오른발 앞 디딤','오른발 가운데 디딤','오른발 뒤 디딤')}
)
$manifest=@{card='K-075';decision='D-080 / D-081';native_cell=@(96,128);game_cell=@(288,384);native_pivot_top_left=@(48,120);game_pivot_top_left=@(144,360);unity_normalized_pivot=@(0.5,0.0625);palette=[K075Pixels]::Palette();scale=3;frame_direction='left_to_right';sheets=@();integration_notes=@('Game/는 변경하지 않았다. 파일별 칸 크기와 피벗은 Claude가 연결한다.','견인은 칸 경계에서 36px 이동한다. 연속 이동 보간의 접지는 게임 측 처리가 필요하다.','정지 대포병 노멀맵은 제작하지 않았다. 기존 크기의 노멀맵을 새 시트에 그대로 쓰지 않는다.');sources=@()}
$qa=@{checks=@();sheets=@();source_hashes=@{}}
$framesByKind=@{}
foreach($c in $cfg){
 $old=Join-Path $root "Game/Assets/_Project/Art/$($c.path)"
 $qa.source_hashes[$c.path]=(Get-FileHash -LiteralPath $old -Algorithm SHA256).Hash
 $src=[K075Pixels]::Load("$PSScriptRoot/source/$($c.source).png")
 $parts=[K075Pixels]::Split($src,$c.count)
 $firstBounds=[K075Pixels]::Bounds($parts[0]);$firstHair=[K075Pixels]::HairTop($parts[0])
 $bodyTop=$firstHair-[int][Math]::Round(($firstBounds.Bottom-$firstHair)/73.0)
 if($c.kind -eq 'static'){$bodyTop=$firstBounds.Top}
 if($c.kind -eq 'fall'){$bodyTop=$firstBounds.Top}
 $scale=73.0/($firstBounds.Bottom-$bodyTop)
 $sheet=[Drawing.Bitmap]::new(96*$c.count,128);$frames=@();$records=@()
 for($i=0;$i -lt $c.count;$i++){
  $p=$parts[$i];$b=[K075Pixels]::Bounds($p)
  $top=[K075Pixels]::HairTop($p)
  if($c.kind -eq 'ignite'){$top=$bodyTop}
  $axis=[K075Pixels]::HairAxis($p,$top,$firstBounds.Bottom-$bodyTop)
  if($c.kind -in @('push','pull')){$axis=[K075Pixels]::CoatAxis($p,$bodyTop,$firstBounds.Bottom-$bodyTop)}
  if($c.kind -eq 'fall'){$axis=[int][Math]::Round(($b.Left+$b.Right)/2.0)}
  #  쓰러짐 외에는 머리부터 발까지 같은 높이를 유지한다.
  $s=$scale
  if($c.kind -notin @('fall','ignite','static')){$bt=$top-[int][Math]::Round(($b.Bottom-$top)/73.0);$s=73.0/($b.Bottom-$bt)}
  $f=[K075Pixels]::Normalize($p,$s,$axis,120)
  if($c.kind -eq 'ignite' -and $i -gt 0){for($y=100;$y -lt 120;$y++){for($x=0;$x -lt 96;$x++){$f.SetPixel($x,$y,$frames[0].GetPixel($x,$y))}}}
  if($c.kind -in @('push','pull')){$f=[K075Pixels]::FixSole($f,(36+12*($i%3)))}
  [K075Pixels]::Clean($f,3)
  $frames+=,$f
  [K075Pixels]::Paste($sheet,$f,96*$i,0)
  $r=[K075Pixels]::Bounds($f);$sole=[K075Pixels]::Sole($f);$hot=[K075Pixels]::Hot($f)
  $rec=@{index=$i;label=$c.order[$i];bounds=@($r.X,$r.Y,$r.Width,$r.Height);baseline_bottom_exclusive=$r.Bottom;sole_x=$sole;normalization_axis_x=48;source_axis_x=$axis;source_scale=$s}
  if($c.kind -eq 'ignite'){$rec.flame_center_native=@($hot.X,$hot.Y)}
  if($c.kind -in @('push','pull')){$sole=[K075Pixels]::PlantSole($f,($i%3 -eq 2));$rec.planted_foot_native_x=$sole;$rec.world_foot_game_x=3*$sole-36*$i;$rec.planted_leg=if($i -lt 3){'왼발'}else{'오른발'}}
  $records+=,$rec
 }
 $framesByKind[$c.kind]=$frames
 $native="native/$($c.path)";$game="game/$($c.path)";$preview="previews/$($c.kind)_4x.png"
 [K075Pixels]::Save($sheet,"$PSScriptRoot/$native")
 [K075Pixels]::Save([K075Pixels]::Resize($sheet,$sheet.Width*3,384),"$PSScriptRoot/$game")
 [K075Pixels]::Save([K075Pixels]::Resize($sheet,$sheet.Width*4,512),"$PSScriptRoot/$preview")
 $manifest.sheets+=@{kind=$c.kind;target="Game/Assets/_Project/Art/$($c.path)";native=$native;game=$game;preview_4x=$preview;frame_count=$c.count;frame_order=$c.order;frames=$records;current_source_sha256=$qa.source_hashes[$c.path]}
 $manifest.sources+=@{kind=$c.kind;file="source/$($c.source).png";sha256=(Get-FileHash "$PSScriptRoot/source/$($c.source).png").Hash}
}
$cannonPath=Join-Path $root 'Game/Assets/_Project/Art/Battle/Resources/BattleArt/Cannon/haul_cannon_body.png'
$qa.source_hashes['cannon']=(Get-FileHash $cannonPath).Hash
$manifest.ignite_contact=@{frame_index=3;cannon_source='Game/Assets/_Project/Art/Battle/Resources/BattleArt/Cannon/haul_cannon_body.png';cannon_source_fuse=@(269,132);cannon_source_pivot=@(336,352);cannon_draw_offset_game=@(0,8);fuse_game=@(269,140);crew_draw_offset_game=@(0,0);note='불씨와 도화선 겹침 좌표는 검증 결과를 함께 확인한다.'}
$manifest.haul=@{direction='left';step_at_256_scale=32;step_game=36;step_native=12;planted_sole_native=@(36,48,60,36,48,60);planted_world_game=@(108,108,108,0,0,0);interpolation='none';note='상체 축은 x=48에 정렬하고 하체만 점진적으로 접지 위치에 보정했다. 손 파지와 이동 보간 연결은 실제 게임 검토가 남는다.'}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 "$PSScriptRoot/manifest.json"
$qa | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 "$PSScriptRoot/validation.json"
Write-Output '시트 6종: 원본·게임용·4배 미리보기 저장 완료'
