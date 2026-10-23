# 윈도 빌드 도구: 결과를 한 줄 요약으로만 출력한다 (성공/실패, 크기 MB, 걸린 분, 로그 경로)
# 사용 예: .\build-win.ps1 -Project C:\Unity\1\HLR-work\W29\Game [-Dev]
param(
    [string]$Project = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Game'),
    [switch]$Dev,
    [string]$OutDir = 'C:\Unity\1\HLR-builds\win',
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$Project = (Resolve-Path $Project).Path.TrimEnd('\')
New-Item -ItemType Directory -Force $OutDir | Out-Null
$log = Join-Path (Split-Path $OutDir -Parent) 'build-win.log'
Remove-Item $log -ErrorAction SilentlyContinue
$method = if ($Dev) { 'HumanLastRampart.App.Editor.BuildScripts.BuildWindowsDev' } else { 'HumanLastRampart.App.Editor.BuildScripts.BuildWindows' }
$a = @('-batchmode','-quit','-projectPath',"`"$Project`"",'-buildTarget','StandaloneWindows64','-executeMethod',$method,'-logFile',"`"$log`"")
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $Unity -ArgumentList $a -Wait -NoNewWindow -PassThru
$sw.Stop()
$min = [math]::Round($sw.Elapsed.TotalMinutes, 1)
$exe = Join-Path $OutDir 'HumanLastRampart.exe'
$ok = ($p.ExitCode -eq 0) -and (Test-Path $exe)
if ($ok) {
    $mb = [math]::Round(((Get-ChildItem $OutDir -Recurse -File | Measure-Object Length -Sum).Sum) / 1MB, 1)
    $line = Get-Content $log | Where-Object { $_ -match '\[Build\] 결과' } | Select-Object -Last 1
    Write-Output "윈도 빌드 성공, $mb MB, ${min}분, 로그 $log"
    if ($line) { Write-Output $line }
    exit 0
}
Write-Output "윈도 빌드 실패 (종료 코드 $($p.ExitCode)), ${min}분, 로그 $log"
if (Test-Path $log) {
    Get-Content $log | Where-Object { $_ -match 'error CS|Build Failed|\[Build\]|Exception' } | Select-Object -Last 15 | ForEach-Object { Write-Output $_ }
}
exit 1
