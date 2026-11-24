$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
public class K056 {
 static string dest;
 static Color Near(Color c, Color[] p) { double best=double.MaxValue; Color win=p[0]; foreach(Color v in p) { double d=2*Math.Pow(c.R-v.R,2)+3*Math.Pow(c.G-v.G,2)+Math.Pow(c.B-v.B,2); if(d<best){best=d;win=v;} } return Color.FromArgb(255,win.R,win.G,win.B); }
 static Color[] Palette(string[] files, int[][] targets, string name) {
  Dictionary<int,int> counts=new Dictionary<int,int>();
  foreach(string f in files) using(Bitmap b=new Bitmap(f)) for(int y=0;y<b.Height;y++) for(int x=0;x<b.Width;x++){Color c=b.GetPixel(x,y); if(c.A<180)continue; int k=Color.FromArgb(255,c.R,c.G,c.B).ToArgb();if(!counts.ContainsKey(k))counts[k]=0;counts[k]++;}
  List<Color> pool=new List<Color>();foreach(var e in counts)if(e.Value>=12)pool.Add(Color.FromArgb(e.Key));
  List<Color> p=new List<Color>();foreach(int[] t in targets){Color c=Near(Color.FromArgb(t[0],t[1],t[2]),pool.ToArray());if(!p.Contains(c))p.Add(c);}
  List<string> lines=new List<string>();foreach(Color c in p)lines.Add("#"+c.R.ToString("X2")+c.G.ToString("X2")+c.B.ToString("X2"));File.WriteAllLines(Path.Combine(dest,name+"_palette.txt"),lines.ToArray());return p.ToArray();
 }
 static Rectangle Bounds(Bitmap b, Rectangle area) {int l=area.Right,t=area.Bottom,r=-1,bot=-1;for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++)if(b.GetPixel(x,y).A>=180){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);bot=Math.Max(bot,y);}if(r<l)throw new Exception("빈 칸");return Rectangle.FromLTRB(l,t,r+1,bot+1);}
 static Bitmap Frame(Bitmap b, Rectangle area, Color[] pal,int height) {
  Rectangle q=Bounds(b,area);int w=(int)Math.Round(q.Width*(double)height/q.Height);if(w>60)throw new Exception("칸 폭 초과");
  Bitmap small=new Bitmap(w,height);for(int y=0;y<height;y++)for(int x=0;x<w;x++){Color c=b.GetPixel(q.X+Math.Min(q.Width-1,(int)((x+.5)*q.Width/w)),q.Y+Math.Min(q.Height-1,(int)((y+.5)*q.Height/height)));if(c.A>=180)small.SetPixel(x,y,Near(c,pal));}
  Bitmap result=new Bitmap(64,96);int dx=32-w/2,dy=88-height;for(int y=0;y<height;y++)for(int x=0;x<w;x++){Color c=small.GetPixel(x,y);if(c.A==0)continue;bool edge=x==0||x==w-1||y==0||y==height-1; if(!edge)edge=small.GetPixel(x-1,y).A==0||small.GetPixel(x+1,y).A==0||small.GetPixel(x,y-1).A==0||small.GetPixel(x,y+1).A==0;result.SetPixel(dx+x,dy+y,edge?pal[0]:c);}small.Dispose();return result;
 }
 static void Save(Bitmap b,string name){b.Save(Path.Combine(dest,name+"_64.png"),ImageFormat.Png);using(Bitmap big=new Bitmap(b.Width*4,b.Height*4)){for(int y=0;y<big.Height;y++)for(int x=0;x<big.Width;x++)big.SetPixel(x,y,b.GetPixel(x/4,y/4));big.Save(Path.Combine(dest,name+"_256.png"),ImageFormat.Png);}}
 static Bitmap Sheet(Bitmap[] frames,int cols,int rows,int[] order){Bitmap b=new Bitmap(cols*64,rows*96);for(int i=0;i<order.Length;i++){if(order[i]<0)continue;Bitmap f=frames[order[i]];for(int y=0;y<96;y++)for(int x=0;x<64;x++)b.SetPixel((i%cols)*64+x,(i/cols)*96+y,f.GetPixel(x,y));}return b;}
 public static void Build(string root){
  dest=Path.Combine(root,"Docs/References/codex_k056_v1");string art=Path.Combine(root,"Game/Assets/_Project/Art/Characters");
  int[][] a={new[]{24,16,16},new[]{17,24,39},new[]{29,42,64},new[]{49,64,88},new[]{48,27,21},new[]{79,43,29},new[]{115,65,41},new[]{153,97,63},new[]{190,130,83},new[]{62,40,29},new[]{100,65,41},new[]{147,103,67},new[]{190,154,108},new[]{116,110,99},new[]{182,175,156},new[]{233,225,199},new[]{117,67,42},new[]{193,123,72},new[]{244,177,111}};
  int[][] h={new[]{22,16,18},new[]{27,26,31},new[]{42,39,44},new[]{65,58,62},new[]{91,85,95},new[]{143,137,149},new[]{185,181,188},new[]{226,222,219},new[]{91,53,12},new[]{157,106,12},new[]{210,156,20},new[]{248,208,56},new[]{52,26,18},new[]{89,45,25},new[]{133,73,39},new[]{180,111,58},new[]{153,83,44},new[]{220,144,79},new[]{255,192,126},new[]{37,64,88},new[]{70,111,146}};
  Color[] ap=Palette(new[]{Path.Combine(art,"Static/archer_static.png"),Path.Combine(art,"Fire/archer_fire.png")},a,"archer");Color[] hp=Palette(new[]{Path.Combine(art,"Hero/hero_back_idle.png"),Path.Combine(art,"Hero/hero_walk_run_grid.png")},h,"hero");
  Bitmap[] f=new Bitmap[15];using(Bitmap b=new Bitmap(Path.Combine(dest,"generated_v2.png")))for(int i=0;i<15;i++){int col=i%5,row=i/5;Rectangle cell=Rectangle.FromLTRB(col*b.Width/5,row*b.Height/3,(col+1)*b.Width/5,(row+1)*b.Height/3);f[i]=Frame(b,cell,i<9?ap:hp,48);}
  string run=Path.Combine(dest,"generated_run_v2.png");if(File.Exists(run))using(Bitmap b=new Bitmap(run))for(int i=0;i<4;i++){f[11+i].Dispose();f[11+i]=Frame(b,Rectangle.FromLTRB(i*b.Width/4,0,(i+1)*b.Width/4,b.Height),hp,48);}
  for(int i=0;i<f.Length;i++)Save(f[i],"frame_"+i.ToString("00"));
  Save(f[0],"archer_back_idle");Save(Sheet(f,4,1,new[]{1,2,3,4}),"archer_back_walk4");Save(Sheet(f,4,1,new[]{5,6,7,8}),"archer_fire4");
  Save(Sheet(f,6,1,new[]{5,6,7,7,8,8}),"archer_fire6_compat");
  Save(f[10],"hero_back_idle");Save(f[9],"hero_front_idle");Save(Sheet(f,4,1,new[]{11,12,13,14}),"hero_side_run4");
  Save(Sheet(f,4,4,new[]{10,-1,-1,-1,-1,-1,-1,-1,11,12,13,14,9,-1,-1,-1}),"hero_layout_preview");
  Save(Sheet(f,5,3,new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13,14}),"all_frames");
 }
}
'@
[K056]::Build((Get-Location).Path)
