$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;using System.Drawing;using System.Drawing.Imaging;using System.Collections.Generic;
public static class Hagen108 {
 public static Bitmap Scale(Bitmap b,int k){var o=new Bitmap(b.Width*k,b.Height*k);for(int y=0;y<o.Height;y++)for(int x=0;x<o.Width;x++)o.SetPixel(x,y,b.GetPixel(x/k,y/k));return o;}
 public static void Paste(Bitmap o,Bitmap b,int dx,int dy){for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)o.SetPixel(x+dx,y+dy,b.GetPixel(x,y));}
 public static Rectangle Bounds(Bitmap b){int l=b.Width,t=b.Height,r=-1,d=-1;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A>=128){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);d=Math.Max(d,y);}return Rectangle.FromLTRB(l,t,r+1,d+1);}
 public static void Build(string root){
 string[] names={"neutral","determined","alarmed","tired"};
 using(var sheet=new Bitmap(384,128))using(var storySheet=new Bitmap(1024,384)){
 for(int i=0;i<4;i++)using(var src=new Bitmap(root+"/sources/hagen_"+names[i]+"_generated.png")){
 var bb=Bounds(src);int h=74,w=(int)Math.Round((double)bb.Width*h/bb.Height);using(var sprite=new Bitmap(w,h)){
 for(int y=0;y<h;y++)for(int x=0;x<w;x++){var c=src.GetPixel(bb.X+Math.Min(bb.Width-1,(int)((x+.5)*bb.Width/w)),bb.Y+Math.Min(bb.Height-1,(int)((y+.5)*bb.Height/h)));sprite.SetPixel(x,y,c.A>=128?Color.FromArgb(255,c.R,c.G,c.B):Color.FromArgb(0,0,0,0));}
 using(var native=new Bitmap(96,128))using(var logical=new Bitmap(64,96)){
 Paste(native,sprite,48-w/2,120-h);Paste(logical,sprite,32-w/2,88-h);
 using(var story=Scale(logical,4))using(var preview=Scale(story,4)){
 story.Save(root+"/portrait_hagen_"+names[i]+".png",ImageFormat.Png);preview.Save(root+"/portrait_hagen_"+names[i]+"_4x.png",ImageFormat.Png);Paste(storySheet,story,i*256,0);}
 Paste(sheet,native,i*96,0);
 }}}
 sheet.Save(root+"/hagen_expressions_native_96x128.png",ImageFormat.Png);
 using(var game=Scale(sheet,3))game.Save(root+"/hagen_expressions_game_288x384.png",ImageFormat.Png);
 using(var preview=Scale(sheet,4))preview.Save(root+"/hagen_expressions_native_4x.png",ImageFormat.Png);
 storySheet.Save(root+"/hagen_expressions_story_256x384.png",ImageFormat.Png);
 }
 }
 public static string Verify(string p){using(var b=new Bitmap(p)){var bb=Bounds(b);int soft=0,components=0,minSize=int.MaxValue;var seen=new bool[b.Width*b.Height];for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){if(b.GetPixel(x,y).A>0&&b.GetPixel(x,y).A<255)soft++;int id=y*b.Width+x;if(seen[id]||b.GetPixel(x,y).A==0)continue;components++;int count=0;var q=new Queue<int>();q.Enqueue(id);seen[id]=true;while(q.Count>0){int v=q.Dequeue(),vx=v%b.Width,vy=v/b.Width;count++;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int nx=vx+dx,ny=vy+dy;if(nx<0||ny<0||nx>=b.Width||ny>=b.Height)continue;int ni=ny*b.Width+nx;if(!seen[ni]&&b.GetPixel(nx,ny).A>0){seen[ni]=true;q.Enqueue(ni);}}}minSize=Math.Min(minSize,count);}
 return "{\"width\":"+b.Width+",\"height\":"+b.Height+",\"bbox\":["+bb.X+","+bb.Y+","+(bb.Right-1)+","+(bb.Bottom-1)+"],\"partial_alpha_pixels\":"+soft+",\"components_8_connected\":"+components+",\"smallest_component\":"+minSize+"}";}}
}
'@
$root=$PSScriptRoot
[Hagen108]::Build($root)
$comparison=[Drawing.Bitmap]::new(768,384)
$paths=@("$root/portrait_hagen_neutral.png","$root/../../../Game/Assets/_Project/Story/Resources/StoryArt/portrait_wizard_neutral.png","$root/../../../Game/Assets/_Project/Story/Resources/StoryArt/portrait_archer_neutral.png")
for($i=0;$i -lt 3;$i++){$b=[Drawing.Bitmap]::FromFile($paths[$i]);[Hagen108]::Paste($comparison,$b,256*$i,0);$b.Dispose()}
$comparison.Save("$root/comparison_hagen_wizard_archer.png",[Drawing.Imaging.ImageFormat]::Png)
$big=[Hagen108]::Scale($comparison,4);$big.Save("$root/comparison_hagen_wizard_archer_4x.png",[Drawing.Imaging.ImageFormat]::Png);$big.Dispose();$comparison.Dispose()
$checks=[ordered]@{}
foreach($n in @('neutral','determined','alarmed','tired')){$checks[$n]=[Hagen108]::Verify("$root/portrait_hagen_$n.png")|ConvertFrom-Json}
$checks|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 "$root/validation.json"
$checks|ConvertTo-Json -Depth 8
