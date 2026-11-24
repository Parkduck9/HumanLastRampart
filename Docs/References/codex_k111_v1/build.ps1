$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;using System.Drawing;using System.Drawing.Imaging;using System.Drawing.Drawing2D;using System.Collections.Generic;
public static class K111 {
 public static string Root,Repo;
 public static Bitmap Read(string p){return new Bitmap(p);}
 public static Bitmap Resize(Bitmap b,int w,int h){var o=new Bitmap(w,h,PixelFormat.Format32bppArgb);var si=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);var di=o.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);byte[] src=new byte[si.Stride*b.Height],dst=new byte[di.Stride*h];System.Runtime.InteropServices.Marshal.Copy(si.Scan0,src,0,src.Length);for(int y=0;y<h;y++){int sy=Math.Min(b.Height-1,(int)((y+.5)*b.Height/h));for(int x=0;x<w;x++){int sx=Math.Min(b.Width-1,(int)((x+.5)*b.Width/w));int s=sy*si.Stride+sx*4,d=y*di.Stride+x*4;dst[d]=src[s];dst[d+1]=src[s+1];dst[d+2]=src[s+2];dst[d+3]=src[s+3];}}System.Runtime.InteropServices.Marshal.Copy(dst,0,di.Scan0,dst.Length);b.UnlockBits(si);o.UnlockBits(di);return o;}
 public static Bitmap Crop(Bitmap b,int x,int y,int w,int h){return b.Clone(new Rectangle(x,y,w,h),PixelFormat.Format32bppArgb);}
 public static void Save(Bitmap b,string p){b.Save(Root+"/"+p,ImageFormat.Png);}
 public static void Paste(Bitmap d,Bitmap s,int x,int y){using(var g=Graphics.FromImage(d)){g.DrawImageUnscaled(s,x,y);}}
 public static void Preview(Bitmap b,string name){using(var p=Resize(b,b.Width*4,b.Height*4))Save(p,"previews/"+name+"_4x.png");}
 public static void Portraits(){
 string[] names={"neutral","determined","alarmed","tired"};
 using(var gen=Read(Root+"/sources/hagen_mouths_generated.png"))using(var story=new Bitmap(1024,384))using(var native=Read(Repo+"/Docs/References/codex_k108_v1/hagen_expressions_native_96x128.png")){
 for(int i=0;i<4;i++)using(var old=Read(Repo+"/Docs/References/codex_k108_v1/portrait_hagen_"+names[i]+".png")){
 for(int ly=32;ly<35;ly++)for(int lx=32;lx<38;lx++){
 var c=gen.GetPixel((int)((i*256+lx*4+2)*gen.Width/1024.0),(int)((ly*4+2)*gen.Height/384.0));
 if(c.R<160&&c.G<105&&c.B<85&&c.A>100){
 var ink=Color.FromArgb(255,Math.Min(112,(int)c.R),Math.Min(60,(int)c.G),Math.Min(40,(int)c.B));
 for(int dy=0;dy<4;dy++)for(int dx=0;dx<4;dx++)old.SetPixel(lx*4+dx,ly*4+dy,ink);
 native.SetPixel(i*96+lx+16,ly+32,ink);
 }}
 Save(old,"portrait_hagen_"+names[i]+".png");Preview(old,"portrait_hagen_"+names[i]);Paste(story,old,i*256,0);
 }
 Save(story,"hagen_expressions_story_256x384.png");Save(native,"hagen_expressions_native_96x128.png");Preview(native,"hagen_expressions_native");using(var game=Resize(native,1152,384))Save(game,"hagen_expressions_game_288x384.png");
 }}
 public static void Effects(){
 string[] levels={"low","normal","high"};int[] radii={36,46,56},peaks={40,64,88};
 using(var src=Read(Root+"/sources/glow_generated.png")){
 for(int k=0;k<3;k++)using(var o=new Bitmap(192,192)){
 for(int y=0;y<192;y++)for(int x=0;x<192;x++){
 double dx=x-95.5,dy=y-95.5,r=Math.Sqrt(dx*dx+dy*dy)/radii[k];if(r>=1)continue;
 int sx=(int)(src.Width*.49+dx/radii[k]*src.Width*.185),sy=(int)(src.Height*.49+dy/radii[k]*src.Height*.185);
 var c=src.GetPixel(Math.Max(0,Math.Min(src.Width-1,sx)),Math.Max(0,Math.Min(src.Height-1,sy)));
 int band=r<.24?4:r<.48?3:r<.72?2:1;int a=peaks[k]*band/4;
 o.SetPixel(x,y,Color.FromArgb(a,Math.Max(205,(int)c.R),Math.Max(105,Math.Min(155,(int)c.G)),24));
 }
 Save(o,"native/glow_"+levels[k]+".png");Preview(o,"glow_"+levels[k]);using(var g=Resize(o,576,576))Save(g,"game/glow_"+levels[k]+".png");
 }}
 using(var src=Read(Root+"/sources/fog_generated.png"))using(var band=new Bitmap(512,128)){
 for(int y=36;y<64;y++)for(int x=0;x<512;x++){
 int sy=(int)(src.Height*(.31+(y-36)/28.0*.25));int sx=(int)(x/511.0*(src.Width-1));var c=src.GetPixel(sx,Math.Min(src.Height-1,sy));
 double edge=Math.Min((y-36)/4.0,(63-y)/4.0);int a=(int)(Math.Min(1,edge)*Math.Min(210,(int)c.A));if(a<16)continue;
 int shade=c.B>24?2:c.B>12?1:0;band.SetPixel(x,y,Color.FromArgb(a,4+shade*2,7+shade*3,14+shade*5));
 }
 for(int y=0;y<128;y++)band.SetPixel(511,y,band.GetPixel(0,y));
 Save(band,"native/fog_band.png");Preview(band,"fog_band");using(var g=Resize(band,1536,384))Save(g,"game/fog_band.png");
 }
 }
 public static void Backgrounds(){foreach(string n in new[]{"dead","relit"})using(var src=Read(Root+"/sources/story_"+n+"_generated.png"))using(var o=Resize(src,1920,1080)){Save(o,"story_"+n+"_beacon_1920x1080.png");Preview(o,"story_"+n+"_beacon");}}
 public static void Battle(){
 using(var src=Read(Repo+"/Docs/References/beacon_game_20261022/05_pf_night_clean.png"))using(var o=new Bitmap(1920,1080)){
 int[] lights={395,1415};
 for(int y=0;y<1080;y++)for(int x=0;x<1920;x++){
 var c=src.GetPixel(x,y);double warm=0;
 foreach(int cx in lights){double d=Math.Sqrt(Math.Pow((x-cx)/175.0,2)+Math.Pow((y-690)/130.0,2));double l=Math.Max(0,1-d);warm=Math.Max(warm,l*l);}
 bool flame=(c.R>145&&c.R>c.B*1.6&&c.G>c.B*1.25)||(c.R>190&&c.G>170&&c.B>150&&((y<250&&x<1000)||(y>580&&y<675&&((x>350&&x<460)||(x>1370&&x<1470)))));
 double ambient=.30+.22*Math.Max(0,Math.Min(1,(y-500)/160.0));
 double r=c.R*ambient*.66,g=c.G*ambient*.80,b=c.B*ambient*1.13;
 r+=c.R*warm*.65;g+=c.G*warm*.40;b+=c.B*warm*.08;
 if(flame){r=Math.Max(r,c.R*.82);g=Math.Max(g,c.G*.72);b=Math.Max(b,c.B*.62);}
 o.SetPixel(x,y,Color.FromArgb(255,Math.Min(255,(int)r),Math.Min(255,(int)g),Math.Min(255,(int)b)));
 }
 using(var g=Graphics.FromImage(o)){
 using(var shadow=new SolidBrush(Color.FromArgb(145,2,5,13))){
 foreach(int cx in lights){g.FillPolygon(shadow,new[]{new Point(cx+7,722),new Point(cx+30,722),new Point(cx+118,806),new Point(cx+70,806)});}
 }
 }
 using(var band=Read(Root+"/native/fog_band.png"))using(var stretched=Resize(band,1920,128))Paste(o,stretched,0,-28);
 using(var sh=Read(Repo+"/Docs/References/codex_k103_v1/native/brazier_normal.png"))using(var first=Crop(sh,0,0,96,128))using(var b=Resize(first,96,128))foreach(int cx in lights)Paste(o,b,cx-48,602);
 Save(o,"mockup_north_wall_high_1920x1080.png");Preview(o,"mockup_north_wall_high");
 }}
 public static void Build(string root){Root=root;Repo=System.IO.Path.GetFullPath(root+"/../../..");Portraits();Effects();Backgrounds();Battle();}
}
'@
foreach($d in @('sources','native','game','previews')){New-Item -ItemType Directory -Force "$PSScriptRoot/$d"|Out-Null}
[K111]::Build($PSScriptRoot)
Write-Output '그림 구성 완료'
