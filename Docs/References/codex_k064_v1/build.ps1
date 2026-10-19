$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
# 생성 원안은 보존하고, 도트 규격과 기존 시트 팔레트만 정규화한다.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Linq;
public static class K064Pixels {
 public static Rectangle Bounds(Bitmap b, Rectangle r) {
  int l=r.Right,t=r.Bottom,rr=r.Left,bb=r.Top;
  for(int y=r.Top;y<r.Bottom;y++) for(int x=r.Left;x<r.Right;x++) if(b.GetPixel(x,y).A>=128){l=Math.Min(l,x);t=Math.Min(t,y);rr=Math.Max(rr,x+1);bb=Math.Max(bb,y+1);}
  if(rr<=l||bb<=t) throw new Exception("불투명 인물이 없음"); return Rectangle.FromLTRB(l,t,rr,bb);
 }
 public static Color[] Palette(string source,string targets) {
  using(var b=new Bitmap(source)) {
   var colors=new HashSet<int>();
   for(int y=0;y<Math.Min(384,b.Height);y++) for(int x=0;x<Math.Min(256,b.Width);x++){var c=b.GetPixel(x,y);if(c.A>=128)colors.Add(Color.FromArgb(255,c.R,c.G,c.B).ToArgb());}
   return targets.Split(',').Select(s=>ColorTranslator.FromHtml("#"+s)).Select(c=>Color.FromArgb(colors.OrderBy(a=>Dist(c,Color.FromArgb(a))).First())).Distinct().ToArray();
  }
 }
 static int Dist(Color a,Color b) {int r=a.R-b.R,g=a.G-b.G,bl=a.B-b.B;return 2*r*r+3*g*g+2*bl*bl;}
 public static string Make(string source,int half,int cw,int ch,int foot,int height,string output,Color[] palette,int sourceCell) {
  using(var b=new Bitmap(source)) {
   int split=b.Width/2; if(sourceCell==0) { int best=int.MaxValue; for(int x=b.Width*3/10;x<b.Width*7/10;x++){int count=0;for(int y=0;y<b.Height;y++)if(b.GetPixel(x,y).A>=128)count++; int score=count*b.Width+Math.Abs(x-b.Width/2);if(score<best){best=score;split=x;}} } Rectangle region=sourceCell>0?new Rectangle(0,0,Math.Min(sourceCell,b.Width),Math.Min(384,b.Height)):(half==0?new Rectangle(0,0,split,b.Height):new Rectangle(split,0,b.Width-split,b.Height));
   var box=Bounds(b,region); int h=height,w=(int)Math.Round((double)box.Width*h/box.Height);
   if(w>cw-4) throw new Exception("칸 폭을 초과함: "+output+" "+w);
   int left=(cw-w)/2,top=foot-h;
   using(var dest=new Bitmap(cw,ch,PixelFormat.Format32bppArgb)) {
    for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
     int sx=box.Left+Math.Min(box.Width-1,(int)((x+0.5)*box.Width/w)),sy=box.Top+Math.Min(box.Height-1,(int)((y+0.5)*box.Height/h));
     var c=b.GetPixel(sx,sy); if(c.A<128)continue;
     c=Color.FromArgb(255,c.R,c.G,c.B); if(palette!=null)c=palette.OrderBy(p=>Dist(c,p)).First();dest.SetPixel(left+x,top+y,c);
    }
    dest.Save(output,ImageFormat.Png);
    using(var big=new Bitmap(cw*4,ch*4,PixelFormat.Format32bppArgb)) {
     for(int y=0;y<ch;y++)for(int x=0;x<cw;x++){var c=dest.GetPixel(x,y);for(int j=0;j<4;j++)for(int i=0;i<4;i++)big.SetPixel(x*4+i,y*4+j,c);}
     big.Save(Path.Combine(Path.GetDirectoryName(output),Path.GetFileNameWithoutExtension(output)+"_4x.png"),ImageFormat.Png);
    }
   }
   return String.Format("{0},{1},{2},{3};{4},{5};{6},{7}",box.X,box.Y,box.Width,box.Height,w,h,left,top);
  }
 }
 public static string Check(string file) {
  using(var b=new Bitmap(file)) {var colors=new HashSet<int>(); int semi=0;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,y);if(c.A!=0&&c.A!=255)semi++;if(c.A==255)colors.Add(c.ToArgb());}var box=Bounds(b,new Rectangle(0,0,b.Width,b.Height));return String.Format("{0}: {1}×{2}, 반투명 {3}, 색 {4}, 경계 {5},{6},{7},{8}",Path.GetFileName(file),b.Width,b.Height,semi,colors.Count,box.X,box.Y,box.Width,box.Height);}
 }
}
'@
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$art = Join-Path $root 'Game/Assets/_Project/Art/Characters'
$skinGoblin='12100C,252914,41451D,646C2C,8A923D,A9AF5B,241C18,3A2C26,584236,7A5A43,302014,51331E,795132,A67B4A,6D491F,B68A3E,E0B54D,E2D6AA'
$skinLadder='100A09,252321,494444,6A6261,363D16,596A23,819332,A7B34D,332016,58331E,87502D,AF7544,6B5437,9F8158,C9AF82,E2D1A6,B88E45,E8C860,934D36,C8805D'
$skinOgre='100D09,393A29,5E6045,7D7D5C,A19773,B7AE8B,302017,553120,7F4930,A66D47,49291E,68402B,986043,C18B5A,4B4943,77766C,ABA89A,DDD1AF,6A251B,A5412B'
$configs=@(
 @{id='goblin';label='곤봉 고블린';input='goblin_selected.png';source='Goblin/goblin_walk_attack4.png';targets=$skinGoblin;cw=56;ch=112;foot=104;height=96},
 @{id='ladder_empty';label='사다리 고블린 · 빈손';input='ladder_empty_corrected.png';source='Static/ladder_goblin_static.png';targets=$skinLadder;cw=56;ch=112;foot=104;height=96},
 @{id='ladder_carry';label='사다리 고블린 · 운반';input='ladder_carry_generated.png';source='Static/ladder_goblin_static.png';targets=$skinLadder;cw=56;ch=112;foot=104;height=98},
 @{id='ogre';label='오우거';input='ogre_generated.png';source='Static/ogre_static.png';targets=$skinOgre;cw=112;ch=160;foot=152;height=128}
)
$manifest=@();$palettes=@{}
foreach($c in $configs){
 $palette=[K064Pixels]::Palette((Join-Path $art $c.source),$c.targets)
 $palettes[$c.id]=@{source=('Game/Assets/_Project/Art/Characters/'+$c.source);colors=@($palette|ForEach-Object {'#{0:X2}{1:X2}{2:X2}' -f $_.R,$_.G,$_.B})}
 foreach($view in @('front','side')){
  $half=if($view -eq 'front'){0}else{1};$file=$c.id+'_'+$view+'.png'
  $conversion=[K064Pixels]::Make((Join-Path $PSScriptRoot $c.input),$half,$c.cw,$c.ch,$c.foot,$c.height,(Join-Path $PSScriptRoot $file),$palette,0)
  $manifest+=@{file=$file;label=$c.label;view=$view;canvas=@($c.cw,$c.ch);foot=@(($c.cw/2),$c.foot);conversion=$conversion;sha256=(Get-FileHash (Join-Path $PSScriptRoot $file)).Hash}
 }
 # 빈손 사다리병은 별도 기존 원화가 없으므로 이전 운반 원화로 표시한다.
 [K064Pixels]::Make((Join-Path $art $c.source),0,160,$c.ch,$c.foot,$c.height,(Join-Path $PSScriptRoot ($c.id+'_before.png')),$null,256)|Out-Null
}
[K064Pixels]::Make((Join-Path $art 'Static/archer_static.png'),0,112,112,104,96,(Join-Path $PSScriptRoot 'soldier_reference.png'),$null,256)|Out-Null
Copy-Item -LiteralPath (Join-Path $root 'Docs/References/codex_hero_octopath_review_v6/hero_idle_56x112.png') -Destination (Join-Path $PSScriptRoot 'hero_v6_reference.png')
$palettes|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'palettes.json')
$manifest|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'manifest.json')
$checks=Get-ChildItem $PSScriptRoot -Filter '*.png'|Where-Object {$_.Name -match '_(front|side)(?:_4x)?\.png$'}|ForEach-Object {[K064Pixels]::Check($_.FullName)}
$checks|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'validation.txt')
$checks
