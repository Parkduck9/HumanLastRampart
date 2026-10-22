$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -Path "$PSScriptRoot/../codex_k075_v1/PixelTools.cs"
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
public static class K086Proof {
 public static PointF Fire(Bitmap b, int left) {
  double xx=0,yy=0,n=0;
  for(int y=0;y<b.Height;y++)for(int x=left;x<Math.Min(left+65,b.Width);x++){
   var c=b.GetPixel(x,y);if(c.A>=128 && c.R>220 && c.G>170 && c.B<220){xx+=x;yy+=y;n++;}
  }
  if(n==0)throw new Exception("불씨를 찾지 못했습니다.");
  return new PointF((float)(xx/n),(float)(yy/n));
 }
 public static Bitmap Normalize(Bitmap b, int axis, double sx, double sy) {
  var o=new Bitmap(144,128);
  for(int y=0;y<128;y++)for(int x=0;x<144;x++){
   int xx=(int)Math.Round(axis+(x-96)/sx),yy=(int)Math.Floor(627+(y-120)/sy);
   if(xx>=0&&xx<b.Width&&yy>=0&&yy<b.Height)o.SetPixel(x,y,b.GetPixel(xx,yy));
  }
  return o;
 }
}
'@
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$src=[K075Pixels]::Load("$PSScriptRoot/source/ignite_back_generated.png")
$parts=[K075Pixels]::Split($src,6)
$axes=@(184,528,925,1435,1741,2058)
$fire=@();for($i=0;$i -lt 6;$i++){$fire+=,[K086Proof]::Fire($parts[$i],([K075Pixels]::Bounds($parts[$i])).Left)}
$ppm=73.0/0.35
$reach=0.30*$ppm
$sx=$reach/($axes[3]-$fire[3].X)
$sy=73.0/(627-264)
$sheet=[Drawing.Bitmap]::new(864,128)
$records=@();$frames=@()
$names=@('불붙은 횃불을 들고 대기','왼쪽으로 다가가는 자세','횃불 내리기','포신 후방 상단 접촉 후보','횃불 거두기','대기 복귀')
for($i=0;$i -lt 6;$i++){
 $f=[K086Proof]::Normalize($parts[$i],$axes[$i],$sx,$sy)
 [K075Pixels]::Quantize($f);[K075Pixels]::Clean($f,3)
 $frames+=,$f
 [K075Pixels]::Paste($sheet,$f,144*$i,0)
 $b=[K075Pixels]::Bounds($f)
 $tip=@((96+($fire[$i].X-$axes[$i])*$sx),(120+($fire[$i].Y-627)*$sy))
 $records+=@{index=$i;label=$names[$i];proof_tip=$tip;required_cell_tip=@(($tip[0]-48),$tip[1]);game_tip=@((3*($tip[0]-48)),(3*$tip[1]));bounds=@($b.X,$b.Y,$b.Width,$b.Height);baseline_bottom_exclusive=$b.Bottom;binary_alpha=[K075Pixels]::BinaryAlpha($f);component_sizes=@([K075Pixels]::Components($f)|ForEach-Object {$_.Count})}
}
[K075Pixels]::Save($sheet,"$PSScriptRoot/previews/ignite_back_proof.png")
[K075Pixels]::Save([K075Pixels]::Resize($sheet,3456,512),"$PSScriptRoot/previews/ignite_back_proof_4x.png")
# 대포 원본은 그대로 축소해 합성하며 도화선 위치는 미확정 후보로만 표시한다.
$cannonSource='Game/Assets/_Project/Art/Battle/Resources/BattleArt/Cannon/cannon_recoil.png'
$cannon=[K075Pixels]::Load("$root/$cannonSource")
$cannonFrame=[K075Pixels]::Crop($cannon,[Drawing.Rectangle]::new(0,0,256,256))
$cs=0.40*$ppm/234.0
$cw=[int][Math]::Round(256*$cs)
$cannonSmall=[K075Pixels]::Resize($cannonFrame,$cw,$cw)
$cx=128;$ground=120
$cLeft=$cx-[int][Math]::Round(128*$cs);$cTop=$ground-[int][Math]::Round(240*$cs)
$crewFoot=$cx+$reach
$crewLeft=[int][Math]::Round($crewFoot-96)
$composite=[Drawing.Bitmap]::new(288*6,160)
for($i=0;$i -lt 6;$i++){
 [K075Pixels]::Paste($composite,$cannonSmall,288*$i+$cLeft,$cTop)
 [K075Pixels]::Paste($composite,$frames[$i],288*$i+$crewLeft,0)
}
[K075Pixels]::Save($composite,"$PSScriptRoot/previews/composite6.png")
[K075Pixels]::Save([K075Pixels]::Resize($composite,6912,640),"$PSScriptRoot/previews/composite6_4x.png")
$fuseY=240+($records[3].proof_tip[1]-120)/$cs
$manifest=@{card='K-086';status='blocked_pending_cell_exception_and_fuse_confirmation';production_ready=$false;required_native_cell=@(96,128);required_game_cell=@(288,384);required_native_pivot=@(48,120);required_game_pivot=@(144,360);frame_count=6;frame_direction='left_to_right';target='Game/Assets/_Project/Art/Characters/Fire/cannon_crew_ignite6.png';native_and_game_sheets='미제작: 접촉점이 칸 밖에 있어 확인 대기';proof=@{file='previews/ignite_back_proof.png';cell=@(144,128);pivot=@(96,120);purpose='규격 검토용이며 게임 교체용이 아니다';proposed_game_cell=@(432,384);proposed_game_pivot=@(288,360)};geometry=@{soldier_height_m=0.35;soldier_native_height=73;native_pixels_per_meter=$ppm;igniter_offset_m=0.30;igniter_offset_native=$reach;cannon_visible_width_m=0.40;cannon_source_visible_width=234;cannon_source_pivot=@(128,240);cannon_source_to_native=$cs;required_contact_x=(48-$reach);required_contact_game_x=(3*(48-$reach));fuse_candidate_source=@(128,$fuseY);fuse_status='포신 후방 상단 후보. 원본에는 구분 가능한 도화선이 없으므로 사용자 확인 필요';equation='tip_x = 48 - 0.30 * 73 / 0.35 = -14.5714';source_code_difference='이 worktree는 CannonScale=1.5, CannonShooterOffset=0.4, CannonCrewOffset=0.55이다. 합성은 카드의 0.40/0.30m를 따른다.'};composite=@{file='previews/composite6.png';cell=@(288,160);cannon_pivot=@($cx,$ground);crew_pivot=@(($crewLeft+96),120);rounded_crew_offset=($crewLeft+96-$cx);left_crew_offset_requested_m=-0.45;left_crew_included=$false;note='점화병과 설치 대포만 표시. 접점 후보 표시는 HTML 안내선이며 대포 원본에는 추가하지 않았다.'};normalization=@{source='source/ignite_back_generated.png';horizontal_scale=$sx;vertical_scale=$sy;source_feet_bottom_exclusive=627;source_body_top=264;alpha_threshold=128;palette=[K075Pixels]::Palette();minimum_component=3};frames=$records;open_questions=@('96×128 대신 왼쪽 여백 48px를 추가한 144×128 칸과 피벗 (96,120)을 승인할지','포신 후방 상단의 접점 후보를 도화선 위치로 확정할지','최종 연결 시 카드 배치와 현재 코드 배치 중 어느 수치를 사용할지')}
$manifest.native_and_game_sheets='미제작: 확정 폭에서는 접촉점이 칸 밖에 있으므로 규격 확인 대기'
$manifest.proof.purpose='규격 검토용이며 게임 교체용이 아니다'
$manifest | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 "$PSScriptRoot/manifest.json"
$old='Game/Assets/_Project/Art/Characters/Fire/cannon_crew_ignite6.png'
$hash=@{};foreach($p in @($old,$cannonSource)){$hash[$p]=(Get-FileHash -LiteralPath "$root/$p").Hash}
@{source_hashes=$hash;checks=@{six_frames=($records.Count -eq 6);all_baseline_120=(@($records|Where-Object baseline_bottom_exclusive -ne 120).Count -eq 0);binary_alpha=(@($records|Where-Object binary_alpha -eq $false).Count -eq 0);no_components_under_3=(@($records.component_sizes|Where-Object {$_ -lt 3}).Count -eq 0);contact_outside_required_cell=($manifest.geometry.required_contact_x -lt 0)};limitations=@('후면 원안의 보폭·손동작·접점은 최종 검토 전','96/288 규격의 최종 교체 시트는 만들지 않았다','Unity와 실제 게임 검증은 미수행')} | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 "$PSScriptRoot/validation.json"
Write-Output ($manifest.geometry | ConvertTo-Json -Depth 4)
