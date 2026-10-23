$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
public static class K118 {
 public static Bitmap Resize(Bitmap src,int w,int h) {
  Bitmap b=new Bitmap(w,h,PixelFormat.Format32bppArgb);
  for(int y=0;y<h;y++)for(int x=0;x<w;x++) b.SetPixel(x,y,src.GetPixel(Math.Min(src.Width-1,x*src.Width/w),Math.Min(src.Height-1,y*src.Height/h)));
  return b;
 }
 public static void Save(Bitmap b,string path,int factor=1) {
  if(factor==1)b.Save(path,ImageFormat.Png); else using(Bitmap r=Resize(b,b.Width*factor,b.Height*factor)) r.Save(path,ImageFormat.Png);
 }
 public static Color Snap(Color c,bool alpha) {
  if(alpha&&c.A<160)return Color.FromArgb(0,0,0,0);
  return Color.FromArgb(255,Math.Min(255,((int)c.R+12)/24*24),Math.Min(255,((int)c.G+12)/24*24),Math.Min(255,((int)c.B+12)/24*24));
 }
 public static Bitmap Texture(string path) {
  using(Bitmap src=new Bitmap(path)) {
   Bitmap b=Resize(src,256,256);
   for(int y=0;y<256;y++)for(int x=0;x<256;x++)b.SetPixel(x,y,Snap(b.GetPixel(x,y),false));
   for(int y=0;y<256;y++)b.SetPixel(255,y,b.GetPixel(0,y));
   for(int x=0;x<256;x++)b.SetPixel(x,255,b.GetPixel(x,0));
   return b;
  }
 }
 public static Bitmap Sprite(string path,Rectangle crop,int w,int h) {
  using(Bitmap src=new Bitmap(path))using(Bitmap c=src.Clone(crop,PixelFormat.Format32bppArgb)) {
   int x0=c.Width,y0=c.Height,x1=0,y1=0;
   for(int y=0;y<c.Height;y++)for(int x=0;x<c.Width;x++)if(c.GetPixel(x,y).A>=160){x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);}
   if(x0>x1)throw new Exception("빈 스프라이트");
   using(Bitmap tight=c.Clone(Rectangle.FromLTRB(x0,y0,x1+1,y1+1),PixelFormat.Format32bppArgb)) {
    double s=Math.Min((w-4.0)/tight.Width,(h-4.0)/tight.Height);
    int nw=Math.Max(1,(int)Math.Round(tight.Width*s)),nh=Math.Max(1,(int)Math.Round(tight.Height*s));
    Bitmap b=new Bitmap(w,h,PixelFormat.Format32bppArgb);
    using(Bitmap r=Resize(tight,nw,nh))for(int y=0;y<nh;y++)for(int x=0;x<nw;x++)b.SetPixel((w-nw)/2+x,h-3-nh+y,Snap(r.GetPixel(x,y),true));
    bool[] seen=new bool[w*h];
    for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(!seen[y*w+x]&&b.GetPixel(x,y).A>0){
     Queue<int> q=new Queue<int>();List<int> comp=new List<int>();q.Enqueue(y*w+x);seen[y*w+x]=true;
     while(q.Count>0){int p=q.Dequeue();comp.Add(p);int px=p%w,py=p/w;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int ax=px+dx,ay=py+dy;if(ax>=0&&ay>=0&&ax<w&&ay<h&&!seen[ay*w+ax]&&b.GetPixel(ax,ay).A>0){seen[ay*w+ax]=true;q.Enqueue(ay*w+ax);}}}
     if(comp.Count<3)foreach(int p in comp)b.SetPixel(p%w,p/w,Color.FromArgb(0,0,0,0));
    }
    return b;
   }
  }
 }
 public static Bitmap Lit(Bitmap src) {
  Bitmap b=(Bitmap)src.Clone();
  for(int y=36;y<50;y++)for(int x=38;x<89;x++){Color c=b.GetPixel(x,y);if((x<49||x>=80)&&c.A>0&&c.B>=c.R&&c.B>=c.G&&c.B>25)b.SetPixel(x,y,Color.FromArgb(255,216,144,72));}
  return b;
 }
 public static Bitmap Mock(string original,string generated,string maskFile,bool night) {
  using(Bitmap src=new Bitmap(original))using(Bitmap gen=new Bitmap(generated))using(Bitmap r=Resize(gen,src.Width,src.Height)) {
   Bitmap result=(Bitmap)src.Clone(),mask=new Bitmap(src.Width,src.Height);
   using(Graphics g=Graphics.FromImage(mask)){
    g.Clear(Color.Black);
    if(night){g.FillRectangle(Brushes.White,250,825,510,103);g.FillRectangle(Brushes.White,1140,825,510,103);}
    else {
     int[][] a={new[]{679,211,787,174,857,209,851,269,746,299,684,272},new[]{739,170,765,164,784,170,784,194,741,200},new[]{1055,191,1135,191,1314,239,1304,319,1206,351,1056,299},new[]{1435,335,1483,287,1788,367,1743,400,1696,496,1436,404},new[]{64,364,297,330,341,378,324,449,148,511,107,445},new[]{0,444,50,417,80,467,76,521,0,548},new[]{0,678,43,662,183,803,159,868,0,949},new[]{1828,696,1919,596,1919,825,1830,783},new[]{330,372,390,349,414,360,414,390,357,407}};
     foreach(int[] p in a){Point[] pts=new Point[p.Length/2];for(int k=0;k<pts.Length;k++)pts[k]=new Point(p[k*2],p[k*2+1]);g.FillPolygon(Brushes.White,pts);}
    }
   }
   for(int y=0;y<src.Height;y++)for(int x=0;x<src.Width;x++)if(mask.GetPixel(x,y).R>0)result.SetPixel(x,y,r.GetPixel(x,y));
   mask.Save(maskFile,ImageFormat.Png);mask.Dispose();return result;
  }
 }
}
'@
$root = $PSScriptRoot
$sources = Get-Content -Encoding UTF8 "$root/generation.json" | ConvertFrom-Json
foreach ($name in @('plaster_timber','rough_stone','plank_wall','clay_tiles','wood_shingles','slate_roof','thatch')) {
 $b=[K118]::Texture("$root/sources/$name.png")
 [K118]::Save($b,"$root/textures/$name.png",1)
 [K118]::Save($b,"$root/previews/${name}_4x.png",4)
 $repeat=New-Object System.Drawing.Bitmap 768,768
 $g=[System.Drawing.Graphics]::FromImage($repeat)
 for($y=0;$y -lt 3;$y++){for($x=0;$x -lt 3;$x++){$g.DrawImageUnscaled($b,$x*256,$y*256)}}
 $g.Dispose();[K118]::Save($repeat,"$root/previews/${name}_repeat3x3.png",1);$repeat.Dispose();$b.Dispose()
}
$f=New-Object System.Drawing.Bitmap "$root/sources/facades.png"
$names=@('repair','barracks','trap_workshop','shop')
for($i=0;$i -lt 4;$i++) {
 $left=[int][Math]::Round($f.Width*$i/4);$right=[int][Math]::Round($f.Width*($i+1)/4)
 $crop=New-Object System.Drawing.Rectangle ($left+4),([int]($f.Height*0.20)),($right-$left-8),([int]($f.Height*0.63))
 $b=[K118]::Sprite("$root/sources/facades.png",$crop,144,64)
 $name=$names[$i];[K118]::Save($b,"$root/decals/${name}_day.png",1);[K118]::Save($b,"$root/previews/${name}_4x.png",4)
 $lit=[K118]::Lit($b);[K118]::Save($lit,"$root/decals/${name}_night.png",1)
 $b.Dispose();$lit.Dispose()
}
$f.Dispose()
$p=New-Object System.Drawing.Bitmap "$root/sources/props.png"
$items=@(@('chimney',24,48),@('belfry',36,40),@('fence',64,24),@('hay',48,28),@('woodpile',52,28),@('well',48,56),@('trough',48,20),@('beacon_banner',24,40))
for($i=0;$i -lt 8;$i++) {
 $col=$i%4;$row=[int][Math]::Floor($i/4)
 $l=[int][Math]::Round($p.Width*$col/4)+5;$r=[int][Math]::Round($p.Width*($col+1)/4)-5
 $t=[int][Math]::Round($p.Height*$row/2)+5;$bot=[int][Math]::Round($p.Height*($row+1)/2)-5
 $crop=New-Object System.Drawing.Rectangle $l,$t,($r-$l),($bot-$t)
 $name=$items[$i][0];$b=[K118]::Sprite("$root/sources/props.png",$crop,$items[$i][1],$items[$i][2])
 [K118]::Save($b,"$root/props/$name.png",1);[K118]::Save($b,"$root/previews/${name}_4x.png",4);$b.Dispose()
}
$p.Dispose()
$repo=(Resolve-Path "$root/../../..").Path
$b=[K118]::Mock("$repo/Docs/References/courtyard_20261023/01_bldg_day_0_overview.png","$root/sources/day.png","$root/previews/mockup_day_mask.png",$false)
[K118]::Save($b,"$root/mockup_day.png",1);$b.Dispose()
$b=[K118]::Mock("$repo/Docs/References/courtyard_20261023/279_w27_night_1_wall_top_tower_north.png","$root/sources/night.png","$root/previews/mockup_night_mask.png",$true)
[K118]::Save($b,"$root/mockup_night.png",1);$b.Dispose()
