# 세 도구의 사용량 한눈에 (사용자 2026-10-20 "서로간의 토큰도 확인할 수 있어?")
# 쓰는 법: powershell -NoProfile -ExecutionPolicy Bypass -File Tools\UsageCheck\usage.ps1
# - 코덱스: 가장 최근 대화 기록(~/.codex/sessions)의 사용 한도(5시간·1주) + 카드별 토큰(카드 폴더 CARD_CODEX_LOG.jsonl의 turn.completed)
# - 안티그래비티: 실행 도구 기록(Tools/GeminiHelper/runs.log) — 남은 한도는 agy 대화형 화면에서만 보인다
# - 클로드: 이 도구로는 못 읽는다 — 앱의 사용량 화면(또는 Claude가 세션 도구로 확인)
[Console]::OutputEncoding = [Text.Encoding]::UTF8
function When([long]$unix) { [DateTimeOffset]::FromUnixTimeSeconds($unix).ToLocalTime().ToString('MM-dd HH:mm') }

Write-Output "== 코덱스"
$s = Get-ChildItem "$env:USERPROFILE\.codex\sessions" -Recurse -Filter *.jsonl -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
if ($s) {
    $line = Get-Content $s.FullName -Tail 300 | Select-String '"rate_limits"' | Select-Object -Last 1
    if ($line) {
        $j = ($line.Line | ConvertFrom-Json)
        $r = $j.payload.rate_limits; if (-not $r) { $r = $j.rate_limits }
        if ($r) {
            Write-Output ("  5시간 한도: {0}% 사용 (초기화 {1})" -f $r.primary.used_percent, (When $r.primary.resets_at))
            Write-Output ("  1주 한도:  {0}% 사용 (초기화 {1})" -f $r.secondary.used_percent, (When $r.secondary.resets_at))
        } else { Write-Output "  한도 정보를 읽지 못함 (기록 모양이 바뀜)" }
    } else { Write-Output "  최근 기록에 한도 정보 없음" }
}
Get-ChildItem 'C:\Unity\1\HLR-cards' -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    $log = Join-Path $_.FullName 'CARD_CODEX_LOG.jsonl'
    if (-not (Test-Path $log)) { return }
    $done = Get-Content $log | Select-String '"turn.completed"' | Select-Object -Last 1
    if ($done) {
        $u = ($done.Line | ConvertFrom-Json).usage
        Write-Output ("  {0}: 입력 {1:N0} (캐시 {2:N0}) · 출력 {3:N0}" -f $_.Name, $u.input_tokens, $u.cached_input_tokens, $u.output_tokens)
    } elseif ((Get-Item $log).LastWriteTime -gt (Get-Date).AddMinutes(-10)) { Write-Output "  $($_.Name): 진행 중" }
}

Write-Output "== 안티그래비티 (최근 실행)"
$runs = 'C:\Unity\1\HumanLastRampart\Tools\GeminiHelper\runs.log'
if (Test-Path $runs) { Get-Content $runs -Encoding UTF8 -Tail 5 | ForEach-Object { "  $_" } } else { Write-Output "  기록 없음" }
