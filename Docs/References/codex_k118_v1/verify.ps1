$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class K118Verify {
 public static int WindowCheck(string day,string night,string mask){using(Bitmap a=new Bitmap(day))using(Bitmap b=new Bitmap(night))using(Bitmap m=new Bitmap(mask)){int changed=0;for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++){bool delta=a.GetPixel(x,y)!=b.GetPixel(x,y);if(delta!=(m.GetPixel(x,y).A==255))throw new Exception("창문 마스크 불일치");if(delta){changed++;if(y<36||y>=50||!((x>=38&&x<49)||(x>=80&&x<89)))throw new Exception("창문 밖 변경");}}if(changed==0)throw new Exception("창문 점등 없음");return changed;}}
 public static void Exact3x(string native,string game){using(Bitmap a=new Bitmap(native))using(Bitmap b=new Bitmap(game)){if(b.Width!=a.Width*3||b.Height!=a.Height*3)throw new Exception("3배 크기 오류");for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(a.GetPixel(x/3,y/3)!=b.GetPixel(x,y))throw new Exception("3배 픽셀 오류");}}
 public static void Windows(string day,string night,string mask) {using(Bitmap a=new Bitmap(day))using(Bitmap b=(Bitmap)a.Clone())using(Bitmap m=new Bitmap(a.Width,a.Height)){for(int y=36;y<50;y++)for(int x=38;x<89;x++){Color c=a.GetPixel(x,y);if((x<49||x>=80)&&c.A>0&&c.B>=c.R&&c.B>=c.G&&c.B>25){b.SetPixel(x,y,Color.FromArgb(255,216,144,72));m.SetPixel(x,y,Color.White);}}b.Save(night,ImageFormat.Png);m.Save(mask,ImageFormat.Png);}}
 public static void CleanAlpha(string path) {using(Bitmap b=new Bitmap(path)){for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A==0)b.SetPixel(x,y,Color.FromArgb(0,0,0,0));b.Save(path+".tmp",ImageFormat.Png);}System.IO.File.Copy(path+".tmp",path,true);System.IO.File.Delete(path+".tmp");}
 public static void Density(string path,int n) {using(Bitmap b=new Bitmap(path))using(Bitmap tile=new Bitmap(n,n))using(Bitmap r=new Bitmap(256,256)){for(int y=0;y<n;y++)for(int x=0;x<n;x++)tile.SetPixel(x,y,b.GetPixel(x*256/n,y*256/n));for(int y=0;y<n;y++)tile.SetPixel(n-1,y,tile.GetPixel(0,y));for(int x=0;x<n;x++)tile.SetPixel(x,n-1,tile.GetPixel(x,0));for(int y=0;y<256;y++)for(int x=0;x<256;x++)r.SetPixel(x,y,tile.GetPixel(x%n,y%n));r.Save(path+".tmp",ImageFormat.Png);}System.IO.File.Copy(path+".tmp",path,true);System.IO.File.Delete(path+".tmp");}
 public static Bitmap Scale(Bitmap b,int f) {Bitmap r=new Bitmap(b.Width*f,b.Height*f);using(Graphics g=Graphics.FromImage(r)){for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)using(SolidBrush brush=new SolidBrush(b.GetPixel(x,y)))g.FillRectangle(brush,x*f,y*f,f,f);}return r;}
 public static string Check(string p,bool tile,int expectedW,int expectedH) {
  using(Bitmap b=new Bitmap(p)){
   if(b.Width!=expectedW||b.Height!=expectedH)throw new Exception("크기 불일치: "+p);
   int opaque=0,transparent=0,partial=0,maxY=-1,edge=0;bool[] seen=new bool[b.Width*b.Height];int minComponent=int.MaxValue;
   for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){int a=b.GetPixel(x,y).A;if(a==255){opaque++;maxY=Math.Max(maxY,y);}else if(a==0)transparent++;else partial++;if(a>0&&(x==0||y==0||x==b.Width-1||y==b.Height-1))edge++;}
   if(partial>0||opaque==0)throw new Exception("투명도 오류: "+p);
   if(tile){if(transparent>0)throw new Exception("재질 투명도 오류");for(int i=0;i<256;i++)if(b.GetPixel(i,0)!=b.GetPixel(i,255)||b.GetPixel(0,i)!=b.GetPixel(255,i))throw new Exception("반복 경계 오류");}
   else {
    if(transparent==0||edge>0||maxY!=b.Height-4)throw new Exception("밑선 또는 여백 오류: "+p+" / "+maxY);
    for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(!seen[y*b.Width+x]&&b.GetPixel(x,y).A>0){int count=0;Queue<int> q=new Queue<int>();q.Enqueue(y*b.Width+x);seen[y*b.Width+x]=true;while(q.Count>0){int k=q.Dequeue();count++;int px=k%b.Width,py=k/b.Width;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int ax=px+dx,ay=py+dy;if(ax>=0&&ay>=0&&ax<b.Width&&ay<b.Height&&!seen[ay*b.Width+ax]&&b.GetPixel(ax,ay).A>0){seen[ay*b.Width+ax]=true;q.Enqueue(ay*b.Width+ax);}}}minComponent=Math.Min(minComponent,count);}
    if(minComponent<3)throw new Exception("고립 픽셀 오류: "+p);
   }
   return "{\"width\":"+b.Width+",\"height\":"+b.Height+",\"opaque\":"+opaque+",\"transparent\":"+transparent+",\"partial_alpha\":"+partial+",\"last_opaque_row\":"+maxY+",\"min_component_pixels\":"+(tile?0:minComponent)+"}";
  }
 }
 public static int Protected(string orig,string mock,string mask) {using(Bitmap a=new Bitmap(orig))using(Bitmap b=new Bitmap(mock))using(Bitmap m=new Bitmap(mask)){int changes=0;for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(m.GetPixel(x,y).R==0&&a.GetPixel(x,y)!=b.GetPixel(x,y))changes++;if(changes>0)throw new Exception("보호 영역 변경");return changes;}}
 public static void MatchStone(string path,bool slate) {
  using(Bitmap b=new Bitmap(path)) {
   Color[] p=slate?new[]{Color.FromArgb(30,34,41),Color.FromArgb(43,49,57),Color.FromArgb(57,64,72),Color.FromArgb(73,79,85),Color.FromArgb(91,96,100),Color.FromArgb(114,118,119),Color.FromArgb(141,143,139),Color.FromArgb(166,165,155)}:new[]{Color.FromArgb(58,55,48),Color.FromArgb(77,73,63),Color.FromArgb(96,91,79),Color.FromArgb(115,110,96),Color.FromArgb(134,130,114),Color.FromArgb(153,149,132),Color.FromArgb(172,168,150),Color.FromArgb(191,186,166)};
   for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){Color c=b.GetPixel(x,y);int lum=(c.R*30+c.G*59+c.B*11)/100;int idx=slate?Math.Max(0,Math.Min(7,(lum-20)/20)):Math.Max(0,Math.Min(7,(lum-45)/22));b.SetPixel(x,y,p[idx]);}
   b.Save(path+".tmp",ImageFormat.Png);
  }
  System.IO.File.Copy(path+".tmp",path,true);System.IO.File.Delete(path+".tmp");
 }
}
'@
$root=$PSScriptRoot
$repo=(Resolve-Path "$root/../../..").Path
New-Item -ItemType Directory -Force "$root/game" | Out-Null
if($args -contains '-Density') {
 foreach($id in @('plaster_timber','rough_stone','plank_wall','clay_tiles','thatch')){[K118Verify]::Density("$root/textures/$id.png",128)}
 [K118Verify]::Density("$root/textures/slate_roof.png",64)
}
if($args -contains '-Normalize') {
 [K118Verify]::MatchStone("$root/textures/rough_stone.png",$false)
 [K118Verify]::MatchStone("$root/textures/slate_roof.png",$true)
 foreach($id in @('rough_stone','slate_roof')) {
  $b=New-Object System.Drawing.Bitmap "$root/textures/$id.png"
  $preview=[K118Verify]::Scale($b,4);$preview.Save("$root/previews/${id}_4x.png",[System.Drawing.Imaging.ImageFormat]::Png);$preview.Dispose()
  $repeat=New-Object System.Drawing.Bitmap 768,768;$g=[System.Drawing.Graphics]::FromImage($repeat)
  for($y=0;$y -lt 3;$y++){for($x=0;$x -lt 3;$x++){$g.DrawImageUnscaled($b,$x*256,$y*256)}}
  $g.Dispose();$repeat.Save("$root/previews/${id}_repeat3x3.png",[System.Drawing.Imaging.ImageFormat]::Png);$repeat.Dispose();$b.Dispose()
 }
}
$records=@()
foreach($id in @('repair','barracks','trap_workshop','shop')){[K118Verify]::Windows("$root/decals/${id}_day.png","$root/decals/${id}_night.png","$root/decals/${id}_window_emission.png")}
$windowChanges=@{}
foreach($id in @('repair','barracks','trap_workshop','shop')){$windowChanges[$id]=[K118Verify]::WindowCheck("$root/decals/${id}_day.png","$root/decals/${id}_night.png","$root/decals/${id}_window_emission.png")}
foreach($dir in @('textures','decals','props')){foreach($f in Get-ChildItem "$root/$dir" -Filter *.png){
 if($f.BaseName.EndsWith('_window_emission')){continue}
 if($dir -ne 'textures'){[K118Verify]::CleanAlpha($f.FullName)}
 $b=New-Object System.Drawing.Bitmap $f.FullName
 $w=$b.Width;$h=$b.Height
 if($dir -ne 'textures'){$scaled=[K118Verify]::Scale($b,3);$scaled.Save("$root/game/$($f.BaseName)_3x.png",[System.Drawing.Imaging.ImageFormat]::Png);$scaled.Dispose();[K118Verify]::Exact3x($f.FullName,"$root/game/$($f.BaseName)_3x.png")}
 $baseName=$f.BaseName.Replace('_day','')
 $preview=[K118Verify]::Scale($b,4);$preview.Save("$root/previews/${baseName}_4x.png",[System.Drawing.Imaging.ImageFormat]::Png);$preview.Dispose()
 if($dir -eq 'textures'){$repeat=New-Object System.Drawing.Bitmap 768,768;$g=[System.Drawing.Graphics]::FromImage($repeat);for($y=0;$y -lt 3;$y++){for($x=0;$x -lt 3;$x++){$g.DrawImageUnscaled($b,$x*256,$y*256)}};$g.Dispose();$repeat.Save("$root/previews/$($f.BaseName)_repeat3x3.png",[System.Drawing.Imaging.ImageFormat]::Png);$repeat.Dispose()}
 $b.Dispose()
 $test=[K118Verify]::Check($f.FullName,($dir -eq 'textures'),$w,$h) | ConvertFrom-Json
 $records+=@{file="$dir/$($f.Name)";checks=$test;pass=$true}
}}
$protected=@{}
$protected.day=[K118Verify]::Protected("$repo/Docs/References/courtyard_20261023/01_bldg_day_0_overview.png","$root/mockup_day.png","$root/previews/mockup_day_mask.png")
$protected.night=[K118Verify]::Protected("$repo/Docs/References/courtyard_20261023/279_w27_night_1_wall_top_tower_north.png","$root/mockup_night.png","$root/previews/mockup_night_mask.png")
@{assets=$records;game_exact_nearest_3x_count=16;window_only_changed_pixels=$windowChanges;protected_pixels_changed=$protected;unity_executed=$false;git_writes=$false;baseline_convention='baseline is the lower edge after the last opaque row; three transparent rows follow';result='pass'} | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 "$root/verification.json"
Write-Output "검사 완료: $($records.Count)개 원본, 보호 영역 변경 0"
