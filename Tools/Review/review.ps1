# 소넷 작업 묶음을 명령줄 클로드(오퍼스)로 검토한다. 이 대화의 토큰을 아끼려고 따로 돌린다 (사용자 2026-10-22).
# 사용: powershell -ExecutionPolicy Bypass -File Tools\Review\review.ps1 -Name W12 -Worktree C:\Unity\1\HLR-work\W12 -Commit 63fe8a6 -Intent "무엇을 만들었나 (영어)"
# 결과: C:\Unity\1\HLR-work\reviews\<Name>.md  (첫 줄 = 판정 한 줄 요약)
param(
    [Parameter(Mandatory)] [string] $Name,
    [Parameter(Mandatory)] [string] $Worktree,
    [Parameter(Mandatory)] [string] $Commit,
    [Parameter(Mandatory)] [string] $Intent,
    [string] $Base = 'develop',
    [string] $Model = 'opus'
)
$ErrorActionPreference = 'Stop'
$git = 'C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe'
$exe = Get-ChildItem "$env:LOCALAPPDATA\Claude-Data\claude_*\claude-code\*\*\claude.exe" |
    Sort-Object { [version]($_.Directory.Parent.Name) } | Select-Object -Last 1
if (-not $exe) { throw '명령줄 클로드를 찾지 못했습니다 (앱 업데이트로 경로가 바뀌었을 수 있음)' }

$outDir = 'C:\Unity\1\HLR-work\reviews'
New-Item -ItemType Directory -Force $outDir | Out-Null
$diff = Join-Path $outDir "$Name.diff"
$out  = Join-Path $outDir "$Name.md"

# 검토자는 셸 없이 읽기만 하므로 차이를 미리 파일로 만든다
& $git -C $Worktree diff "--output=$diff" "$Base...$Commit"
if (-not (Test-Path $diff) -or (Get-Item $diff).Length -eq 0) { throw "차이 파일이 비었습니다: $diff" }

$prompt = @"
Read-only code review of package $Name for the Unity project Human Last Rampart.
Worktree: $Worktree (files at commit $Commit). Full diff vs ${Base}: $diff
Intent: $Intent
Use only Read/Grep/Glob. Do not edit anything.
Check correctness, flow/state bugs, save compatibility, input conflicts, per-frame allocations, leaks, mobile cost (PC primary, mobile capable), test quality, and UI clutter.
Output format (English, concise):
Line 1 exactly: VERDICT: <merge | merge after fixes | rework> | BLOCKER <n> | SHOULD-FIX <n> | NIT <n>
Then findings ranked, each one line: [TAG] file:line - failure scenario - fix.
Then 'Questions for user:' (only design decisions), or 'none'.
"@

# 앱 안에서 실행하면 앱 세션 설정을 물려받아 자기 로그인을 못 쓴다 → 이 프로세스에서만 지운다
Get-ChildItem env: | Where-Object { $_.Name -like 'CLAUDE_CODE_*' -or $_.Name -like 'CLAUDE_AGENT_SDK_*' -or $_.Name -in 'CLAUDECODE','CLAUDE_PID','CLAUDE_PREVIEW_CLASSIFIER_FLOOR' } |
    ForEach-Object { [Environment]::SetEnvironmentVariable($_.Name, $null, 'Process') }
& $exe.FullName -p $prompt --model $Model --allowedTools Read Grep Glob --add-dir $Worktree --add-dir $outDir |
    Out-File $out -Encoding utf8
$first = Get-Content $out -Encoding utf8 -TotalCount 1
if ($first -notmatch '^VERDICT:') { throw "검토 실패: $first" }
$first
