$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
public static class K091 {
 static string root;
 static Color C(string h){return ColorTranslator.FromHtml(h);}
 static Bitmap B(int w,int h){return new Bitmap(w,h,PixelFormat.Format32bppArgb);}
 static void Rect(Bitmap b,int x,int y,int w,int h,Color c){for(int j=Math.Max(y,0);j<Math.Min(y+h,b.Height);j++)for(int i=Math.Max(x,0);i<Math.Min(x+w,b.Width);i++)b.SetPixel(i,j,c);}
 static void Line(Bitmap b,int x,int y,int X,int Y,Color c){int dx=Math.Abs(X-x),sx=x<X?1:-1,dy=-Math.Abs(Y-y),sy=y<Y?1:-1,e=dx+dy;while(true){if(x>=0&&y>=0&&x<b.Width&&y<b.Height)b.SetPixel(x,y,c);if(x==X&&y==Y)break;int t=2*e;if(t>=dy){e+=dy;x+=sx;}if(t<=dx){e+=dx;y+=sy;}}}
 public static Bitmap Scale(Bitmap b,int n){Bitmap o=B(b.Width*n,b.Height*n);for(int y=0;y<o.Height;y++)for(int x=0;x<o.Width;x++)o.SetPixel(x,y,b.GetPixel(x/n,y/n));return o;}
 static void Save(Bitmap b,string p){string f=Path.Combine(root,p);Directory.CreateDirectory(Path.GetDirectoryName(f));b.Save(f,ImageFormat.Png);}
 static void Asset(Bitmap b,string id,bool triple){Save(b,"native/"+id+".png");using(Bitmap g=Scale(b,triple?3:1)){Save(g,id+".png");using(Bitmap p=Scale(g,4))Save(p,"previews/"+id+"_4x.png");}}
 static int Side(int x,int y){int[] a={y,255-x,255-y,x};int k=0;for(int i=1;i<4;i++)if(a[i]<a[k])k=i;return k;}
 static Bitmap Turn(Bitmap b,int side){Bitmap o=(Bitmap)b.Clone();if(side==1)o.RotateFlip(RotateFlipType.Rotate90FlipNone);if(side==2)o.RotateFlip(RotateFlipType.Rotate180FlipNone);if(side==3)o.RotateFlip(RotateFlipType.Rotate270FlipNone);return o;}
 static void Paste(Bitmap b,Bitmap s,int x,int y){using(Graphics g=Graphics.FromImage(b))g.DrawImageUnscaled(s,x,y);}
 static Bitmap Decal(Bitmap atlas,int part,int w,int h){Bitmap o=B(w,h);int top=part==0?95:part==1?490:840;int height=part==2?330:290;for(int y=0;y<h;y++)for(int x=0;x<w;x++){int xx=(75+x*1110/w)*atlas.Width/1254,yy=(top+y*height/h)*atlas.Height/1254;Color c=atlas.GetPixel(xx,yy);if(c.A>=128)o.SetPixel(x,y,Color.FromArgb(255,c.R,c.G,c.B));}return o;}
 static void Scar(Bitmap b,int x,int y){Color dark=C("#28221e"),hi=C("#a69b7d");Line(b,x,y,x+5,y+5,dark);Line(b,x+5,y+5,x+2,y+10,dark);Line(b,x+2,y+10,x+7,y+16,dark);Line(b,x+5,y+5,x+10,y+3,dark);Line(b,x+3,y+10,x+7,y+15,hi);}
 static Bitmap Wall(Bitmap castle,Bitmap atlas,int side,int state){Bitmap north=B(256,256);using(Bitmap rotated=Turn(castle,(4-side)%4))for(int y=0;y<256;y++)for(int x=0;x<256;x++)if(Side(x,y)==0)north.SetPixel(x,y,rotated.GetPixel(x,y));
  if(state>0){Scar(north,86,24);Scar(north,166,25);using(Bitmap d=Decal(atlas,0,42,20))Paste(north,d,108,23);}
  if(state>=2){for(int y=19;y<34;y++)for(int x=108;x<133;x++)if(y<24+(x%7))north.SetPixel(x,y,Color.Transparent);Scar(north,143,29);using(Bitmap d=Decal(atlas,1,46,15))Paste(north,d,104,37);}
  if(state==3){for(int y=14;y<50;y++)for(int x=103;x<149;x++)if(x>106+(y%5)&&x<146-(y%4))north.SetPixel(x,y,Color.Transparent);using(Bitmap d=Decal(atlas,1,32,13)){Paste(north,d,83,39);Paste(north,d,145,39);}}
  for(int y=0;y<256;y++)for(int x=0;x<256;x++)if(Side(x,y)!=0)north.SetPixel(x,y,Color.Transparent);
  Bitmap result=Turn(north,side);north.Dispose();for(int y=0;y<256;y++)for(int x=0;x<256;x++)if(Side(x,y)!=side)result.SetPixel(x,y,Color.Transparent);return result;
 }
 static Bitmap Hover(Bitmap wall){Bitmap b=B(256,256);for(int y=2;y<254;y++)for(int x=2;x<254;x++){bool inside=wall.GetPixel(x,y).A>0,near=false,empty=false;for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++){if(wall.GetPixel(x+dx,y+dy).A>0)near=true;else empty=true;}if(near&&empty)b.SetPixel(x,y,Color.FromArgb(inside?225:150,245,217,156));}return b;}
 static Bitmap Fade(Bitmap src,int a){Bitmap b=B(src.Width,src.Height);for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){Color c=src.GetPixel(x,y);if(c.A>0)b.SetPixel(x,y,Color.FromArgb((c.A*a+127)/255,c.R,c.G,c.B));}return b;}
 static void Effect(string id,Bitmap shape){int[] alpha={255,179,102,38};Bitmap sheet=B(shape.Width*4,shape.Height);for(int i=0;i<4;i++)using(Bitmap f=Fade(shape,alpha[i])){Asset(f,id+"_"+new string[]{"100","070","040","015"}[i],true);Paste(sheet,f,i*shape.Width,0);}Asset(sheet,id+"_sheet",true);sheet.Dispose();shape.Dispose();}
 static void Disc(Bitmap b,int r,Color c){for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){double dx=x-(b.Width-1)*.5,dy=y-(b.Height-1)*.5;if(dx*dx+dy*dy<=r*r)b.SetPixel(x,y,c);}}
 public static void Run(string output,string castleFile,string iconFile,string atlasFile){root=output;using(Bitmap castle=new Bitmap(castleFile))using(Bitmap icon=new Bitmap(iconFile))using(Bitmap atlas=new Bitmap(atlasFile)){
  using(Bitmap copy=(Bitmap)castle.Clone())Save(copy,"reference_castle_perimeter.png");Save(icon,"reference_item_telescope_lens.png");
  using(Bitmap i=Scale(icon,2))Asset(i,"item_telescope_lens_64",false);using(Bitmap i=Scale(icon,4))Asset(i,"item_telescope_lens_128",false);
  Bitmap courtyard=B(256,256);Rect(courtyard,40,40,176,176,C("#34332c"));for(int y=43;y<214;y+=8)for(int x=43;x<214;x+=8)Rect(courtyard,x,y,1,1,C("#48463a"));Asset(courtyard,"repair_castle_base",true);courtyard.Dispose();
  string[] sides={"north","east","south","west"};for(int s=0;s<4;s++){Bitmap sheet=B(1024,256);for(int state=0;state<4;state++)using(Bitmap wall=Wall(castle,atlas,s,state)){string id="repair_wall_"+sides[s]+"_"+new string[]{"healthy","damaged","badly_damaged","breached"}[state];Asset(wall,id,true);Paste(sheet,wall,state*256,0);if(state==0)using(Bitmap hover=Hover(wall))Asset(hover,"repair_wall_"+sides[s]+"_hover",true);}Asset(sheet,"repair_wall_"+sides[s]+"_states_sheet",true);sheet.Dispose();
   using(Bitmap repair=B(256,256)){using(Bitmap d=Decal(atlas,2,58,24))Paste(repair,d,98,37);using(Bitmap turned=Turn(repair,s))Asset(turned,"repair_wall_"+sides[s]+"_repairing",true);}
  }
  Bitmap normal=B(16,16);Disc(normal,4,C("#39221e"));Disc(normal,3,C("#c45f49"));Rect(normal,6,5,3,1,C("#efb58c"));Effect("afterimage_normal",normal);
  Bitmap special=B(16,16);Disc(special,6,C("#34251a"));Disc(special,5,C("#dbb66e"));Disc(special,3,C("#a45338"));Rect(special,6,5,3,2,C("#f4dca2"));Effect("afterimage_special",special);
  Bitmap label=B(40,20);Rect(label,1,3,38,14,C("#241c16"));Rect(label,2,4,36,12,C("#b89658"));Rect(label,3,5,34,10,C("#493a29"));for(int i=0;i<3;i++)Rect(label,5+i*3,9-(i==1?2:0),2,2,C("#e7c78d"));Effect("afterimage_group_label",label);
  Bitmap ring=B(128,128);for(int y=2;y<126;y++)for(int x=2;x<126;x++){double d=Math.Sqrt((x-63.5)*(x-63.5)+(y-63.5)*(y-63.5));if(d>=58&&d<59)ring.SetPixel(x,y,Color.FromArgb(64,221,199,151));else if(d>=59&&d<60)ring.SetPixel(x,y,Color.FromArgb(30,221,199,151));}Effect("lens_ghost_ring",ring);
 }}
}
'@
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
[K091]::Run($PSScriptRoot,(Join-Path $root 'Game/Assets/_Project/Art/UI/Resources/TrapMap/castle_perimeter.png'),(Join-Path $root 'Docs/References/codex_k067_v1/item_telescope_lens.png'),(Join-Path $PSScriptRoot 'source_damage_atlas.png'))
