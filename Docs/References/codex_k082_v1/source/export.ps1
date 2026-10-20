$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class K082Pixels {
  public static Rectangle Bounds(Bitmap b, int start, int width, int alpha) {
    int l=width,t=b.Height,r=-1,d=-1;
    for(int y=0;y<b.Height;y++) for(int x=0;x<width;x++) if(b.GetPixel(start+x,y).A>=alpha){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);d=Math.Max(d,y);}
    if(r<l) throw new Exception("투명 영역만 존재합니다");
    return new Rectangle(l,t,r-l+1,d-t+1);
  }
  public static Bitmap Sample(Bitmap src,Rectangle crop,int w,int h) {
    Bitmap b=new Bitmap(w,h,PixelFormat.Format32bppArgb);
    for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
      Color c=src.GetPixel(crop.X+Math.Min(crop.Width-1,(int)((x+0.5)*crop.Width/w)),crop.Y+Math.Min(crop.Height-1,(int)((y+0.5)*crop.Height/h)));
      if(c.A>=224)b.SetPixel(x,y,Color.FromArgb(255,c.R,c.G,c.B));
    }
    return b;
  }
  public static int Clean(Bitmap b) {
    bool[,] seen=new bool[b.Width,b.Height];int removed=0;
    for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++) {
      if(seen[x,y]||b.GetPixel(x,y).A==0)continue;
      var points=new List<Point>();var q=new Queue<Point>();q.Enqueue(new Point(x,y));seen[x,y]=true;
      while(q.Count>0){Point p=q.Dequeue();points.Add(p);for(int yy=Math.Max(0,p.Y-1);yy<=Math.Min(b.Height-1,p.Y+1);yy++)for(int xx=Math.Max(0,p.X-1);xx<=Math.Min(b.Width-1,p.X+1);xx++)if(!seen[xx,yy]&&b.GetPixel(xx,yy).A>0){seen[xx,yy]=true;q.Enqueue(new Point(xx,yy));}}
      if(points.Count<=2)foreach(Point p in points){b.SetPixel(p.X,p.Y,Color.Transparent);removed++;}
    }return removed;
  }
  public static void Palette(Bitmap b) {
    string[] colors={"171219","9b5635","db9155","ffc686","fff1ce","49291f","75432b","ad6b3c","d39558","4e2d23","8b4e31","bd7447","111b2b","26364b","40516b","9e907a","d5c7a4","434650","898890","d8d8d5","71451c","b38028","f3c34d","706c79","b8b2b5","f2ece0","142a53","28699c","79cbe9","ffffff","261537","502b73","8152a3"};
    Color[] palette=Array.ConvertAll(colors,s=>ColorTranslator.FromHtml("#"+s));
    Bitmap copy=(Bitmap)b.Clone();
    for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++) {
      Color c=copy.GetPixel(x,y);if(c.A==0)continue;
      bool edge=x==0||y==0||x==b.Width-1||y==b.Height-1;
      if(!edge)edge=copy.GetPixel(x-1,y).A==0||copy.GetPixel(x+1,y).A==0||copy.GetPixel(x,y-1).A==0||copy.GetPixel(x,y+1).A==0;
      if(edge){b.SetPixel(x,y,palette[0]);continue;}
      int best=int.MaxValue;Color chosen=palette[0];
      foreach(Color p in palette){int d=(c.R-p.R)*(c.R-p.R)+(c.G-p.G)*(c.G-p.G)+(c.B-p.B)*(c.B-p.B);if(d<best){best=d;chosen=p;}}
      b.SetPixel(x,y,chosen);
    }copy.Dispose();
  }
  public static Bitmap Enlarge(Bitmap b,int n) {
    Bitmap dst=new Bitmap(b.Width*n,b.Height*n,PixelFormat.Format32bppArgb);
    for(int y=0;y<dst.Height;y++)for(int x=0;x<dst.Width;x++)dst.SetPixel(x,y,b.GetPixel(x/n,y/n));
    return dst;
  }
  public static void Put(Bitmap dst,Bitmap src,int x,int y) {
    for(int yy=0;yy<src.Height;yy++)for(int xx=0;xx<src.Width;xx++)if(xx+x>=0&&xx+x<dst.Width&&yy+y>=0&&yy+y<dst.Height)dst.SetPixel(xx+x,yy+y,src.GetPixel(xx,yy));
  }
  public static int NavyCenter(Bitmap b, Rectangle box, int offset) {
    int l=b.Width,r=-1;
    for(int y=box.Top+box.Height/3;y<box.Top+box.Height*3/4;y++)for(int x=box.Left;x<box.Right;x++){
      Color c=b.GetPixel(offset+x,y);if(c.A>=224&&c.B>c.R*1.25&&c.B>c.G*1.02&&c.R<85){l=Math.Min(l,x);r=Math.Max(r,x);}}
    return r>=l?(l+r+1)/2:box.Left+box.Width/2;
  }
}
'@
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$out = Split-Path $PSScriptRoot -Parent
$paths = Get-Content (Join-Path $PSScriptRoot 'generated_paths.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$records = @()
foreach($folder in @('originals','result','TrapMap','native','previews')) { New-Item -ItemType Directory -Force (Join-Path $out $folder) | Out-Null }
foreach($item in $paths.PSObject.Properties) {
  $id=$item.Name
  $original=Join-Path $out ('originals/'+$id+'.png')
  if(!(Test-Path -LiteralPath $original)){Copy-Item -LiteralPath $item.Value -Destination $original}
  $src=[System.Drawing.Bitmap]::new($original)
  $worker=$id.StartsWith('worker_')
  $oldPath=if($worker){'Game/Assets/_Project/Art/UI/Resources/TrapMap/'+$id+'.png'}else{'Docs/References/codex_k028_v1/result/codex_'+$id+'.png'}
  $old=[System.Drawing.Bitmap]::new((Join-Path $root $oldPath))
  $frames=if($worker){4}else{1}
  $cw=if($worker){64}else{64};$ch=if($worker){96}else{64};$factor=if($worker){4}else{8}
  $native=[System.Drawing.Bitmap]::new(($cw*$frames),$ch)
  $frameRecords=@()
  for($f=0;$f -lt $frames;$f++) {
    $sw=[int]($src.Width/$frames)
    $box=[K082Pixels]::Bounds($src,($f*$sw),$sw,224)
    $oldCw=[int]($old.Width/$frames)
    $oldBox=[K082Pixels]::Bounds($old,($f*$oldCw),$oldCw,128)
    $h=if($worker){[int][Math]::Round($oldBox.Height/4.0)}else{55}
    $w=[int][Math]::Round($box.Width*$h/[double]$box.Height)
    if($w -gt 62){$h=[int][Math]::Round($h*62.0/$w);$w=62}
    $crop=[System.Drawing.Rectangle]::new(($box.X+$f*$sw),$box.Y,$box.Width,$box.Height)
    $small=[K082Pixels]::Sample($src,$crop,$w,$h)
    $removed=[K082Pixels]::Clean($small)
    [K082Pixels]::Palette($small)
    $anchor=if($worker){[int][Math]::Round([K082Pixels]::NavyCenter($old,$oldBox,($f*$oldCw))/4.0)}else{32}
    $center=if($worker){[K082Pixels]::NavyCenter($small,([System.Drawing.Rectangle]::new(0,0,$w,$h)),0)}else{[int]($w/2)}
    $dx=$anchor-$center
    $dx=[Math]::Max(1,[Math]::Min((63-$w),$dx))
    $bottom=if($worker){88}else{60}
    $cleanBox=[K082Pixels]::Bounds($small,0,$small.Width,224)
    $dy=$bottom-$cleanBox.Bottom
    [K082Pixels]::Put($native,$small,($f*$cw+$dx),$dy)
    $frameRecords+=@{index=$f;source_bounds=@($box.X,$box.Y,$box.Width,$box.Height);old_bounds=@($oldBox.X,$oldBox.Y,$oldBox.Width,$oldBox.Height);placed_bounds=@(($dx*$factor),($dy*$factor),($w*$factor),($h*$factor));anchor_top_left_px=@(($anchor*$factor),($bottom*$factor));removed_isolated_native_pixels=$removed}
    $small.Dispose()
  }
  $native.Save((Join-Path $out ('native/'+$id+'.png')),[System.Drawing.Imaging.ImageFormat]::Png)
  $sheet=[K082Pixels]::Enlarge($native,$factor)
  $relative=if($worker){'TrapMap/'+$id+'.png'}else{'result/codex_'+$id+'.png'}
  $sheet.Save((Join-Path $out $relative),[System.Drawing.Imaging.ImageFormat]::Png)
  $preview=[K082Pixels]::Enlarge($sheet,4)
  $preview.Save((Join-Path $out ('previews/'+$id+'_4x.png')),[System.Drawing.Imaging.ImageFormat]::Png)
  $order=if($id.StartsWith('worker_dig')){@('plant_shovel','push_shovel','lift_soil','throw_soil')}elseif($worker){@('carry_step_1','carry_step_2','carry_step_3','carry_step_4')}else{@('front_bust')}
  $pivot=if($worker){@(128,352)}else{@(256,480)}
  $records+=@{id=$id;file=$relative;source=$oldPath;width=$sheet.Width;height=$sheet.Height;frame_count=$frames;frame_order=$order;pivot_top_left_px=$pivot;cell_size=@(($cw*$factor),($ch*$factor));native_cell=@($cw,$ch);anchor_policy=if($worker){'existing torso alignment; foot baseline y=352'}else{'center x=256; bust bottom y=480'};frames=$frameRecords;preview='previews/'+$id+'_4x.png'}
  $preview.Dispose();$sheet.Dispose();$native.Dispose();$old.Dispose();$src.Dispose()
}
@{card='K-082';generator='built-in image_gen';pixel_processing='nearest-neighbor; alpha threshold 224; 33-color shared palette; dark 1-native-pixel outline; remove 8-connected islands <=2 native pixels';assets=$records} | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $out 'manifest.json') -Encoding UTF8
Write-Output '인물 PNG 11종과 확대본 출력 완료'
