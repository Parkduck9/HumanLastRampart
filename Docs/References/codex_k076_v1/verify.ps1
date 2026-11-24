$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;using System.Drawing;using System.Collections.Generic;
public class SheetCheck {
 public static int[] Verify(string native,string game,int count){
  using(var n=new Bitmap(native))using(var g=new Bitmap(game)){
   if(n.Width!=96*count||n.Height!=128||g.Width!=288*count||g.Height!=384)throw new Exception("크기 오류");
   var feet=new List<int>();
   for(int f=0;f<count;f++){
    int bottom=0;int components=0;bool[,] seen=new bool[96,128];
    for(int y=0;y<128;y++)for(int x=0;x<96;x++){
     Color p=n.GetPixel(f*96+x,y);if(p.A!=0&&p.A!=255)throw new Exception("중간 알파");
     if(p.A>0){bottom=Math.Max(bottom,y+1);if(x==0||x==95||y==0||y==127)throw new Exception("가장자리 접촉");}
     for(int dy=0;dy<3;dy++)for(int dx=0;dx<3;dx++)if(g.GetPixel((f*96+x)*3+dx,y*3+dy).ToArgb()!=p.ToArgb())throw new Exception("최근접 확대 불일치");
     if(p.A>0&&!seen[x,y]){components++;var q=new Queue<Point>();q.Enqueue(new Point(x,y));seen[x,y]=true;while(q.Count>0){var at=q.Dequeue();for(int yy=-1;yy<=1;yy++)for(int xx=-1;xx<=1;xx++){int nx=at.X+xx,ny=at.Y+yy;if(nx>=0&&nx<96&&ny>=0&&ny<128&&!seen[nx,ny]&&n.GetPixel(f*96+nx,ny).A>0){seen[nx,ny]=true;q.Enqueue(new Point(nx,ny));}}}}
    }
    if(bottom!=120||components!=1)throw new Exception("발선 또는 고립 영역 오류");feet.Add(bottom);
   }return feet.ToArray();
  }
 }
}
'@
$m=Get-Content -Raw -Encoding UTF8 "$PSScriptRoot/manifest.json"|ConvertFrom-Json
$results=@();foreach($s in $m.sheets){$feet=[SheetCheck]::Verify("$PSScriptRoot/$($s.native)","$PSScriptRoot/$($s.name)",$s.frames);$results+=@{name=$s.name;frames=$s.frames;baseline=@($feet);binary_alpha=$true;single_connected_component_per_frame=$true;nearest_3x_exact=$true}}
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
foreach($s in (Get-Content -Raw -Encoding UTF8 "$PSScriptRoot/source_hashes.json"|ConvertFrom-Json)){if((Get-FileHash "$root/$($s.path)" -Algorithm SHA256).Hash -ne $s.sha256){throw '게임 원본 해시 변경'}}
$page=[IO.File]::ReadAllText("$root/_HTML/4_그림_캐릭터/codex_K-076_마법사동작_v1.html")
$images=[regex]::Matches($page,'data:image/png;base64,([A-Za-z0-9+/=]+)');foreach($match in $images){$stream=[IO.MemoryStream]::new([Convert]::FromBase64String($match.Groups[1].Value));$im=[Drawing.Image]::FromStream($stream);$im.Dispose();$stream.Dispose()}
@{sheets=$results;embedded_png_decodes=$images.Count;game_source_hashes_unchanged=$true;browser_visual_check='미실시: 연결 가능한 브라우저 없음';unity='미실시'}|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 "$PSScriptRoot/validation.json"
Write-Output "PNG 검증 통과, 내장 이미지 $($images.Count)개 디코딩 통과"
