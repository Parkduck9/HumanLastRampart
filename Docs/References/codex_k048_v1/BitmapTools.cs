using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Collections.Generic;

// 원안의 칸 분리, 경계 실측, 최근접 배율 정리와 시트 포장만 담당한다.
public static class BitmapTools {
 public static Rectangle Bounds(Bitmap b, int threshold=128) {
  int l=b.Width,t=b.Height,r=-1,d=-1;
  for(int y=0;y<b.Height;y++) for(int x=0;x<b.Width;x++) if(b.GetPixel(x,y).A>threshold){l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);d=Math.Max(d,y);}
  return r<l?Rectangle.Empty:Rectangle.FromLTRB(l,t,r+1,d+1);
 }
 public static Bitmap Crop(Bitmap b, Rectangle r){return b.Clone(r,PixelFormat.Format32bppArgb);}
 public static Bitmap Resize(Bitmap b,int w,int h){
  Bitmap o=new Bitmap(w,h);using(Graphics g=Graphics.FromImage(o)){g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.CompositingMode=CompositingMode.SourceCopy;g.DrawImage(b,new Rectangle(0,0,w,h),0,0,b.Width,b.Height,GraphicsUnit.Pixel);}return o;
 }
 public static void Paste(Bitmap dst,Bitmap src,int x,int y){using(Graphics g=Graphics.FromImage(dst)){g.DrawImageUnscaled(src,x,y);}}
 // 가장 낮은 불투명 두 줄에서 부츠 덩어리를 찾아 접지 좌표를 잰다.
 public static int Sole(Bitmap b,bool forward){
  Rectangle q=Bounds(b);List<int[]> groups=new List<int[]>();int start=-1,last=-1;
  for(int x=0;x<b.Width;x++){bool hit=false;for(int y=Math.Max(0,q.Bottom-2);y<q.Bottom;y++)if(b.GetPixel(x,y).A>128)hit=true;
   if(hit){if(start<0)start=x;last=x;} else if(start>=0){groups.Add(new int[]{start,last});start=-1;}}
  if(start>=0)groups.Add(new int[]{start,last});if(groups.Count==0)return b.Width/2;
  int[] a=groups[forward?0:groups.Count-1];return (a[0]+a[1])/2;
 }
 public static string Rect(Rectangle r){return String.Join(",",new int[]{r.X,r.Y,r.Width,r.Height});}
 public static int BodyTop(Bitmap b){
  for(int y=0;y<b.Height;y++)for(int x=(int)(b.Width*.45);x<(int)(b.Width*.9);x++){Color c=b.GetPixel(x,y);if(c.A>128&&c.R+c.G+c.B<300)return y;}return 0;
 }
 public static Point Flame(Bitmap b){
  int last=-1,top=BodyTop(b);for(int y=0;y<Math.Min(b.Height,top+120);y++)for(int x=0;x<b.Width*.4;x++){Color c=b.GetPixel(x,y);if(c.A>128&&c.R>240&&c.G>210&&c.B<140)last=Math.Max(last,y);}
  int sum=0,n=0;for(int y=Math.Max(0,last-2);y<=last;y++)for(int x=0;x<b.Width*.4;x++){Color c=b.GetPixel(x,y);if(c.A>128&&c.R>240&&c.G>210&&c.B<140){sum+=x;n++;}}
  if(n==0)throw new Exception("불씨 픽셀을 찾지 못함");
  return new Point(n>0?sum/n:0,last);
 }
 public static Point Fuse(Bitmap b,int desiredY){
  for(int delta=0;delta<20;delta++)foreach(int y in new int[]{desiredY+delta,desiredY-delta}){if(y<125||y>168)continue;int sum=0,n=0;for(int x=325;x<358;x++){Color c=b.GetPixel(x,y);if(c.A>128&&c.R>120&&c.G>65&&c.R>c.G*1.12&&c.B<c.G*.85){sum+=x;n++;}}if(n>0)return new Point(sum/n,y);}
  throw new Exception("도화선 색상 접점을 찾지 못함");
 }
 // 인접 칸에서 들어온 작은 고립 성분을 제외하고 주 인물의 알파를 보존한다.
 public static void Clean(Bitmap b){
  int w=b.Width,h=b.Height;int[] ids=new int[w*h];int id=0,best=0,bestSize=0;Queue<int> q=new Queue<int>();
  for(int y=0;y<h;y++)for(int x=0;x<w;x++){int p=y*w+x;if(ids[p]!=0||b.GetPixel(x,y).A==0)continue;id++;int size=0;ids[p]=id;q.Enqueue(p);
   while(q.Count>0){int z=q.Dequeue(),zx=z%w,zy=z/w;size++;for(int yy=Math.Max(0,zy-1);yy<=Math.Min(h-1,zy+1);yy++)for(int xx=Math.Max(0,zx-1);xx<=Math.Min(w-1,zx+1);xx++){int a=yy*w+xx;if(ids[a]==0&&b.GetPixel(xx,yy).A>0){ids[a]=id;q.Enqueue(a);}}}
   if(size>bestSize){best=id;bestSize=size;}}
  for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(ids[y*w+x]!=best)b.SetPixel(x,y,Color.Transparent);
 }
}
