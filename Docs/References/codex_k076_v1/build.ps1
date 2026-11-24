$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
public class WizardPack {
 public static Bitmap Scale(Bitmap a,int k) {
  Bitmap b=new Bitmap(a.Width*k,a.Height*k);
  for(int y=0;y<b.Height;y++) for(int x=0;x<b.Width;x++) b.SetPixel(x,y,a.GetPixel(x/k,y/k));
  return b;
 }
 public static void Save(Bitmap a,string p) {a.Save(p,ImageFormat.Png);}
 public static List<int[]> Build(string source,string dest) {
  Bitmap a=new Bitmap(source); var frames=new List<Bitmap>(); var metrics=new List<int[]>();
  string[] hex={"17121c","291828","402044","542361","703482","9955a3","c582bd","48251c","713923","9b502c","bd6b36","e28c4c","f5b76a","895c31","c48d35","e9bb48","ffe384","402f2d","685049","e3a173","ffca91","ffe5b7","14365d","1f6395","339fce","7ce1f5","edfaff"};
  var pal=new List<Color>(); foreach(string h in hex) pal.Add(Color.FromArgb(255,Convert.ToInt32(h.Substring(0,2),16),Convert.ToInt32(h.Substring(2,2),16),Convert.ToInt32(h.Substring(4,2),16)));
  for(int i=0;i<10;i++) {
   int l=i%5*a.Width/5,r=(i%5+1)*a.Width/5,t=i<5?0:a.Height*54/100,b=i<5?a.Height*54/100:a.Height;
   int top=b,bottom=t,hleft=r,hright=l;
   for(int y=t;y<b;y++) for(int x=l;x<r;x++) {
    Color c=a.GetPixel(x,y); if(c.A<180)continue;
    if(y>t+(b-t)*.55)bottom=Math.Max(bottom,y+1);
    if(x>l+(r-l)*.20 && x<l+(r-l)*.67 && y<t+(b-t)*.73 && c.R>75 && c.R>c.G*1.30 && c.G>c.B*1.35)top=Math.Min(top,y);
   }
   int hh=(int)((bottom-top)*.31);
   for(int y=top;y<top+hh;y++) for(int x=l;x<r;x++) {
    Color c=a.GetPixel(x,y); if(c.A>=180 && c.R>75 && c.R>c.G*1.30 && c.G>c.B*1.35) {hleft=Math.Min(hleft,x);hright=Math.Max(hright,x+1);}
   }
   double k=73.0/(bottom-top),axis=(hleft+hright)/2.0;
   Bitmap f=new Bitmap(96,128);
   for(int y=0;y<128;y++) for(int x=0;x<96;x++) {
    int sx=(int)Math.Floor(axis+(x-48+.5)/k),sy=(int)Math.Floor(bottom+(y-120+.5)/k);
    if(sx<l||sx>=r||sy<t||sy>=b)continue;
    Color c=a.GetPixel(sx,sy); if(c.A<180)continue;
    int best=int.MaxValue;Color chosen=pal[0];
    foreach(Color p in pal) {int d=2*(c.R-p.R)*(c.R-p.R)+3*(c.G-p.G)*(c.G-p.G)+2*(c.B-p.B)*(c.B-p.B); if(d<best){best=d;chosen=p;}}
    f.SetPixel(x,y,chosen);
   }
   // 알파 경계의 고립된 생성 잔점을 지우고 연결된 인물과 지팡이만 남긴다.
   bool[,] seen=new bool[96,128]; var groups=new List<List<Point>>();
   for(int y=0;y<128;y++)for(int x=0;x<96;x++)if(!seen[x,y]&&f.GetPixel(x,y).A>0) {
    var q=new Queue<Point>();var g=new List<Point>();q.Enqueue(new Point(x,y));seen[x,y]=true;
    while(q.Count>0){var p=q.Dequeue();g.Add(p);for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int nx=p.X+dx,ny=p.Y+dy;if(nx>=0&&nx<96&&ny>=0&&ny<128&&!seen[nx,ny]&&f.GetPixel(nx,ny).A>0){seen[nx,ny]=true;q.Enqueue(new Point(nx,ny));}}}
    groups.Add(g);
   }
   int largest=0; foreach(var g in groups)largest=Math.Max(largest,g.Count); foreach(var g in groups)if(g.Count<largest)foreach(var p in g)f.SetPixel(p.X,p.Y,Color.Transparent);
   int minX=96,minY=128,maxX=0,maxY=0,count=0;
   for(int y=0;y<128;y++)for(int x=0;x<96;x++)if(f.GetPixel(x,y).A>0){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x+1);maxY=Math.Max(maxY,y+1);count++;}
   if(maxY!=120||minX==0||maxX==96||minY==0)throw new Exception("발선/여백 오류: "+i);
   metrics.Add(new int[]{i,l,t,r,b,top,bottom,(int)axis,minX,minY,maxX,maxY,count});frames.Add(f);
  }
  string[] names={"wizard_static","wizard_back_static","wizard_cast","wizard_walk"};
  int[][] map={new int[]{0},new int[]{1},new int[]{2,3,4,5},new int[]{6,7,8,9}};
  for(int n=0;n<4;n++) {
   Bitmap sheet=new Bitmap(map[n].Length*96,128);
   for(int j=0;j<map[n].Length;j++)for(int y=0;y<128;y++)for(int x=0;x<96;x++)sheet.SetPixel(j*96+x,y,frames[map[n][j]].GetPixel(x,y));
   Save(sheet,dest+"/native/"+names[n]+".png");using(var game=Scale(sheet,3))Save(game,dest+"/"+names[n]+".png");using(var preview=Scale(sheet,4))Save(preview,dest+"/"+names[n]+"_4x.png");
   sheet.Dispose();
  }
  foreach(var f in frames)f.Dispose();a.Dispose();return metrics;
 }
}
'@
New-Item -ItemType Directory -Force "$PSScriptRoot/native" | Out-Null
$metrics=[WizardPack]::Build("$PSScriptRoot/master_generated.png",$PSScriptRoot)
$metrics | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 "$PSScriptRoot/measurements.json"
$names=@('wizard_static','wizard_back_static','wizard_cast','wizard_walk')
$labels=@('정면 대기','후면 대기','시전','옆 걷기')
$orders=@(@('대기'),@('대기'),@('지팡이 들기','빛 모으기','방출','정리'),@('오른발 착지','왼발 통과','왼발 착지','오른발 통과'))
$records=@(); $htmlCards=''; $checks=@()
for($i=0;$i -lt 4;$i++) {
 $name=$names[$i];$count=@($orders[$i]).Count
 $current=Join-Path $root "Game/Assets/_Project/Art/Characters/Static/$name.png"
 $old=$null
 if(Test-Path $current) {
  $im=[Drawing.Bitmap]::new($current); $old=@{url='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes($current));cell=@(256,384);count=$count};$im.Dispose()
  $checks+=@{path=$current.Substring($root.Length+1);sha256=(Get-FileHash $current -Algorithm SHA256).Hash}
 }
 $records+=@{name="$name.png";native="native/$name.png";preview="${name}_4x.png";frames=$count;order=@($orders[$i]);native_cell=@(96,128);game_cell=@(288,384);native_pivot_top_left=@(48,120);game_pivot_top_left=@(144,360);normalized_unity_pivot=@(0.5,0.0625);target="Art/Characters/Static/$name.png"}
 $new='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes("$PSScriptRoot/native/$name.png"))
 $json=@{name=$labels[$i];order=@($orders[$i]);url=$new;cell=@(96,128);count=$count;old=$old}|ConvertTo-Json -Compress -Depth 8
 $htmlCards+="<section><h2>$($labels[$i])</h2><div class='pair' data-config='$json'><div><h3>새 그림 · 원본 3배 표시</h3><canvas class='new' width='288' height='384'></canvas></div><div><h3>현재 게임 시트 · 원래 크기</h3>"+$(if($old){"<canvas class='old' width='288' height='384'></canvas>"}else{"<p>기존 걷기 시트 없음</p>"})+"</div></div><p class='phase'></p><details><summary>전체 프레임 · 4배</summary><img alt='$($labels[$i]) 전체 프레임' src='data:image/png;base64,$([Convert]::ToBase64String([IO.File]::ReadAllBytes("$PSScriptRoot/${name}_4x.png")))'></details></section>"
}
@{card='K-076';decision='D-080 정정 / D-081';figure_height=73;baseline_convention='발바닥 아래 경계 y=120, 최하단 불투명 행 y=119';frame_direction='왼쪽에서 오른쪽';sheets=$records;source='master_generated.png';tool='내장 image_gen';notes='Game/에 복사하지 않음. Claude가 칸 크기와 시전 동작을 연결한다.'}|ConvertTo-Json -Depth 10|Set-Content -Encoding UTF8 "$PSScriptRoot/manifest.json"
$checks|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 "$PSScriptRoot/source_hashes.json"
$page=@"
<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>K-076 마법사 동작 검토</title>
<style>body{background:#161a24;color:#eee;font:16px/1.65 system-ui;margin:24px auto;max-width:1120px;padding:0 20px}h1,h2,h3{line-height:1.3}section{background:#242b3a;border-radius:12px;padding:20px;margin:24px 0}.pair{display:flex;flex-wrap:wrap;gap:40px}canvas{image-rendering:pixelated;background:repeating-conic-gradient(#303a49 0% 25%,#394554 0% 50%) 0/24px 24px;border-bottom:2px solid #79b899}img{image-rendering:pixelated;max-width:100%;height:auto}button,input{font:inherit;margin:6px;padding:6px}small{color:#c2cede}.phase{min-height:1.7em}details{overflow:auto}</style>
<h1>K-076 · 마법사 동작 1판</h1><p>확정된 인물 키 약 73점, 큰 머리와 파란 눈, 밤색 머리·보라색 금테 로브·보석 지팡이. 원본 칸 96×128 → 게임 칸 288×384. 발바닥 아래 경계는 원본 y=120입니다.</p><p>새 그림과 기존 게임 시트를 같은 발선에 나란히 표시합니다. 걷기는 오른쪽 방향입니다. 실제 게임 연결과 Unity 재생 확인은 아직 수행하지 않았습니다.</p><button id="toggle">일시 정지</button><label>재생 속도 <input id="speed" type="range" min="1" max="10" value="4"> <span id="fps">4</span>프레임/초</label><button id="step">다음 프레임</button><p id="status">그림 불러오는 중</p>
$htmlCards
<script>
const rows=[];let playing=true,frame=0,last=0;window.reviewCheck={loaded:0,errors:[],draws:0};
function load(url){return new Promise((resolve,reject)=>{const i=new Image();i.onload=()=>{reviewCheck.loaded++;resolve(i)};i.onerror=()=>{reviewCheck.errors.push('그림 읽기 실패');reject(Error('그림 읽기 실패'))};i.src=url})}
function paint(){for(const r of rows){const f=frame%r.c.count;for(const [canvas,img,cell] of [[r.el.querySelector('.new'),r.img,r.c.cell],[r.el.querySelector('.old'),r.old,r.c.old?.cell]]){if(!canvas||!img)continue;const ctx=canvas.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.clearRect(0,0,288,384);const scale=cell[0]===96?3:1;const w=cell[0]*scale,h=cell[1]*scale;ctx.drawImage(img,f*cell[0],0,...cell,(288-w)/2,360-(cell[0]===96?120*scale:352),w,h);ctx.strokeStyle='#79b899';ctx.beginPath();ctx.moveTo(0,360.5);ctx.lineTo(288,360.5);ctx.stroke()}r.el.parentElement.querySelector('.phase').textContent=(f+1)+' / '+r.c.count+' · '+r.c.order[f]}reviewCheck.draws++}
Promise.all([...document.querySelectorAll('[data-config]')].map(async el=>{const c=JSON.parse(el.dataset.config);rows.push({el,c,img:await load(c.url),old:c.old?await load(c.old.url):null})})).then(()=>{paint();document.querySelector('#status').textContent='그림 '+reviewCheck.loaded+'개 읽기 완료 · 발선 표시'}).catch(e=>document.querySelector('#status').textContent=e.message);
document.querySelector('#toggle').onclick=e=>{playing=!playing;e.target.textContent=playing?'일시 정지':'재생'};document.querySelector('#step').onclick=()=>{playing=false;document.querySelector('#toggle').textContent='재생';frame++;paint()};document.querySelector('#speed').oninput=e=>document.querySelector('#fps').textContent=e.target.value;
function tick(t){if(playing&&t-last>=1000/Number(document.querySelector('#speed').value)){last=t;frame++;paint()}requestAnimationFrame(tick)}requestAnimationFrame(tick);
</script></html>
"@
[IO.File]::WriteAllText((Join-Path $root '_HTML/4_그림_캐릭터/codex_K-076_마법사동작_v1.html'),$page,[Text.UTF8Encoding]::new($false))
Write-Output '시트 4종과 내장 검토 HTML 생성 완료'
