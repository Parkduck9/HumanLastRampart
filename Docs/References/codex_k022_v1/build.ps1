# 생성 원안은 보존하고 자르기·최근접 격자 배치와 노멀 데이터 인코딩만 수행한다.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$base = $PSScriptRoot
function Bounds($im, $left, $right, $normal=$false) {
  $x0=$right; $y0=$im.Height; $x1=-1; $y1=-1
  for($y=0;$y -lt $im.Height;$y++){for($x=$left;$x -lt $right;$x++){
    $p=$im.GetPixel($x,$y)
    $hit=$p.A -gt 32
    if($normal){$hit=([Math]::Abs([int]$p.R-128)+[Math]::Abs([int]$p.G-128)) -gt 24}
    if($hit){$x0=[Math]::Min($x0,$x);$x1=[Math]::Max($x1,$x);$y0=[Math]::Min($y0,$y);$y1=[Math]::Max($y1,$y)}
  }}
  if($x1 -lt 0){throw '실체 경계를 찾지 못했습니다.'}
  return [Drawing.Rectangle]::new($x0,$y0,$x1-$x0+1,$y1-$y0+1)
}
$manifest=@{card='K-022';status='시안·사용자 확정 전·게임 미연결';cell=@(256,384);pivotTopLeft=@(128,352);unityPivot=@(0.5,0.0833333333);assets=@();normals=@()}
foreach($name in @('goblin_fall4','ogre_attack4','ogre_fall4')){
  $im=[Drawing.Bitmap]::FromFile("$base/sources/$name.png")
  $edges=@(0,512,1024,1536,2048)
  if($name -eq 'goblin_fall4'){$edges=@(0,540,1010,1535,2048)}
  if($name -eq 'ogre_fall4'){$edges=@(0,530,1010,1530,2048)}
  if($im.Width -ne 2048){throw '원안 너비가 달라 수동 경계 재검토가 필요합니다.'}
  $boxes=@();for($i=0;$i -lt 4;$i++){$boxes+=Bounds $im $edges[$i] $edges[$i+1]}
  $maxW=($boxes | Measure-Object Width -Maximum).Maximum
  $maxH=($boxes | Measure-Object Height -Maximum).Maximum
  $scale=[Math]::Min(224.0/$maxW,240.0/$maxH)
  if($name -eq 'ogre_attack4'){
    $top=($boxes | Measure-Object Top -Minimum).Minimum
    $bottom=($boxes | Measure-Object Bottom -Maximum).Maximum
    $scale=[Math]::Min(224.0/$maxW,[Math]::Min(304.0/(676-$top),28.0/($bottom-676)))
  }
  $sheet=[Drawing.Bitmap]::new(1024,384,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g=[Drawing.Graphics]::FromImage($sheet);$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor;$g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half;$g.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
  $cells=@()
  for($i=0;$i -lt 4;$i++){
    $b=$boxes[$i];$w=[int][Math]::Round($b.Width*$scale);$h=[int][Math]::Round($b.Height*$scale)
    $x=$i*256+[int][Math]::Floor((256-$w)/2);$y=352-$h
    if($name -eq 'ogre_attack4'){$y=[int][Math]::Round(352+($b.Y-676)*$scale)}
    $dst=[Drawing.Rectangle]::new($x,$y,$w,$h)
    $g.DrawImage($im,$dst,$b,[Drawing.GraphicsUnit]::Pixel)
    $cells+=@{frame=$i+1;source=@($b.X,$b.Y,$b.Width,$b.Height);destination=@(($x-$i*256),$y,$w,$h)}
  }
  $g.Dispose();$sheet.Save("$base/assets/$name.png",[Drawing.Imaging.ImageFormat]::Png);$sheet.Dispose();$im.Dispose()
  $manifest.assets+=@{name=$name;file="assets/$name.png";frames=4;size=@(1024,384);scale=$scale;loop=($name -eq 'ogre_attack4');cells=$cells;sha256=(Get-FileHash "$base/assets/$name.png").Hash}
}
# K-017의 인물과 소품은 변형하지 않은 바이트 동일 복사본이다.
foreach($name in @('carry_run4','deploy4','climb4','ladder_front','ladder_side')){
  Copy-Item -LiteralPath "$base/../codex_ladder_v1/assets/$name.png" -Destination "$base/references/k017_$name.png"
}
foreach($name in @('goblin','ogre','ladder_goblin')){
  $albedo=[Drawing.Bitmap]::FromFile("$base/references/${name}_albedo.png")
  $src=[Drawing.Bitmap]::FromFile("$base/sources/${name}_normal.png")
  $ab=Bounds $albedo 0 256
  $nb=Bounds $src 0 $src.Width $true
  $out=[Drawing.Bitmap]::new(256,384,[Drawing.Imaging.PixelFormat]::Format24bppRgb)
  $maxError=0.0;$flatCount=0
  for($y=0;$y -lt 384;$y++){for($x=0;$x -lt 256;$x++){
    $c=[Drawing.Color]::FromArgb(128,128,255)
    if($albedo.GetPixel($x,$y).A -gt 32){
      $sx=[Math]::Max(0,[Math]::Min($src.Width-1,[int][Math]::Floor($nb.X+($x-$ab.X)*$nb.Width/$ab.Width)))
      $sy=[Math]::Max(0,[Math]::Min($src.Height-1,[int][Math]::Floor($nb.Y+($y-$ab.Y)*$nb.Height/$ab.Height)))
      $p=$src.GetPixel($sx,$sy)
      $nx=($p.R/127.5-1)*0.55;$ny=($p.G/127.5-1)*0.55;$nz=[Math]::Max(0.1,$p.B/127.5-1)
      $len=[Math]::Sqrt($nx*$nx+$ny*$ny+$nz*$nz)
      $r=[int][Math]::Round(($nx/$len+1)*127.5);$g=[int][Math]::Round(($ny/$len+1)*127.5);$b=[int][Math]::Round(($nz/$len+1)*127.5)
      $c=[Drawing.Color]::FromArgb($r,$g,$b)
      $len2=[Math]::Sqrt([Math]::Pow($r/127.5-1,2)+[Math]::Pow($g/127.5-1,2)+[Math]::Pow($b/127.5-1,2))
      $maxError=[Math]::Max($maxError,[Math]::Abs($len2-1))
    }else{$flatCount++}
    $out.SetPixel($x,$y,$c)
  }}
  $out.Save("$base/assets/${name}_normal.png",[Drawing.Imaging.ImageFormat]::Png)
  $manifest.normals+=@{name=$name;file="assets/${name}_normal.png";albedo="references/${name}_albedo.png";size=@(256,384);normalSourceBounds=@($nb.X,$nb.Y,$nb.Width,$nb.Height);albedoBounds=@($ab.X,$ab.Y,$ab.Width,$ab.Height);strength=0.55;flatPixels=$flatCount;maxUnitVectorError=$maxError;sha256=(Get-FileHash "$base/assets/${name}_normal.png").Hash}
  $out.Dispose();$src.Dispose();$albedo.Dispose()
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 "$base/manifest.json"
