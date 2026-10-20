$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$out = $PSScriptRoot
foreach ($dir in @('source','icons','rings','effects','previews','sheets','qa')) { [IO.Directory]::CreateDirectory((Join-Path $out $dir)) | Out-Null }
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class K094Art {
 public static Bitmap Canvas(int w,int h){return new Bitmap(w,h,PixelFormat.Format32bppArgb);}
 public static Bitmap Scale(Bitmap b,int w,int h){var o=Canvas(w,h);for(int y=0;y<h;y++)for(int x=0;x<w;x++)o.SetPixel(x,y,b.GetPixel(x*b.Width/w,y*b.Height/h));return o;}
 public static void Blit(Bitmap dst,Bitmap src,int x,int y){for(int j=0;j<src.Height;j++)for(int i=0;i<src.Width;i++){int dx=x+i,dy=y+j;if(dx<0||dy<0||dx>=dst.Width||dy>=dst.Height)continue;var p=src.GetPixel(i,j);if(p.A==0)continue;var q=dst.GetPixel(dx,dy);if(q.A==0||p.A==255){dst.SetPixel(dx,dy,p);continue;}double a=p.A/255.0,qa=q.A/255.0,oa=a+qa*(1-a);dst.SetPixel(dx,dy,Color.FromArgb((int)Math.Round(oa*255),(int)Math.Round((p.R*a+q.R*qa*(1-a))/oa),(int)Math.Round((p.G*a+q.G*qa*(1-a))/oa),(int)Math.Round((p.B*a+q.B*qa*(1-a))/oa)));}}
 public static Bitmap Alpha(Bitmap b,double a,bool red){var o=Canvas(b.Width,b.Height);for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var p=b.GetPixel(x,y);o.SetPixel(x,y,Color.FromArgb((int)(p.A*a),red?218:p.R,red?(p.G/3+35):p.G,red?(p.B/3+30):p.B));}return o;}
 public static Bitmap Extract(Bitmap src,Rectangle area,int w,int h,bool dust){
 int l=area.Right,t=area.Bottom,r=area.Left,b=area.Top;
 for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++)if(src.GetPixel(x,y).A>128){l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);b=Math.Max(b,y);}
 var o=Canvas(w,h);string[] hex=dust?new[]{"30271e","705239","ac7e4a","d5a864","f2cf8c"}:new[]{"29251d","443b28","675536","8a7145","ac8e54","d6b873","373a32","565b4c","b74835"};
 var pal=new List<Color>();foreach(var s in hex)pal.Add(ColorTranslator.FromHtml("#"+s));
 for(int y=0;y<h;y++)for(int x=0;x<w;x++){var p=src.GetPixel(l+(int)((x+.5)*(r-l+1)/w),t+(int)((y+.5)*(b-t+1)/h));if(p.A<128)continue;int best=int.MaxValue;Color c=pal[0];foreach(var q in pal){int d=(p.R-q.R)*(p.R-q.R)+(p.G-q.G)*(p.G-q.G)+(p.B-q.B)*(p.B-q.B);if(d<best){best=d;c=q;}}o.SetPixel(x,y,c);}
 KeepLargest(o);return o;
 }
 public static int Components(Bitmap b){return Groups(b).Count;}
 static List<List<Point>> Groups(Bitmap b){var seen=new bool[b.Width,b.Height];var groups=new List<List<Point>>();for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){if(seen[x,y]||b.GetPixel(x,y).A==0)continue;var group=new List<Point>();var q=new Queue<Point>();q.Enqueue(new Point(x,y));seen[x,y]=true;while(q.Count>0){var p=q.Dequeue();group.Add(p);for(int j=-1;j<=1;j++)for(int i=-1;i<=1;i++){int nx=p.X+i,ny=p.Y+j;if(nx>=0&&ny>=0&&nx<b.Width&&ny<b.Height&&!seen[nx,ny]&&b.GetPixel(nx,ny).A>0){seen[nx,ny]=true;q.Enqueue(new Point(nx,ny));}}}groups.Add(group);}return groups;}
 public static void KeepLargest(Bitmap b){var groups=Groups(b);if(groups.Count==0)return;groups.Sort((a,c)=>c.Count.CompareTo(a.Count));for(int i=1;i<groups.Count;i++)foreach(var p in groups[i])b.SetPixel(p.X,p.Y,Color.Transparent);}
 public static void Outline(Bitmap b){var copy=(Bitmap)b.Clone();var c=ColorTranslator.FromHtml("#29251d");for(int y=1;y<b.Height-1;y++)for(int x=1;x<b.Width-1;x++)if(copy.GetPixel(x,y).A==0&&(copy.GetPixel(x-1,y).A>0||copy.GetPixel(x+1,y).A>0||copy.GetPixel(x,y-1).A>0||copy.GetPixel(x,y+1).A>0))b.SetPixel(x,y,c);copy.Dispose();}
 public static Bitmap Ring(int radius,Color color,bool line,bool fill){int c=radius+2;var o=Canvas(c*2+1,c*2+1);if(fill)for(int y=0;y<o.Height;y++)for(int x=0;x<o.Width;x++){double d=Math.Sqrt((x-c)*(x-c)+(y-c)*(y-c));if(d<radius){int a=(int)(8+22*Math.Pow(d/radius,2));o.SetPixel(x,y,Color.FromArgb(a,color.R,color.G,color.B));}}
 if(line){int x=radius,y=0,err=1-radius;while(x>=y){int[] dx={x,y,-y,-x,-x,-y,y,x};int[] dy={y,x,x,y,-y,-x,-x,-y};for(int k=0;k<8;k++){double arc=(Math.Atan2(dy[k],dx[k])+Math.PI*2)*radius; if(((int)Math.Floor(arc/4))%2==0)o.SetPixel(c+dx[k],c+dy[k],color);}y++;if(err<0)err+=2*y+1;else{x--;err+=2*(y-x)+1;}}}return o;}
 public static int[] Bounds(Bitmap b){int l=b.Width,t=b.Height,r=-1,bt=-1,min=255,max=0;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var p=b.GetPixel(x,y);if(p.A==0)continue;l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);bt=Math.Max(bt,y);min=Math.Min(min,p.A);max=Math.Max(max,p.A);}return new[]{l,t,r,bt,min,max};}
}
'@
function Save-Png($bmp,$relative) { $bmp.Save((Join-Path $out $relative),[Drawing.Imaging.ImageFormat]::Png) }
function Preview($bmp,$name) { $p=[K094Art]::Scale($bmp,($bmp.Width*4),($bmp.Height*4)); Save-Png $p "previews/$name`_4x.png"; $p.Dispose() }
$sourcePath=Join-Path $out 'source/generated.png'
if (!(Test-Path $sourcePath)) { Copy-Item -LiteralPath 'C:/Users/User/.codex/generated_images/01a15811-0697-7a11-93be-574f9963e1fd/exec-9a66e0de-5e8e-46cc-b600-c563ab6bb033.png' -Destination $sourcePath }
$source=[Drawing.Bitmap]::FromFile($sourcePath)
$small=[K094Art]::Extract($source,[Drawing.Rectangle]::new(0,0,([int]($source.Width/2)),$source.Height),20,20,$false)
$base=[K094Art]::Canvas(24,24); [K094Art]::Blit($base,$small,2,2); [K094Art]::Outline($base)
$armed=$base.Clone(); $red=[Drawing.ColorTranslator]::FromHtml('#cb6543'); $light=[Drawing.ColorTranslator]::FromHtml('#f2cf8c')
$armed.SetPixel(13,7,$red);$armed.SetPixel(14,6,$red);$armed.SetPixel(14,5,$light)
$selected=$armed.Clone();$gold=[Drawing.ColorTranslator]::FromHtml('#ead18b')
for($i=0;$i -lt 5;$i++){$selected.SetPixel($i,0,$gold);$selected.SetPixel(0,$i,$gold)}
# 오른쪽·아래 모서리도 바깥쪽 끝에 맞춘다.
for($i=0;$i -lt 5;$i++){ $selected.SetPixel((23-$i),0,$gold);$selected.SetPixel(23,$i,$gold);$selected.SetPixel(0,(23-$i),$gold);$selected.SetPixel($i,23,$gold);$selected.SetPixel((23-$i),23,$gold);$selected.SetPixel(23,(23-$i),$gold) }
# 선택 모서리는 중심 그림과 분리된 의도적인 표시다.
$ghost=[K094Art]::Alpha($armed,.45,$false);$invalid=[K094Art]::Alpha($armed,.45,$true)
$names=@('buried','armed','selected','ghost','ghost_invalid');$icons=@($base,$armed,$selected,$ghost,$invalid)
$sheet=[K094Art]::Canvas(120,24);$native=[K094Art]::Canvas(480,128)
for($i=0;$i -lt $icons.Count;$i++){Save-Png $icons[$i] "icons/trap_bomb_$($names[$i]).png";Preview $icons[$i] "trap_bomb_$($names[$i])";[K094Art]::Blit($sheet,$icons[$i],($i*24),0);[K094Art]::Blit($native,$icons[$i],($i*96+36),97)}
Save-Png $sheet 'sheets/trap_bomb_map_states.png';Preview $sheet 'trap_bomb_map_states';Save-Png $native 'sheets/trap_bomb_states_native_96x128.png'
$game=[K094Art]::Scale($native,1440,384);Save-Png $game 'sheets/trap_bomb_states_game_288x384.png'
$cloud=[K094Art]::Extract($source,[Drawing.Rectangle]::new(([int]($source.Width/2)),0,([int]($source.Width/2)),$source.Height),28,18,$true)
$dustSheet=[K094Art]::Canvas(128,32);$dustNative=[K094Art]::Canvas(384,128)
$sizes=@(@(10,7),@(18,12),@(26,17),@(28,18));$alpha=@(1,.88,.62,.30);$rise=@(0,1,3,5)
for($i=0;$i -lt 4;$i++){$s=[K094Art]::Scale($cloud,$sizes[$i][0],$sizes[$i][1]);[K094Art]::KeepLargest($s);$s=[K094Art]::Alpha($s,$alpha[$i],$false);$frame=[K094Art]::Canvas(32,32);[K094Art]::Blit($frame,$s,(16-[int]($s.Width/2)),(26-$s.Height-$rise[$i]));Save-Png $frame "effects/dig_$i.png";[K094Art]::Blit($dustSheet,$frame,($i*32),0);[K094Art]::Blit($dustNative,$frame,($i*96+32),94);$s.Dispose();$frame.Dispose()}
Save-Png $dustSheet 'effects/dig_map_4f.png';Preview $dustSheet 'dig_map_4f';Save-Png $dustNative 'sheets/dig_native_96x128_4f.png';$dg=[K094Art]::Scale($dustNative,1152,384);Save-Png $dg 'sheets/dig_game_288x384_4f.png'
$colors=@{valid_brass='#d5b16b';valid_green='#9abe77';invalid='#e16d61'}
foreach($kind in $colors.Keys){foreach($radius in @(32,64,96,128)){foreach($layer in @('line','fill','combined')){$b=[K094Art]::Ring($radius,[Drawing.ColorTranslator]::FromHtml($colors[$kind]),($layer -ne 'fill'),($layer -ne 'line'));Save-Png $b "rings/radius_$($kind)_r$($radius)_$layer.png";if($radius -eq 64 -and $layer -eq 'combined'){Preview $b "radius_$kind"};$b.Dispose()}}
$svg=@"
<svg xmlns="http://www.w3.org/2000/svg" width="133" height="133" viewBox="0 0 133 133"><defs><radialGradient id="f"><stop stop-color="$($colors[$kind])" stop-opacity=".03"/><stop offset="1" stop-color="$($colors[$kind])" stop-opacity=".12"/></radialGradient></defs><circle cx="66.5" cy="66.5" r="64" fill="url(#f)"/><circle cx="66.5" cy="66.5" r="64" fill="none" stroke="$($colors[$kind])" stroke-width="1" stroke-dasharray="4 4" vector-effect="non-scaling-stroke"/></svg>
"@
[IO.File]::WriteAllText((Join-Path $out "rings/radius_$kind`_scalable.svg"),$svg,[Text.UTF8Encoding]::new($false))}
# 현재 게임 타일로 재조합한 지도 견본이며 실제 실행 화면이 아니다.
$tiles=@{};foreach($id in @('grass_a','grass_b','path_5','trees','rocks','hill_1','castle_perimeter','trap_buried')){$tiles[$id]=[Drawing.Bitmap]::FromFile((Join-Path $root "Game/Assets/_Project/Art/UI/Resources/TrapMap/$id.png"))}
$map=[K094Art]::Canvas(768,576);for($y=0;$y -lt 18;$y++){for($x=0;$x -lt 24;$x++){$id=if(($x*3+$y*7)%5 -lt 3){'grass_a'}else{'grass_b'};if($x -eq 12 -and ($y -lt 8 -or $y -gt 15)){$id='path_5'};[K094Art]::Blit($map,$tiles[$id],($x*32),($y*32))}}
foreach($p in @(@(2,2),@(3,3),@(20,2),@(21,4),@(2,12),@(19,13),@(21,14))){[K094Art]::Blit($map,$tiles['trees'],($p[0]*32),($p[1]*32))}
foreach($p in @(@(5,3),@(19,5),@(4,14))){[K094Art]::Blit($map,$tiles['rocks'],($p[0]*32),($p[1]*32))}
[K094Art]::Blit($map,$tiles['castle_perimeter'],256,256);Save-Png $map 'map_base_current_tiles.png'
$mock=$map.Clone();$placements=@(@(307,114,'armed','valid_brass'),@(451,177,'selected','valid_green'),@(569,284,'buried','valid_brass'),@(184,371,'armed','valid_green'),@(662,145,'ghost_invalid','invalid'))
foreach($p in $placements){$ring=[Drawing.Bitmap]::FromFile((Join-Path $out "rings/radius_$($p[3])_r64_combined.png"));[K094Art]::Blit($mock,$ring,($p[0]-66),($p[1]-66));$idx=[Array]::IndexOf($names,$p[2]);[K094Art]::Blit($mock,$icons[$idx],($p[0]-12),($p[1]-12));$ring.Dispose()};Save-Png $mock 'mockup_free_placement.png'
foreach($i in $icons){$i.Dispose()};foreach($t in $tiles.Values){$t.Dispose()};$source.Dispose();$small.Dispose();$cloud.Dispose();$sheet.Dispose();$native.Dispose();$game.Dispose();$dustSheet.Dispose();$dustNative.Dispose();$dg.Dispose();$map.Dispose();$mock.Dispose()
Write-Output 'K-094 그림 생성 완료'
