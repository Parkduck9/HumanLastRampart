# 새 작업 폴더(git worktree) 만들기 + 메인의 Library 복사(깨지기 쉬운 항목 제외)
# 사용 예: .\new-worktree.ps1 -Name W17 -Branch feature/w17-xxx
param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$Branch,
    [string]$Main = 'C:\Unity\1\HumanLastRampart',
    [string]$Git = 'C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe'
)
$ErrorActionPreference = 'Stop'
$sw = [Diagnostics.Stopwatch]::StartNew()
$dest = "C:\Unity\1\HLR-work\$Name"
& $Git -C $Main worktree add $dest -b $Branch develop | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Output "worktree 생성 실패"; exit 1 }
$src = Join-Path $Main 'Game\Library'
$dst = Join-Path $dest 'Game\Library'
if (Test-Path $src) {
    # 제외: 스크립트 어셈블리/Bee/ScriptMapper/MonoManager(복사하면 DefinitionCatalog 로딩이 깨짐), 잠금 파일
    robocopy $src $dst /E /MT:16 /NFL /NDL /NJH /NJS /NP /XD ScriptAssemblies Bee /XF ScriptMapper MonoManager.asset UnityLockfile *.lock | Out-Null
    if ($LASTEXITCODE -ge 8) { Write-Output "Library 복사 실패 (robocopy $LASTEXITCODE)"; exit 1 }
}
$sw.Stop()
Write-Output "$dest 생성 완료, $([int]$sw.Elapsed.TotalSeconds)초"
exit 0