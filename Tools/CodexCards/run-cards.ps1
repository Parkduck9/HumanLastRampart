# 코덱스 카드 일괄 실행 (Claude 없이도 돌릴 수 있게, 2026-10-20)
# 사용 예:
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools\CodexCards\run-cards.ps1 -Cards K-071,K-072
#   (다른 창에서 동시에 한 줄 더 돌려도 된다: -Cards K-082,K-083)
# 하는 일: 카드마다 ① 카드 폴더가 없으면 만든다(develop에서) ② COMMON.md + 카드 지시문을 코덱스에게 넘긴다
#          ③ 카드 가지에 커밋(Game/·CARD_* 제외) ④ -NoMerge가 아니면 develop에 합친다
# 크레딧이 떨어지면 그 카드는 결과 보고 없이 멈춘다 → 다음 카드도 곧 멈추므로 결과 파일(CARD_RESULT.md)을 확인한다.
param(
    [Parameter(Mandatory = $true)][string[]]$Cards,
    [switch]$NoMerge
)
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$git = 'C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe'
$repo = 'C:\Unity\1\HumanLastRampart'
$briefs = Join-Path $repo 'Tools\CodexCards\briefs'
$Cards = @($Cards | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
foreach ($card in $Cards) {
    $brief = Join-Path $briefs "$card.md"
    if (-not (Test-Path $brief)) { Write-Output "지시문 없음: $brief"; continue }
    $dir = "C:\Unity\1\HLR-cards\$card"
    if (-not (Test-Path $dir)) { powershell -NoProfile -ExecutionPolicy Bypass -File "$repo\.claude\card-new.ps1" -Card $card | Select-Object -Last 1 }
    $tmp = Join-Path $env:TEMP "codex_brief_$card.md"
    $text = (Get-Content -Raw -Encoding UTF8 (Join-Path $briefs 'COMMON.md')) + "`n" + (Get-Content -Raw -Encoding UTF8 $brief)
    [IO.File]::WriteAllText($tmp, $text, (New-Object System.Text.UTF8Encoding($true)))
    Write-Output "== $card 시작 $(Get-Date -Format 'MM-dd HH:mm')"
    powershell -NoProfile -ExecutionPolicy Bypass -File "$repo\.claude\codex-card.ps1" -Card $card -BriefFile $tmp | Select-Object -Last 1
    if (Select-String -Path "$dir\CARD_CODEX_LOG.jsonl" -Pattern 'out of credits' -Quiet -ErrorAction SilentlyContinue) { Write-Output "  주의: 코덱스 크레딧 부족으로 멈춤" }
    & $git -C $dir add -A -- . ':!CARD_*' 2>$null
    $game = @(& $git -C $dir diff --cached --name-only | Where-Object { $_ -like 'Game/*' })
    if ($game.Count -gt 0) { & $git -C $dir reset -q -- Game 2>$null; Write-Output "  주의: Game/ 변경 $($game.Count)개는 커밋에서 뺌" }
    $staged = @(& $git -C $dir diff --cached --name-only)
    if ($staged.Count -eq 0) { Write-Output "  바뀐 파일 없음"; continue }
    & $git -C $dir commit -q -m "[$card] Codex 시안 (Tools/CodexCards)" 2>$null
    Write-Output "  커밋 — 파일 $($staged.Count)개"
    if (-not $NoMerge) {
        & $git -C $repo merge --no-ff --no-edit -m "[$card] 카드 합치기 (RULES 10.6)" "card/$card" 2>&1 | Select-Object -Last 1
    }
    Write-Output "== $card 끝 $(Get-Date -Format 'MM-dd HH:mm')"
}
