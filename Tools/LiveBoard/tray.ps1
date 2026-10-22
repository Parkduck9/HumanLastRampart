# 작업 현황 알림 영역 프로그램 (2026-10-22 사용자 요청)
# 작업 표시줄 오른쪽 알림 영역에 아이콘을 띄운다. 아이콘을 누르거나 오른쪽 누름 → [열기] 하면 진척도·토큰 사용량 창이 뜬다.
# 인공지능 토큰을 쓰지 않는다: 5초마다 작업 폴더·코덱스 기록·유니티 실행 여부를 읽기만 한다.
# 같은 자료를 _HTML/live/board_data.js 에도 써서 웹 페이지(board.html)도 함께 갱신된다.
# 실행: Tools\LiveBoard\start.cmd
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'SilentlyContinue'
[System.Windows.Forms.Application]::EnableVisualStyles()

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$stagesFile = Join-Path $PSScriptRoot 'stages.json'
$claudeFile = Join-Path $PSScriptRoot 'claude_usage.json'   # Claude가 사용량을 확인할 때마다 적어 둔다
$outFile = Join-Path $repo '_HTML\live\board_data.js'
$git = 'C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe'
$agents = Join-Path $env:USERPROFILE '.claude\projects\C--Unity-1-HumanLastRampart'
$stageNames = @('대기', '코딩', '검토', '합침', '시험', '끝')
$band = @(@(0, 0), @(5, 60), @(60, 75), @(75, 85), @(85, 99), @(100, 100))
$script:countCache = @{}

function Count-Matches($path, $pattern) {
    if (-not (Test-Path -LiteralPath $path)) { return 0 }
    $stamp = (Get-Item -LiteralPath $path).LastWriteTime.Ticks
    $key = "$path|$pattern"
    if ($script:countCache.ContainsKey($key) -and $script:countCache[$key][0] -eq $stamp) { return $script:countCache[$key][1] }
    $n = (Select-String -LiteralPath $path -Pattern ([regex]::Escape($pattern)) -AllMatches | ForEach-Object { $_.Matches.Count } | Measure-Object -Sum).Sum
    if (-not $n) { $n = 0 }
    $script:countCache[$key] = @($stamp, $n)
    return $n
}

function When([long]$unix) { [DateTimeOffset]::FromUnixTimeSeconds($unix).ToLocalTime().ToString('MM-dd HH:mm') }

function Get-Board {
    $now = Get-Date
    $cfg = Get-Content -Raw -Encoding UTF8 $stagesFile | ConvertFrom-Json
    $rows = @()
    foreach ($it in $cfg.items) {
        $stage = [int]$it.stage; $detail = ''; $inner = 0.0; $last = $null
        if ($it.kind -eq 'coder') {
            $log = Get-ChildItem -Path $agents -Recurse -Filter "agent-$($it.agent).jsonl" | Select-Object -First 1
            if ($log) {
                $uses = Count-Matches $log.FullName '"type":"tool_use"'
                $inner = [Math]::Min(1.0, $uses / 70.0); $last = $log.LastWriteTime
                $detail = "도구 사용 ${uses}번"
            }
            if (Test-Path -LiteralPath $it.folder) { $changed = @(& $git -C $it.folder status --short).Count; $detail += " · 바뀐 파일 ${changed}개" }
        }
        elseif ($it.kind -eq 'card') {
            $log = Join-Path $it.folder 'CARD_CODEX_LOG.jsonl'
            if (Test-Path -LiteralPath $log) {
                $done = Count-Matches $log '"item.completed"'
                $inner = [Math]::Min(1.0, $done / 60.0); $last = (Get-Item -LiteralPath $log).LastWriteTime
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

    $unity = Get-Process Unity -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -First 1
    $unityRow = if ($unity) { $m = ($now - $unity.StartTime).TotalMinutes; [ordered]@{ running = $true; minutes = [Math]::Round($m, 1); pct = [int][Math]::Min(99, $m / 9.0 * 100) } } else { [ordered]@{ running = $false } }

    # 코덱스 사용 한도: 가장 최근 코덱스 기록의 rate_limits (Tools/UsageCheck/usage.ps1 과 같은 방법)
    $codex = $null
    $s = Get-ChildItem "$env:USERPROFILE\.codex\sessions" -Recurse -Filter *.jsonl | Sort-Object LastWriteTime | Select-Object -Last 1
    if ($s) {
        $line = Get-Content $s.FullName -Tail 300 | Select-String '"rate_limits"' | Select-Object -Last 1
        if ($line) {
            $j = $line.Line | ConvertFrom-Json; $r = $j.payload.rate_limits; if (-not $r) { $r = $j.rate_limits }
            if ($r) { $codex = [ordered]@{ five = [double]$r.primary.used_percent; fiveReset = (When $r.primary.resets_at); week = [double]$r.secondary.used_percent; weekReset = (When $r.secondary.resets_at) } }
        }
    }
    $claude = $null
    if (Test-Path $claudeFile) { $claude = Get-Content -Raw -Encoding UTF8 $claudeFile | ConvertFrom-Json }

    $data = [ordered]@{ updated = $now.ToString('yyyy-MM-dd HH:mm:ss'); items = $rows; unity = $unityRow; codex = $codex; claude = $claude }
    [IO.File]::WriteAllText($outFile, 'window.BOARD = ' + ($data | ConvertTo-Json -Depth 5 -Compress) + ';', (New-Object Text.UTF8Encoding $false))
    return $data
}

# ── 창 ──
$font = New-Object System.Drawing.Font('Malgun Gothic', 10)
$bold = New-Object System.Drawing.Font('Malgun Gothic', 10, [System.Drawing.FontStyle]::Bold)
$form = New-Object System.Windows.Forms.Form
$form.Text = 'HLR 작업 현황'
$form.Size = New-Object System.Drawing.Size(520, 560)
$form.StartPosition = 'Manual'
$area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$form.Location = New-Object System.Drawing.Point(($area.Right - 540), ($area.Bottom - 580))
$form.BackColor = [System.Drawing.Color]::FromArgb(29, 24, 19)
$form.ForeColor = [System.Drawing.Color]::FromArgb(239, 227, 204)
$form.Font = $font
$form.ShowInTaskbar = $false
$form.TopMost = $true
$panel = New-Object System.Windows.Forms.FlowLayoutPanel
$panel.Dock = 'Fill'; $panel.FlowDirection = 'TopDown'; $panel.WrapContents = $false; $panel.AutoScroll = $true; $panel.Padding = New-Object System.Windows.Forms.Padding(10)
$form.Controls.Add($panel)

function Add-Label($text, $f, $color) {
    $l = New-Object System.Windows.Forms.Label
    $l.Text = $text; $l.AutoSize = $true; $l.MaximumSize = New-Object System.Drawing.Size(470, 0); $l.Font = $f
    if ($color) { $l.ForeColor = $color }
    $panel.Controls.Add($l)
}
function Add-Bar($pct) {
    $b = New-Object System.Windows.Forms.ProgressBar
    $b.Width = 470; $b.Height = 14; $b.Minimum = 0; $b.Maximum = 100; $b.Value = [Math]::Max(0, [Math]::Min(100, [int]$pct))
    $panel.Controls.Add($b)
}
$brass = [System.Drawing.Color]::FromArgb(212, 166, 74)
$dim = [System.Drawing.Color]::FromArgb(168, 151, 124)
$warn = [System.Drawing.Color]::FromArgb(224, 164, 88)

function Render($d) {
    $panel.SuspendLayout(); $panel.Controls.Clear()
    Add-Label ('갱신 ' + $d.updated) $font $dim
    Add-Label '── 토큰 사용량 ──' $bold $brass
    if ($d.claude) {
        Add-Label ("클로드 이번 주 {0}% · 5시간 {1}%  (확인 {2})" -f $d.claude.week, $d.claude.five, $d.claude.checked) $font $null
        Add-Bar $d.claude.week
    } else { Add-Label '클로드: 아직 기록 없음 (Claude가 확인할 때 채워짐)' $font $dim }
    if ($d.codex) {
        Add-Label ("코덱스 이번 주 {0}% (초기화 {1}) · 5시간 {2}% (초기화 {3})" -f $d.codex.week, $d.codex.weekReset, $d.codex.five, $d.codex.fiveReset) $font $null
        Add-Bar $d.codex.week
    } else { Add-Label '코덱스: 한도 정보를 읽지 못함' $font $dim }
    Add-Label '── 진행 중인 작업 ──' $bold $brass
    if ($d.unity.running) { Add-Label ("유니티 시험 실행 중 · {0}분째 (약 {1}%)" -f $d.unity.minutes, $d.unity.pct) $font $null; Add-Bar $d.unity.pct }
    foreach ($it in $d.items) {
        Add-Label ("{0}  [{1}]  {2}%" -f $it.name, $it.stage, $it.pct) $bold $null
        $tail = if ($it.idle -gt 180 -and $it.pct -lt 100) { " · {0}분째 움직임 없음" -f [int]($it.idle / 60) } elseif ($it.idle -ge 0) { " · $($it.idle)초 전 움직임" } else { '' }
        Add-Label ($it.who + ' · ' + $it.detail + $tail) $font $(if ($it.idle -gt 180 -and $it.pct -lt 100) { $warn } else { $dim })
        Add-Bar $it.pct
    }
    if (-not $d.items) { Add-Label '진행 중인 작업이 없습니다' $font $dim }
    $panel.ResumeLayout()
}

# ── 알림 영역 아이콘 ──
$bmp = New-Object System.Drawing.Bitmap(32, 32)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.FillEllipse((New-Object System.Drawing.SolidBrush($brass)), 1, 1, 30, 30)
$g.DrawString('H', (New-Object System.Drawing.Font('Arial', 15, [System.Drawing.FontStyle]::Bold)), [System.Drawing.Brushes]::Black, 6, 4)
$g.Dispose()
$icon = New-Object System.Windows.Forms.NotifyIcon
$icon.Icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$icon.Text = 'HLR 작업 현황'
$menu = New-Object System.Windows.Forms.ContextMenuStrip
$open = $menu.Items.Add('열기')
$null = $menu.Items.Add('-')
$quit = $menu.Items.Add('종료')
$icon.ContextMenuStrip = $menu
$icon.Visible = $true

$show = { Render (Get-Board); $form.Show(); $form.Activate() }
$open.Add_Click($show)
$icon.Add_MouseClick({ param($s, $e) if ($e.Button -eq [System.Windows.Forms.MouseButtons]::Left) { & $show } })
$quit.Add_Click({ $timer.Stop(); $icon.Visible = $false; $icon.Dispose(); [System.Windows.Forms.Application]::Exit() })
$form.Add_FormClosing({ param($s, $e) if ($e.CloseReason -eq [System.Windows.Forms.CloseReason]::UserClosing) { $e.Cancel = $true; $form.Hide() } })

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 5000
$timer.Add_Tick({
    $d = Get-Board
    if ($form.Visible) { Render $d }
    $active = @($d.items | Where-Object { $_.pct -lt 100 }).Count
    $icon.Text = ("HLR · 진행 {0}개" -f $active)
})
$timer.Start()
$null = Get-Board
[System.Windows.Forms.Application]::Run()
