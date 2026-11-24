$ErrorActionPreference = 'Stop'
# 선택본 알파 경계 실측과 최근접 출력·검사를 함께 재생성한다.
$pythonPath = 'C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $pythonPath -W ignore (Join-Path $PSScriptRoot 'build.py')
if ($LASTEXITCODE -ne 0) { throw '측정 및 도트 출력 재생성 실패' }
