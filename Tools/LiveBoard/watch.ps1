# 작업 실시간 보기 — 감시 도구 (2026-10-22)
# 5초마다 작업 폴더·코덱스 기록·유니티 실행 여부를 읽어 _HTML/live/board_data.js 에 쓴다. 인공지능 토큰을 쓰지 않는다.
# 실행: Tools\LiveBoard\start.cmd 를 두 번 누르거나
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LiveBoard\watch.ps1
param([int]$Seconds = 5, [switch]$Once)
$ErrorActionPreference = 'SilentlyContinue'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$stagesFile = Join-Path $PSScriptRoot 'stages.json'
$outDir = Join-Path $repo '_HTML\live'
New-Item -ItemType Directory -Force $outDir | Out-Null
$outFile = Join-Path $outDir 'board_data.js'
$git = 'C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe'
$tasks = Join-Path $env:USERPROFILE '.claude\projects\C--Unity-1-HumanLastRampart'
$countCache = @{}   # 파일이 바뀌었을 때만 다시 센다 (자원 아끼기)
$stageNames = @('대기', '코딩', '검토', '합침', '시험', '끝')
# 단계마다 막대가 차지하는 구간 (시작%, 끝%)
$band = @(@(0, 0), @(5, 60), @(60, 75), @(75, 85), @(85, 99), @(100, 100))

function Count-Lines($path, $pattern) {
    if (-not (Test-Path -LiteralPath $path)) { return 0 }
    $stamp = (Get-Item -LiteralPath $path).LastWriteTime.Ticks
    $key = "$path|$pattern"
    if ($countCache.ContainsKey($key) -and $countCache[$key][0] -eq $stamp) { return $countCache[$key][1] }
    $n = 0
    try { $n = (Select-String -LiteralPath $path -Pattern ([regex]::Escape($pattern)) -AllMatches | ForEach-Object { $_.Matches.Count } | Measure-Object -Sum).Sum } catch { }
    if (-not $n) { $n = 0 }
    $countCache[$key] = @($stamp, $n)
    return $n
}

while ($true) {
    $now = Get-Date
    $cfg = Get-Content -Raw -Encoding UTF8 $stagesFile | ConvertFrom-Json
    $rows = @()
    foreach ($it in $cfg.items) {
        $stage = [int]$it.stage
        $detail = ''
        $inner = 0.0
        $last = $null
        if ($it.kind -eq 'coder') {
            # 하위 일꾼 기록에서 도구 사용 횟수를 센다 (보통 40~80번이면 끝난다)
            $log = Get-ChildItem -Path $tasks -Recurse -Filter "agent-$($it.agent).jsonl" | Select-Object -First 1
            if ($log) {
                $uses = Count-Lines $log.FullName '"type":"tool_use"'
                $inner = [Math]::Min(1.0, $uses / 70.0)
                $last = $log.LastWriteTime
                $detail = "도구 사용 ${uses}번"
            }
            if (Test-Path -LiteralPath $it.folder) {
                $changed = @(& $git -C $it.folder status --short).Count
                $detail += " · 바뀐 파일 ${changed}개"
            }
        }
        elseif ($it.kind -eq 'card') {
            $log = Join-Path $it.folder 'CARD_CODEX_LOG.jsonl'
            if (Test-Path -LiteralPath $log) {
                $done = Count-Lines $log '"item.completed"'
                $inner = [Math]::Min(1.0, $done / 60.0)
                $last = (Get-Item -LiteralPath $log).LastWriteTime
                $detail = "코덱스 작업 단계 ${done}개"
                if (Test-Path -LiteralPath (Join-Path $it.folder 'CARD_RESULT.md')) { if ($stage -lt 3) { $stage = 3 }; $detail += ' · 결과 보고 나옴' }
            }
            else { $detail = '시작 전 (차례 기다림)' }
        }
        $lo = $band[$stage][0]; $hi = $band[$stage][1]
        $pct = if ($stage -eq 1) { [int]($lo + ($hi - $lo) * $inner) } else { $lo }
        $idle = if ($last) { [int]($now - $last).TotalSeconds } else { -1 }
        $rows += [ordered]@{ id = $it.id; name = $it.name; who = $it.who; stage = $stageNames[$stage]; pct = $pct; detail = $detail; idle = $idle }
    }

    # 유니티 시험: 실행 중이면 경과 시간으로 어림 (전체 시험은 보통 9분 안팎)
    $unity = Get-Process Unity -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -First 1
    $unityRow = $null
    if ($unity) {
        $mins = ($now - $unity.StartTime).TotalMinutes
        $unityRow = [ordered]@{ running = $true; minutes = [Math]::Round($mins, 1); pct = [int][Math]::Min(99, $mins / 9.0 * 100) }
    }
    else { $unityRow = [ordered]@{ running = $false } }

    $data = [ordered]@{ updated = $now.ToString('yyyy-MM-dd HH:mm:ss'); items = $rows; unity = $unityRow }
    $json = $data | ConvertTo-Json -Depth 5 -Compress
    [IO.File]::WriteAllText($outFile, "window.BOARD = $json;", (New-Object Text.UTF8Encoding $false))
    if ($Once) { break }
    Start-Sleep -Seconds $Seconds
}
