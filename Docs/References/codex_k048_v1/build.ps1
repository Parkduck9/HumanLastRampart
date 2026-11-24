param([string]$WalkSource,[string]$CannonSource,[string]$IgniteSource)
$ErrorActionPreference='Stop'
$OutputEncoding=[Console]::OutputEncoding=[System.Text.UTF8Encoding]::new()
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'BitmapTools.cs') -ReferencedAssemblies System.Drawing
$root=Split-Path (Split-Path (Split-Path $PSScriptRoot))
$p=$PSScriptRoot
if(-not $WalkSource){$WalkSource=Join-Path $p 'crew_source.png'}
if(-not $CannonSource){$CannonSource=Join-Path $p 'cannon_source.png'}
if(-not $IgniteSource){$IgniteSource=Join-Path $p 'ignite_source.png'}
function Save-Png($im,$name){$im.Save((Join-Path $p $name),[Drawing.Imaging.ImageFormat]::Png)}
function Json($name,$obj){[IO.File]::WriteAllText((Join-Path $p $name),($obj|ConvertTo-Json -Depth 30),[Text.UTF8Encoding]::new($false))}
function Load($path){return [Drawing.Bitmap]::new((Join-Path $root $path))}
function Measure-Sprite($file,$cw){
 $im=Load $file;$frames=@()
 for($i=0;$i -lt $im.Width/$cw;$i++){$cell=[BitmapTools]::Crop($im,[Drawing.Rectangle]::new($i*$cw,0,$cw,$im.Height));$b=[BitmapTools]::Bounds($cell,128);$frames+=@{index=$i;bounds=@($b.X,$b.Y,$b.Width,$b.Height)};$cell.Dispose()}
 $im.Dispose();return @{file=$file;cellWidth=$cw;threshold='알파 128 초과, 우측·하단 경계 제외';frames=$frames}
}
$measure=@(
 (Measure-Sprite 'Game/Assets/_Project/Art/Battle/Resources/BattleArt/cannon_aim3.png' 512),
 (Measure-Sprite 'Game/Assets/_Project/Art/Battle/Resources/BattleArt/Cannon/cannon_recoil.png' 256),
 (Measure-Sprite 'Game/Assets/_Project/Art/Characters/Static/cannon_crew_static.png' 256),
 (Measure-Sprite 'Game/Assets/_Project/Art/Characters/Run/cannon_crew_run.png' 256))
Json 'measurements.json' @{sources=$measure;soldierRawHeight=300;soldierTargetHeight=210;referenceScale=0.7;cannonTarget=@(242,224);ratio=@(1.15,1.0666666667);note='정면 설치 대포345×320 / 정지 대포병300. 0.7배 후 241.5×224를242×224로 반올림. 원본 픽셀의 비율이며 실게임 화면 실측이 아니다.'}
if([IO.Path]::GetFullPath($WalkSource) -ne (Join-Path $p 'crew_source.png')){Copy-Item -LiteralPath $WalkSource -Destination (Join-Path $p 'crew_source.png') -Force}
if([IO.Path]::GetFullPath($CannonSource) -ne (Join-Path $p 'cannon_source.png')){Copy-Item -LiteralPath $CannonSource -Destination (Join-Path $p 'cannon_source.png') -Force}
$source=[Drawing.Bitmap]::new((Join-Path $p 'crew_source.png'))
$crew=@{}
for($row=0;$row -lt 2;$row++){
 $sheet=[Drawing.Bitmap]::new(1536,384);$details=@();$name=@('crew_pull6','crew_push6')[$row]
 for($i=0;$i -lt 6;$i++){
  $sourceIndex=@(0,1,5,3,4,2)[$i];$x=[int][Math]::Floor($source.Width*$sourceIndex/6);$x2=[int][Math]::Floor($source.Width*($sourceIndex+1)/6)
  $y=[int][Math]::Floor($source.Height*$row/2);$y2=[int][Math]::Floor($source.Height*($row+1)/2)
  $cell=[BitmapTools]::Crop($source,[Drawing.Rectangle]::new($x,$y,$x2-$x,$y2-$y));$b=[BitmapTools]::Bounds($cell,128)
  [BitmapTools]::Clean($cell);$b=[BitmapTools]::Bounds($cell,128)
  $sprite=[BitmapTools]::Crop($cell,$b);$w=[int][Math]::Round($b.Width*210.0/$b.Height);$s=[BitmapTools]::Resize($sprite,$w,210)
  $phase=$i%3;$sole=[BitmapTools]::Sole($s,($phase -le 1));$contact=84+40*$phase;$dx=$contact-$sole;$dy=142
  if($dx -lt 1 -or $dx+$w -gt 255){throw "병사 칸 초과: $name $i $dx $w"}
  [BitmapTools]::Paste($sheet,$s,$i*256+$dx,$dy)
  $handX=if($row -eq 0){$dx+$w-10}else{$dx+10}
  $details+=@{index=$i;sourceRect=@(($x+$b.X),($y+$b.Y),$b.Width,$b.Height);placed=@($dx,$dy,$w,210);stanceFoot=@($contact,352);stanceLeg=$(if($i -lt 3){'왼발'}else{'오른발'});hand=@($handX,251);distance=$i*40}
  $cell.Dispose();$sprite.Dispose();$s.Dispose()
 }
 Save-Png $sheet "$name.png";$crew[$name]=@{cell=@(256,384);pivot=@(128,352);height=210;frames=$details};$sheet.Dispose()
}
$source.Dispose()
$im=[Drawing.Bitmap]::new((Join-Path $p 'cannon_source.png'));$b=[BitmapTools]::Bounds($im,128);$crop=[BitmapTools]::Crop($im,$b);$c=[BitmapTools]::Resize($crop,242,224);$out=[Drawing.Bitmap]::new(512,384)
[BitmapTools]::Paste($out,$c,135,128);Save-Png $out 'cannon_body.png'
$cannon=@{cell=@(512,384);pivot=@(256,352);bounds=@(135,128,242,224);sourceBounds=@($b.X,$b.Y,$b.Width,$b.Height);sourceScale=@((242.0/$b.Width),(224.0/$b.Height));wheelCenter=@(258,281);wheelRadius=71;frontHandle=@(174,268);rearHandle=@(374,273);fuse=@(347,132);muzzle=@(151,170)}
$im.Dispose();$crop.Dispose();$c.Dispose();$out.Dispose()

if([IO.Path]::GetFullPath($IgniteSource) -ne (Join-Path $p 'ignite_source.png')){Copy-Item -LiteralPath $IgniteSource -Destination (Join-Path $p 'ignite_source.png') -Force}
$src=[Drawing.Bitmap]::new((Join-Path $p 'ignite_source.png'));$sheet=[Drawing.Bitmap]::new(1536,384);$contacts=@();$igniteDetails=@()
for($i=0;$i -lt 6;$i++){
 $x=[int][Math]::Floor($src.Width*$i/6);$x2=[int][Math]::Floor($src.Width*($i+1)/6)
 $cell=[BitmapTools]::Crop($src,[Drawing.Rectangle]::new($x,0,$x2-$x,$src.Height));$b=[BitmapTools]::Bounds($cell,128);$top=[BitmapTools]::BodyTop($cell);$scale=210.0/($b.Bottom-$top)
 $flame=[BitmapTools]::Flame($cell);$foot=[BitmapTools]::Sole($cell,$false);$sprite=[BitmapTools]::Crop($cell,$b);$w=[int][Math]::Round($b.Width*$scale);$h=[int][Math]::Round($b.Height*$scale)
 $small=[BitmapTools]::Resize($sprite,$w,$h);$dx=174-[int][Math]::Round(($foot-$b.X)*$scale);$dy=352-$h
 if($dx -lt 1 -or $dx+$w -gt 255){throw "점화 칸 경계 초과 $i $dx $w $top $h"}
 [BitmapTools]::Paste($sheet,$small,($i*256+$dx),$dy)
 $contacts+=,@(($dx+[int][Math]::Round(($flame.X-$b.X)*$scale)),($dy+[int][Math]::Round(($flame.Y-$b.Y)*$scale)))
 $igniteDetails+=@{index=$i;bodyTop=$top;scale=$scale;sourceBounds=@($b.X,$b.Y,$b.Width,$b.Height);placed=@($dx,$dy,$w,$h)}
 $small.Dispose();$sprite.Dispose();$cell.Dispose()
}
Save-Png $sheet 'crew_ignite6.png';$sheet.Dispose();$src.Dispose()
$canon=[Drawing.Bitmap]::new((Join-Path $p 'cannon_body.png'));$fuse=[BitmapTools]::Fuse($canon,$contacts[3][1]);$canon.Dispose()
if([Math]::Abs($fuse.Y-$contacts[3][1]) -gt 2){throw '점화 높이 오차 2px 초과'}
$cannon.fuse=@($fuse.X,$fuse.Y)

Copy-Item (Join-Path $root 'Docs/References/codex_k043_v1/muzzle_smoke.png') (Join-Path $p 'muzzle_smoke.png') -Force
Copy-Item (Join-Path $root 'Docs/References/codex_k043_v1/cannon_body.png') (Join-Path $p 'previous_cannon.png') -Force
Copy-Item (Join-Path $root 'Game/Assets/_Project/Art/Battle/Resources/BattleArt/cannon_aim3.png') (Join-Path $p 'installed_aim3.png') -Force
Copy-Item (Join-Path $root 'Game/Assets/_Project/Art/Battle/Resources/BattleArt/Cannon/cannon_recoil.png') (Join-Path $p 'installed_recoil3.png') -Force
Copy-Item (Join-Path $root 'Game/Assets/_Project/Art/Characters/Static/cannon_crew_static.png') (Join-Path $p 'installed_crew.png') -Force
$meta=@{crew=$crew;cannon=$cannon;ignite=@{cell=@(256,384);pivot=@(128,352);contacts=$contacts;frames=$igniteDetails;contactSeconds=0.5;fireSeconds=1.5;cycleSeconds=3.2};walk=@{fps=6;pixelsPerFrame=40;cycleDistance=240;stanceLocalX=@(84,124,164,84,124,164);note='접지 좌표를 맞춘 여섯 칸 이동. 한 칸 안에서는 위치를 유지하며 칸 전환 때40px 이동한다.'}}
Json 'manifest.json' $meta
$keys=@('crew_pull6','crew_push6','crew_ignite6','cannon_body','previous_cannon','installed_aim3','installed_recoil3','installed_crew','muzzle_smoke')
$data=@{};foreach($key in $keys){$data[$key]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $p "$key.png")))}
$html=[IO.File]::ReadAllText((Join-Path $p 'review_template.html')).Replace('__DATA__',($data|ConvertTo-Json -Compress)).Replace('__META__',($meta|ConvertTo-Json -Depth 30 -Compress))
[IO.File]::WriteAllText((Join-Path $root '_HTML/4_그림_캐릭터/codex_K-048_대포끌기_다시_v1.html'),$html,[Text.UTF8Encoding]::new($false))
Write-Output '실측·병사 접지 정렬·대포·내장 검토 페이지 작성 완료'
