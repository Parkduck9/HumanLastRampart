# Human Last Rampart — 밤 시험 (K-003)
# 사용자가 퇴근할 때 실행: 편집 시험 → 화면 시험 → 측정(Fuzz10, Funnel40Sweep)을 차례로 일괄 실행하고
# 결과를 reports/<날짜시각>/ 에 모아 아침 보고서 report.md · report.html 을 만든다.
# Windows PowerShell 5.1용. 이 파일은 UTF-8 BOM으로 저장한다 (안 그러면 한글이 깨짐).
#
# 예) powershell -NoProfile -ExecutionPolicy Bypass -File Tools\NightTest\run-night.ps1
#     powershell -NoProfile -ExecutionPolicy Bypass -File Tools\NightTest\run-night.ps1 -ProjectPath C:\Unity\1\HumanLastRampart\Game -SkipPlay

param(
    [string]$ProjectPath = 'C:\Unity\1\HumanLastRampart\Game',
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe',
    [int]$TimeoutMinutes = 120,     # 단계 하나가 이 시간을 넘으면 유니티를 끄고 다음 단계로
    [switch]$SkipEdit,
    [switch]$SkipPlay,
    [switch]$SkipProbe
)

$ErrorActionPreference = 'Stop'
$Utf8Bom = New-Object System.Text.UTF8Encoding($true)

function Write-Utf8Bom([string]$Path, [string]$Text) {
    [System.IO.File]::WriteAllText($Path, $Text, $Utf8Bom)
}

function ConvertTo-HtmlText([string]$Text) {
    if ($null -eq $Text) { return '' }
    return [System.Net.WebUtility]::HtmlEncode($Text)
}

function ConvertTo-MdCell([string]$Text) {
    # 표 안에 넣을 때 줄바꿈·세로줄이 표를 깨지 않게
    if ($null -eq $Text) { return '' }
    return ($Text -replace '\r?\n', ' ' -replace '\|', '\|').Trim()
}

# ---------- 시작 전 확인 ----------
if (-not (Test-Path -LiteralPath $UnityPath)) {
    Write-Host "유니티 실행 파일이 없습니다: $UnityPath" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'Assets'))) {
    Write-Host "유니티 프로젝트 폴더가 아닙니다 (Assets 없음): $ProjectPath" -ForegroundColor Red
    exit 1
}
$running = Get-Process -Name Unity -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "유니티가 이미 실행 중입니다 (프로세스 $(@($running).Count)개). 유니티를 모두 닫고 다시 실행하세요." -ForegroundColor Yellow
    exit 1
}

$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$stamp = Get-Date -Format 'yyyy-MM-dd_HHmm'
$ReportDir = Join-Path $PSScriptRoot (Join-Path 'reports' $stamp)
New-Item -ItemType Directory -Force -Path $ReportDir | Out-Null
$ShotSource = Join-Path $ProjectPath 'playprobe'
$NightStart = Get-Date

Write-Host "밤 시험 시작: $NightStart"
Write-Host "프로젝트: $ProjectPath"
Write-Host "결과 폴더: $ReportDir"

# ---------- 유니티 일괄 실행 ----------
function Invoke-UnityStep {
    param(
        [string]$Key,          # 파일 이름에 쓰는 짧은 이름
        [string]$Title,        # 보고서에 쓰는 한글 이름
        [string]$Platform,     # EditMode / PlayMode
        [string]$Filter,       # 비우면 필터 없음
        [string]$ProbeOut      # 비우면 측정 출력 없음
    )

    $xml = Join-Path $ReportDir "$Key-results.xml"
    $log = Join-Path $ReportDir "$Key-log.txt"
    $unityArgs = @(
        '-batchmode',
        '-projectPath', ('"' + $ProjectPath + '"'),
        '-runTests',
        '-testPlatform', $Platform,
        '-testResults', ('"' + $xml + '"'),
        '-logFile', ('"' + $log + '"')
    )
    if ($Filter) { $unityArgs += @('-testFilter', ('"' + $Filter + '"')) }

    $hadProbe = Test-Path Env:HLR_PROBE_OUT
    $oldProbe = $env:HLR_PROBE_OUT
    if ($ProbeOut) { $env:HLR_PROBE_OUT = $ProbeOut }

    $started = Get-Date
    Write-Host ""
    Write-Host "[$($started.ToString('HH:mm:ss'))] $Title 시작"
    $exitCode = $null
    $timedOut = $false
    try {
        $proc = Start-Process -FilePath $UnityPath -ArgumentList ($unityArgs -join ' ') -WorkingDirectory $ProjectPath -PassThru
        if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
            $timedOut = $true
            Write-Host "  $TimeoutMinutes 분을 넘어 유니티를 끕니다." -ForegroundColor Yellow
            try { $proc.Kill() } catch { }
            $proc.WaitForExit()
        }
        $exitCode = $proc.ExitCode
    }
    catch {
        Write-Host "  실행 실패: $($_.Exception.Message)" -ForegroundColor Red
    }
    finally {
        if ($ProbeOut) {
            if ($hadProbe) { $env:HLR_PROBE_OUT = $oldProbe } else { Remove-Item Env:HLR_PROBE_OUT -ErrorAction SilentlyContinue }
        }
    }
    $ended = Get-Date
    Write-Host "[$($ended.ToString('HH:mm:ss'))] $Title 끝 (종료 코드 $exitCode, $([int]($ended - $started).TotalMinutes)분)"

    return [pscustomobject]@{
        Key = $Key; Title = $Title; Platform = $Platform; Filter = $Filter
        Xml = $xml; Log = $log; ProbeOut = $ProbeOut
        ExitCode = $exitCode; TimedOut = $timedOut
        Started = $started; Ended = $ended
    }
}

$steps = @()
if (-not $SkipEdit) {
    $steps += Invoke-UnityStep -Key '1-edit' -Title '편집 시험 (EditMode, SimProbe 제외)' -Platform 'EditMode' -Filter '!SimProbe' -ProbeOut ''
}
$playStart = Get-Date
if (-not $SkipPlay) {
    $steps += Invoke-UnityStep -Key '2-play' -Title '화면 시험 (PlayMode)' -Platform 'PlayMode' -Filter '' -ProbeOut ''
}
if (-not $SkipProbe) {
    $steps += Invoke-UnityStep -Key '3-fuzz10' -Title '측정 SimProbe.Fuzz10' -Platform 'EditMode' -Filter 'SimProbe.Fuzz10' -ProbeOut (Join-Path $ReportDir 'probe-Fuzz10.txt')
    $steps += Invoke-UnityStep -Key '4-funnel40' -Title '측정 SimProbe.Funnel40Sweep' -Platform 'EditMode' -Filter 'SimProbe.Funnel40Sweep' -ProbeOut (Join-Path $ReportDir 'probe-Funnel40Sweep.txt')
}

# ---------- 결과 읽기 ----------
function Read-TestResults($step) {
    $r = [pscustomobject]@{
        Found = $false; Total = 0; Passed = 0; Failed = 0; Skipped = 0; Inconclusive = 0
        Failures = @(); ErrorLines = @()
    }
    if (Test-Path -LiteralPath $step.Xml) {
        try {
            [xml]$doc = Get-Content -LiteralPath $step.Xml -Raw -Encoding UTF8
            $run = $doc.SelectSingleNode('/test-run')
            if ($run) {
                $r.Found = $true
                $r.Total = [int]$run.GetAttribute('total')
                $r.Passed = [int]$run.GetAttribute('passed')
                $r.Failed = [int]$run.GetAttribute('failed')
                $r.Skipped = [int]$run.GetAttribute('skipped')
                $r.Inconclusive = [int]$run.GetAttribute('inconclusive')
            }
            $fails = @()
            foreach ($tc in $doc.SelectNodes("//test-case[@result='Failed']")) {
                $msgNode = $tc.SelectSingleNode('failure/message')
                $stackNode = $tc.SelectSingleNode('failure/stack-trace')
                $msg = ''
                if ($msgNode) { $msg = $msgNode.InnerText.Trim() }
                $stack = ''
                if ($stackNode) { $stack = $stackNode.InnerText.Trim() }
                $fails += [pscustomobject]@{ Name = $tc.GetAttribute('fullname'); Message = $msg; Stack = $stack }
            }
            $r.Failures = $fails
        }
        catch {
            $r.Found = $false
        }
    }
    if (Test-Path -LiteralPath $step.Log) {
        $seen = @{}
        $lines = @()
        foreach ($m in (Select-String -LiteralPath $step.Log -Pattern 'error CS\d+', 'Exception' -Encoding UTF8)) {
            $t = $m.Line.Trim()
            if ($t -and -not $seen.ContainsKey($t)) {
                $seen[$t] = $true
                $lines += ("{0}: {1}" -f $m.LineNumber, $t)
            }
        }
        $r.ErrorLines = $lines
    }
    return $r
}

$results = @()
foreach ($s in $steps) { $results += [pscustomobject]@{ Step = $s; R = (Read-TestResults $s) } }

# 화면 사진: 화면 시험 시작 뒤에 바뀐 Game/playprobe/*.png
$shots = @()
if ((-not $SkipPlay) -and (Test-Path -LiteralPath $ShotSource)) {
    $shots = @(Get-ChildItem -LiteralPath $ShotSource -Filter '*.png' -File | Where-Object { $_.LastWriteTime -ge $playStart } | Sort-Object Name)
    if ($shots.Count -gt 0) {
        $shotDir = Join-Path $ReportDir 'shots'
        New-Item -ItemType Directory -Force -Path $shotDir | Out-Null
        foreach ($f in $shots) { Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $shotDir $f.Name) -Force }
    }
}

function Get-StatusText($x) {
    if ($x.Step.TimedOut) { return '시간 초과' }
    if (-not $x.R.Found) { return '결과 없음' }
    if ($x.R.Failed -gt 0) { return '실패 있음' }
    return '통과'
}

$NightEnd = Get-Date
$MaxErrLines = 80
$MaxProbeLines = 400

# ---------- report.md ----------
$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine('# 밤 시험 아침 보고서')
[void]$md.AppendLine('')
[void]$md.AppendLine("- 시작 $($NightStart.ToString('yyyy-MM-dd HH:mm')) · 끝 $($NightEnd.ToString('yyyy-MM-dd HH:mm')) · 걸린 시간 $([int]($NightEnd - $NightStart).TotalMinutes)분")
[void]$md.AppendLine("- 프로젝트 ``$ProjectPath``")
[void]$md.AppendLine('')
[void]$md.AppendLine('## 시험별 결과')
[void]$md.AppendLine('')
[void]$md.AppendLine('| 시험 | 상태 | 전체 | 통과 | 실패 | 건너뜀 | 종료 코드 | 걸린 시간 |')
[void]$md.AppendLine('|---|---|---|---|---|---|---|---|')
foreach ($x in $results) {
    $min = [int]($x.Step.Ended - $x.Step.Started).TotalMinutes
    [void]$md.AppendLine("| $($x.Step.Title) | $(Get-StatusText $x) | $($x.R.Total) | $($x.R.Passed) | $($x.R.Failed) | $($x.R.Skipped) | $($x.Step.ExitCode) | ${min}분 |")
}
[void]$md.AppendLine('')
[void]$md.AppendLine('## 실패한 시험')
[void]$md.AppendLine('')
$anyFail = $false
foreach ($x in $results) {
    foreach ($f in $x.R.Failures) {
        $anyFail = $true
        [void]$md.AppendLine("- **$($f.Name)** ($($x.Step.Title))")
        if ($f.Message) { [void]$md.AppendLine("  - $(ConvertTo-MdCell $f.Message)") }
    }
}
if (-not $anyFail) { [void]$md.AppendLine('없음') }
[void]$md.AppendLine('')
[void]$md.AppendLine('## 로그의 오류 줄 (error CS · Exception)')
[void]$md.AppendLine('')
foreach ($x in $results) {
    [void]$md.AppendLine("### $($x.Step.Title) — $($x.R.ErrorLines.Count)줄")
    if ($x.R.ErrorLines.Count -eq 0) {
        [void]$md.AppendLine('없음')
    }
    else {
        [void]$md.AppendLine('```')
        foreach ($l in ($x.R.ErrorLines | Select-Object -First $MaxErrLines)) { [void]$md.AppendLine($l) }
        if ($x.R.ErrorLines.Count -gt $MaxErrLines) { [void]$md.AppendLine("... 나머지 $($x.R.ErrorLines.Count - $MaxErrLines)줄은 $([System.IO.Path]::GetFileName($x.Step.Log)) 참고") }
        [void]$md.AppendLine('```')
    }
    [void]$md.AppendLine('')
}
[void]$md.AppendLine('## 측정 결과')
[void]$md.AppendLine('')
$probeTexts = @()
foreach ($x in $results) {
    if (-not $x.Step.ProbeOut) { continue }
    $name = [System.IO.Path]::GetFileName($x.Step.ProbeOut)
    $text = $null
    if (Test-Path -LiteralPath $x.Step.ProbeOut) { $text = Get-Content -LiteralPath $x.Step.ProbeOut -Encoding UTF8 }
    $probeTexts += [pscustomobject]@{ Title = $x.Step.Title; Name = $name; Lines = $text }
    [void]$md.AppendLine("### $($x.Step.Title) (``$name``)")
    if ($null -eq $text) {
        [void]$md.AppendLine('측정 파일이 만들어지지 않았습니다.')
    }
    else {
        [void]$md.AppendLine('```')
        foreach ($l in ($text | Select-Object -First $MaxProbeLines)) { [void]$md.AppendLine($l) }
        if (@($text).Count -gt $MaxProbeLines) { [void]$md.AppendLine("... 나머지는 $name 참고") }
        [void]$md.AppendLine('```')
    }
    [void]$md.AppendLine('')
}
if ($probeTexts.Count -eq 0) { [void]$md.AppendLine('이번에는 측정을 건너뛰었습니다.'); [void]$md.AppendLine('') }
[void]$md.AppendLine("## 새 화면 사진 ($($shots.Count)장, ``shots/``에 복사)")
[void]$md.AppendLine('')
if ($shots.Count -eq 0) { [void]$md.AppendLine('없음') }
foreach ($f in $shots) { [void]$md.AppendLine("- $($f.Name)") }
[void]$md.AppendLine('')
[void]$md.AppendLine('## 이 폴더의 파일')
[void]$md.AppendLine('')
foreach ($f in (Get-ChildItem -LiteralPath $ReportDir -File | Sort-Object Name)) { [void]$md.AppendLine("- $($f.Name)") }
Write-Utf8Bom (Join-Path $ReportDir 'report.md') $md.ToString()

# ---------- report.html ----------
$failTotal = 0; $passTotal = 0; $errTotal = 0
foreach ($x in $results) { $failTotal += $x.R.Failed; $passTotal += $x.R.Passed; $errTotal += $x.R.ErrorLines.Count }

$h = New-Object System.Text.StringBuilder
[void]$h.AppendLine(@'
<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>밤 시험 보고서</title>
<style>
  :root { --bg:#f6f4ef; --card:#fff; --ink:#24221e; --muted:#6b675e; --line:#ddd8cc; --accent:#b5651d; --ok:#1f7a5a; --warn:#b07d10; --bad:#c0392b; }
  @media (prefers-color-scheme: dark) { :root { --bg:#1b1a17; --card:#25231f; --ink:#ece8df; --muted:#a6a092; --line:#3a372f; --accent:#e0913f; --ok:#4fbf95; --warn:#e0b44f; --bad:#e06656; } }
  * { box-sizing:border-box; }
  body { margin:0; background:var(--bg); color:var(--ink); font-family:"Malgun Gothic","Apple SD Gothic Neo",sans-serif; line-height:1.65; }
  main { max-width:980px; margin:0 auto; padding:24px 16px 48px; }
  h1 { font-size:24px; margin:0 0 4px; } h2 { font-size:19px; margin:30px 0 10px; } h3 { font-size:16px; margin:16px 0 6px; }
  .sub { color:var(--muted); font-size:14px; margin-bottom:18px; }
  .card { background:var(--card); border:1px solid var(--line); border-radius:12px; padding:16px; margin:12px 0; }
  .kpis { display:grid; grid-template-columns:repeat(auto-fit,minmax(150px,1fr)); gap:10px; }
  .kpi { background:var(--card); border:1px solid var(--line); border-radius:12px; padding:14px; }
  .kpi b { display:block; font-size:26px; line-height:1.2; }
  .kpi span { color:var(--muted); font-size:13px; }
  .ok { color:var(--ok); } .warn { color:var(--warn); } .bad { color:var(--bad); }
  table { width:100%; border-collapse:collapse; font-size:14px; }
  th, td { border-bottom:1px solid var(--line); padding:7px 6px; text-align:left; vertical-align:top; }
  th { color:var(--muted); font-weight:600; white-space:nowrap; }
  td.n { text-align:right; font-variant-numeric:tabular-nums; }
  .wrap { overflow-x:auto; }
  pre { background:var(--bg); border:1px solid var(--line); border-radius:8px; padding:10px; font-size:12.5px; overflow-x:auto; white-space:pre; margin:6px 0; }
  .shots { display:grid; grid-template-columns:repeat(auto-fit,minmax(240px,1fr)); gap:12px; }
  figure { margin:0; background:var(--card); border:1px solid var(--line); border-radius:12px; overflow:hidden; }
  figure img { display:block; width:100%; height:auto; cursor:zoom-in; }
  figcaption { padding:8px 12px; font-size:13px; color:var(--muted); word-break:break-all; }
  ul { margin:6px 0; padding-left:20px; } li { margin:3px 0; }
  dialog { border:none; padding:0; max-width:96vw; background:transparent; }
  dialog img { max-width:96vw; max-height:92vh; display:block; }
  dialog::backdrop { background:rgba(0,0,0,.8); }
</style>
</head>
<body>
<main>
  <h1>밤 시험 아침 보고서</h1>
'@)
[void]$h.AppendLine("  <div class=""sub"">$(ConvertTo-HtmlText $NightStart.ToString('yyyy-MM-dd HH:mm')) ~ $(ConvertTo-HtmlText $NightEnd.ToString('HH:mm')) · $([int]($NightEnd - $NightStart).TotalMinutes)분 · $(ConvertTo-HtmlText $ProjectPath)</div>")

$failClass = 'ok'; if ($failTotal -gt 0) { $failClass = 'bad' }
$errClass = 'ok'; if ($errTotal -gt 0) { $errClass = 'warn' }
[void]$h.AppendLine('  <div class="kpis">')
[void]$h.AppendLine("    <div class=""kpi""><b class=""ok"">$passTotal</b><span>통과한 시험</span></div>")
[void]$h.AppendLine("    <div class=""kpi""><b class=""$failClass"">$failTotal</b><span>실패한 시험</span></div>")
[void]$h.AppendLine("    <div class=""kpi""><b class=""$errClass"">$errTotal</b><span>로그 오류 줄</span></div>")
[void]$h.AppendLine("    <div class=""kpi""><b>$($shots.Count)</b><span>새 화면 사진</span></div>")
[void]$h.AppendLine('  </div>')

[void]$h.AppendLine('  <h2>시험별 결과</h2>')
[void]$h.AppendLine('  <div class="card wrap"><table><tr><th>시험</th><th>상태</th><th>전체</th><th>통과</th><th>실패</th><th>건너뜀</th><th>종료 코드</th><th>걸린 시간</th></tr>')
foreach ($x in $results) {
    $st = Get-StatusText $x
    $cls = 'ok'; if ($st -ne '통과') { $cls = 'bad' }
    $min = [int]($x.Step.Ended - $x.Step.Started).TotalMinutes
    [void]$h.AppendLine("    <tr><td>$(ConvertTo-HtmlText $x.Step.Title)</td><td class=""$cls"">$st</td><td class=""n"">$($x.R.Total)</td><td class=""n"">$($x.R.Passed)</td><td class=""n"">$($x.R.Failed)</td><td class=""n"">$($x.R.Skipped)</td><td class=""n"">$($x.Step.ExitCode)</td><td class=""n"">${min}분</td></tr>")
}
[void]$h.AppendLine('  </table></div>')

[void]$h.AppendLine('  <h2>실패한 시험</h2>')
[void]$h.AppendLine('  <div class="card">')
if (-not $anyFail) { [void]$h.AppendLine('    <span class="ok">없음</span>') }
foreach ($x in $results) {
    foreach ($f in $x.R.Failures) {
        [void]$h.AppendLine("    <h3 class=""bad"">$(ConvertTo-HtmlText $f.Name)</h3><div class=""sub"">$(ConvertTo-HtmlText $x.Step.Title)</div>")
        if ($f.Message) { [void]$h.AppendLine("    <pre>$(ConvertTo-HtmlText $f.Message)</pre>") }
        if ($f.Stack) { [void]$h.AppendLine("    <details><summary>호출 순서</summary><pre>$(ConvertTo-HtmlText $f.Stack)</pre></details>") }
    }
}
[void]$h.AppendLine('  </div>')

[void]$h.AppendLine('  <h2>로그의 오류 줄 (error CS · Exception)</h2>')
foreach ($x in $results) {
    [void]$h.AppendLine("  <div class=""card""><h3>$(ConvertTo-HtmlText $x.Step.Title) — $($x.R.ErrorLines.Count)줄</h3>")
    if ($x.R.ErrorLines.Count -eq 0) {
        [void]$h.AppendLine('    <span class="ok">없음</span>')
    }
    else {
        $body = ($x.R.ErrorLines | Select-Object -First $MaxErrLines) -join "`n"
        if ($x.R.ErrorLines.Count -gt $MaxErrLines) { $body += "`n... 나머지 $($x.R.ErrorLines.Count - $MaxErrLines)줄은 $([System.IO.Path]::GetFileName($x.Step.Log)) 참고" }
        [void]$h.AppendLine("    <pre>$(ConvertTo-HtmlText $body)</pre>")
    }
    [void]$h.AppendLine('  </div>')
}

[void]$h.AppendLine('  <h2>측정 결과</h2>')
if ($probeTexts.Count -eq 0) { [void]$h.AppendLine('  <div class="card">이번에는 측정을 건너뛰었습니다.</div>') }
foreach ($p in $probeTexts) {
    [void]$h.AppendLine("  <div class=""card""><h3>$(ConvertTo-HtmlText $p.Title)</h3><div class=""sub"">$(ConvertTo-HtmlText $p.Name)</div>")
    if ($null -eq $p.Lines) {
        [void]$h.AppendLine('    <span class="bad">측정 파일이 만들어지지 않았습니다.</span>')
    }
    else {
        $body = (@($p.Lines) | Select-Object -First $MaxProbeLines) -join "`n"
        if (@($p.Lines).Count -gt $MaxProbeLines) { $body += "`n... 나머지는 $($p.Name) 참고" }
        [void]$h.AppendLine("    <pre>$(ConvertTo-HtmlText $body)</pre>")
    }
    [void]$h.AppendLine('  </div>')
}

[void]$h.AppendLine("  <h2>새 화면 사진 ($($shots.Count)장)</h2>")
if ($shots.Count -eq 0) {
    [void]$h.AppendLine('  <div class="card">없음</div>')
}
else {
    [void]$h.AppendLine('  <div class="shots">')
    foreach ($f in $shots) {
        $src = 'shots/' + [System.Uri]::EscapeDataString($f.Name)
        [void]$h.AppendLine("    <figure><img loading=""lazy"" src=""$src"" alt=""$(ConvertTo-HtmlText $f.Name)""><figcaption>$(ConvertTo-HtmlText $f.Name)</figcaption></figure>")
    }
    [void]$h.AppendLine('  </div>')
}

[void]$h.AppendLine(@'
  <dialog id="zoom"><img id="zoomImg" alt=""></dialog>
</main>
<script>
  const dlg = document.getElementById('zoom'), big = document.getElementById('zoomImg');
  document.querySelectorAll('figure img').forEach(function (img) {
    img.addEventListener('click', function () { big.src = img.src; dlg.showModal(); });
  });
  dlg.addEventListener('click', function () { dlg.close(); });
</script>
</body>
</html>
'@)
Write-Utf8Bom (Join-Path $ReportDir 'report.html') $h.ToString()

Write-Host ""
Write-Host "밤 시험 끝. 통과 $passTotal · 실패 $failTotal · 로그 오류 줄 $errTotal · 새 사진 $($shots.Count)장"
Write-Host "보고서: $(Join-Path $ReportDir 'report.html')"
if ($failTotal -gt 0) { exit 2 }
exit 0
