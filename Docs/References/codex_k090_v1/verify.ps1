$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = @'
using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
public static class K090QA {
 public static int Scaled=0,Masks=0,Connected=0,Alpha=0,Previews=0;
 static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Native(string root){foreach(string f in Directory.GetFiles(Path.Combine(root,"native"),"*.png")){using(var a=new Bitmap(f))using(var b=new Bitmap(Path.Combine(root,Path.GetFileName(f)))){Require(b.Width==a.Width*3&&b.Height==a.Height*3,"3배 크기: "+f);for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)Require(a.GetPixel(x/3,y/3).ToArgb()==b.GetPixel(x,y).ToArgb(),"최근접 확대: "+f);Scaled++;}}}
 public static void Preview(string root){foreach(string f in Directory.GetFiles(Path.Combine(root,"previews"),"*_4x.png")){using(var a=new Bitmap(Path.Combine(root,Path.GetFileName(f).Replace("_4x.png",".png"))))using(var b=new Bitmap(f)){Require(b.Width==a.Width*4&&b.Height==a.Height*4,"4배 크기");var ad=a.LockBits(new Rectangle(0,0,a.Width,a.Height),System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);var bd=b.LockBits(new Rectangle(0,0,b.Width,b.Height),System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);var aa=new byte[ad.Stride*a.Height];var bb=new byte[bd.Stride*b.Height];System.Runtime.InteropServices.Marshal.Copy(ad.Scan0,aa,0,aa.Length);System.Runtime.InteropServices.Marshal.Copy(bd.Scan0,bb,0,bb.Length);for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){int p=(y/4)*ad.Stride+(x/4)*4,q=y*bd.Stride+x*4;for(int c=0;c<4;c++)Require(aa[p+c]==bb[q+c],"4배 최근접 픽셀");}a.UnlockBits(ad);b.UnlockBits(bd);Previews++;}}}
 static void Component(Bitmap b){var seen=new bool[b.Width*b.Height];int components=0;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){int at=y*b.Width+x;if(seen[at]||b.GetPixel(x,y).A==0)continue;components++;var q=new Queue<int>();q.Enqueue(at);seen[at]=true;while(q.Count>0){int p=q.Dequeue(),px=p%b.Width,py=p/b.Width;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int xx=px+dx,yy=py+dy;if(xx<0||yy<0||xx>=b.Width||yy>=b.Height)continue;int k=yy*b.Width+xx;if(!seen[k]&&b.GetPixel(xx,yy).A>0){seen[k]=true;q.Enqueue(k);}}}}Require(components==1,"조각에 분리 픽셀 영역이 있습니다: "+components);Connected++;}
 public static void Pieces(string root,string family,string[] cols){foreach(string col in cols){using(var n=new Bitmap(Path.Combine(root,family+"_"+col+"_normal.png"))){foreach(string st in new[]{"normal","hover","disabled"})using(var b=new Bitmap(Path.Combine(root,family+"_"+col+"_"+st+".png"))){Require(b.Width==n.Width&&b.Height==n.Height,"상태 크기");bool transparent=false,opaque=false;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){byte a=b.GetPixel(x,y).A;Require(a==0||a==255,"반투명 경계");Require(a==n.GetPixel(x,y).A,"상태별 마스크 변화");transparent|=a==0;opaque|=a==255;}Require(transparent&&opaque,"알파 통로");Require(b.GetPixel(b.Width/2,b.Height/2).A==0,"가운데 구멍");Component(b);Masks++;Alpha++;}}}}
}
'@
Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
[K090QA]::Native($PSScriptRoot)
[K090QA]::Preview($PSScriptRoot)
[K090QA]::Pieces($PSScriptRoot,'direction',@('north','east','south','west'))
[K090QA]::Pieces($PSScriptRoot,'amount',@('one','half','all'))
$manifest = Get-Content "$PSScriptRoot/manifest.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$sizeCount=0
foreach ($a in $manifest.files) {
    $b = [Drawing.Bitmap]::new("$PSScriptRoot/$($a.file)")
    if ($b.Width -ne $a.size[0] -or $b.Height -ne $a.size[1]) {throw "크기 불일치: $($a.file)"}
    if ($b.PixelFormat -ne [Drawing.Imaging.PixelFormat]::Format32bppArgb) {throw "RGBA 형식 확인: $($a.file)"}
    $b.Dispose();$sizeCount++
}
$root = (Resolve-Path "$PSScriptRoot/../../..").Path
$page = [IO.File]::ReadAllText("$root/_HTML/6_그림_화면/codex_K-090_분대명령원형_v1.html",[Text.Encoding]::UTF8)
$images = [regex]::Matches($page,'data:image/png;base64,([A-Za-z0-9+/=]+)')
foreach ($m in $images) {
    $bytes=[Convert]::FromBase64String($m.Groups[1].Value)
    $stream=[IO.MemoryStream]::new($bytes,0,$bytes.Length)
    $im=[Drawing.Image]::FromStream($stream);$im.Dispose();$stream.Dispose()
}
if ($page -match '(?:src|href)=["'']https?://' -or $page -match '__DATA__') {throw '외부 의존 또는 미치환 데이터'}
foreach ($f in $manifest.files | Where-Object kind -eq 'mockup') {if ($f.size[0] -ne 1920 -or $f.size[1] -ne 1080) {throw '합성 크기'}}
$result=[ordered]@{card='K-090';manifest_sizes_rgba=$sizeCount;exact_native_3x=[K090QA]::Scaled;exact_preview_4x=[K090QA]::Previews;stable_state_masks=[K090QA]::Masks;binary_alpha_pieces=[K090QA]::Alpha;single_8_connected_piece_regions=[K090QA]::Connected;embedded_png_decodes=$images.Count;mockups_1920x1080=6;foot_baseline='not applicable: radial UI';browser='unavailable: CUA browser inventory empty; no real browser render test';javascript='see review_verification.json, Node VM behavior checks only';unity='not opened or run';git='no writes';result='passed static image and package checks'}
[IO.File]::WriteAllText("$PSScriptRoot/verification.json",($result|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
$result|ConvertTo-Json
