# K-025 원안 보관, 최근접 축소, 독립 검토 페이지 생성
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$repo = [IO.Path]::GetFullPath((Join-Path $root '../../..'))
$generated = 'C:/Users/User/.codex/generated_images/01a141c2-99d1-7940-9990-841df34e4f3a'
foreach ($name in @('originals','sprites','backgrounds','qa')) { [IO.Directory]::CreateDirectory((Join-Path $root $name)) | Out-Null }
$files = [ordered]@{
 oak='exec-91b90e4e-b904-478f-a319-2d2a8e704eff.png'; pine='exec-4c56aaae-dea3-4b51-89ff-fe4f4a70eeb7.png'; rock_a='exec-e781741e-91de-454b-940f-ff705311730f.png'; rock_b='exec-3293797d-c777-4bc0-9c1a-08c7977343c4.png'; bush='exec-7486749e-9e7a-4d3e-b84e-c0b785db5ed4.png'; horizon_day='exec-99a1c024-4a17-47ef-9184-fcdfbbc162dd.png'; horizon_clean='exec-b774c13b-11e0-49db-a6db-1ceece9a1782.png'; pine_clean='exec-732fb163-7764-48ae-9b08-fd33675ece5e.png'; rock_a_clean='exec-e5c5c00f-e0d4-49a8-8529-82b6973950a4.png'; bush_clean='exec-888c45bb-4cea-4bcc-baf5-804a073396da.png'; horizon_night='exec-a38687b4-a928-47a6-a209-6481995dd2ec.png'
}
foreach ($id in $files.Keys) { $target=Join-Path $root ('originals/'+$id+'.png'); if (!(Test-Path $target)) { Copy-Item -LiteralPath (Join-Path $generated $files[$id]) -Destination $target } }
# 게임 폴더는 읽기만 하여 전후 비교 기준을 보관한다.
$baseline=Join-Path $root 'qa/game_before.json'
if (!(Test-Path $baseline)) { @(Get-ChildItem (Join-Path $repo 'Game') -Recurse -File | ForEach-Object { @{path=$_.FullName.Substring($repo.Length+1); sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} }) | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $baseline -Encoding UTF8 }
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class K025Raster {
 public static Rectangle Bounds(Bitmap b, int threshold) {
  int l=b.Width,t=b.Height,r=-1,d=-1;
  for(int y=0;y<b.Height;y++) for(int x=0;x<b.Width;x++) if(b.GetPixel(x,y).A>threshold){ l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);d=Math.Max(d,y); }
  if(r<0) throw new Exception("실체 화소가 없습니다.");
  return Rectangle.FromLTRB(l,t,r+1,d+1);
 }
 public static int[] Prop(string input,string output,int w,int h,int maxW,int maxH,bool center) {
  using(var b=new Bitmap(input)) { Rectangle crop=Bounds(b,16); double s=Math.Min((double)maxW/crop.Width,(double)maxH/crop.Height); int dw=(int)Math.Round(crop.Width*s), dh=(int)Math.Round(crop.Height*s);
   int dy=center?(h-dh)/2:h-8-dh;
   using(var o=new Bitmap(w,h,PixelFormat.Format32bppArgb)) using(var g=Graphics.FromImage(o)) { g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.CompositingMode=CompositingMode.SourceCopy; g.DrawImage(b,new Rectangle((w-dw)/2,dy,dw,dh),crop,GraphicsUnit.Pixel);o.Save(output,ImageFormat.Png); }
   return new int[]{crop.X,crop.Y,crop.Width,crop.Height,(w-dw)/2,dy,dw,dh};
  }
 }
 public static void Strip(string input,string output) {
  using(var b=new Bitmap(input)) using(var o=new Bitmap(1024,352,PixelFormat.Format32bppArgb)) using(var g=Graphics.FromImage(o)) {
   g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.CompositingMode=CompositingMode.SourceCopy;g.DrawImage(b,new Rectangle(0,0,1024,352),new Rectangle(0,0,b.Width,b.Height),GraphicsUnit.Pixel);o.Save(output,ImageFormat.Png);
  }
 }
 public static void Sky(string path,string upper,string middle,string lower) {
  Color a=ColorTranslator.FromHtml(upper),b=ColorTranslator.FromHtml(middle),c=ColorTranslator.FromHtml(lower);
  using(var o=new Bitmap(32,256)) { for(int y=0;y<256;y++){ double f=(double)y/255; Color p=f<0.65?a:b,q=f<0.65?b:c; double u=f<0.65?f/0.65:(f-0.65)/0.35;
   Color k=Color.FromArgb(255,(int)Math.Round(p.R+(q.R-p.R)*u),(int)Math.Round(p.G+(q.G-p.G)*u),(int)Math.Round(p.B+(q.B-p.B)*u));for(int x=0;x<32;x++)o.SetPixel(x,y,k); } o.Save(path,ImageFormat.Png); }
 }
}
'@
$specs=@(
 @{id='oak';label='넓은잎나무';source='oak';w=128;h=160;mw=112;mh=144},
 @{id='pine';label='침엽수';source='pine_clean';w=128;h=224;mw=112;mh=208},
 @{id='rock_a';label='낮은 바위';source='rock_a_clean';w=96;h=80;mw=80;mh=64},
 @{id='rock_b';label='갈라진 바위';source='rock_b';w=64;h=112;mw=48;mh=96},
 @{id='bush';label='풀숲';source='bush_clean';w=64;h=48;mw=48;mh=32}
)
$assets=@()
foreach($s in $specs){
 $file='sprites/'+$s.id+'.png';$crop=[K025Raster]::Prop((Join-Path $root ('originals/'+$s.source+'.png')),(Join-Path $root $file),$s.w,$s.h,$s.mw,$s.mh,$false)
 $assets+=@{id=$s.id;label=$s.label;file=$file;source='originals/'+$s.source+'.png';width=$s.w;height=$s.h;pivotTopLeft=@(($s.w/2),($s.h-8));cropAndPlacement=$crop;ppuProposal=32;view='oblique';status='사용자 확정 전 경사 시점 판 시안'}
}
$topFiles=[ordered]@{oak_top='exec-34b7d906-19a4-47c9-aada-46a7b0448f0e.png';pine_top='exec-9ce8e46d-ab0f-4114-b8fb-354538783546.png';rock_a_top='exec-58ad3618-e3dc-4734-8c1e-cdab47633233.png';rock_b_top='exec-767f91cc-718a-4f29-b178-129cef2c0943.png';bush_top='exec-9e1958b6-c561-48ad-8d3c-c8a081b1060f.png'}
foreach($s in $specs){
 $id=$s.id+'_top';$size=switch($s.id){'oak'{128};'pine'{128};'rock_a'{96};default{64}}
 $original=Join-Path $root ('originals/'+$id+'.png');if(!(Test-Path $original)){Copy-Item -LiteralPath (Join-Path $generated $topFiles[$id]) -Destination $original}
 $file='sprites/'+$id+'.png';$crop=[K025Raster]::Prop($original,(Join-Path $root $file),$size,$size,($size-16),($size-16),$true)
 $assets+=@{id=$id;label=$s.label+' 윗면';file=$file;source='originals/'+$id+'.png';width=$size;height=$size;pivotTopLeft=@(($size/2),($size/2));cropAndPlacement=$crop;ppuProposal=32;view='top';status='사용자 확정 전 수평 윗면 판 시안'}
}
[K025Raster]::Strip((Join-Path $root 'originals/horizon_clean.png'),(Join-Path $root 'backgrounds/horizon_day.png'))
[K025Raster]::Strip((Join-Path $root 'originals/horizon_night.png'),(Join-Path $root 'backgrounds/horizon_night.png'))
[K025Raster]::Sky((Join-Path $root 'backgrounds/sky_day.png'),'#607f8c','#a8b5ad','#dbceb0')
[K025Raster]::Sky((Join-Path $root 'backgrounds/sky_night.png'),'#101a31','#29364f','#566273')
$manifest=@{card='K-025';status='시안 · 사용자 방향 미확정 · 게임 미연결';assets=$assets;backgrounds=@{day='backgrounds/horizon_day.png';night='backgrounds/horizon_night.png';width=1024;height=352;repeat='직접 반복은 이음새 미해결. HTML의 반사 반복은 비교용 제안';skyDay='backgrounds/sky_day.png';skyNight='backgrounds/sky_night.png';skySize=@(32,256)};processing='생성 도구로 제작·보정. 알파 유지 자르기와 최근접 축소. 하늘은 단순 수직 그라데이션 수식.'}
$manifest | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $root 'manifest.json') -Encoding UTF8
# 검토 페이지에 모든 그림을 내장한다. 기존 질감은 복사하지 않고 읽어 내장한다.
$packed=@{}
foreach($s in $assets){$packed[$s.id]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $root $s.file)))}
foreach($id in @('horizon_day','horizon_night','sky_day','sky_night')){$packed[$id]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $root ('backgrounds/'+$id+'.png'))))}
$grass=Join-Path $repo 'Game/Assets/_Project/Art/Battle/Resources/BattleArt/World/field_grass_albedo.png'
$packed['grass']='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes($grass))
$page=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'review.template.html'))
$page=$page.Replace('__PACKED__',($packed | ConvertTo-Json -Compress -Depth 5)).Replace('__MANIFEST__',($manifest | ConvertTo-Json -Compress -Depth 10))
[IO.File]::WriteAllText((Join-Path $repo '_HTML/codex_K-025_field_background_v1.html'),$page,[Text.UTF8Encoding]::new($false))
Write-Output 'K-025 그림·명세·독립 HTML 생성 완료'
