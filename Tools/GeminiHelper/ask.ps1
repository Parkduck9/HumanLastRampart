# 제미나이 부하 직원에게 일 맡기기 (사용자 2026-10-20: 공식 제미나이 명령줄 도구, Claude는 검증만)
# 쓰는 법: powershell -NoProfile -ExecutionPolicy Bypass -File Tools\GeminiHelper\ask.ps1 -Task <지시 파일.md> -Out <결과.md> [-Model <이름>]
# - 지시 파일 맨 위에 공통 규칙(RULES.md)을 붙여 넘긴다. 제미나이는 읽기·조사만 하고 파일을 고치지 않는다.
# - 할당량이 떨어지면 종료 코드 3 + 결과 파일 첫 줄 "QUOTA_EXHAUSTED" → Claude가 자기 토큰으로 이어 한다.
# - 실행 기록은 Tools/GeminiHelper/runs.log 에 한 줄씩 (시각·지시·걸린 초·결과).
param(
    [Parameter(Mandatory = $true)][string]$Task,
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$Model = ''
)
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$repo = 'C:\Unity\1\HumanLastRampart'
$here = Join-Path $repo 'Tools\GeminiHelper'
# 2026-10-20: 개인 계정의 제미나이 명령줄 도구 로그인이 막혔다("안티그래비티로 옮기라") → 공식 안티그래비티 명령줄 도구(agy)를 쓴다.
# agy가 없으면 예전 gemini.cmd(키를 넣은 경우만 동작)로. 이 실행에서만 경로를 앞에 붙인다
$env:Path = "$env:LOCALAPPDATA\agy\bin;C:\Program Files\nodejs;$env:APPDATA\npm;" + $env:Path
$tool = if (Get-Command agy -ErrorAction SilentlyContinue) { 'agy' } elseif (Get-Command gemini.cmd -ErrorAction SilentlyContinue) { 'gemini.cmd' } else { $null }
if (-not $tool) { Write-Output "안티그래비티 명령줄 도구(agy)가 없습니다 (설치: Docs/GDD/Gemini_Helper.md 1장)"; exit 2 }

$rules = Get-Content -Raw -Encoding UTF8 (Join-Path $here 'RULES.md')
$body = Get-Content -Raw -Encoding UTF8 $Task
# 긴 지시를 명령줄에 그대로 넘기면 윈도우 파워셸 5.1이 따옴표·줄바꿈을 깨뜨린다(첫 실행 실패, 2026-10-20) →
# 저장소 안 임시 파일(.agy/)에 적고 "이 파일을 읽고 따르라"는 짧은 한 줄만 넘긴다
$work = Join-Path $repo '.agy'
New-Item -ItemType Directory -Force $work | Out-Null
$taskCopy = Join-Path $work ("task_" + (Get-Date -Format 'yyyyMMdd_HHmmss') + ".md")
[IO.File]::WriteAllText($taskCopy, ($rules + "`n`n---`n`n" + $body), (New-Object Text.UTF8Encoding $false))
$rel = $taskCopy.Substring($repo.Length + 1).Replace('\', '/')
$prompt = "Read the file $rel in the current directory and follow its instructions exactly. Reply only with the requested output."
$argv = @('-p', $prompt)   # $args는 파워셸 예약 변수라 쓰지 않는다
# agy 헤드리스: 사람에게 묻는 창이 없다 — 허가가 필요한 도구(파일 쓰기·명령)는 기본 'request-review'라 실행되지 않는다.
# --dangerously-skip-permissions 는 절대 쓰지 않는다. --sandbox 로 터미널 제한도 켠다
if ($tool -eq 'agy') { $argv += @('--sandbox', '--output-format', 'text', '--print-timeout', '10m'); if ($Model) { $argv += @('--model', $Model) } }
elseif ($Model) { $argv += @('-m', $Model) }

$watch = [Diagnostics.Stopwatch]::StartNew()
Push-Location $repo
try { $text = (& $tool @argv 2>&1 | Out-String) } finally { Pop-Location }   # gemini는 .ps1 판이 실행 정책에 막혀 .cmd 판으로
$watch.Stop()

$quota = $text -match 'RESOURCE_EXHAUSTED|quota|429|rate limit|Quota exceeded'
if ($quota) { $text = "QUOTA_EXHAUSTED`n" + $text }
[IO.File]::WriteAllText($Out, $text, (New-Object Text.UTF8Encoding $false))
$line = "{0:yyyy-MM-dd HH:mm} | {1} | {2:0}s | {3}" -f (Get-Date), (Split-Path $Task -Leaf), $watch.Elapsed.TotalSeconds, ($(if ($quota) { '할당량 소진' } else { "결과 $($text.Length)자" }))
Add-Content -Path (Join-Path $here 'runs.log') -Value $line -Encoding UTF8
Write-Output $line
if ($quota) { exit 3 }
