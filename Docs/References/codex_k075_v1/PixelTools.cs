using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Linq;

public static class K075Pixels {
    // 좌표는 왼쪽 위 원점이며 발선은 불투명 영역 아래의 배타 경계다.
    public static Bitmap Load(string p) { using(var a=new Bitmap(p)) return new Bitmap(a); }
    public static Rectangle Bounds(Bitmap b) {
        int l=b.Width,t=b.Height,r=-1,d=-1;
        for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A>=128){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);d=Math.Max(d,y);}
        return r<0?Rectangle.Empty:Rectangle.FromLTRB(l,t,r+1,d+1);
    }
    public static void Save(Bitmap b,string p){Directory.CreateDirectory(Path.GetDirectoryName(p));b.Save(p,ImageFormat.Png);}
    public static Bitmap Crop(Bitmap b,Rectangle r){var o=new Bitmap(r.Width,r.Height);for(int y=0;y<r.Height;y++)for(int x=0;x<r.Width;x++)o.SetPixel(x,y,b.GetPixel(x+r.X,y+r.Y));return o;}
    public static Bitmap Resize(Bitmap b,int w,int h){var o=new Bitmap(w,h);for(int y=0;y<h;y++)for(int x=0;x<w;x++)o.SetPixel(x,y,b.GetPixel(Math.Min(b.Width-1,(int)((x+.5)*b.Width/w)),Math.Min(b.Height-1,(int)((y+.5)*b.Height/h))));return o;}
    public static void Paste(Bitmap dst,Bitmap src,int dx,int dy){for(int y=0;y<src.Height;y++)for(int x=0;x<src.Width;x++){int xx=x+dx,yy=y+dy;if(xx>=0&&xx<dst.Width&&yy>=0&&yy<dst.Height){var c=src.GetPixel(x,y);if(c.A>0)dst.SetPixel(xx,yy,c);}}}
    public static List<List<int>> Components(Bitmap b){
        int w=b.Width,h=b.Height;var used=new bool[w*h];var solid=new bool[w*h];var result=new List<List<int>>();
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)solid[y*w+x]=b.GetPixel(x,y).A>=128;
        for(int i=0;i<used.Length;i++)if(solid[i]&&!used[i]){var part=new List<int>();var q=new Queue<int>();q.Enqueue(i);used[i]=true;while(q.Count>0){int p=q.Dequeue();part.Add(p);int x=p%w,y=p/w;for(int yy=Math.Max(0,y-1);yy<=Math.Min(h-1,y+1);yy++)for(int xx=Math.Max(0,x-1);xx<=Math.Min(w-1,x+1);xx++){int n=yy*w+xx;if(solid[n]&&!used[n]){used[n]=true;q.Enqueue(n);}}}result.Add(part);}
        return result.OrderByDescending(v=>v.Count).ToList();
    }
    public static Bitmap[] Split(Bitmap b,int count){
        var parts=Components(b);var big=parts.Take(count).OrderBy(v=>v.Average(i=>i%b.Width)).ToArray();
        if(big.Length!=count)throw new Exception("그림 개수가 다릅니다.");
        var full=new Bitmap[count];for(int j=0;j<count;j++)full[j]=new Bitmap(b.Width,b.Height);
        var centers=big.Select(v=>v.Average(i=>i%b.Width)).ToArray();
        foreach(var part in parts){if(part.Count<24)continue;double x=part.Average(i=>i%b.Width);int k=Enumerable.Range(0,count).OrderBy(j=>Math.Abs(x-centers[j])).First();foreach(int i in part)full[k].SetPixel(i%b.Width,i/b.Width,b.GetPixel(i%b.Width,i/b.Width));}
        return full;
    }
    static Color[] palette=new string[]{"#110E16","#292733","#45434F","#686673","#111E32","#203D60","#32628B","#A98756","#DBC593","#FFF0C5","#8D5537","#D99059","#FFD19A","#3A221D","#6B3C28","#A3633C","#CB8B53","#1D4E7A","#5D9FC8","#FFF9E8","#E75913","#FFA927","#FFF27F"}.Select(ColorTranslator.FromHtml).ToArray();
    public static void Quantize(Bitmap b){for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,y);if(c.A<128){b.SetPixel(x,y,Color.Transparent);continue;}var p=palette.OrderBy(v=>2*(v.R-c.R)*(v.R-c.R)+3*(v.G-c.G)*(v.G-c.G)+2*(v.B-c.B)*(v.B-c.B)).First();b.SetPixel(x,y,p);}}
    public static void Clean(Bitmap b,int minimum){foreach(var part in Components(b).Where(v=>v.Count<minimum))foreach(int i in part)b.SetPixel(i%b.Width,i/b.Width,Color.Transparent);}
    public static int HairTop(Bitmap b){int top=b.Height;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,y);int max=Math.Max(c.R,Math.Max(c.G,c.B)),min=Math.Min(c.R,Math.Min(c.G,c.B));if(c.A>=128&&max>35&&max<150&&max-min<34)top=Math.Min(top,y);}return top;}
    public static int HairAxis(Bitmap b,int top,int bodyHeight){var xs=new List<int>();for(int y=top;y<Math.Min(b.Height,top+(int)(bodyHeight*.22));y++)for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,y);int max=Math.Max(c.R,Math.Max(c.G,c.B)),min=Math.Min(c.R,Math.Min(c.G,c.B));if(c.A>=128&&max>30&&max<155&&max-min<35)xs.Add(x);}return xs.Count>0?(int)Math.Round(xs.Average()):b.Width/2;}
    public static int Sole(Bitmap b){var bb=Bounds(b);var xs=new List<int>();for(int y=bb.Bottom-2;y<bb.Bottom;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A>=128)xs.Add(x);xs.Sort();return xs[xs.Count/2];}
    public static Point Hot(Bitmap b){var xs=new List<int>();var ys=new List<int>();int first=128;for(int y=0;y<90;y++)for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,y);if(c.A>=128&&c.R>225&&c.G<220&&c.B<90){first=Math.Min(first,y);if(y<first+8){xs.Add(x);ys.Add(y);}}}if(xs.Count==0)return Point.Empty;return new Point((int)Math.Round(xs.Average()),(int)Math.Round(ys.Average()));}
    public static int CoatAxis(Bitmap b,int top,int height){var xs=new List<int>();for(int y=top+(int)(height*.40);y<top+(int)(height*.67);y++)for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,y);if(c.A>=128&&c.B>c.R*1.25&&c.B>c.G*1.08&&c.B>45)xs.Add(x);}return (int)Math.Round(xs.Average());}
    public static int PlantSole(Bitmap b){return PlantSole(b,false);}
    public static int PlantSole(Bitmap b,bool behind){var bb=Bounds(b);var groups=new List<List<int>>();List<int> run=null;for(int x=0;x<b.Width;x++){bool solid=false;for(int y=bb.Bottom-2;y<bb.Bottom;y++)if(b.GetPixel(x,y).A>=128)solid=true;if(solid){if(run==null){run=new List<int>();groups.Add(run);}run.Add(x);}else run=null;}return (int)Math.Round((behind?groups.Last():groups.First()).Average());}
    public static Bitmap Normalize(Bitmap src,double scale,int axis,int foot){
        var bb=Bounds(src);var crop=Crop(src,bb);var small=Resize(crop,(int)Math.Round(bb.Width*scale),(int)Math.Round(bb.Height*scale));Quantize(small);Clean(small,2);var dst=new Bitmap(96,128);Paste(dst,small,48-(int)Math.Round((axis-bb.Left)*scale),foot-small.Height);Clean(dst,3);return dst;
    }
    public static Bitmap FixSole(Bitmap src,int target){
        int shift=target-PlantSole(src,target==60);var dst=new Bitmap(96,128);
        // 장화 모양은 통째로 옮기고 바지 부분에서만 연결을 보정한다.
        for(int y=0;y<128;y++){int dx=(int)Math.Round(shift*Math.Max(0,Math.Min(1,(y-88)/12.0)));for(int x=0;x<96;x++){int xx=x+dx;if(xx>=0&&xx<96)dst.SetPixel(xx,y,src.GetPixel(x,y));}}
        return dst;
    }
    public static bool Equal(Bitmap a,Bitmap b){if(a.Size!=b.Size)return false;for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y))return false;return true;}
    public static bool BinaryAlpha(Bitmap b){for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){int a=b.GetPixel(x,y).A;if(a!=0&&a!=255)return false;}return true;}
    public static bool IsFire(Color c){return c.A==255&&c.R>225&&c.G<220&&c.B<90;}
    public static bool SameFeet(Bitmap a,Bitmap b){for(int y=100;y<120;y++)for(int x=0;x<96;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y))return false;return true;}
    public static string[] Palette(){return palette.Select(c=>ColorTranslator.ToHtml(c)).ToArray();}
}
