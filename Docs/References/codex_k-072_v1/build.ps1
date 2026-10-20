$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
public static class K072Pack {
    public static Bitmap Scale(Bitmap b, int scale) {
        Bitmap o=new Bitmap(b.Width*scale,b.Height*scale,PixelFormat.Format32bppArgb);
        for(int y=0;y<o.Height;y++) for(int x=0;x<o.Width;x++) o.SetPixel(x,y,b.GetPixel(x/scale,y/scale));
        return o;
    }
    public static void Pack(string input,string output,string native,string preview,int frames,int maxW,int maxH,int baseY,string[] hex) {
        Color[] palette=Array.ConvertAll(hex,h=>ColorTranslator.FromHtml(h));
        using(Bitmap src=new Bitmap(input)) {
            int cols=frames==1?1:3, rows=frames==1?1:2;
            Rectangle[] bounds=new Rectangle[frames]; int widest=1,tallest=1;
            for(int f=0;f<frames;f++) {
                int left=src.Width,top=src.Height,right=-1,bottom=-1;
                int x0=f%cols*src.Width/cols,x1=(f%cols+1)*src.Width/cols;
                int y0=f/cols*src.Height/rows,y1=(f/cols+1)*src.Height/rows;
                for(int y=y0;y<y1;y++) for(int x=x0;x<x1;x++) if(src.GetPixel(x,y).A>=160) {
                    left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                }
                if(right<left) throw new Exception("빈 원본 프레임: "+input+" "+f);
                bounds[f]=Rectangle.FromLTRB(left,top,right+1,bottom+1);
                widest=Math.Max(widest,right-left+1);tallest=Math.Max(tallest,bottom-top+1);
            }
            double ratio=Math.Min((double)maxW/widest,(double)maxH/tallest);
            int height=frames==1?32:64;
            using(Bitmap art=new Bitmap(frames*64,height,PixelFormat.Format32bppArgb)) {
                for(int f=0;f<frames;f++) {
                    Rectangle b=bounds[f]; int w=Math.Max(1,(int)Math.Round(b.Width*ratio));
                    int h=Math.Max(1,(int)Math.Round(b.Height*ratio));
                    int ox=f*64+(64-w)/2,oy=frames==1?(height-h)/2:baseY-h;
                    for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
                        Color c=src.GetPixel(Math.Min(b.Right-1,b.X+(int)((x+0.5)*b.Width/w)),Math.Min(b.Bottom-1,b.Y+(int)((y+0.5)*b.Height/h)));
                        if(c.A<160) continue;
                        Color chosen=palette[0];double best=double.MaxValue;
                        foreach(Color p in palette) {double d=2*(c.R-p.R)*(c.R-p.R)+3*(c.G-p.G)*(c.G-p.G)+(c.B-p.B)*(c.B-p.B);if(d<best){best=d;chosen=p;}}
                        if(ox+x>=0&&ox+x<art.Width&&oy+y>=0&&oy+y<height) art.SetPixel(ox+x,oy+y,chosen);
                    }
                    // 홀로 남은 한 점만 제거하고 두 점 이상 붙은 의도적인 불티는 보존한다.
                    bool[,] visited=new bool[64,height];
                    for(int yy=0;yy<height;yy++) for(int xx=0;xx<64;xx++) {
                        if(visited[xx,yy]||art.GetPixel(f*64+xx,yy).A==0)continue;
                        List<Point> points=new List<Point>();Queue<Point> q=new Queue<Point>();q.Enqueue(new Point(xx,yy));visited[xx,yy]=true;
                        while(q.Count>0){Point p=q.Dequeue();points.Add(p);for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int nx=p.X+dx,ny=p.Y+dy;if(nx<0||nx>=64||ny<0||ny>=height||visited[nx,ny]||art.GetPixel(f*64+nx,ny).A==0)continue;visited[nx,ny]=true;q.Enqueue(new Point(nx,ny));}}
                        if(points.Count==1)art.SetPixel(f*64+xx,yy,Color.Transparent);
                    }
                    int minx=64,miny=height,maxx=-1,maxy=-1;
                    for(int yy=0;yy<height;yy++)for(int xx=0;xx<64;xx++)if(art.GetPixel(f*64+xx,yy).A>0){minx=Math.Min(minx,xx);maxx=Math.Max(maxx,xx);miny=Math.Min(miny,yy);maxy=Math.Max(maxy,yy);}
                    if(maxx<minx)throw new Exception("정리 후 빈 프레임: "+input+" "+f);
                    int shiftX=(64-(maxx-minx+1))/2-minx;
                    int shiftY=frames==1?(height-(maxy-miny+1))/2-miny:baseY-1-maxy;
                    using(Bitmap aligned=new Bitmap(64,height,PixelFormat.Format32bppArgb)) {
                        for(int yy=miny;yy<=maxy;yy++)for(int xx=minx;xx<=maxx;xx++)aligned.SetPixel(xx+shiftX,yy+shiftY,art.GetPixel(f*64+xx,yy));
                        for(int yy=0;yy<height;yy++)for(int xx=0;xx<64;xx++)art.SetPixel(f*64+xx,yy,aligned.GetPixel(xx,yy));
                    }
                }
                art.Save(native,ImageFormat.Png);
                using(Bitmap sheet=Scale(art,4)) {sheet.Save(output,ImageFormat.Png);using(Bitmap big=Scale(sheet,4))big.Save(preview,ImageFormat.Png);}
            }
        }
    }
}
'@
$root=$PSScriptRoot
$jobs=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'inputs.json') | ConvertFrom-Json
foreach($dir in @('sheets','native','previews','generated')) { [IO.Directory]::CreateDirectory((Join-Path $root $dir)) | Out-Null }
$solid=@('#211b22','#493237','#805035','#bc8550','#eee0c6','#514b60','#8a8a9c','#ccd0d4','#fff4da')
$flame=@('#2b2028','#673138','#a84032','#df6130','#f6a52b','#ffd957','#fff1bf','#56474a','#8b7770','#beaaa0')
$smoke=@('#2b2028','#51464b','#88776e','#bdab91','#e4d4b7','#ffd957')
foreach($j in $jobs) {
    $palette=$flame
    if($j.frames -eq 1){$palette=$solid}
    if($j.name -eq 'bullet_trace'){$palette=$flame}
    if($j.name -in @('dust_puff','muzzle_smoke')){$palette=$smoke}
    $raw=Join-Path $root ('generated/'+$j.name+'.png')
    $inputPath=$j.source
    if(-not [IO.Path]::IsPathRooted($inputPath)){$inputPath=Join-Path $root $inputPath}
    if([IO.Path]::GetFullPath($inputPath) -ne [IO.Path]::GetFullPath($raw)) { Copy-Item -LiteralPath $inputPath -Destination $raw -Force }
    [K072Pack]::Pack($raw,(Join-Path $root ('sheets/'+$j.name+'.png')),(Join-Path $root ('native/'+$j.name+'.png')),(Join-Path $root ('previews/'+$j.name+'_4x.png')),$j.frames,$j.maxW,$j.maxH,$j.baseY,$palette)
}
