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
$cardStageNames = @('대기', '그리는 중', '검토', '합침', '확인', '끝')   # 코덱스는 그림만 그린다 (사용자 2026-10-22)
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

# 모델 이름을 짧은 한글로 (사용자 2026-10-22: % 오른쪽에 어떤 모델인지)
function ModelName([string]$id) {
    if (-not $id) { return '' }
    if ($id -match 'claude-(sonnet|opus|haiku|fable)-(\d+)-(\d+)') {
        $n = @{ sonnet = '소넷'; opus = '오퍼스'; haiku = '하이쿠'; fable = '페이블' }[$Matches[1]]
        return "$n $($Matches[2]).$($Matches[3])"
    }
    return $id
}
function CodexModel {
    $cfg = "$env:USERPROFILE\.codex\config.toml"
    if (-not (Test-Path $cfg)) { return '' }
    $m = Select-String -Path $cfg -Pattern '^\s*model\s*=\s*"([^"]+)"' | Select-Object -First 1
    if ($m) { return $m.Matches[0].Groups[1].Value } else { return '' }
}

function Get-Board {
    $now = Get-Date
    $cfg = Get-Content -Raw -Encoding UTF8 $stagesFile | ConvertFrom-Json
    $codexModel = CodexModel
    $rows = @()
    foreach ($it in $cfg.items) {
        if ([int]$it.stage -ge 5) { continue }   # 끝난 작업은 보이지 않는다 (사용자 2026-10-22)
        $stage = [int]$it.stage; $detail = ''; $inner = 0.0; $last = $null
        $model = if ($it.kind -eq 'card') { $codexModel } else { '' }
        if ($it.kind -eq 'coder') {
            $log = Get-ChildItem -Path $agents -Recurse -Filter "agent-$($it.agent).jsonl" | Select-Object -First 1
            if ($log) {
                $uses = Count-Matches $log.FullName '"type":"tool_use"'
                # 끝부분 응답 줄에서 읽는다 (첫 줄의 model은 작업자 모델이 아님). 5초마다 전체를 읽지 않게 끝 40줄만
                $mm = Get-Content -LiteralPath $log.FullName -Tail 40 | Select-String -Pattern '"model":"(claude-[^"]+)"' | Select-Object -Last 1
                if ($mm) { $model = ModelName $mm.Matches[0].Groups[1].Value }
                $inner = [Math]::Min(1.0, $uses / 70.0); $last = $log.LastWriteTime
                $detail = "도구 사용 ${uses}번"
            }
            # 바뀐 파일 수는 화면에서 뺐다 (2026-10-22) — 5초마다 git을 부르지 않는다. 검토 작업처럼 폴더가 없는 줄도 있다
        }
        elseif ($it.kind -eq 'card') {
            $log = Join-Path $it.folder 'CARD_CODEX_LOG.jsonl'
            if (Test-Path -LiteralPath $log) {
                $done = Count-Matches $log '"item.completed"'
                $inner = [Math]::Min(1.0, $done / 60.0); $last = (Get-Item -LiteralPath $log).LastWriteTime
                $detail = "그림 작업 단계 ${done}개"
                if (Test-Path -LiteralPath (Join-Path $it.folder 'CARD_RESULT.md')) { if ($stage -lt 3) { $stage = 3 }; $detail += ' · 결과 보고 나옴' }
            }
            else { $detail = '시작 전 (차례 기다림)' }
        }
        $lo = $band[$stage][0]; $hi = $band[$stage][1]
        $pct = if ($stage -eq 1) { [int]($lo + ($hi - $lo) * $inner) } else { $lo }
        $idle = if ($last) { [int]($now - $last).TotalSeconds } else { -1 }
        $names = if ($it.kind -eq 'card') { $cardStageNames } else { $stageNames }
        $rows += [ordered]@{ id = $it.id; name = $it.name; who = $it.who; kind = $it.kind; desc = $it.desc; model = $model; stage = $names[$stage]; pct = $pct; detail = $detail; idle = $idle }
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
            if ($r) {
                # 코덱스가 일할 때만 값이 새로 기록된다 — 초기화 시각이 지났으면 0%로 본다 (사용자 2026-10-22: 눌러야만 바뀌는 문제)
                $nowUnix = [DateTimeOffset]::Now.ToUnixTimeSeconds()
                $five = if ([long]$r.primary.resets_at -le $nowUnix) { 0 } else { [double]$r.primary.used_percent }
                $week = if ([long]$r.secondary.resets_at -le $nowUnix) { 0 } else { [double]$r.secondary.used_percent }
                $codex = [ordered]@{ five = $five; fiveReset = (When $r.primary.resets_at); week = $week; weekReset = (When $r.secondary.resets_at); age = [int]($now - $s.LastWriteTime).TotalMinutes }
            }
        }
    }
    $claude = $null
    if (Test-Path $claudeFile) {
        $claude = Get-Content -Raw -Encoding UTF8 $claudeFile | ConvertFrom-Json
        # 클로드 값은 Claude가 확인할 때만 적힌다 — 초기화 시각(fiveResetAt·weekResetAt, 있을 때)이 지났으면 0%
        if ($claude.fiveResetAt -and [DateTime]$claude.fiveResetAt -le $now) { $claude.five = 0 }
        if ($claude.weekResetAt -and [DateTime]$claude.weekResetAt -le $now) { $claude.week = 0 }
    }

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
$form.GetType().GetProperty('DoubleBuffered', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($form, $true, $null)
$panel = New-Object System.Windows.Forms.FlowLayoutPanel
$panel.Dock = 'Fill'; $panel.FlowDirection = 'TopDown'; $panel.WrapContents = $false; $panel.AutoScroll = $true; $panel.Padding = New-Object System.Windows.Forms.Padding(10)
$panel.GetType().GetProperty('DoubleBuffered', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($panel, $true, $null)
$form.Controls.Add($panel)

$brass = [System.Drawing.Color]::FromArgb(212, 166, 74)
$dim = [System.Drawing.Color]::FromArgb(168, 151, 124)
$warn = [System.Drawing.Color]::FromArgb(224, 164, 88)
$track = [System.Drawing.Color]::FromArgb(18, 14, 10)
# 깜빡임 없애기 (사용자 2026-10-22): 줄을 한 번 만들고 글과 막대 길이만 바꾼다. 줄 모양이 바뀔 때만 다시 만든다.
# 막대는 윈도우 기본 진행 막대 대신 직접 그린 판이라 0부터 차오르는 움직임이 없다.
$script:shape = ''
$script:slots = @()

# 화면 2판 (사용자 2026-10-22: 꾸밈 줄·도구 사용 수·바뀐 파일 수 같은 쓸모없는 정보 빼고, 사용량은 꼭 보이게)
# 한 칸 = 제목 줄(이름 + 오른쪽 확인 시각) → 사용량 두 줄(이번 주 · 5시간, 막대 + 숫자) → 작업 줄(이름 + 오른쪽 단계·%·모델, 얇은 막대)
$W = 470
$head = New-Object System.Drawing.Font('Malgun Gothic', 12, [System.Drawing.FontStyle]::Bold)
$small = New-Object System.Drawing.Font('Malgun Gothic', 9)

function Rows($d) {
    $R = New-Object System.Collections.ArrayList
    function Row($kind, $left, $right, $pct) { [void]$R.Add([pscustomobject]@{ Kind = $kind; Left = $left; Right = $right; Pct = $pct }) }
    function Usage($u, $note) {
        if ($u) {
            Row 'meter' '이번 주' ("{0}%" -f [int]$u.week) $u.week
            Row 'meter' '5시간' ("{0}%" -f [int]$u.five) $u.five
        } else { Row 'note' $note '' $null }
    }
    function Tasks($list, $empty) {
        if (-not $list) { Row 'note' $empty '' $null; return }
        foreach ($it in $list) {
            $right = "{0}  {1}%" -f $it.stage, $it.pct
            if ($it.model) { $right += "  ·  $($it.model)" }
            Row 'task' $it.name $right $it.pct
        }
    }

    $claudeAt = if ($d.claude) { "확인 $($d.claude.checked.Substring(6))" } else { '' }
    Row 'title' '클로드' $claudeAt $null
    Usage $d.claude '사용량 기록 없음'
    $mine = @($d.items | Where-Object { $_.kind -ne 'card' })
    if ($d.unity.running) { Row 'task' '유니티 시험' ("{0}분째" -f [int]$d.unity.minutes) $d.unity.pct }
    if ($d.unity.running -and -not $mine) { } else { Tasks $mine '진행 중인 작업 없음' }
    Row 'gap' '' '' $null

    $codexAt = if ($d.codex) { "초기화 {0} · 5시간 {1}" -f $d.codex.weekReset, $d.codex.fiveReset.Substring(6) } else { '' }
    Row 'title' '코덱스' $codexAt $null
    Usage $d.codex '사용량을 읽지 못함'
    Tasks @($d.items | Where-Object { $_.kind -eq 'card' }) '진행 중인 그림 없음'
    Row 'gap' '' '' $null
    Row 'note' ('갱신 ' + $d.updated.Substring(11)) '' $null
    # 읽기에 실패하면 멈춘 화면을 그대로 두지 않고 알린다 (2026-10-22: 4시 22분에 멈춘 채로 보였음)
    if ($script:failText) { Row 'note' $script:failText '' $null }
    return $R
}

function NewLabel($text, $f, $color, $x, $y, $w, $align) {
    $l = New-Object System.Windows.Forms.Label
    $l.AutoSize = $false; $l.Font = $f; $l.ForeColor = $color; $l.Text = $text
    $l.Location = New-Object System.Drawing.Point($x, $y); $l.Size = New-Object System.Drawing.Size($w, 22); $l.TextAlign = $align
    return $l
}

function Render($d) {
    $rows = Rows $d
    $shape = ($rows | ForEach-Object { $_.Kind }) -join ';'
    if ($shape -ne $script:shape) {
        # 줄 모양이 바뀌었을 때만 다시 만든다 (깜빡임 없음)
        $panel.SuspendLayout(); $panel.Controls.Clear(); $script:slots = @()
        foreach ($r in $rows) {
            $p = New-Object System.Windows.Forms.Panel
            $p.Width = $W; $p.Margin = New-Object System.Windows.Forms.Padding(0)
            $a = $null; $b = $null; $fill = $null; $barW = 0
            switch ($r.Kind) {
                'title' { $p.Height = 34; $a = NewLabel '' $head $brass 0 6 200 'MiddleLeft'; $b = NewLabel '' $small $dim 200 8 ($W - 200) 'MiddleRight' }
                'meter' {
                    $p.Height = 24; $a = NewLabel '' $font $dim 0 1 70 'MiddleLeft'; $b = NewLabel '' $bold $form.ForeColor ($W - 60) 1 60 'MiddleRight'
                    $barW = $W - 140
                    $bar = New-Object System.Windows.Forms.Panel; $bar.BackColor = $track; $bar.Location = New-Object System.Drawing.Point(72, 8); $bar.Size = New-Object System.Drawing.Size($barW, 8)
                    $fill = New-Object System.Windows.Forms.Panel; $fill.BackColor = $brass; $fill.Height = 8; $bar.Controls.Add($fill); $p.Controls.Add($bar)
                }
                'task' {
                    $p.Height = 32; $a = NewLabel '' $font $form.ForeColor 0 0 250 'MiddleLeft'; $b = NewLabel '' $small $dim 250 0 ($W - 250) 'MiddleRight'
                    $barW = $W
                    $bar = New-Object System.Windows.Forms.Panel; $bar.BackColor = $track; $bar.Location = New-Object System.Drawing.Point(0, 24); $bar.Size = New-Object System.Drawing.Size($W, 3)
                    $fill = New-Object System.Windows.Forms.Panel; $fill.BackColor = $brass; $fill.Height = 3; $bar.Controls.Add($fill); $p.Controls.Add($bar)
                }
                'note' { $p.Height = 24; $a = NewLabel '' $small $dim 0 0 $W 'MiddleLeft' }
                default { $p.Height = 12 }
            }
            if ($a) { $p.Controls.Add($a) }; if ($b) { $p.Controls.Add($b) }
            $panel.Controls.Add($p)
            $script:slots += , @($a, $b, $fill, $barW)
        }
        $panel.ResumeLayout()
        $script:shape = $shape
        # 창 높이를 내용에 맞춘다 (오른쪽 아래에 붙여 둔다)
        $h = 20; foreach ($c in $panel.Controls) { $h += $c.Height }
        $form.ClientSize = New-Object System.Drawing.Size($form.ClientSize.Width, [Math]::Min(900, $h))
        $form.Top = $area.Bottom - $form.Height - 20
    }
    for ($i = 0; $i -lt $rows.Count; $i++) {
        $r = $rows[$i]; $a = $script:slots[$i][0]; $b = $script:slots[$i][1]; $fill = $script:slots[$i][2]; $barW = $script:slots[$i][3]
        if ($a -and $a.Text -ne $r.Left) { $a.Text = $r.Left }
        if ($b -and $b.Text -ne $r.Right) { $b.Text = $r.Right }
        if ($fill) {
            $w = [int]($barW * [Math]::Max(0, [Math]::Min(100, [double]$r.Pct)) / 100)
            if ($fill.Width -ne $w) { $fill.Width = $w }
        }
    }
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
    # 목록 파일을 고치는 순간 읽으면 오류가 날 수 있다 — 그 한 번만 건너뛰고 계속 돈다 (2026-10-22 프로그램이 꺼진 일)
    try {
        $d = Get-Board
        $script:failText = $null; $script:lastGood = $d
        if ($form.Visible) { Render $d }
        $active = @($d.items | Where-Object { $_.pct -lt 100 }).Count
        $icon.Text = ("HLR · 진행 {0}개" -f $active)
    } catch {
        $script:failText = "갱신 실패 $((Get-Date).ToString('HH:mm:ss')) — 위는 마지막으로 읽은 값: $($_.Exception.Message)"
        if ($form.Visible -and $script:lastGood) { Render $script:lastGood }
        $icon.Text = 'HLR · 갱신 실패'
    }
})
$timer.Start()
$null = Get-Board
[System.Windows.Forms.Application]::Run()
