$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Collections.Generic;
public class K082Report {
 public int PartialAlpha,OpaquePixels,TransparentPixels,GridErrors,SmallIslands,Colors;
 public int[][] Bounds;
}
public static class K082Verify {
 public static K082Report Inspect(Bitmap sheet,Bitmap native,int scale,int frames) {
  var r=new K082Report();var colors=new HashSet<int>();
  for(int y=0;y<sheet.Height;y++)for(int x=0;x<sheet.Width;x++){
   Color c=sheet.GetPixel(x,y);if(c.A==0)r.TransparentPixels++;else if(c.A==255){r.OpaquePixels++;colors.Add(c.ToArgb());}else r.PartialAlpha++;
   if(c.ToArgb()!=native.GetPixel(x/scale,y/scale).ToArgb())r.GridErrors++;
  }r.Colors=colors.Count;r.Bounds=new int[frames][];
  int cw=sheet.Width/frames;
  for(int f=0;f<frames;f++){
   int l=cw,t=sheet.Height,rr=-1,b=-1;
   for(int y=0;y<sheet.Height;y++)for(int x=0;x<cw;x++)if(sheet.GetPixel(f*cw+x,y).A>0){l=Math.Min(l,x);t=Math.Min(t,y);rr=Math.Max(rr,x);b=Math.Max(b,y);}
   r.Bounds[f]=new[]{l,t,rr+1,b+1};
  }
  bool[,] seen=new bool[native.Width,native.Height];
  for(int y=0;y<native.Height;y++)for(int x=0;x<native.Width;x++){
   if(seen[x,y]||native.GetPixel(x,y).A==0)continue;
   int n=0;var q=new Queue<Point>();q.Enqueue(new Point(x,y));seen[x,y]=true;
   while(q.Count>0){Point p=q.Dequeue();n++;for(int yy=Math.Max(0,p.Y-1);yy<=Math.Min(native.Height-1,p.Y+1);yy++)for(int xx=Math.Max(0,p.X-1);xx<=Math.Min(native.Width-1,p.X+1);xx++)if(!seen[xx,yy]&&native.GetPixel(xx,yy).A>0){seen[xx,yy]=true;q.Enqueue(new Point(xx,yy));}}
   if(n<=2)r.SmallIslands++;
  }return r;
 }
}
'@
$out=Split-Path $PSScriptRoot -Parent
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$manifest=Get-Content (Join-Path $out 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$records=@()
foreach($a in $manifest.assets){
 $b=[System.Drawing.Bitmap]::new((Join-Path $out $a.file));$n=[System.Drawing.Bitmap]::new((Join-Path $out ('native/'+$a.id+'.png')))
 $old=[System.Drawing.Bitmap]::new((Join-Path $root $a.source))
 $scale=if($a.frame_count -eq 4){4}else{8}
 $r=[K082Verify]::Inspect($b,$n,$scale,$a.frame_count)
 $expectedBottom=if($a.frame_count -eq 4){352}else{480}
 $bottomPass=@($r.Bounds | Where-Object { $_[3] -ne $expectedBottom }).Count -eq 0
 $preview=[System.Drawing.Bitmap]::new((Join-Path $out $a.preview))
 $pass=$b.Width -eq $old.Width -and $b.Height -eq $old.Height -and $r.PartialAlpha -eq 0 -and $r.OpaquePixels -gt 0 -and $r.TransparentPixels -gt 0 -and $r.GridErrors -eq 0 -and $r.SmallIslands -eq 0 -and $bottomPass -and $r.Colors -le 33 -and $preview.Width -eq $b.Width*4 -and $preview.Height -eq $b.Height*4
 $records+=@{file=$a.file;pass=$pass;size=@($b.Width,$b.Height);same_size_as_source=($b.Width -eq $old.Width -and $b.Height -eq $old.Height);alpha_partial_pixels=$r.PartialAlpha;opaque_pixels=$r.OpaquePixels;transparent_pixels=$r.TransparentPixels;nearest_neighbor_grid_errors=$r.GridErrors;native_islands_le_2_pixels=$r.SmallIslands;opaque_color_count=$r.Colors;frame_bounds_exclusive=$r.Bounds;baseline_pass=$bottomPass;preview_size=@($preview.Width,$preview.Height)}
 $preview.Dispose();$old.Dispose();$n.Dispose();$b.Dispose()
}
$hashes=Get-Content (Join-Path $PSScriptRoot 'source_hashes.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$changed=@();foreach($h in $hashes.PSObject.Properties){if((Get-FileHash -LiteralPath (Join-Path $root $h.Name) -Algorithm SHA256).Hash.ToLower() -ne $h.Value){$changed+=$h.Name}}
$allPass=@($records | Where-Object {!$_.pass}).Count -eq 0 -and $records.Count -eq 11 -and $changed.Count -eq 0
@{pass=$allPass;assets=$records;read_only_files_checked=@($hashes.PSObject.Properties).Count;read_only_files_changed=$changed;browser_render_verified=$false;unity_run=$false} | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $out 'validation.json') -Encoding UTF8
if(!$allPass){throw '인물 에셋 검사 실패: validation.json 확인'}
Write-Output ('11종 PNG 규격·알파·발선·격자·고립 픽셀 검사 통과, 읽기 전용 원본 '+@($hashes.PSObject.Properties).Count+'개 유지')
