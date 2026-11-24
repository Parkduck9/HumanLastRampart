$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = (Resolve-Path "$PSScriptRoot/../../..").Path
$code = @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Collections.Generic;
public class Mockup084 {
 static string root, dest; static Bitmap b; static Graphics g;
 static Color ink=Color.FromArgb(38,25,16), light=Color.FromArgb(250,235,201);
 static List<string> contrast=new List<string>();
 static void Reduce(Bitmap n){
  var pixels=new List<Color>();for(int y=0;y<n.Height;y++)for(int x=0;x<n.Width;x++){var c=n.GetPixel(x,y);if(c.A>127)pixels.Add(c);}
  if(pixels.Count==0)return;var boxes=new List<List<Color>>();boxes.Add(pixels);
  while(boxes.Count<256){int bi=-1,best=-1,axis=0;for(int i=0;i<boxes.Count;i++){if(boxes[i].Count<2)continue;int[] lo={255,255,255},hi={0,0,0};foreach(var c in boxes[i]){int[] v={c.R,c.G,c.B};for(int j=0;j<3;j++){lo[j]=Math.Min(lo[j],v[j]);hi[j]=Math.Max(hi[j],v[j]);}}for(int j=0;j<3;j++){int score=(hi[j]-lo[j])*(int)Math.Sqrt(boxes[i].Count);if(score>best){best=score;bi=i;axis=j;}}}if(bi<0||best==0)break;var box=boxes[bi];int ax=axis;box.Sort((a,c)=>(ax==0?a.R:ax==1?a.G:a.B).CompareTo(ax==0?c.R:ax==1?c.G:c.B));int half=box.Count/2;boxes[bi]=box.GetRange(0,half);boxes.Add(box.GetRange(half,box.Count-half));}
  var palette=new List<Color>();foreach(var box in boxes){long r=0,g=0,b=0;foreach(var c in box){r+=c.R;g+=c.G;b+=c.B;}palette.Add(Color.FromArgb((int)(r/box.Count),(int)(g/box.Count),(int)(b/box.Count)));}
  var cache=new Dictionary<int,Color>();for(int y=0;y<n.Height;y++)for(int x=0;x<n.Width;x++){var c=n.GetPixel(x,y);if(c.A<128){n.SetPixel(x,y,Color.Transparent);continue;}int key=(c.R>>2)*4096+(c.G>>2)*64+(c.B>>2);Color chosen;if(!cache.TryGetValue(key,out chosen)){int best=Int32.MaxValue;chosen=palette[0];foreach(var v in palette){int d=(c.R-v.R)*(c.R-v.R)+(c.G-v.G)*(c.G-v.G)+(c.B-v.B)*(c.B-v.B);if(d<best){best=d;chosen=v;}}cache[key]=chosen;}n.SetPixel(x,y,chosen);}
 }
 static void Bust(string who,int x,int y){using(var a=Load("Docs/References/codex_k071_v1/"+who+"_neutral_64x96.png",64,96,true)){g.DrawImage(a,new Rectangle(x,y,32,44),new Rectangle(18,0,32,44),GraphicsUnit.Pixel);}}
 static void Title(string s,int x,int y){
  using(var m=new Bitmap(220,40))using(var z=Graphics.FromImage(m))using(var f=new Font("Malgun Gothic",22,FontStyle.Bold,GraphicsUnit.Pixel)){
   z.TextRenderingHint=TextRenderingHint.SingleBitPerPixelGridFit;z.DrawString(s,f,Brushes.White,0,0,StringFormat.GenericTypographic);
   for(int yy=0;yy<39;yy++)for(int xx=0;xx<219;xx++)if(m.GetPixel(xx,yy).A>127)Rect(x+xx-1,y+yy-1,3,4,C("#100e13"));
   double min=100;for(int yy=1;yy<39;yy++)for(int xx=1;xx<219;xx++)if(m.GetPixel(xx,yy).A>127){Color c=m.GetPixel(xx,yy-1).A<128?C("#faebc9"):m.GetPixel(xx,yy+1).A<128?C("#c6a251"):C("#e4c481");double v=Lum(b.GetPixel(x+xx,y+yy)),l=Lum(c);min=Math.Min(min,(Math.Max(v,l)+.05)/(Math.Min(v,l)+.05));b.SetPixel(x+xx,y+yy,c);}contrast.Add(s+"\t"+min.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"\t제목 양각");
  }
 }
 static Color C(string s){return ColorTranslator.FromHtml(s);}
 static void Rect(int x,int y,int w,int h,Color c){using(var q=new SolidBrush(c))g.FillRectangle(q,x,y,w,h);}
 static Bitmap Load(string p,int w,int h,bool quant){
  using(var a=new Bitmap(Path.Combine(root,p))){var n=new Bitmap(w,h);using(var z=Graphics.FromImage(n)){z.InterpolationMode=InterpolationMode.NearestNeighbor;z.PixelOffsetMode=PixelOffsetMode.Half;z.DrawImage(a,new Rectangle(0,0,w,h));}
   if(quant) Reduce(n);return n;}
 }
 static void Start(string p){b=Load(p,640,360,true);g=Graphics.FromImage(b);g.SmoothingMode=SmoothingMode.None;g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;}
 static double Lum(Color c){double[] a={c.R/255.0,c.G/255.0,c.B/255.0};for(int i=0;i<3;i++)a[i]=a[i]<=.04045?a[i]/12.92:Math.Pow((a[i]+.055)/1.055,2.4);return a[0]*.2126+a[1]*.7152+a[2]*.0722;}
 static void Text(string s,int x,int y,int size,Color c){
  using(var mask=new Bitmap(600,60))using(var z=Graphics.FromImage(mask))using(var f=new Font("Malgun Gothic",size,FontStyle.Bold,GraphicsUnit.Pixel)){
   z.Clear(Color.Black);z.TextRenderingHint=TextRenderingHint.SingleBitPerPixelGridFit;z.DrawString(s,f,Brushes.White,0,0,StringFormat.GenericTypographic);
   double min=100; int count=0;
   for(int yy=0;yy<60;yy++)for(int xx=0;xx<600;xx++)if(mask.GetPixel(xx,yy).R>127&&x+xx<640&&y+yy<360){var bg=b.GetPixel(x+xx,y+yy);double l=Lum(c),v=Lum(bg);min=Math.Min(min,(Math.Max(l,v)+.05)/(Math.Min(l,v)+.05));b.SetPixel(x+xx,y+yy,c);count++;}
   contrast.Add(s.Replace("\"","")+"\t"+min.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"\t"+count);
  }
 }
 static void Image(string p,int x,int y,int w,int h){using(var a=Load(p,w,h,true))g.DrawImageUnscaled(a,x,y);}
 static void Panel(int x,int y,int w,int h,bool paper){
  Rect(x,y,w,h,C("#211a13"));Rect(x+1,y+1,w-2,h-2,C(paper?"#8e6c33":"#6d5133"));
  for(int yy=y+3;yy<y+h-3;yy++)for(int xx=x+3;xx<x+w-3;xx++){
   int tx=(xx-x)%24,ty=(yy-y)%24;Color c;
   if(paper){c=C("#c9aa62");if((tx*13+ty*7)%41==0)c=C("#bea057");if((tx*3+ty*11)%53==0)c=C("#d2b570");int dx=xx-x,dy=yy-y;if((dx-25)*(dx-25)+(dy-h+9)*(dy-h+9)<1200||(dx-w+25)*(dx-w+25)+(dy-10)*(dy-10)<800)c=C("#b99b53");if(xx==x+w/2||yy==y+h/2)c=C("#b99b53");if((xx-x<8||xx-x>w-9)||(yy-y<7||yy-y>h-8))c=C("#b2944e");}
   else{c=C("#35251c");if(ty==0||ty==1)c=C("#2b1d17");else if((tx*5+ty)%29==0)c=C("#483024");}
   b.SetPixel(xx,yy,c);
  }
  foreach(int xx in new int[]{x+2,x+w-4})foreach(int yy in new int[]{y+2,y+h-4})Rect(xx,yy,2,2,C(paper?"#9e803d":"#b1945c"));
 }
 static void Brass(string text,int x,int y,int w,int h){Rect(x,y,w,h,C("#291d15"));Rect(x+1,y+1,w-2,h-2,C("#725327"));Rect(x+2,y+2,w-4,h-4,C("#c6a251"));Rect(x+3,y+3,w-6,2,C("#e3c477"));Rect(x+3,y+h-5,w-6,2,C("#ab8740"));Text(text,x+(w-text.Length*15)/2,y+5,15,ink);}
 static void Save(string name){g.Dispose();using(var full=new Bitmap(1920,1080))using(var z=Graphics.FromImage(full)){z.InterpolationMode=InterpolationMode.NearestNeighbor;z.PixelOffsetMode=PixelOffsetMode.Half;z.DrawImage(b,new Rectangle(0,0,1920,1080));full.Save(Path.Combine(dest,name+".png"),ImageFormat.Png);}b.Save(Path.Combine(dest,name+"_native.png"));using(var crop=b.Clone(new Rectangle(name.Contains("story")?424:name.Contains("result")?100:28,name.Contains("story")?150:name.Contains("result")?70:60,160,120),PixelFormat.Format32bppArgb))using(var p=new Bitmap(640,480))using(var z=Graphics.FromImage(p)){z.InterpolationMode=InterpolationMode.NearestNeighbor;z.PixelOffsetMode=PixelOffsetMode.Half;z.DrawImage(crop,new Rectangle(0,0,640,480));p.Save(Path.Combine(dest,name+"_detail_4x.png"));}b.Dispose();}
 static void Story(bool paper){Start("Game/Assets/_Project/Art/UI/Resources/ScreenArt/main.png");
  Panel(490,12,52,20,false);Text("자동",502,15,11,light);Panel(550,12,66,20,false);Text("넘기기",564,15,11,light);
  Image("Docs/References/codex_k071_v1/archer_neutral_64x96.png",440,157,64,96);
  Panel(32,250,576,94,paper);Panel(48,235,78,23,false);Text("궁수",69,237,15,light);
  Color c=paper?ink:light;
  Text("장교님! 정말 돌아오셨군요.",52,268,11,c);
  Text("북쪽 들판에 놈들이 보입니다!",52,285,11,c);
  using(var brush=new SolidBrush(c))g.FillPolygon(brush,new Point[]{new Point(588,327),new Point(596,327),new Point(592,331)});
  Save(paper?"after_story_paper":"after_story_wood");
 }
 static void Bar(int x,int y,int w,int filled){Rect(x,y,w,5,ink);Rect(x+1,y+1,w-2,3,C("#875537"));Rect(x+1,y+1,filled,3,C("#59753b"));}
 static void Result(){Start("Game/Assets/_Project/Art/UI/Resources/ScreenArt/victory.png");Panel(84,29,472,302,true);
  Text("전투 1 보고",107,43,15,ink);
  // 봉랍은 기존 화면의 약 두 배 지름으로 찍는다.
  var pts=new Point[24];for(int i=0;i<24;i++){double a=i*Math.PI/12;double r=i%2==0?32:29;pts[i]=new Point(142+(int)(r*Math.Cos(a)),103+(int)(r*Math.Sin(a)));}
  using(var q=new SolidBrush(C("#30462b")))g.FillPolygon(q,pts);using(var p=new Pen(C("#a0ac65"),2))g.DrawEllipse(p,116,77,52,52);
  Text("승리",120,89,22,light);Text("잃은 병사 −1",190,78,22,ink);Text("처치 120     획득 골드 +60",192,111,11,ink);
  Text("성벽",107,145,15,ink);Text("병력",295,145,15,ink);
  // 색과 함께 방향·현재 수치를 적어 색만으로 구분하지 않는다.
  Text("북 950/1000",112,171,11,ink);Rect(128,193,94,9,C("#996333"));
  Rect(128,204,9,53,C("#59753b"));Rect(213,204,9,53,C("#59753b"));Rect(139,259,72,9,C("#59753b"));
  Text("서",107,216,11,ink);Text("동",230,216,11,ink);Text("1000",96,233,11,ink);Text("1000",227,233,11,ink);Text("남 1000/1000",118,274,11,ink);
  string[] names={"대포 운용병 31/32  −1","궁수 160/160","총병 60/60"};string[] icons={"training_cannon_crew_1.png","merc_archer.png","merc_gunner.png"};
  for(int i=0;i<3;i++){int yy=170+i*35;Image("Docs/References/codex_k067_v1/"+icons[i],285,yy-3,32,32);Text(names[i],320,yy,11,ink);Bar(320,yy+20,209,i==0?200:207);}
  Bust("hero",285,273);Bust("wizard",396,273);
  Text("주인공",320,276,11,ink);Text("100/100",320,294,11,ink);Text("마법사",433,276,11,ink);Text("60/60",433,294,11,ink);
  Panel(459,336,157,22,false);Text("보고 마치기",504,340,11,light);Save("after_result");
 }
 public static void Run(string r,string d){root=r;dest=d;
  Start("Docs/References/codex_k083_v1/main_menu_keyart.png");
  // 한글 제목을 읽을 수 있도록 황동색 활자를 격자 위에 조판한다.
  Title("인류의",32,66);Title("마지막 성벽",32,96);
  Brass("출정",32,160,164,34);Text("이어하기",45,210,11,light);Text("설정",45,238,11,light);Text("종료",45,266,11,light);Save("after_main");Story(true);Story(false);Result();
  File.WriteAllLines(Path.Combine(dest,"contrast.tsv"),contrast);
 }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
[Mockup084]::Run($root,$PSScriptRoot)
