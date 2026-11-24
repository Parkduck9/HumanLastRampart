# 윈도 빌드 스모크 실행: exe를 -hlrSmoke로 돌리고 결과 폴더 경로를 출력한다
# 사용 예: .\smoke-win.ps1 -Dir C:\Unity\1\HLR-builds\w44
param(
    [string]$Dir = 'C:\Unity\1\HLR-builds\win',
    [string]$Out = ($Dir.TrimEnd('\') + '_smoke')
)
$exe = Join-Path $Dir 'Lightbearers.exe'
Remove-Item $Out -Recurse -Force -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $exe -ArgumentList @('-hlrSmoke','-hlrSmokeOut',"`"$Out`"",'-screen-fullscreen','0') -Wait -PassThru
Write-Output "종료 코드 $($p.ExitCode), 결과 $Out"
Get-ChildItem $Out -Filter *.txt -ErrorAction SilentlyContinue | ForEach-Object { Get-Content $_.FullName | Select-Object -Last 25 }
