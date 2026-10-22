# 유니티 시험 실행 도구: 결과를 한 줄 요약으로만 출력한다 (토큰 절약)
# 사용 예: .\run-tests.ps1 -Project C:\Unity\1\HLR-work\W17\Game -Mode EditMode -Filter "HLR.Combat"
param(
    [string]$Project = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Game'),
    [ValidateSet('EditMode','PlayMode','All')][string]$Mode = 'All',
    [string]$Filter = '',
    [string]$Assemblies = '',
    [string]$OutDir = '',
    [switch]$Graphics,   # 호환용. 기본이 그래픽 사용이다.
    [switch]$NoGraphics, # 요청할 때만 -nographics 추가
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$Project = (Resolve-Path $Project).Path.TrimEnd('\')
$worktree = Split-Path $Project -Parent
if (-not $OutDir) { $OutDir = Join-Path (Split-Path $worktree -Parent) ((Split-Path $worktree -Leaf) + '_test') }
New-Item -ItemType Directory -Force $OutDir | Out-Null

# 같은 프로젝트를 다른 유니티가 열고 있는지 검사
$busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.ToLower().Replace('"','').Replace('/','\').Contains($Project.ToLower())
}
$lock = Join-Path $Project 'Temp\UnityLockfile'
if (-not $busy -and (Test-Path $lock)) {
    try { $fs = [IO.File]::Open($lock,'Open','ReadWrite','None'); $fs.Close() } catch { $busy = $true }
}
if ($busy) {
    $pidText = if ($busy -is [bool]) { '?' } else { ($busy | ForEach-Object { $_.ProcessId }) -join ',' }
    Write-Output "잠김: 이 프로젝트를 다른 유니티(PID $pidText)가 열고 있음. 끝나길 기다리거나 다른 작업 폴더를 쓰세요: $Project"
    exit 2
}

$modes = if ($Mode -eq 'All') { @('EditMode','PlayMode') } else { @($Mode) }
$anyFail = $false
foreach ($m in $modes) {
    $xml = Join-Path $OutDir "$m.xml"
    $log = Join-Path $OutDir "$m.log"
    Remove-Item $xml,$log -ErrorAction SilentlyContinue
    $a = @('-batchmode','-projectPath',"`"$Project`"",'-runTests','-testPlatform',$m,'-testResults',"`"$xml`"",'-logFile',"`"$log`"")
    if ($NoGraphics) { $a += '-nographics' }
    if ($Filter)     { $a += @('-testFilter',"`"$Filter`"") }
    if ($Assemblies) { $a += @('-assemblyNames',"`"$Assemblies`"") }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    Start-Process -FilePath $Unity -ArgumentList $a -Wait -NoNewWindow | Out-Null
    $sw.Stop()
    $t = [int]$sw.Elapsed.TotalSeconds
    $time = if ($t -ge 60) { "$([int][math]::Floor($t/60))분 $($t%60)초" } else { "${t}초" }

    if (-not (Test-Path $xml)) {
        $anyFail = $true
        Write-Output "$m 결과 없음 (컴파일 오류 등), $time"
        if (Test-Path $log) {
            Get-Content $log | Where-Object { $_ -match 'error|Exception' } | Select-Object -Last 15 | ForEach-Object { Write-Output $_ }
        } else { Write-Output "로그 없음: $log" }
        continue
    }
    [xml]$doc = Get-Content $xml -Raw -Encoding UTF8
    $r = $doc.'test-run'
    $total = [int]$r.total; $passed = [int]$r.passed; $failed = [int]$r.failed
    $skipped = [int]$r.skipped + [int]$r.inconclusive
    Write-Output "$m $passed/$total 통과, 실패 $failed, 건너뜀 $skipped, $time"
    if ($failed -gt 0) {
        $anyFail = $true
        $bad = $doc.SelectNodes("//test-case[@result='Failed']")
        $n = 0
        foreach ($c in $bad) {
            if ($n -ge 10) { break }
            $msg = ''
            $node = $c.SelectSingleNode('failure/message')
            if ($node) { $msg = ($node.InnerText -split "`r?`n" | Where-Object { $_.Trim() } | Select-Object -First 1) }
            if ($msg.Length -gt 160) { $msg = $msg.Substring(0,160) }
            Write-Output "  실패: $($c.fullname) -- $msg"
            $n++
        }
        if ($bad.Count -gt 10) { Write-Output "  ... 외 $($bad.Count - 10)개" }
    }
    Write-Output "  xml: $xml"
    Write-Output "  log: $log"
}
if ($anyFail) { exit 1 } else { exit 0 }