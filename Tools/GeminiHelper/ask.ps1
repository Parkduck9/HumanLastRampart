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
if (-not (Get-Command gemini -ErrorAction SilentlyContinue)) { Write-Output "제미나이 명령줄 도구가 없습니다 (설치: Docs/GDD/Gemini_Helper.md 1장)"; exit 2 }

$rules = Get-Content -Raw -Encoding UTF8 (Join-Path $here 'RULES.md')
$body = Get-Content -Raw -Encoding UTF8 $Task
$prompt = $rules + "`n`n---`n`n" + $body
$args = @('-p', $prompt)
if ($Model) { $args += @('-m', $Model) }

$watch = [Diagnostics.Stopwatch]::StartNew()
Push-Location $repo
try { $text = (& gemini @args 2>&1 | Out-String) } finally { Pop-Location }
$watch.Stop()

$quota = $text -match 'RESOURCE_EXHAUSTED|quota|429|rate limit|Quota exceeded'
if ($quota) { $text = "QUOTA_EXHAUSTED`n" + $text }
[IO.File]::WriteAllText($Out, $text, (New-Object Text.UTF8Encoding $false))
$line = "{0:yyyy-MM-dd HH:mm} | {1} | {2:0}s | {3}" -f (Get-Date), (Split-Path $Task -Leaf), $watch.Elapsed.TotalSeconds, ($(if ($quota) { '할당량 소진' } else { "결과 $($text.Length)자" }))
Add-Content -Path (Join-Path $here 'runs.log') -Value $line -Encoding UTF8
Write-Output $line
if ($quota) { exit 3 }
