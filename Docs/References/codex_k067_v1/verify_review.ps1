$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Net.WebSockets;
using System.Threading;
using System.IO;
public class K067Cdp : IDisposable {
 ClientWebSocket ws=new ClientWebSocket();
 public K067Cdp(string url){ws.ConnectAsync(new Uri(url),CancellationToken.None).GetAwaiter().GetResult();}
 public string Call(string request,int id){
  var token=new CancellationTokenSource(15000);byte[] bytes=Encoding.UTF8.GetBytes(request);
  ws.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,token.Token).GetAwaiter().GetResult();
  while(true){using(var stream=new MemoryStream()){
   var buffer=new byte[65536];WebSocketReceiveResult part;
   do{part=ws.ReceiveAsync(new ArraySegment<byte>(buffer),token.Token).GetAwaiter().GetResult();stream.Write(buffer,0,part.Count);}while(!part.EndOfMessage);
   string message=Encoding.UTF8.GetString(stream.ToArray());
   if(message.Contains("\"id\":"+id+",")||message.Contains("\"id\":"+id+"}"))return message;
  }}
 }
 public void Dispose(){ws.Dispose();}
}
'@
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$page=Join-Path $root '_HTML/6_그림_화면/codex_K-067_강화아이콘_v1.html'
$profile=Join-Path $env:TEMP "codex-k067-check-$PID"
$browser=Start-Process 'C:/Program Files/Google/Chrome/Application/chrome.exe' -WindowStyle Hidden -PassThru -ArgumentList @('--headless','--no-sandbox','--disable-gpu','--disable-software-rasterizer','--disable-extensions','--no-first-run','--no-default-browser-check','--remote-debugging-port=9678',"--user-data-dir=$profile",([uri]$page).AbsoluteUri)
$cdp=$null
try {
    $targets=$null
    for ($i=0;$i -lt 30;$i++) { try { $targets=Invoke-RestMethod 'http://127.0.0.1:9678/json'; break } catch { Start-Sleep -Milliseconds 200 } }
    if (!$targets) { throw '검사용 브라우저에 연결할 수 없습니다' }
    $target=$targets | Where-Object { $_.type -eq 'page' -and $_.url.StartsWith('file:') } | Select-Object -First 1
    $cdp=New-Object K067Cdp($target.webSocketDebuggerUrl)
    $script:sequence=0
    function Call([string]$method,$parameters) {
        $script:sequence++
        $request=@{id=$script:sequence;method=$method;params=$parameters} | ConvertTo-Json -Depth 12 -Compress
        $response=$cdp.Call($request,$script:sequence) | ConvertFrom-Json
        if ($response.error) { throw ($response.error | ConvertTo-Json -Compress) }
        return $response.result
    }
    function Evaluate([string]$expression) {
        $r=Call 'Runtime.evaluate' @{expression=$expression;returnByValue=$true;awaitPromise=$true}
        if ($r.exceptionDetails) { throw ($r.exceptionDetails | ConvertTo-Json -Depth 6 -Compress) }
        return $r.result.value
    }
    $null=Call 'Page.enable' @{}
    $null=Call 'Runtime.enable' @{}
    $null=Evaluate "Promise.all([...document.images].map(i=>i.decode())).then(()=>true)"
    $report=@{}
    $report.basic=Evaluate "JSON.stringify({language:document.documentElement.lang,cards:document.querySelectorAll('.card').length,samples:document.querySelectorAll('.samples img').length,broken:[...document.images].filter(i=>!i.complete||!i.naturalWidth).length,external:[...document.querySelectorAll('[src],[href]')].filter(e=>!((e.getAttribute('src')||e.getAttribute('href')).startsWith('data:'))).length,rows:document.querySelectorAll('.shop-row').length})" | ConvertFrom-Json
    if ($report.basic.cards -ne 26 -or $report.basic.samples -ne 156 -or $report.basic.broken -ne 0 -or $report.basic.external -ne 0 -or $report.basic.rows -ne 10) { throw '페이지 구성 검사 실패' }
    $report.filters=Evaluate "JSON.stringify({checks:[...document.querySelectorAll('[data-filter]')].map(b=>{b.click();return {group:b.dataset.filter,count:document.querySelectorAll('.card:not([hidden])').length,pressed:document.querySelectorAll('[aria-pressed=true]').length}})})" | ConvertFrom-Json
    $expected=@(26,11,9,2,4)
    for ($i=0;$i -lt 5;$i++) { if ($report.filters.checks[$i].count -ne $expected[$i] -or $report.filters.checks[$i].pressed -ne 1) { throw '종류 필터 검사 실패' } }
    $null=Evaluate "document.querySelector('[data-filter=전체]').click()"
    $report.layout=@()
    foreach ($width in 1440,390) {
        $null=Call 'Emulation.setDeviceMetricsOverride' @{width=$width;height=1250;deviceScaleFactor=1;mobile=$false}
        $null=Evaluate 'new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)))'
        $layout=Evaluate "JSON.stringify({width:innerWidth,document:document.documentElement.scrollWidth,overflow:[...document.querySelectorAll('.card,.surface,.samples')].filter(e=>e.scrollWidth>e.clientWidth+1).length})" | ConvertFrom-Json
        if ($layout.document -gt $width -or $layout.overflow -gt 0) { throw "가로 넘침 발견: $width" }
        $report.layout += $layout
        $null=Evaluate 'scrollTo(0,0)'
        $shot=Call 'Page.captureScreenshot' @{format='png';captureBeyondViewport=$false}
        $suffix=if ($width -eq 1440) { 'desktop' } else { 'mobile' }
        [IO.File]::WriteAllBytes((Join-Path $PSScriptRoot "review_$suffix.png"),[Convert]::FromBase64String($shot.data))
    }
    $null=Call 'Emulation.setDeviceMetricsOverride' @{width=1440;height=1250;deviceScaleFactor=1;mobile=$false}
    $null=Evaluate "document.querySelector('[data-filter=훈련]').click();document.querySelector('.catalogue').scrollIntoView();new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)))"
    $shot=Call 'Page.captureScreenshot' @{format='png';captureBeyondViewport=$false}
    [IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'review_training.png'),[Convert]::FromBase64String($shot.data))
    $report.result='통과'
    $report | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'qa_review.json')
    Write-Output '로컬 파일 열기, 이미지 156개, 종류 필터 5개, 1440px·390px 배치 검사 통과'
} finally {
    if ($cdp) { try { $null=Call 'Browser.close' @{} } catch {} ; $cdp.Dispose() }
    elseif ($browser -and !$browser.HasExited) { $browser | Stop-Process }
}
