$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Collections.Generic;
public static class K067Check {
 public static int[] Check(string path) {
  var allowed=new HashSet<int>();foreach(string s in new[]{"#150e09","#3a2a1e","#e8d9b5","#c9b48a","#d5b16b","#f5d99c","#8a6a32","#5e4430","#b4533c","#ff9a3c","#ffd75a"})allowed.Add(ColorTranslator.FromHtml(s).ToArgb());
  using(var b=new Bitmap(path)){
   if(b.Width!=32||b.Height!=32)throw new Exception("원본 크기 오류: "+path);
   int minX=32,minY=32,maxX=-1,maxY=-1,count=0;var colors=new HashSet<int>();
   for(int y=0;y<32;y++)for(int x=0;x<32;x++){
    Color c=b.GetPixel(x,y);if(c.A==0)continue;
    if(c.A!=255)throw new Exception("반투명 점 발견: "+path);
    if(x==0||y==0||x==31||y==31)throw new Exception("가장자리 여백 오류: "+path);
    if(!allowed.Contains(c.ToArgb()))throw new Exception("팔레트 밖 색상: "+path);
    minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);count++;colors.Add(c.ToArgb());
   }
   if(count==0)throw new Exception("빈 그림: "+path);
   if(Math.Abs((minX+maxX)/2.0-15.5)>2.5||Math.Abs((minY+maxY)/2.0-15.5)>2.5)throw new Exception("중앙 배치 오류: "+path);
   return new[]{minX,minY,maxX,maxY,count,colors.Count};
  }
 }
 public static void Scale(string native,string preview,int f) {
  using(var a=new Bitmap(native))using(var b=new Bitmap(preview)){
   if(b.Width!=32*f||b.Height!=32*f)throw new Exception("확대본 크기 오류: "+preview);
   for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(a.GetPixel(x/f,y/f).ToArgb()!=b.GetPixel(x,y).ToArgb())throw new Exception("최근접 확대 불일치: "+preview);
  }
 }
 public static void Emblem(string first,string other) {
  using(var a=new Bitmap(first))using(var b=new Bitmap(other))for(int y=0;y<25;y++)for(int x=0;x<32;x++)if(a.GetPixel(x,y).ToArgb()!=b.GetPixel(x,y).ToArgb())throw new Exception("훈련 병종 문양 불일치: "+other);
 }
}
'@
$entries=Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'manifest.json') | ConvertFrom-Json
if ($entries.Count -ne 26) { throw '그림 수가 26종이 아닙니다' }
$report=@()
foreach ($e in $entries) {
    $path=Join-Path $PSScriptRoot "$($e.id).png"
    $check=[K067Check]::Check($path)
    foreach ($f in 2,4) { [K067Check]::Scale($path,(Join-Path $PSScriptRoot "preview_${f}x/$($e.id).png"),$f) }
    $report += [pscustomobject]@{id=$e.id;name=$e.name;bounds=@($check[0..3]);opaquePixels=$check[4];paletteColors=$check[5];size='32×32';alpha='0 또는 255';margin='1px 이상';scales='2배·4배 점 일치'}
}
foreach ($class in 'archer','gunner','cannon_crew') {
    foreach ($rank in 2,3) { [K067Check]::Emblem((Join-Path $PSScriptRoot "training_${class}_1.png"),(Join-Path $PSScriptRoot "training_${class}_${rank}.png")) }
}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$actual=@(foreach ($group in 'Items','Training','Mercenaries') { (Get-ChildItem (Join-Path $root "Game/Assets/_Project/Data/Meta/$group") -Filter *.asset).BaseName })
if (@(Compare-Object ($entries | Where-Object group -ne '공용 표시').id $actual).Count -ne 0) { throw '데이터 품목과 아이콘 목록이 다릅니다' }
$hashes=$entries | ForEach-Object { (Get-FileHash (Join-Path $PSScriptRoot "$($_.id).png") -Algorithm SHA256).Hash }
if (@($hashes | Sort-Object -Unique).Count -ne 26) { throw '중복된 원본 그림이 있습니다' }
[pscustomobject]@{result='통과';nativeCount=26;previewCount=52;dataEntries=22;sharedMarks=4;trainingEmblems='단계별 상단 문양 동일';uniqueImages=26;icons=$report} | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'qa_assets.json')
Write-Output '26종 크기·투명도·팔레트·여백·중앙 배치, 확대본 52개, 훈련 문양, 데이터 대응 검사 통과'
