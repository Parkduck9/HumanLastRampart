$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class K059Ui {
 static string dir;
 static Color C(string s){return ColorTranslator.FromHtml(s);}
 static Color ink=C("#3a2a1e"), edge=C("#150e09"), paper=C("#e8d9b5"), shade=C("#c9b48a"), wood=C("#3b2a1f"), grain=C("#5e4430"), dark=C("#23170f"), gold=C("#d5b16b"), gleam=C("#f5d99c"), tarnish=C("#8a6a32"), rust=C("#b4533c");
 static Bitmap New(int w,int h){return new Bitmap(w,h,PixelFormat.Format32bppArgb);}
 static void P(Bitmap b,int x,int y,Color c){if(x>=0&&y>=0&&x<b.Width&&y<b.Height)b.SetPixel(x,y,c);}
 static void R(Bitmap b,int x,int y,int w,int h,Color c){for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)P(b,xx,yy,c);}
 // 깎은 귀는 같은 두 점 계단을 따른다.
 static bool Inside(int x,int y,int w,int h,int n){return x>=n&&y>=n&&x<w-n&&y<h-n&&((x>=n+2&&x<w-n-2)||(y>=n+2&&y<h-n-2));}
 static void Save(Bitmap b,string n){b.Save(Path.Combine(dir,n+".png"),ImageFormat.Png);b.Dispose();}
 static void Rivet(Bitmap b,int x,int y,bool brass){R(b,x-1,y-1,3,3,edge);P(b,x,y,brass?gold:shade);P(b,x,y-1,brass?gleam:paper);}
 static Bitmap Frame(int w,int h,string type,bool down){
  Bitmap b=New(w,h);bool pap=type=="paper"||type=="bubble"||type=="slip"||type=="danger";bool brass=type=="brass"||type=="band";bool bubble=type=="bubble";
  for(int y=0;y<h;y++)for(int x=0;x<w;x++){
   if(!Inside(x,y,w,h,0))continue;
   Color c=pap?paper:wood;
   if(pap){ // 가운데는 낮은 대비의 끊긴 종이 섬유, 끝 두 줄은 비워 반복 경계를 숨긴다.
    if(!bubble&&((y==11&&(x==12||x==13))||(y==23&&x==22)))c=shade;
   }else{
    if((y%5==2&&x%13>2&&x%13<10)||(y%9==4&&x%17==8))c=grain;
    if(y%10==7)c=dark;
   }
   if(type=="brass"){c=gold;if(y%7==3&&x%9<3)c=tarnish;}
   if(!Inside(x,y,w,h,1))c=pap?ink:edge;
   else if(!Inside(x,y,w,h,2))c=(y<2||x<2)?(pap?shade:brass?gleam:grain):(pap?shade:dark);
   if(bubble){
    if(y==2+(x%11==6?1:0)&&x>3&&x<w-4)c=ink;
    if(y==h-3-(x%13==4?1:0)&&x>3&&x<w-4)c=ink;
    if(x==2+(y%9==5?1:0)&&y>3&&y<h-4)c=ink;
    if(x==w-3-(y%11==6?1:0)&&y>3&&y<h-4)c=ink;
    if(y==4&&x>5&&x<w-6)c=paper;
    if(y==h-5&&x>5&&x<w-6)c=shade;
   }
   if(brass&&type=="band"&&!Inside(x,y,w,h,3)&&Inside(x,y,w,h,1))c=(x<3||y<3)?gleam:tarnish;
   P(b,x,y,c);
  }
  if(bubble){P(b,5,5,ink);P(b,6,5,shade);P(b,w-6,5,ink);P(b,w-7,6,shade);P(b,5,h-6,ink);P(b,6,h-6,shade);P(b,w-6,h-6,ink);P(b,w-7,h-7,shade);}
  if(type=="wood"||type=="band"){Rivet(b,4,4,brass);Rivet(b,w-5,4,brass);Rivet(b,4,h-5,brass);Rivet(b,w-5,h-5,brass);}
  if(type=="plate"){P(b,3,h/2,edge);P(b,w-4,h/2,edge);P(b,3,h/2-1,gold);P(b,w-4,h/2-1,gold);}
  if(type=="slip"||type=="danger"){
   for(int y=0;y<5;y++)for(int x=w-5;x<w;x++){if(x-(w-5)>y)P(b,x,y,Color.Transparent);else P(b,x,y,x-(w-5)==y?ink:shade);}
   if(type=="danger")R(b,2,3,3,h-6,rust);
  }
  if(type=="button"||type=="brass"){
   R(b,3,2,w-6,1,down?dark:(brass?gleam:grain));
   R(b,3,h-4,w-6,2,down?(brass?tarnish:wood):dark);
   if(down)R(b,2,2,w-4,2,brass?tarnish:dark);
  }
  return b;
 }
 static void Poly(Bitmap b,Color c,params int[] a){using(Graphics g=Graphics.FromImage(b))using(SolidBrush s=new SolidBrush(c)){Point[] p=new Point[a.Length/2];for(int i=0;i<p.Length;i++)p[i]=new Point(a[i*2],a[i*2+1]);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.None;g.FillPolygon(s,p);}}
 static void Outline(Bitmap b){Bitmap a=(Bitmap)b.Clone();for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(a.GetPixel(x,y).A==0){bool hit=false;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(Math.Abs(dx)+Math.Abs(dy)==1&&x+dx>=0&&x+dx<b.Width&&y+dy>=0&&y+dy<b.Height&&a.GetPixel(x+dx,y+dy).A>0)hit=true;if(hit)P(b,x,y,edge);}a.Dispose();}
 static Bitmap Icon(string n){
  int w=n=="speed1"?6:n=="speed2"?12:n=="speed4"?15:11;Bitmap b=New(w,11);Color c=Color.White;
  if(n=="close"){for(int i=2;i<9;i++){P(b,i,i,c);P(b,10-i,i,c);}}
  if(n=="up"||n=="down"){for(int i=0;i<4;i++){int y=n=="up"?3+i:7-i;R(b,5-i,y,1,1,c);R(b,5+i,y,1,1,c);}}
  if(n=="play"||n=="right")Poly(b,c,3,2,7,5,3,8);
  if(n=="pause"){R(b,2,2,2,7,c);R(b,7,2,2,7,c);}
  if(n.StartsWith("speed")){int count=n=="speed1"?1:n=="speed2"?2:3;for(int i=0;i<count;i++)Poly(b,c,1+i*(count==2?6:5),2,(count==3?3:4)+i*(count==2?6:5),5,1+i*(count==2?6:5),8);}
  if(n=="hero"){R(b,3,2,5,2,c);R(b,2,4,7,1,c);R(b,4,5,3,2,c);R(b,2,8,7,1,c);P(b,1,7,c);P(b,9,7,c);}
  if(n=="north")Poly(b,c,5,1,8,8,5,6,2,8);
  if(n=="check"){Poly(b,c,1,5,3,7,8,2,9,3,3,9,1,7);}
  if(n=="flag"){R(b,2,1,1,9,c);Poly(b,c,3,2,8,2,6,4,8,5,3,5);}
  if(n=="auto"){for(int y=2;y<9;y++)for(int x=2;x<9;x++){int d=(x-5)*(x-5)+(y-5)*(y-5);if(d>=7&&d<=13)P(b,x,y,c);}P(b,5,5,c);}
  if(n=="idle"||n=="minus")R(b,2,5,7,1,c);
  if(n=="plus"){R(b,2,5,7,1,c);R(b,5,2,1,7,c);}
  if(n=="special"){Poly(b,c,2,2,4,3,6,3,8,2,8,7,5,9,2,7);P(b,4,5,Color.Transparent);P(b,6,5,Color.Transparent);}
  if(n=="warn"){Poly(b,c,5,1,9,8,1,8);R(b,5,3,1,3,Color.Transparent);P(b,5,7,Color.Transparent);}
  Outline(b);return b;
 }
 public static void Build(string folder){
  dir=folder;Directory.CreateDirectory(dir);
  Save(Frame(32,32,"paper",false),"panel_parchment");Save(Frame(32,32,"wood",false),"panel_wood");Save(Frame(32,32,"band",false),"panel_band");
  Save(Frame(16,16,"plate",false),"plate");Save(Frame(20,14,"slip",false),"slip");Save(Frame(20,14,"danger",false),"slip_danger");Save(Frame(32,32,"bubble",false),"bubble");
  Bitmap tail=New(12,9);Poly(tail,ink,0,0,11,0,2,8);Poly(tail,paper,2,0,8,0,3,5);Save(tail,"bubble_tail");
  Save(Frame(12,12,"button",false),"button_wood");Save(Frame(12,12,"button",true),"button_wood_down");Save(Frame(12,12,"brass",false),"button_brass");Save(Frame(12,12,"brass",true),"button_brass_down");
  Bitmap glow=New(12,12);for(int y=0;y<12;y++)for(int x=0;x<12;x++){if(Inside(x,y,12,12,0)&&!Inside(x,y,12,12,1))P(glow,x,y,gleam);else if(Inside(x,y,12,12,1)&&!Inside(x,y,12,12,2))P(glow,x,y,Color.FromArgb(110,gold));}Save(glow,"frame_glow");
  Bitmap key=Frame(10,11,"paper",false);R(key,3,8,4,2,shade);Save(key,"keycap");
  Bitmap track=Frame(8,8,"button",true);R(track,3,3,2,2,dark);Save(track,"bar_track");
  foreach(string n in new[]{"close","up","down","play","pause","speed1","speed2","speed4","hero","north","check","flag","auto","idle","special","minus","plus","right","warn"})Save(Icon(n),"icon_"+n);
  Bitmap seal=New(32,32);for(int y=0;y<32;y++)for(int x=0;x<32;x++){double d=Math.Sqrt((x-15.5)*(x-15.5)+(y-15.5)*(y-15.5));if((d>11&&d<14)||(d>9&&d<10))P(seal,x,y,Color.FromArgb(((x*7+y*13)%17==0)?110:255,255,255,255));}Save(seal,"seal");
  Bitmap v=New(64,36);for(int y=0;y<36;y++)for(int x=0;x<64;x++){double dx=Math.Abs((x-31.5)/31.5),dy=Math.Abs((y-17.5)/17.5);double a=Math.Pow(Math.Max(0,(Math.Max(dx,dy)-0.45)/0.55),2)*225;P(v,x,y,Color.FromArgb((int)a,255,255,255));}Save(v,"vignette");
 }
 public static void Story(string folder){
  dir=folder;Save(Frame(96,40,"bubble",false),"story_box");Bitmap name=Frame(48,18,"button",false);Rivet(name,5,8,true);Rivet(name,42,8,true);Save(name,"story_nameplate");
  for(int i=0;i<4;i++){Bitmap b=New(7,5);int y=i==1||i==2?1:0;Poly(b,ink,1,y,5,y,3,y+3);P(b,3,y,gold);Save(b,"story_next_"+i);}
  Save(Frame(40,16,"button",false),"story_button");Bitmap hover=Frame(40,16,"button",false);for(int x=3;x<37;x++)P(hover,x,1,gleam);Save(hover,"story_button_hover");
  Bitmap on=Frame(40,16,"button",false);for(int y=0;y<16;y++)for(int x=0;x<40;x++)if(Inside(x,y,40,16,1)&&!Inside(x,y,40,16,3))P(on,x,y,x<3||y<3?gleam:gold);Save(on,"story_button_on");
  Bitmap fade=New(64,64);for(int y=0;y<64;y++)for(int x=0;x<64;x++){double d=Math.Max(Math.Abs(x-31.5),Math.Abs(y-31.5));int a=(int)Math.Max(0,Math.Min(255,(d-17)*18+((x*3+y*7)%11)*3));P(fade,x,y,Color.FromArgb(a,ink));}Save(fade,"story_fade_edge");
 }
}
'@
[K059Ui]::Build($PSScriptRoot)
Write-Output 'UI 키트 36종 제작 완료'
if ($args -contains '-Story') { [K059Ui]::Story($PSScriptRoot); Write-Output '스토리 부품 10종 제작 완료' }
