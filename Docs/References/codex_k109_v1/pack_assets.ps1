$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class K109Pack {
 public static Bitmap Load(string p){using(var a=new Bitmap(p))return new Bitmap(a);}
 public static void Save(Bitmap b,string p){b.Save(p,ImageFormat.Png);}
 public static Bitmap Scale(Bitmap a,int w,int h){var b=new Bitmap(w,h);for(int y=0;y<h;y++)for(int x=0;x<w;x++)b.SetPixel(x,y,a.GetPixel(Math.Min(a.Width-1,(int)((x+.5)*a.Width/w)),Math.Min(a.Height-1,(int)((y+.5)*a.Height/h))));return b;}
 public static Bitmap ScalePhase(Bitmap a,int w,int h,double phase){var b=new Bitmap(w,h);for(int y=0;y<h;y++)for(int x=0;x<w;x++)b.SetPixel(x,y,a.GetPixel(Math.Min(a.Width-1,(int)((x+phase)*a.Width/w)),Math.Min(a.Height-1,(int)((y+phase)*a.Height/h))));return b;}
 public static Bitmap Crop(Bitmap a,int x,int y,int w,int h){return a.Clone(new Rectangle(x,y,w,h),PixelFormat.Format32bppArgb);}
 public static Rectangle Box(Bitmap a){int l=a.Width,t=a.Height,r=-1,b=-1;for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y).A>=128){l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);b=Math.Max(b,y);}return r<0?Rectangle.Empty:new Rectangle(l,t,r-l+1,b-t+1);}
 public static void Paste(Bitmap dst,Bitmap src,int dx,int dy){for(int y=0;y<src.Height;y++)for(int x=0;x<src.Width;x++)if(dx+x>=0&&dy+y>=0&&dx+x<dst.Width&&dy+y<dst.Height)dst.SetPixel(dx+x,dy+y,src.GetPixel(x,y));}
 static Color[] pal=Array.ConvertAll(new string[]{"17131d","a96948","e5a56f","ffd4a2","35231f","6c4230","a7724e","181b28","2c2e3e","484958","6c451d","b78235","efbd62","ffae2b","fff0b0","153653","2e78ad","f5f6ec","512f25","8a5134","bd8052","101e30","21344c","3a5268","aa9271","ddc69d","fff0cf"},s=>Color.FromArgb(255,Convert.ToInt32(s.Substring(0,2),16),Convert.ToInt32(s.Substring(2,2),16),Convert.ToInt32(s.Substring(4,2),16)));
 public static void Quant(Bitmap b){for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,y);if(c.A<128){b.SetPixel(x,y,Color.Transparent);continue;}int d=int.MaxValue;Color p=pal[0];foreach(var q in pal){int r=c.R-q.R,g=c.G-q.G,bl=c.B-q.B;int v=r*r*2+g*g*3+bl*bl*2;if(v<d){d=v;p=q;}}b.SetPixel(x,y,p);}Clean(b,3);}
 public static List<int> Components(Bitmap b){return Clean(b,0);}
 public static List<int> Clean(Bitmap b,int threshold){int w=b.Width,h=b.Height;bool[] seen=new bool[w*h];var sizes=new List<int>();for(int y=0;y<h;y++)for(int x=0;x<w;x++){int k=y*w+x;if(seen[k]||b.GetPixel(x,y).A<128)continue;var q=new Queue<int>();var pts=new List<int>();q.Enqueue(k);seen[k]=true;while(q.Count>0){int p=q.Dequeue();pts.Add(p);int xx=p%w,yy=p/w;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int nx=xx+dx,ny=yy+dy;if(nx<0||ny<0||nx>=w||ny>=h)continue;int n=ny*w+nx;if(!seen[n]&&b.GetPixel(nx,ny).A>=128){seen[n]=true;q.Enqueue(n);}}}if(pts.Count<=threshold)foreach(int p in pts)b.SetPixel(p%w,p/w,Color.Transparent);else sizes.Add(pts.Count);}sizes.Sort();sizes.Reverse();return sizes;}
 public static double Axis(Bitmap b){var bb=Box(b);double sum=0,n=0;int y0=bb.Y+(int)(bb.Height*.46),y1=bb.Y+(int)(bb.Height*.58);for(int y=y0;y<=y1;y++){int l=b.Width,r=-1;for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A>=128){l=Math.Min(l,x);r=Math.Max(r,x);}if(r>=l){sum+=(l+r)*.5;n++;}}return sum/Math.Max(1,n);}
 public static Bitmap Figure(Bitmap src,Rectangle area,int height,int cw,int ch,int foot,bool quant){using(var reg=Crop(src,area.X,area.Y,area.Width,area.Height)){var sizes=Components(reg);if(sizes.Count>1)Clean(reg,sizes[0]-1);var bb=Box(reg);using(var tight=Crop(reg,bb.X,bb.Y,bb.Width,bb.Height)){int width=(int)Math.Round((double)bb.Width*height/bb.Height);using(var small=(cw==64?ScalePhase(tight,width,height,.75):Scale(tight,width,height))){if(quant)Quant(small);var dst=new Bitmap(cw,ch);int dx=(int)Math.Round(cw*.5-Axis(small));Paste(dst,small,dx,foot-height);for(int y=0;y<ch;y++)for(int x=0;x<cw;x++)if(dst.GetPixel(x,y).A==0)dst.SetPixel(x,y,Color.FromArgb(0,0,0,0));return dst;}}}}
 public static void Maps(Bitmap b,string normal,string spec){var n=new Bitmap(b.Width,b.Height);var s=new Bitmap(b.Width,b.Height);for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,y);if(c.A==0){n.SetPixel(x,y,Color.FromArgb(128,128,255));s.SetPixel(x,y,Color.Black);continue;}double l=0,r=0,u=0,d=0;for(int k=1;k<=4;k++){l+=x-k>=0&&b.GetPixel(x-k,y).A>0?1:0;r+=x+k<b.Width&&b.GetPixel(x+k,y).A>0?1:0;u+=y-k>=0&&b.GetPixel(x,y-k).A>0?1:0;d+=y+k<b.Height&&b.GetPixel(x,y+k).A>0?1:0;}double nx=(l-r)*.15,ny=(d-u)*.15,nz=1,len=Math.Sqrt(nx*nx+ny*ny+nz*nz);n.SetPixel(x,y,Color.FromArgb((int)(128+127*nx/len),(int)(128+127*ny/len),(int)(128+127*nz/len)));bool gold=(c.R==108&&c.G==69&&c.B==29)||(c.R==183&&c.G==130&&c.B==53)||(c.R==239&&c.G==189&&c.B==98);gold=gold && y%128>=75 && y%128<=106;int val=gold?220:((c.R==255&&c.G==174)?150:12);s.SetPixel(x,y,Color.FromArgb(val,val,val));}Save(n,normal);Save(s,spec);n.Dispose();s.Dispose();}
}
'@
$root=Split-Path $PSScriptRoot -Parent
$repo=(Resolve-Path "$PSScriptRoot/../../..").Path
$out=$PSScriptRoot
$walk=[K109Pack]::Load("$out/walk_final_source.png")
$command=[K109Pack]::Load("$out/command_generated.png")
$portraits=[K109Pack]::Load("$out/portraits_final_source.png")
$grid=[Drawing.Bitmap]::new(384,512)
$placement=@()
for($row=0;$row -lt 4;$row++){for($col=0;$col -lt 4;$col++){
 $x=[int][Math]::Floor($col*$walk.Width/4);$y=[int][Math]::Floor($row*$walk.Height/4)
 $x1=[int][Math]::Floor(($col+1)*$walk.Width/4);$y1=[int][Math]::Floor(($row+1)*$walk.Height/4)
 $cell=[K109Pack]::Figure($walk,[Drawing.Rectangle]::new($x,$y,$x1-$x,$y1-$y),73,96,128,120,$true)
 [K109Pack]::Paste($grid,$cell,$col*96,$row*128)
 $placement+=@{sheet='hero_walk_run_grid';row=$row;column=$col;source_region=@($x,$y,$x1,$y1)}
 $cell.Dispose()
}}
$cmd=[Drawing.Bitmap]::new(288,128)
for($i=0;$i -lt 3;$i++){
 $x=[int][Math]::Floor($i*$command.Width/3);$x1=[int][Math]::Floor(($i+1)*$command.Width/3)
 $cell=[K109Pack]::Figure($command,[Drawing.Rectangle]::new($x,0,$x1-$x,$command.Height),73,96,128,120,$true)
 [K109Pack]::Paste($cmd,$cell,$i*96,0);$cell.Dispose()
}
$idle=[K109Pack]::Crop($cmd,0,0,96,128)
$assets=@{'hero_walk_run_grid'=$grid;'hero_command'=$cmd;'hero_back_idle'=$idle}
foreach($name in $assets.Keys){
 $b=$assets[$name];[K109Pack]::Save($b,"$out/${name}_native.png")
 $game=[K109Pack]::Scale($b,$b.Width*3,$b.Height*3);[K109Pack]::Save($game,"$out/$name.png")
 $preview=[K109Pack]::Scale($b,$b.Width*4,$b.Height*4);[K109Pack]::Save($preview,"$out/${name}_native_4x.png");$preview.Dispose()
 [K109Pack]::Maps($b,"$out/${name}_normal_native.png","$out/${name}_spec_native.png")
 foreach($map in @('normal','spec')){$m=[K109Pack]::Load("$out/${name}_${map}_native.png");$large=[K109Pack]::Scale($m,$m.Width*3,$m.Height*3);[K109Pack]::Save($large,"$out/${name}_${map}.png");$large.Dispose();$m.Dispose()}
 $game.Dispose()
}
$emotions=@('neutral','determined','surprised','tired')
for($i=0;$i -lt 4;$i++){
 $cell=[K109Pack]::Figure($portraits,[Drawing.Rectangle]::new($i*384,0,384,656),75,64,96,89,$true)
 $big=[K109Pack]::Scale($cell,256,384);[K109Pack]::Save($big,"$out/portrait_hero_$($emotions[$i]).png")
 $prev=[K109Pack]::Scale($big,1024,1536);[K109Pack]::Save($prev,"$out/portrait_hero_$($emotions[$i])_4x.png")
 $cell.Dispose();$big.Dispose();$prev.Dispose()
}
$strip=[Drawing.Bitmap]::new(192,48)
for($i=0;$i -lt 4;$i++){
 $cell=[K109Pack]::Figure($portraits,[Drawing.Rectangle]::new($i*384,656,384,$portraits.Height-656),44,48,48,47,$true)
 [K109Pack]::Paste($strip,$cell,$i*48,0);$cell.Dispose()
}
$big=[K109Pack]::Scale($strip,576,144);[K109Pack]::Save($big,"$out/officer_expressions.png")
$prev=[K109Pack]::Scale($big,2304,576);[K109Pack]::Save($prev,"$out/officer_expressions_4x.png")
$placement | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 "$out/placement.json"
Write-Output '자산 시트 패킹 완료'
