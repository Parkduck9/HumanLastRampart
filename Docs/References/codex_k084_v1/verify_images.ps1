$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public class Check084 {
 public static string Check(string p){
  using(var b=new Bitmap(p)){if(b.Width!=1920||b.Height!=1080)throw new Exception("크기 불일치");
   var data=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
   byte[] a=new byte[data.Stride*b.Height];Marshal.Copy(data.Scan0,a,0,a.Length);int transparent=0,mismatch=0;
   for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){int n=y*data.Stride+x*4,src=(y/3*3)*data.Stride+(x/3*3)*4;if(a[n+3]!=255)transparent++;for(int c=0;c<4;c++)if(a[n+c]!=a[src+c]){mismatch++;break;}}
   b.UnlockBits(data);if(transparent!=0||mismatch!=0)throw new Exception("알파 또는 격자 불일치");return "{\"size\":[1920,1080],\"alpha\":[255,255],\"non_uniform_3px_blocks\":"+mismatch+",\"transparent_pixels\":"+transparent+"}";
  }
 }
}
'@
$r=[ordered]@{}
Get-ChildItem $PSScriptRoot -Filter 'after_*.png' | Where-Object {$_.Name -notmatch 'native|detail'} | ForEach-Object {$r[$_.Name]=([Check084]::Check($_.FullName)|ConvertFrom-Json)}
$contrast=Get-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'contrast.tsv') | ForEach-Object {$a=$_ -split "`t";[double]::Parse($a[1],[Globalization.CultureInfo]::InvariantCulture)}
$minimum=($contrast|Measure-Object -Minimum).Minimum
if($minimum -lt 4.5){throw "글 대비 기준 미달: $minimum"}
$r['minimum_text_contrast']=$minimum
$r['static_mockups_only']=$true
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'image_validation.json'),($r|ConvertTo-Json -Depth 5),(New-Object Text.UTF8Encoding($true)))
$r | ConvertTo-Json -Depth 5
