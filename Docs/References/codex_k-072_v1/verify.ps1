$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
public static class K072Check {
    public static int[][] Check(string file,string native,int frames,int baseY) {
        using(Bitmap b=new Bitmap(file))using(Bitmap n=new Bitmap(native)) {
            int h=frames==1?128:256;
            if(b.Width!=256*frames||b.Height!=h||n.Width!=64*frames||n.Height!=h/4)throw new Exception("크기 불일치: "+file);
            int[][] all=new int[frames][];
            for(int f=0;f<frames;f++) {
                int l=64,t=h/4,r=-1,d=-1,count=0,components=0;
                bool[,] seen=new bool[64,h/4];
                for(int y=0;y<h/4;y++)for(int x=0;x<64;x++) {
                    Color c=n.GetPixel(f*64+x,y);if(c.A!=0&&c.A!=255)throw new Exception("중간 알파: "+file);
                    if(c.A>0){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);d=Math.Max(d,y);count++;}
                    if(c.A==0||seen[x,y])continue;
                    int size=0;Queue<Point> q=new Queue<Point>();q.Enqueue(new Point(x,y));seen[x,y]=true;
                    while(q.Count>0){Point p=q.Dequeue();size++;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int nx=p.X+dx,ny=p.Y+dy;if(nx<0||nx>=64||ny<0||ny>=h/4||seen[nx,ny]||n.GetPixel(f*64+nx,ny).A==0)continue;seen[nx,ny]=true;q.Enqueue(new Point(nx,ny));}}
                    if(size==1)throw new Exception("단독 한 점: "+file+" "+f);components++;
                }
                if(count==0||l==0||r==63||t==0)throw new Exception("빈 프레임 또는 셀 가장자리 접촉: "+file);
                if(frames>1&&d!=baseY-1)throw new Exception("아래 기준선 불일치: "+file+" "+f);
                if(Math.Abs((l+r)/2.0-31.5)>0.5)throw new Exception("가운데 불일치: "+file);
                all[f]=new int[]{l*4,t*4,(r+1)*4-1,(d+1)*4-1,count,components};
            }
            for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).ToArgb()!=n.GetPixel(x/4,y/4).ToArgb())throw new Exception("4배 최근접 확대 불일치: "+file);
            return all;
        }
    }
    public static void Preview(string file,string preview) {
        using(Bitmap a=new Bitmap(file))using(Bitmap b=new Bitmap(preview)) {
            if(b.Width!=a.Width*4||b.Height!=a.Height*4)throw new Exception("미리보기 크기 불일치");
            for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).ToArgb()!=a.GetPixel(x/4,y/4).ToArgb())throw new Exception("미리보기 화소 불일치");
        }
    }
}
'@
$root=$PSScriptRoot
$repo=[IO.Path]::GetFullPath((Join-Path $root '../../..'))
$jobs=Get-Content -Encoding UTF8 -Raw (Join-Path $root 'inputs.json')|ConvertFrom-Json
$results=@()
foreach($j in $jobs){
    $path=Join-Path $root ('sheets/'+$j.name+'.png')
    $bboxes=[K072Check]::Check($path,(Join-Path $root ('native/'+$j.name+'.png')),$j.frames,$j.baseY)
    [K072Check]::Preview($path,(Join-Path $root ('previews/'+$j.name+'_4x.png')))
    $currentSize=$null;$currentHash=$null
    if($j.current){
        $currentPath=Join-Path $repo $j.current
        $im=[Drawing.Bitmap]::FromFile($currentPath)
        $currentSize=@($im.Width,$im.Height);$im.Dispose()
        $wantedH=256;if($j.frames -eq 1){$wantedH=128}
        if($currentSize[0] -ne 256*$j.frames -or $currentSize[1] -ne $wantedH){throw '기존 시트와 크기가 다릅니다.'}
        $currentHash=(Get-FileHash -LiteralPath $currentPath -Algorithm SHA256).Hash.ToLower()
    }
    $frameData=@();$i=0
    foreach($bbox in $bboxes){$frameData+=@{index=$i;rectTopLeft=@(($i*256),0,256,$(if($j.frames -eq 1){128}else{256}));alphaBoundsInclusive=@($bbox[0],$bbox[1],$bbox[2],$bbox[3]);nativeOpaquePixels=$bbox[4];connectedClusters=$bbox[5]};$i++}
    $results+=@{file=('sheets/'+$j.name+'.png');name=$j.name;label=$j.label;sheetSize=@((256*$j.frames),$(if($j.frames -eq 1){128}else{256}));cellSize=@(256,$(if($j.frames -eq 1){128}else{256}));frameCount=$j.frames;layout='left-to-right';frameOrder=$j.motion;pivotTopLeftPx=@(128,$j.pivotY);pivotUnityNormalized=@(0.5,$(if($j.frames -eq 1){0.5}else{1.0-$j.pivotY/256.0}));opaqueBaselineBottomExclusive=$(if($j.frames -gt 1){$j.baseY*4}else{$null});forward=$(if($j.frames -eq 1){'+X'}else{$null});fps=$(if($j.frames -eq 1){0}else{10});playback=$(if($j.name -in @('fire_field','torch_flame')){'loop'}elseif($j.frames -eq 1){'static'}else{'one-shot'});emissive=$j.emissive;sourceGamePath=$j.current;sourceGameSize=$currentSize;sourceGameSha256=$currentHash;newSha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLower();frames=$frameData}
}
$page=Join-Path $repo '_HTML/5_그림_전장/codex_K-072_효과6판_v1.html'
$html=[IO.File]::ReadAllText($page,[Text.Encoding]::UTF8)
if($html -match '__DATA__|__HERO__|__CONTEXT__'){throw 'HTML 치환 누락'}
$count=0
foreach($m in [regex]::Matches($html,'data:image/png;base64,([A-Za-z0-9+/=]+)')){
    $bytes=[Convert]::FromBase64String($m.Groups[1].Value)
    $stream=New-Object IO.MemoryStream(,$bytes)
    $img=[Drawing.Image]::FromStream($stream)
    if($img.Width -lt 1 -or $img.Height -lt 1){throw '내장 이미지 해독 실패'}
    $img.Dispose();$stream.Dispose();$count++
}
if($count -ne 21){throw ('내장 비교 이미지 수 불일치: '+$count)}
if($html -match '(src|href)=["''][^"'']*(https?://|file:|\.\./)'){throw '외부 리소스 참조가 있습니다.'}
$manifest=@{card='K-072';version=1;generator='built-in image_gen';artDirection='D-080 generated.png';nativeScale=4;alpha='binary 0/255';filter='nearest';previewScaleRelativeToGameSheet=4;coordinateSystem='top-left pixels; Unity normalized pivot is bottom-left';effects=$results;verification=@{sheetDimensions='passed';matchesCurrentSizesAndFrameCounts='passed';framePivotAndBottomBaseline='passed';nativeGrid4x='passed';previewGrid4x='passed';binaryAlpha='passed';noIsolatedNativePixels='passed';embeddedPngDecodeCount=$count;externalResources=0;browserRuntime='not run: no available browsers in cua inventory';unity='not run'}}
[IO.File]::WriteAllText((Join-Path $root 'manifest.json'),(ConvertTo-Json -Depth 12 -InputObject $manifest),(New-Object Text.UTF8Encoding $false))
Write-Output ('검증 통과: '+$results.Count+'종, 내장 PNG '+$count+'개. 브라우저 실행 및 Unity 검증은 미실행.')
