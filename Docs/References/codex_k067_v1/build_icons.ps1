$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class K067Icons {
 static Color C(string s) { return ColorTranslator.FromHtml(s); }
 static Color edge=C("#150e09"), ink=C("#3a2a1e"), paper=C("#e8d9b5"), shade=C("#c9b48a"), gold=C("#d5b16b"), light=C("#f5d99c"), tarnish=C("#8a6a32"), wood=C("#5e4430"), rust=C("#b4533c"), fire=C("#ff9a3c"), blast=C("#ffd75a");
 static Bitmap New() { return new Bitmap(32,32,PixelFormat.Format32bppArgb); }
 static void P(Bitmap b,int x,int y,Color c) { if(x>=0&&y>=0&&x<32&&y<32)b.SetPixel(x,y,c); }
 static void R(Bitmap b,int x,int y,int w,int h,Color c) { for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)P(b,xx,yy,c); }
 static void L(Bitmap b,int x,int y,int xx,int yy,Color c) {
  int dx=Math.Abs(xx-x),sx=x<xx?1:-1,dy=-Math.Abs(yy-y),sy=y<yy?1:-1,e=dx+dy;
  while(true){P(b,x,y,c);if(x==xx&&y==yy)break;int e2=2*e;if(e2>=dy){e+=dy;x+=sx;}if(e2<=dx){e+=dx;y+=sy;}}
 }
 static void Poly(Bitmap b,Color c,params int[] coords) {
  using(Graphics g=Graphics.FromImage(b))using(SolidBrush brush=new SolidBrush(c)){
   Point[] pts=new Point[coords.Length/2];for(int i=0;i<pts.Length;i++)pts[i]=new Point(coords[i*2],coords[i*2+1]);
   g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.None;g.FillPolygon(brush,pts);
  }
 }
 static void Disc(Bitmap b,int cx,int cy,int radius,Color c) { for(int y=-radius;y<=radius;y++)for(int x=-radius;x<=radius;x++)if(x*x+y*y<=radius*radius)P(b,cx+x,cy+y,c); }
 static bool Body(Color c) { return c.A>0&&c.ToArgb()!=edge.ToArgb(); }
 static void Outline(Bitmap b) {
  using(Bitmap src=(Bitmap)b.Clone())for(int y=1;y<31;y++)for(int x=1;x<31;x++)if(src.GetPixel(x,y).A==0&&(Body(src.GetPixel(x-1,y))||Body(src.GetPixel(x+1,y))||Body(src.GetPixel(x,y-1))||Body(src.GetPixel(x,y+1))))P(b,x,y,edge);
 }
 static void Coin(Bitmap b,int x,int y,int radius) {
  Disc(b,x,y,radius,edge);Disc(b,x,y,radius-1,tarnish);Disc(b,x,y-1,radius-2,gold);
  L(b,x-1,y-radius+2,x+1,y-radius+2,light);L(b,x,y-1,x,y+1,light);P(b,x+1,y,gold);
 }
 static void Flame(Bitmap b,int x,int y) {
  Poly(b,rust,x,y+9,x-4,y+6,x-3,y+2,x-1,y+3,x+1,y-3,x+3,y+2,x+4,y+6,x+2,y+9);
  Poly(b,fire,x-2,y+6,x+1,y+1,x+2,y+6,x+1,y+8,x-1,y+8);L(b,x,y+5,x,y+7,blast);
 }
 static void Bow(Bitmap b) {
  L(b,10,4,16,6,gold);L(b,16,6,20,11,gold);L(b,20,11,20,15,gold);L(b,20,15,16,20,gold);L(b,16,20,10,22,gold);
  L(b,10,5,15,7,light);L(b,15,7,18,11,light);L(b,18,15,15,19,tarnish);L(b,15,19,10,21,tarnish);
  L(b,10,4,10,22,paper);R(b,18,11,2,5,wood);L(b,5,13,26,13,light);
  Poly(b,paper,23,10,28,13,23,16);L(b,23,13,26,13,gold);L(b,5,11,8,13,rust);L(b,5,15,8,13,rust);
 }
 static void Musket(Bitmap b) {
  Poly(b,wood,5,19,9,15,14,16,10,22,6,24,4,22);L(b,6,20,8,18,gold);L(b,6,22,10,20,tarnish);
  Poly(b,shade,10,15,22,3,25,6,13,18);L(b,11,15,22,4,paper);L(b,14,17,25,6,gold);
  L(b,21,5,24,8,ink);L(b,18,8,21,11,ink);R(b,13,14,3,3,tarnish);P(b,15,14,light);
  L(b,12,18,15,20,gold);L(b,15,20,17,18,gold);P(b,16,17,gold);R(b,23,3,2,2,ink);
 }
 static void Cannon(Bitmap b) {
  Poly(b,tarnish,7,10,24,5,27,12,10,17);L(b,8,10,24,6,light);L(b,9,11,24,7,gold);L(b,10,16,26,12,wood);
  L(b,22,6,25,13,ink);L(b,24,5,27,12,paper);L(b,25,7,26,10,ink);
  Poly(b,wood,5,19,10,14,15,15,20,20,26,20,26,22,6,22);L(b,7,19,11,16,gold);
  Disc(b,12,21,4,ink);Disc(b,12,21,3,gold);Disc(b,12,21,1,wood);P(b,11,19,light);L(b,23,19,27,21,gold);
 }
 static void Ranks(Bitmap b,int count) {
  int start=16-(count*6-2)/2;
  for(int i=0;i<count;i++){int x=start+i*6;R(b,x-1,26,5,5,edge);R(b,x,27,3,3,gold);R(b,x,27,3,1,light);P(b,x+2,29,tarnish);}
 }
 public static Bitmap Draw(string n) {
  Bitmap b=New();
  if(n.StartsWith("training_")||n.StartsWith("merc_")){
   if(n.Contains("archer"))Bow(b);else if(n.Contains("gunner"))Musket(b);else Cannon(b);
   if(n.StartsWith("training_"))Ranks(b,int.Parse(n.Substring(n.Length-1)));else Coin(b,24,24,5);
  }
  switch(n){
   case "item_command_badge":
    Poly(b,rust,9,17,15,18,12,29,9,26,6,27);Poly(b,rust,17,18,23,17,26,27,22,26,20,29);
    Poly(b,tarnish,16,3,20,8,26,9,23,15,24,21,17,23,10,21,9,15,6,9,12,8);
    Poly(b,gold,16,5,19,10,24,10,21,14,22,19,17,21,12,19,11,14,8,10,13,10);
    L(b,11,11,15,7,light);Poly(b,paper,16,9,18,13,21,14,18,16,17,19,14,16,12,14,15,13);break;
   case "item_ember_charm":
    L(b,12,4,20,4,tarnish);L(b,12,4,10,9,gold);L(b,20,4,22,9,gold);
    Poly(b,wood,10,10,22,10,25,24,16,28,7,24);Poly(b,gold,11,11,21,11,23,23,16,26,9,23);
    Poly(b,ink,12,13,20,13,21,22,16,24,11,22);Flame(b,16,15);R(b,14,9,4,2,paper);break;
   case "item_fire_arrows":
    for(int i=0;i<3;i++){int x=8+i*7,y=9+(i==1?-3:0);L(b,x,y+3,x,27-(i==1?3:0),tarnish);L(b,x+1,y+3,x+1,26-(i==1?3:0),light);Flame(b,x,y);Poly(b,paper,x-2,24,x,22,x+2,24,x+2,28,x,26,x-2,28);}
    R(b,5,19,21,3,wood);L(b,6,19,25,19,gold);R(b,14,18,3,5,gold);break;
   case "item_gem_staff":
    Poly(b,tarnish,8,28,5,26,20,9,23,12);L(b,7,26,21,11,gold);L(b,9,25,22,11,light);
    Poly(b,gold,18,15,15,9,19,3,26,4,29,10,24,16);Poly(b,rust,19,5,25,5,27,10,23,13,18,10);
    Poly(b,fire,19,5,23,6,22,11,18,10);L(b,19,5,24,5,light);L(b,23,7,25,9,blast);break;
   case "item_hourglass_charm":
    R(b,7,5,18,3,gold);R(b,7,25,18,3,tarnish);R(b,8,25,16,1,light);R(b,8,5,16,1,light);
    L(b,9,8,9,24,tarnish);L(b,22,8,22,24,tarnish);
    Poly(b,paper,11,9,20,9,19,13,16,16,19,19,20,23,11,23,12,19,15,16,12,13);
    Poly(b,gold,12,11,19,11,16,15);P(b,16,17,gold);Poly(b,gold,12,22,16,18,19,22);R(b,13,23,6,1,tarnish);break;
   case "item_loud_whistle":
    L(b,11,10,11,6,tarnish);L(b,11,6,15,3,gold);L(b,15,3,19,6,gold);L(b,19,6,19,10,tarnish);
    Poly(b,gold,6,12,17,10,22,13,22,20,17,24,9,22,6,18);Poly(b,tarnish,8,18,16,21,21,18,20,22,17,24,9,22);
    R(b,8,12,9,2,light);R(b,10,15,5,4,ink);R(b,11,15,3,1,paper);R(b,22,13,6,5,gold);R(b,23,13,4,1,light);R(b,27,14,1,3,ink);break;
   case "item_messenger_horn":
    Poly(b,tarnish,5,11,11,13,14,18,20,18,24,10,28,8,28,24,24,23,20,25,12,24,8,19);
    Poly(b,gold,6,12,10,14,13,20,19,21,24,17,24,21,19,24,13,23,10,21);
    Poly(b,gold,23,11,27,9,27,23,23,21);L(b,24,11,24,21,light);L(b,27,11,27,21,ink);
    R(b,4,10,4,3,paper);L(b,12,21,13,23,light);L(b,15,22,19,22,light);break;
   case "item_powder_keg":
    Poly(b,wood,9,8,23,8,26,13,26,24,23,28,9,28,6,24,6,13);R(b,10,10,12,16,tarnish);
    L(b,10,12,10,25,gold);L(b,16,11,16,26,wood);L(b,22,12,22,25,wood);
    R(b,7,12,18,3,shade);R(b,7,13,18,1,paper);R(b,7,22,18,3,shade);R(b,7,22,18,1,paper);
    R(b,10,7,12,3,gold);R(b,12,8,8,1,wood);L(b,16,7,18,3,shade);P(b,19,3,fire);P(b,20,2,blast);P(b,21,4,fire);break;
   case "item_shield_rail":
    R(b,4,7,3,21,wood);R(b,25,7,3,21,wood);R(b,3,10,26,3,gold);R(b,3,23,26,3,gold);L(b,4,10,27,10,light);
    Poly(b,tarnish,9,8,23,8,22,21,16,27,10,21);Poly(b,gold,10,9,22,9,21,20,16,25,11,20);
    Poly(b,paper,12,11,20,11,19,19,16,22,13,19);L(b,16,11,16,22,ink);R(b,13,15,7,2,ink);break;
   case "item_surgeon_tent":
    Poly(b,shade,3,26,7,12,16,5,25,12,29,26);Poly(b,paper,6,24,9,13,16,7,23,13,26,24);
    L(b,16,5,16,3,gold);L(b,5,25,27,25,wood);Poly(b,wood,11,24,16,16,21,24);L(b,16,17,16,24,ink);
    R(b,14,9,4,8,rust);R(b,12,11,8,4,rust);L(b,9,15,6,23,light);L(b,23,15,26,23,shade);break;
   case "item_telescope_lens":
    Disc(b,16,16,12,tarnish);Disc(b,16,16,10,gold);Disc(b,16,16,8,ink);Disc(b,16,16,7,shade);
    Poly(b,paper,11,11,17,9,22,14,21,18,17,21,11,19);L(b,11,14,15,10,light);L(b,11,16,17,10,light);
    L(b,10,8,15,5,light);L(b,22,23,25,19,wood);R(b,19,17,2,3,paper);break;
   case "mark_gold_coin": Coin(b,16,16,11);L(b,14,12,14,19,light);L(b,18,12,18,19,tarnish);break;
   case "mark_wage":
    Coin(b,15,13,8);L(b,6,22,9,26,paper);L(b,9,26,23,26,paper);L(b,23,26,27,22,paper);L(b,27,22,27,16,paper);
    Poly(b,gold,23,17,27,12,30,17);L(b,9,24,22,24,tarnish);break;
   case "badge_one_battle":
    Poly(b,gold,9,5,23,5,27,10,27,23,22,28,10,28,5,23,5,10);Poly(b,rust,10,7,22,7,25,11,25,22,21,26,11,26,7,22,7,11);
    Poly(b,paper,10,19,20,9,23,8,22,12,12,22);Poly(b,gold,9,18,14,23,13,25,7,19);
    R(b,19,18,2,6,light);P(b,18,19,light);R(b,18,24,4,1,light);break;
   case "badge_per_wall":
    R(b,5,9,5,7,gold);R(b,13,9,6,7,gold);R(b,22,9,5,7,gold);R(b,5,15,22,12,shade);R(b,6,16,20,2,paper);
    L(b,6,21,26,21,ink);L(b,11,18,11,20,ink);L(b,20,18,20,20,ink);L(b,15,22,15,26,ink);
    L(b,8,24,11,24,gold);L(b,21,24,24,24,gold);break;
  }
  Outline(b);return b;
 }
 public static Bitmap Scale(Bitmap b,int factor) {
  Bitmap scaled=new Bitmap(b.Width*factor,b.Height*factor,PixelFormat.Format32bppArgb);
  for(int y=0;y<scaled.Height;y++)for(int x=0;x<scaled.Width;x++)scaled.SetPixel(x,y,b.GetPixel(x/factor,y/factor));return scaled;
 }
 public static void Build(string folder,string[] names) {
  foreach(string n in names)using(Bitmap b=Draw(n)){
   b.Save(Path.Combine(folder,n+".png"),ImageFormat.Png);
   foreach(int f in new[]{2,4})using(Bitmap s=Scale(b,f))s.Save(Path.Combine(folder,"preview_"+f+"x",n+".png"),ImageFormat.Png);
  }
 }
 public static void Sheet(string folder,string[] names,string[] labels) {
  int cols=5,cw=320,ch=222,head=84,rows=(names.Length+cols-1)/cols;
  using(Bitmap sheet=new Bitmap(cols*cw,head+rows*ch))using(Graphics g=Graphics.FromImage(sheet))using(Font title=new Font("Malgun Gothic",22,FontStyle.Bold))using(Font font=new Font("Malgun Gothic",12))using(Font small=new Font("Malgun Gothic",9)){
   g.Clear(C("#23170f"));g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   using(Brush text=new SolidBrush(paper)){g.DrawString("강화 · 훈련 · 용병 / 아이콘 1판",title,text,20,12);g.DrawString("원본 32×32 · 아래 그림은 4배 확대 · 왼쪽 양피지 / 오른쪽 짙은 나무",font,text,22,50);}
   for(int i=0;i<names.Length;i++){
    int x=(i%cols)*cw,y=head+(i/cols)*ch;
    using(Brush p=new SolidBrush(paper))g.FillRectangle(p,x+8,y+8,148,140);
    using(Brush p=new SolidBrush(C("#3b2a1f")))g.FillRectangle(p,x+164,y+8,148,140);
    using(Bitmap b=new Bitmap(Path.Combine(folder,names[i]+".png")))using(Bitmap s=Scale(b,4)){g.DrawImageUnscaled(s,x+18,y+14);g.DrawImageUnscaled(s,x+174,y+14);}
    using(Brush p=new SolidBrush(paper)){g.DrawString(labels[i],font,p,x+10,y+155);g.DrawString(names[i],small,p,x+10,y+184);}
   }
   sheet.Save(Path.Combine(folder,"contact_sheet.png"),ImageFormat.Png);
  }
 }
}
'@
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
foreach ($scale in 2,4) { [IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot "preview_${scale}x")) | Out-Null }
$entries = @()
foreach ($group in @(@('Items','상점'),@('Training','훈련'),@('Mercenaries','용병'))) {
    foreach ($asset in Get-ChildItem (Join-Path $root "Game/Assets/_Project/Data/Meta/$($group[0])") -Filter *.asset | Sort-Object Name) {
        $raw = Get-Content -Raw -Encoding UTF8 $asset.FullName
        $entry = [ordered]@{ id=$asset.BaseName; name=([regex]::Match($raw,'(?m)^  displayName: (.+)\r?$').Groups[1].Value.Trim()); group=$group[1] }
        foreach ($key in 'description','price','contractCost','wage','consumable','perWall') {
            $match=[regex]::Match($raw,"(?m)^  ${key}: (.+)\r?$")
            if ($match.Success) { $entry[$key]=$match.Groups[1].Value.Trim() }
        }
        $entries += [pscustomobject]$entry
    }
}
foreach ($mark in @(@('mark_gold_coin','골드','구매·고용 비용'),@('mark_wage','급료','전투마다 되풀이되는 급료'),@('badge_one_battle','한 전투만','불화살 꾸러미·화약 통의 소모 표시'),@('badge_per_wall','성벽별','방패 난간의 성벽별 구매 표시'))) {
    $entries += [pscustomobject]@{id=$mark[0];name=$mark[1];group='공용 표시';description=$mark[2]}
}
[K067Icons]::Build($PSScriptRoot,[string[]]$entries.id)
[K067Icons]::Sheet($PSScriptRoot,[string[]]$entries.id,[string[]]$entries.name)
$entries | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'manifest.json')
Write-Output "아이콘 $($entries.Count)종과 2배·4배 확대본, 전체 비교표 제작 완료"
