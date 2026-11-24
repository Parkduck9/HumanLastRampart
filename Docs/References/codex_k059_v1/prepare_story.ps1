$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
public static class K059Story {
 // 최근접 표본으로만 규격을 맞춘다. 색 변경이나 자동 배경 제거는 하지 않는다.
 public static void Build(string dir){
  foreach(string n in new[]{"hero","wizard","archer"})using(Bitmap source=new Bitmap(Path.Combine(dir,"source_"+n+".png")))using(Bitmap sheet=new Bitmap(1024,384,PixelFormat.Format32bppArgb)){
   int cw=source.Width/4;
   for(int f=0;f<4;f++)for(int y=0;y<192;y++)for(int x=0;x<128;x++)sheet.SetPixel(f*256+64+x,160+y,source.GetPixel(f*cw+(int)((x+0.5)*cw/128),(int)((y+0.5)*source.Height/192)));
   sheet.Save(Path.Combine(dir,"story_portrait_"+n+".png"),ImageFormat.Png);
  }
  foreach(string n in new[]{"dusk","night"})using(Bitmap source=new Bitmap(Path.Combine(dir,"source_bg_"+n+".png")))using(Bitmap b=new Bitmap(640,360,PixelFormat.Format32bppArgb)){
   for(int y=0;y<360;y++)for(int x=0;x<640;x++)b.SetPixel(x,y,source.GetPixel((int)((x+0.5)*source.Width/640),(int)((y+0.5)*source.Height/360)));
   b.Save(Path.Combine(dir,"story_bg_rampart_"+n+".png"),ImageFormat.Png);
  }
 }
}
'@
[K059Story]::Build($PSScriptRoot)
Write-Output '초상 3종과 배경 2종 규격 저장 완료'
