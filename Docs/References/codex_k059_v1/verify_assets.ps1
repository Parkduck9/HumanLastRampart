$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class K059Check {
 public static string Inspect(string path,bool binary,bool palette){
  HashSet<int> alphas=new HashSet<int>();HashSet<string> colors=new HashSet<string>();int opaque=0,transparent=0,other=0;
  var allowed=new HashSet<string>(new[]{"150e09","3a2a1e","e8d9b5","c9b48a","3b2a1f","5e4430","23170f","d5b16b","f5d99c","8a6a32","b4533c","7fa35a"});
  using(Bitmap b=new Bitmap(path)){
   if(b.PixelFormat!=PixelFormat.Format32bppArgb)throw new Exception("RGBA 형식 오류: "+path);
   for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){Color c=b.GetPixel(x,y);alphas.Add(c.A);if(c.A==255)opaque++;else if(c.A==0)transparent++;else other++;if(c.A>0)colors.Add(c.R.ToString("x2")+c.G.ToString("x2")+c.B.ToString("x2"));}
   if(binary&&other>0)throw new Exception("이진 알파 오류: "+path);
   if(palette)foreach(string c in colors)if(!allowed.Contains(c))throw new Exception("팔레트 오류: "+path+" "+c);
  }
  return "{\"불투명\":"+opaque+",\"투명\":"+transparent+",\"반투명\":"+other+",\"색수\":"+colors.Count+"}";
 }
 static void Nine(Graphics g,Bitmap b,int x,int y,int w,int h,int[] border,bool tile){
  int l=border[0],bt=border[1],r=border[2],t=border[3];int[] sx={0,l,b.Width-r},sy={0,t,b.Height-bt},sw={l,b.Width-l-r,r},sh={t,b.Height-t-bt,bt},dx={x,x+l,x+w-r},dy={y,y+t,y+h-bt},dw={l,w-l-r,r},dh={t,h-t-bt,bt};
  for(int row=0;row<3;row++)for(int col=0;col<3;col++){
   if(tile&&row==1&&col==1){for(int yy=0;yy<dh[row];yy+=sh[row])for(int xx=0;xx<dw[col];xx+=sw[col]){int cw=Math.Min(sw[col],dw[col]-xx),ch=Math.Min(sh[row],dh[row]-yy);g.DrawImage(b,new Rectangle(dx[col]+xx,dy[row]+yy,cw,ch),sx[col],sy[row],cw,ch,GraphicsUnit.Pixel);}}
   else g.DrawImage(b,new Rectangle(dx[col],dy[row],dw[col],dh[row]),sx[col],sy[row],sw[col],sh[row],GraphicsUnit.Pixel);
  }
 }
 public static void Proof(string dir){
  using(Bitmap small=new Bitmap(480,320,PixelFormat.Format32bppArgb))using(Graphics g=Graphics.FromImage(small)){
   g.Clear(ColorTranslator.FromHtml("#23170f"));g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
   string[] ns={"panel_parchment","panel_wood","panel_band","bubble"};
   for(int i=0;i<4;i++)using(Bitmap b=new Bitmap(Path.Combine(dir,ns[i]+".png"))){int p=i==3?8:6;Nine(g,b,8+(i%2)*240,8+(i/2)*104,224,88,new[]{p,p,p,p},i!=3);}
   for(int m=0;m<2;m++)for(int j=0;j<3;j++)using(Bitmap b=new Bitmap(Path.Combine(dir,"button_"+(m==0?"wood":"brass")+(j==2?"_down":"")+".png"))){Nine(g,b,8+j*80,224+m*32,72,24,new[]{4,4,4,4},false);if(j==1)using(Bitmap glow=new Bitmap(Path.Combine(dir,"frame_glow.png")))Nine(g,glow,8+j*80,224+m*32,72,24,new[]{4,4,4,4},false);}
   using(Bitmap b=new Bitmap(Path.Combine(dir,"slip.png")))Nine(g,b,264,224,192,24,new[]{4,4,6,6},false);
   using(Bitmap b=new Bitmap(Path.Combine(dir,"slip_danger.png")))Nine(g,b,264,256,192,24,new[]{6,4,6,6},false);
   using(Bitmap proof=new Bitmap(1440,960))using(Graphics pg=Graphics.FromImage(proof)){pg.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;pg.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;pg.DrawImage(small,new Rectangle(0,0,1440,960));proof.Save(Path.Combine(dir,"proof_ui_3x.png"),ImageFormat.Png);}
  }
 }
}
'@
$records = Get-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
$checks = @()
$sizes = @{ panel_parchment='32x32'; panel_wood='32x32'; panel_band='32x32'; plate='16x16'; slip='20x14'; slip_danger='20x14'; bubble='32x32'; bubble_tail='12x9'; button_wood='12x12'; button_wood_down='12x12'; button_brass='12x12'; button_brass_down='12x12'; frame_glow='12x12'; keycap='10x11'; bar_track='8x8'; seal='32x32'; vignette='64x36'; story_box='96x40'; story_nameplate='48x18'; story_button='40x16'; story_button_hover='40x16'; story_button_on='40x16'; story_fade_edge='64x64' }
foreach ($a in $records) {
 $path = Join-Path $PSScriptRoot ($a.name + '.png')
 $isIcon = $a.name.StartsWith('icon_')
 $isFrame = $null -ne $a.border
 $binary = ($isIcon -or $isFrame -or $a.name -eq 'bubble_tail' -or $a.name.StartsWith('story_next_')) -and $a.name -ne 'frame_glow'
 $palette = ($isFrame -or $a.name -eq 'bubble_tail' -or $a.name.StartsWith('story_next_')) -and $a.name -ne 'frame_glow'
 $stats = [K059Check]::Inspect($path, $binary, $palette) | ConvertFrom-Json
 $size = $a.w.ToString()+'x'+$a.h
 $expected = if ($sizes.ContainsKey($a.name)) { $sizes[$a.name] } elseif ($a.name -eq 'icon_speed1') { '6x11' } elseif ($a.name -eq 'icon_speed2') { '12x11' } elseif ($a.name -eq 'icon_speed4') { '15x11' } elseif ($a.name.StartsWith('icon_')) { '11x11' } elseif ($a.name.StartsWith('story_next_')) { '7x5' } elseif ($a.kind -eq '초상') { '1024x384' } else { '640x360' }
 if ($size -ne $expected) { throw ('요청 규격 불일치: '+$a.name+' '+$size+' / '+$expected) }
 if ($a.border -and ($a.border[0]+$a.border[2] -ge $a.w -or $a.border[1]+$a.border[3] -ge $a.h)) { throw ('9칸 가운데 오류 ' + $a.name) }
 $checks += [ordered]@{이름=$a.name;규격=($a.w.ToString()+'x'+$a.h);이진알파검사=$binary;팔레트검사=$palette;픽셀=$stats}
}
if (@($records | Where-Object kind -eq 'UI').Count -ne 36) { throw 'UI 이름 수 오류' }
if (@($records | Where-Object kind -eq '스토리 부품').Count -ne 10) { throw '스토리 이름 수 오류' }
if (@($records | Where-Object kind -eq '초상' | Where-Object { $_.w -ne 1024 -or $_.h -ne 384 }).Count -ne 0) { throw '초상 규격 오류' }
[K059Check]::Proof($PSScriptRoot)
$qa = [ordered]@{결과='파일 검사 통과';납품PNG수=$records.Count;직접확인='별도 기록 참조';브라우저실행='연결 가능한 브라우저 없음';유니티실행='수행하지 않음';검사=$checks}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'qa_assets.json'), (ConvertTo-Json -Depth 8 -InputObject $qa), [Text.UTF8Encoding]::new($false))
Write-Output ('파일 검사 통과: '+$records.Count+'종')
