$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = (Resolve-Path "$PSScriptRoot/../../..").Path
$source = Get-Content "$root/Docs/References/codex_k093_v1/build.ps1" -Raw -Encoding UTF8
$baseCode = $source.Substring($source.IndexOf("using System;"))
$baseCode = $baseCode.Substring(0,$baseCode.IndexOf("'@"))
$code = @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class Kit97 {
 static Color I=TutorialArt.Ink, B=TutorialArt.Brass, L=TutorialArt.Light, P=TutorialArt.Pale, D=TutorialArt.Dark, S=TutorialArt.Steel, G=TutorialArt.Green;
 static Bitmap N(int w,int h){return TutorialArt.New(w,h);}
 static void R(Bitmap b,int x,int y,int w,int h,Color c){TutorialArt.Rect(b,x,y,w,h,c);}
 static void Ln(Bitmap b,Color c,int width,params int[] xy){TutorialArt.Line(b,c,width,xy);}
 static void Poly(Bitmap b,Color c,params int[] xy){TutorialArt.Poly(b,c,xy);}
 static void Paste(Bitmap b,Bitmap s,int x,int y){TutorialArt.Paste(b,s,x,y);}
 static void Save(Bitmap b,string id){TutorialArt.Asset(b,id);}
 public static Bitmap Panel(int w,int h,bool paper){
  Bitmap b=N(w,h); Color fill=paper?TutorialArt.Paper:D;
  Poly(b,I,4,0,w-5,0,w-1,4,w-1,h-5,w-5,h-1,4,h-1,0,h-5,0,4);
  R(b,2,4,w-4,h-8,TutorialArt.Rust); R(b,4,2,w-8,h-4,TutorialArt.Rust);
  R(b,4,5,w-8,h-10,fill);R(b,5,4,w-10,h-8,fill);
  Ln(b,paper?P:L,1,6,5,w-7,5);Ln(b,paper?TutorialArt.Rust:B,1,6,h-6,w-7,h-6);
  return b;
 }
 static Dictionary<char,string> font=new Dictionary<char,string>{{'S',"111100111001111"},{'H',"101101111101101"},{'I',"111010010010111"},{'F',"111100110100100"},{'T',"111010010010010"},{'E',"111100110100111"},{'C',"111100100100111"}};
 static void Txt(Bitmap b,string s,int cx,int y){int x=cx-(s.Length*4-1)/2; foreach(char c in s){for(int yy=0;yy<5;yy++)for(int xx=0;xx<3;xx++)if(font[c][yy*3+xx]=='1')R(b,x+xx,y+yy,1,1,P);x+=4;}}
 static Bitmap ShiftDrag(int size){
  Bitmap b=N(size,size);R(b,1,3,size-2,size-6,I);R(b,3,1,size-6,size-2,I);R(b,3,3,size-6,size-6,D);Ln(b,B,1,4,3,size-5,3);Ln(b,TutorialArt.Rust,1,4,size-4,size-5,size-4);
  int cx=size/2,y=size==24?11:15;Txt(b,"SHIFT",cx,size==24?5:6);
  R(b,cx-6,y,7,8,I);R(b,cx-5,y+1,5,6,S);R(b,cx-5,y+1,2,3,L);R(b,cx-3,y,1,4,I);
  Ln(b,L,1,cx+2,y+4,cx+7,y+4);Poly(b,L,cx+7,y+2,cx+9,y+4,cx+7,y+6);
  return b;
 }
 static Bitmap Chip(string root,int size,int k){
  if(k==0)return ShiftDrag(size);
  Bitmap b=N(size,size);
  if(k<4){int[] idx={0,1,3,5};using(Bitmap old=new Bitmap(Path.Combine(root,"Docs/References/codex_k093_v1/input_chips_"+size+"_native.png"))) {using(Bitmap cut=old.Clone(new Rectangle(idx[k]*size,0,size,size),PixelFormat.Format32bppArgb))Paste(b,cut,0,0);}}
  else {R(b,1,3,size-2,size-6,I);R(b,3,1,size-6,size-2,I);R(b,3,3,size-6,size-6,D);Ln(b,B,1,4,3,size-5,3);Ln(b,TutorialArt.Rust,1,4,size-4,size-5,size-4);Txt(b,"ESC",size/2,size/2-2);}
  return b;
 }
 public static Bitmap Icon(int k){Bitmap b=N(24,24);
  if(k==0){Poly(b,I,6,4,12,4,13,12,20,15,20,20,10,20,7,15,3,16,2,12,6,10);Poly(b,B,7,5,11,5,11,13,18,16,18,18,11,18,8,13,4,14,4,13,8,11);Ln(b,L,1,8,6,8,10);}
  if(k==1||k==7){Poly(b,I,3,13,14,5,20,12,8,20,3,19);Poly(b,B,5,13,14,7,18,12,8,18,5,17);Ln(b,L,2,7,13,14,9);Ln(b,I,2,14,6,18,12);Ln(b,S,1,16,7,19,11);Ln(b,I,2,11,17,14,21);if(k==7){Poly(b,I,18,1,23,6,21,6,21,10,16,10,16,6,14,6);Poly(b,L,18,3,21,6,20,6,20,9,17,9,17,6,16,6);}}
  if(k==2){Poly(b,I,2,4,8,2,15,5,21,3,22,19,15,22,8,19,2,21);Poly(b,P,4,6,8,4,15,7,19,5,20,17,15,20,8,17,4,19);Ln(b,B,1,8,5,8,16,15,19,15,8);Ln(b,TutorialArt.Rust,1,5,12,10,10,13,14,17,10);}
  if(k==3){for(int j=0;j<3;j++){int x=3+j*7,y=j==1?4:7;R(b,x,y,5,6,I);R(b,x+1,y+1,3,3,P);R(b,x,y+5,5,8,I);R(b,x+1,y+6,3,5,j==1?B:S);}}
  if(k==4){Ln(b,I,7,5,13,17,7);Ln(b,B,4,5,13,17,7);Ln(b,L,1,5,12,16,6);R(b,16,3,4,8,I);R(b,17,4,2,5,S);Ln(b,I,3,8,14,14,18);for(int j=0;j<2;j++){R(b,4+j*10,15,7,7,I);R(b,6+j*10,17,3,3,B);}}
  if(k==5){Poly(b,I,13,1,10,8,5,11,4,17,8,22,16,22,21,16,18,9);Poly(b,B,13,5,12,10,8,12,7,17,10,20,15,20,18,16,16,11);Poly(b,P,13,10,11,15,13,18,15,16);}
  if(k==6){R(b,3,15,18,5,I);R(b,4,16,16,2,B);for(int j=0;j<4;j++){Poly(b,I,3+j*5,15,5+j*5,5,8+j*5,15);Ln(b,S,1,5+j*5,8,5+j*5,14);}Ln(b,L,1,4,17,19,17);}
  return b;
 }
 static Bitmap Pip(int k,int state){Bitmap b=N(32,32);Color rim=state==0?S:state==1?L:G;
  Poly(b,I,5,0,26,0,31,5,31,26,26,31,5,31,0,26,0,5);Poly(b,rim,5,2,26,2,29,5,29,26,26,29,5,29,2,26,2,5);R(b,4,4,24,24,D);
  using(Bitmap icon=Icon(k)){if(state==0){for(int y=0;y<24;y++)for(int x=0;x<24;x++){Color c=icon.GetPixel(x,y);if(c.A>0&&c.ToArgb()!=I.ToArgb())icon.SetPixel(x,y,S);}}Paste(b,icon,4,4);}
  if(state==1){R(b,13,0,6,2,P);R(b,13,30,6,2,P);}
  if(state==2){R(b,21,21,10,10,I);Ln(b,P,2,23,25,25,27,29,23);}
  return b;
 }
 public static void Build(string root,string output){TutorialArt.Out=output;
  using(Bitmap p=Panel(96,48,true))Save(p,"instruction_box_9slice");using(Bitmap p=Panel(48,24,false))Save(p,"hint_chip_9slice");
  using(Bitmap old=new Bitmap(Path.Combine(root,"Docs/References/codex_k093_v1/highlight_ring_native.png")))Save(old,"highlight_ring_pulse");
  string[] dirs={"up","right","down","left"};for(int d=0;d<4;d++){int dir=d;using(Bitmap s=TutorialArt.Sheet(32,32,4,i=>TutorialArt.Arrow(new int[]{0,2,3,2}[i],dir)))Save(s,"pointer_bounce_"+dirs[d]);}
  foreach(int size in new int[]{24,32})using(Bitmap s=TutorialArt.Sheet(size,size,5,k=>Chip(root,size,k)))Save(s,"input_chips_"+size);
  using(Bitmap s=TutorialArt.Sheet(24,24,8,Icon))Save(s,"step_icons_24");
  using(Bitmap s=TutorialArt.Sheet(32,32,24,k=>Pip(k/3,k%3)))Save(s,"step_pips");
  using(Bitmap s=TutorialArt.Sheet(256,32,9,stage=>{Bitmap strip=N(256,32);for(int k=0;k<8;k++)using(Bitmap p=Pip(k,k<stage?2:k==stage?1:0))Paste(strip,p,k*32,0);return strip;}))Save(s,"step_progress_strip");
 }
 public static void Busts(string input){
  using(Bitmap src=new Bitmap(input))using(Bitmap dst=N(192,48)){
   // 네 칸 전체에서 같은 축척을 사용하고 흉상 밑선을 고정한다.
   int cw=src.Width/4;Rectangle[] box=new Rectangle[4];double scale=1000;
   for(int i=0;i<4;i++)using(Bitmap cell=src.Clone(new Rectangle(i*cw,0,cw,src.Height),PixelFormat.Format32bppArgb)){box[i]=TutorialArt.AlphaBounds(cell);scale=Math.Min(scale,Math.Min(44.0/box[i].Width,44.0/box[i].Height));}
   for(int i=0;i<4;i++){Rectangle q=box[i];int w=(int)Math.Round(q.Width*scale),h=(int)Math.Round(q.Height*scale);int headL=cw,headR=0;
    for(int y=q.Y;y<q.Y+q.Height*0.55;y++)for(int x=0;x<cw;x++)if(src.GetPixel(i*cw+x,y).A>=128){headL=Math.Min(headL,x);headR=Math.Max(headR,x);}
    int ox=24-(int)Math.Round(((headL+headR)/2.0-q.X)*scale),oy=46-h;
    for(int y=0;y<h;y++)for(int x=0;x<w;x++){int sx=i*cw+q.X+Math.Min(q.Width-1,(int)(x/scale)),sy=q.Y+Math.Min(q.Height-1,(int)(y/scale));Color c=src.GetPixel(sx,sy);if(c.A>=128)dst.SetPixel(i*48+ox+x,oy+y,Color.FromArgb(255,(c.R/17)*17,(c.G/17)*17,(c.B/17)*17));}}
   TutorialArt.Clean(dst);Save(dst,"officer_expressions");
  }
 }
}
'@
Add-Type -TypeDefinition ($baseCode + $code.Substring($code.IndexOf('public static class Kit97'))) -ReferencedAssemblies System.Drawing
[Kit97]::Build($root,$PSScriptRoot)
if(Test-Path "$PSScriptRoot/officer_expressions_generated.png") {[Kit97]::Busts("$PSScriptRoot/officer_expressions_generated.png")}
