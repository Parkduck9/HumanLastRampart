# 코덱스에게 코드 작업 맡기기 (사용자 2026-10-20 "그래 그렇게 시작하자" — 클로드 주간 한도 96%, 코드는 코덱스·클로드는 확인만)
# 쓰는 법: powershell -NoProfile -ExecutionPolicy Bypass -File Tools\CodexCode\run-code.ps1 -Brief Tools\CodexCode\briefs\CODE-01.md
# - 본 작업 폴더(develop)에서 코덱스가 Game/ 코드와 시험 코드를 고친다. 깃 쓰기·유니티 실행은 하지 않는다
# - 끝나면 CODE_RESULT 파일(.codex-code/<이름>_RESULT.md)을 남긴다 → 클로드(또는 사용자)가 유니티 시험을 돌리고 커밋한다
param([Parameter(Mandatory = $true)][string]$Brief)
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$repo = 'C:\Unity\1\HumanLastRampart'
$codex = Get-ChildItem "$env:LOCALAPPDATA\OpenAI\Codex\bin" -Recurse -Filter codex.exe -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $codex) { Write-Output 'Codex 실행 파일을 찾지 못했습니다'; exit 1 }
$name = [IO.Path]::GetFileNameWithoutExtension($Brief)
$work = Join-Path $repo '.codex-code'
New-Item -ItemType Directory -Force $work | Out-Null
$common = Get-Content -Raw -Encoding UTF8 (Join-Path $repo 'Tools\CodexCode\COMMON.md')
$body = Get-Content -Raw -Encoding UTF8 (Join-Path $repo $Brief)
$prompt = ($common + "`n`n[Result file] .codex-code/${name}_RESULT.md`n`n[Brief]`n" + $body).Replace('"', '\"')
$log = Join-Path $work "${name}_LOG.jsonl"
$out = Join-Path $work "${name}_OUTPUT.md"
Write-Output "== $name 시작 $(Get-Date -Format 'MM-dd HH:mm')"
$null | & $codex.FullName exec -C $repo -s workspace-write --skip-git-repo-check -o $out --json $prompt > $log 2>&1
Write-Output "== $name 끝 $(Get-Date -Format 'MM-dd HH:mm') (종료 코드 $LASTEXITCODE) — 결과 .codex-code\${name}_RESULT.md"
if (Select-String -Path $log -Pattern 'out of credits|usage limit' -Quiet -ErrorAction SilentlyContinue) { Write-Output '  주의: 코덱스 한도로 멈춤' }
