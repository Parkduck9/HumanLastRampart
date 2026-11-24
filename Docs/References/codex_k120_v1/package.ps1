$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
public static class K120 {
 public static Bitmap Resize(Bitmap src,int w,int h) {
  Bitmap b=new Bitmap(w,h,PixelFormat.Format32bppArgb);
  for(int y=0;y<h;y++)for(int x=0;x<w;x++)b.SetPixel(x,y,src.GetPixel(x*src.Width/w,y*src.Height/h));
  return b;
 }
 public static void Make(string source,string root) {
  using(Bitmap s=new Bitmap(source))using(Bitmap crop=s.Clone(new Rectangle(78,75,2016,567),PixelFormat.Format32bppArgb))using(Bitmap n=Resize(crop,1280,360)) {
   n.Save(root+"/prep_yard_native.png",ImageFormat.Png);
   using(Bitmap g=Resize(n,3840,1080))g.Save(root+"/prep_yard.png",ImageFormat.Png);
   using(Bitmap p=Resize(n,5120,1440))p.Save(root+"/prep_yard_preview4x.png",ImageFormat.Png);
  }
 }
 public static string Check(string root) {
  using(Bitmap n=new Bitmap(root+"/prep_yard_native.png"))using(Bitmap g=new Bitmap(root+"/prep_yard.png"))using(Bitmap p=new Bitmap(root+"/prep_yard_preview4x.png")) {
   long alpha=0,magenta=0,mismatch3=0,mismatch4=0;
   for(int y=0;y<g.Height;y++)for(int x=0;x<g.Width;x++){
    Color c=g.GetPixel(x,y); if(c.A!=255)alpha++; if(c.R>220&&c.G<40&&c.B>220)magenta++;
    if(c.ToArgb()!=n.GetPixel(x/3,y/3).ToArgb())mismatch3++;
   }
   for(int y=0;y<p.Height;y++)for(int x=0;x<p.Width;x++)if(p.GetPixel(x,y).ToArgb()!=n.GetPixel(x/4,y/4).ToArgb())mismatch4++;
   if(alpha+magenta+mismatch3+mismatch4!=0)throw new Exception("화상 검증 실패");
   return "{\"opaque\":true,\"nonopaque_pixels\":"+alpha+",\"padding_magenta_pixels\":"+magenta+",\"nearest_3x_mismatches\":"+mismatch3+",\"nearest_4x_mismatches\":"+mismatch4+"}";
  }
 }
}
'@
$root = $PSScriptRoot
New-Item -ItemType Directory -Force -Path "$root/sources" | Out-Null
$source = "$root/sources/generated_padded.png"
if (!(Test-Path -LiteralPath $source)) {
 Copy-Item -LiteralPath 'C:/Users/User/.codex/generated_images/01a166fc-b42f-7041-8580-2907682c5606/exec-11afe50c-a453-4937-ae10-e44d79a21456.png' -Destination $source
}
[K120]::Make($source,$root)
[K120]::Check($root) | Set-Content -Encoding UTF8 -LiteralPath "$root/pixel_verification.json"
Get-Content -LiteralPath "$root/pixel_verification.json"
