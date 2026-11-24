$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$repo=(Resolve-Path (Join-Path $root '../../..')).Path
foreach($dir in @('originals','sprites','sheets','qa')){New-Item -ItemType Directory -Force (Join-Path $root $dir)|Out-Null}
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
public static class K030 {
 public static Rectangle Bound(Bitmap b,Rectangle area){int l=area.Right,t=area.Bottom,r=-1,d=-1;for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++)if(b.GetPixel(x,y).A>128){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);d=Math.Max(d,y);}if(r<0)throw new Exception("빈 그림 칸");return Rectangle.FromLTRB(l,t,r+1,d+1);}
 public static Rectangle Area(Bitmap b,string src,int col,int row,int rows){int[] xs=src.EndsWith("map.png")?new int[]{0,490,980,1340,b.Width}:new int[]{0,b.Width/4,b.Width/2,3*b.Width/4,b.Width};if(src.EndsWith("side.png")&&row==1)xs=new int[]{0,423,900,1330,b.Width};if(rows==3&&row==0&&src.EndsWith("gunner.png"))xs=new int[]{0,291,504,760,b.Width};if(rows==3&&row==0&&src.EndsWith("archer.png"))xs=new int[]{0,254,504,760,b.Width};int[] ys=rows==3?new int[]{0,558,1024,b.Height}:(rows==1?new int[]{0,b.Height}:new int[]{0,b.Height/2,b.Height});return Rectangle.FromLTRB(xs[col],ys[row],xs[col+1],ys[row+1]);}
 public static int[] Cut(string src,string dst,int col,int row,int rows,int w,int h,int mw,int mh,bool top,double scale){using(var b=new Bitmap(src)){var crop=Bound(b,Area(b,src,col,row,rows));double s=scale>0?scale:Math.Min((double)mw/crop.Width,(double)mh/crop.Height);int dw=(int)Math.Round(crop.Width*s),dh=(int)Math.Round(crop.Height*s),dx=(w-dw)/2,dy=top?(h-dh)/2:h-(rows==2?8:32)-dh;using(var o=new Bitmap(w,h,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(o)){g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.CompositingMode=CompositingMode.SourceCopy;g.DrawImage(b,new Rectangle(dx,dy,dw,dh),crop,GraphicsUnit.Pixel);o.Save(dst,ImageFormat.Png);}return new int[]{crop.X,crop.Y,crop.Width,crop.Height,dx,dy,dw,dh};}}
 public static double Scale(string src,int rows){using(var b=new Bitmap(src)){double s=9;for(int j=0;j<rows;j++)for(int i=0;i<4;i++){var q=Bound(b,Area(b,src,i,j,rows));s=Math.Min(s,Math.Min(232.0/q.Width,300.0/q.Height));}return s;}}
 public static void Sheet(string[] paths,string dst){using(var o=new Bitmap(1024,384))using(var g=Graphics.FromImage(o)){g.Clear(Color.Transparent);for(int i=0;i<4;i++)using(var b=new Bitmap(paths[i]))g.DrawImageUnscaled(b,i*256,0);o.Save(dst,ImageFormat.Png);}}
 public static int[] Check(string path){using(var b=new Bitmap(path)){int empty=0,solid=0,edge=0,semi=0;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){int a=b.GetPixel(x,y).A;if(a==0)empty++;else {if(a>128)solid++;if(a<255)semi++;}if(a>128&&(x==0||y==0||x==b.Width-1||y==b.Height-1))edge++;}return new int[]{b.Width,b.Height,empty,solid,semi,edge};}}
 public static void Contact(string root,string[] files,string[] names){using(var o=new Bitmap(1120,((files.Length+3)/4)*238))using(var g=Graphics.FromImage(o))using(var f=new Font("Malgun Gothic",11)){g.Clear(Color.FromArgb(226,220,202));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;for(int i=0;i<files.Length;i++){int x=i%4*280,y=i/4*238;g.DrawString(names[i],f,Brushes.Black,x+10,y+7);using(var b=new Bitmap(System.IO.Path.Combine(root,files[i]))){double s=Math.Min(250.0/b.Width,195.0/b.Height);g.DrawImage(b,new Rectangle(x+(280-(int)(b.Width*s))/2,y+32,(int)(b.Width*s),(int)(b.Height*s)),0,0,b.Width,b.Height,GraphicsUnit.Pixel);}}o.Save(System.IO.Path.Combine(root,"qa/contact_sheet.png"),ImageFormat.Png);}}
}
'@
$specs=@(
 @{id='wall_straight_build';label='직선 토벽 · 짓는 중';map=@(64,32);side=@(320,96)},
 @{id='wall_straight_done';label='직선 토벽 · 완성';map=@(64,32);side=@(320,96)},
 @{id='wall_corner_build';label='모서리 토벽 · 짓는 중';map=@(64,64);side=@(320,160)},
 @{id='wall_corner_done';label='모서리 토벽 · 완성';map=@(64,64);side=@(320,160)},
 @{id='hill_small';label='작게 쌓은 언덕';map=@(32,32);side=@(160,96)},
 @{id='hill_large';label='크게 쌓은 언덕';map=@(64,64);side=@(320,160)},
 @{id='logs';label='쓰러진 나무 더미';map=@(32,32);side=@(160,80)},
 @{id='rocks';label='바위 더미';map=@(32,32);side=@(160,112)}
)
$assets=@();$index=0
foreach($s in $specs){foreach($view in @('map','side')){
 $size=$s[$view];$id=$s.id+'_'+$view;$file='sprites/'+$id+'.png';$isTop=$view -eq 'map';$pad=if($isTop){4}else{16}
 $crop=[K030]::Cut((Join-Path $root ('originals/'+$view+'.png')),(Join-Path $root $file),($index%4),[Math]::Floor($index/4),2,$size[0],$size[1],($size[0]-$pad),($size[1]-$pad),$isTop,0)
 $pivot=if($isTop){@(($size[0]/2),($size[1]/2))}else{@(($size[0]/2),($size[1]-8))}
 $assets+=@{id=$id;label=$s.label;view=$view;file=$file;width=$size[0];height=$size[1];pivotTopLeft=$pivot;pixelsPerMeterProposal=$(if($isTop){6.4}else{32});cropAndPlacement=$crop;source=('originals/'+$view+'.png');footprintMetersProposal=@(($s.map[0]/6.4),($s.map[1]/6.4))}
 };$index++}
$actions=@(@{id='dig';label='삽질'},@{id='earth';label='흙 나르기'},@{id='log';label='통나무 나르기'})
foreach($who in @('archer','gunner')){
 $src=Join-Path $root ('originals/'+$who+'.png');$scale=[K030]::Scale($src,3);$row=0
 foreach($act in $actions){$actSrc=$src;$actRows=3;$actRow=$row;$actScale=$scale;if($act.id -eq 'dig'){$actSrc=Join-Path $root ('originals/'+$who+'_dig.png');$actRows=1;$actRow=0;$actScale=[K030]::Scale($actSrc,1)};$frames=@();$places=@();for($i=0;$i -lt 4;$i++){
 $file='sprites/'+$who+'_'+$act.id+'_'+($i+1)+'.png';$frames+=Join-Path $root $file
 $places+=,@([K030]::Cut($actSrc,(Join-Path $root $file),$i,$actRow,$actRows,256,384,232,300,$false,$actScale))
 };$id=$who+'_'+$act.id;$file='sheets/'+$id+'4.png';[K030]::Sheet([string[]]$frames,(Join-Path $root $file))
 $assets+=@{id=$id;label=($(if($who -eq 'archer'){'궁수'}else{'총병'})+' · '+$act.label);view='action';file=$file;width=1024;height=384;cell=@(256,384);count=4;pivotTopLeft=@(128,352);pivotUnity=@(0.5,(32.0/384));fpsProposal=5;source=('originals/'+[IO.Path]::GetFileName($actSrc));cropAndPlacement=$places;uniformScale=$actScale};$row++
 }
}
$manifest=@{card='K-030';status='사용자 확정 전 그림 시안 · 규칙 미확정 · 게임 미연결';mapScale='32px/5m';battleScaleProposal='32px/m · K-025 참조';assets=$assets}
$manifest|ConvertTo-Json -Depth 12|Set-Content -Encoding UTF8 (Join-Path $root 'manifest.json')
$files=@($assets|ForEach-Object{$_.file});$names=@($assets|ForEach-Object{$_.label+$(if($_.view -eq 'map'){' · 지도'}elseif($_.view -eq 'side'){' · 전투'}else{''})})
[K030]::Contact($root,[string[]]$files,[string[]]$names)
$packed=@{};$packed['k029_grass']='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $root 'references/k029_grass_a.png')));foreach($a in $assets){$packed[$a.id]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $root $a.file)))}
foreach($who in @('archer','gunner')){$packed['reference_'+$who]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $repo ('Game/Assets/_Project/Art/Characters/Static/'+$who+'_static.png'))))}
$html=Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'review.template.html')
$html=$html.Replace('__PACKED__',($packed|ConvertTo-Json -Compress -Depth 4)).Replace('__MANIFEST__',($manifest|ConvertTo-Json -Compress -Depth 12))
[IO.File]::WriteAllText((Join-Path $repo '_HTML/codex_K-030_지형손질_v1.html'),$html,[Text.UTF8Encoding]::new($false))
$checks=@();foreach($a in $assets){$v=[K030]::Check((Join-Path $root $a.file));$checks+=@{id=$a.id;size=@($v[0],$v[1]);transparentPixels=$v[2];visiblePixels=$v[3];partialAlphaPixels=$v[4];edgePixels=$v[5];pass=($v[0] -eq $a.width -and $v[1] -eq $a.height -and $v[2] -gt 0 -and $v[3] -gt 0 -and $v[5] -eq 0)}}
$checks|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $root 'qa/image_checks.json')
Write-Output ('출력 '+$assets.Count+'종 / 그림 검사 실패 '+@($checks|Where-Object{!$_.pass}).Count)
