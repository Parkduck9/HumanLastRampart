# Microsoft Store 제출용 MSIX 만들기 (W46). 서명하지 않은 .msix 를 만든다 (스토어가 서명한다).
# 사용: .\package-msix.ps1 [-BuildDir C:\Unity\1\HLR-builds\win] [-Out ...\Lightbearers_<버전>_x64.msix] [-Images Tools\Build\msix\Images]
# 버전은 ProjectSettings 의 bundleVersion (x.y.z) 에서 읽어 x.y.z.0 으로 매니페스트와 파일 이름에 넣는다.
# 순서: 임시 폴더에 빌드+매니페스트+로고+고지문 모으기 -> makepri 로 resources.pri (로고만 색인) -> makeappx pack
param(
    [string]$BuildDir = 'C:\Unity\1\HLR-builds\win',
    [string]$Out = '',   # 비우면 C:\Unity\1\HLR-builds\msix\Lightbearers_<버전>_x64.msix
    [string]$Images = (Join-Path $PSScriptRoot 'msix\Images')
)
$ErrorActionPreference = 'Stop'
$msixDir = Join-Path $PSScriptRoot 'msix'

# 버전: bundleVersion x.y.z -> x.y.z.0
$projectSettings = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Game\ProjectSettings\ProjectSettings.asset'
$vm = Select-String -Path $projectSettings -Pattern '^\s+bundleVersion:\s*(\d+\.\d+\.\d+)\s*$' | Select-Object -First 1
if (-not $vm) { throw "ProjectSettings 에서 bundleVersion(x.y.z)을 읽지 못했습니다: $projectSettings" }
$Version = $vm.Matches[0].Groups[1].Value + '.0'
if (-not $Out) { $Out = "C:\Unity\1\HLR-builds\msix\Lightbearers_${Version}_x64.msix" }

$BuildDir = (Resolve-Path $BuildDir).Path.TrimEnd('\')
$Images = (Resolve-Path $Images).Path.TrimEnd('\')
if (-not (Test-Path (Join-Path $BuildDir 'Lightbearers.exe'))) { throw "빌드에 Lightbearers.exe 가 없습니다: $BuildDir" }

# 윈도 SDK 도구 (가장 새 버전)
function Find-SdkTool([string]$name) {
    $bin = 'C:\Program Files (x86)\Windows Kits\10\bin'
    $hit = Get-ChildItem $bin -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^10\.' } |
        Sort-Object { [version]$_.Name } -Descending | ForEach-Object { Join-Path $_.FullName "x64\$name" } | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $hit) { throw "$name 을 찾지 못했습니다 (Windows SDK 설치 필요)" }
    return $hit
}
$makeappx = Find-SdkTool 'makeappx.exe'
$makepri = Find-SdkTool 'makepri.exe'

# 스토어 계정 값
$id = Get-Content (Join-Path $msixDir 'store-identity.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$placeholder = ($id.IdentityName -like 'PLACEHOLDER*') -or ($id.Publisher -like 'CN=00000000*') -or ($id.PublisherDisplayName -like 'PLACEHOLDER*')

$tmp = Join-Path ([IO.Path]::GetTempPath()) ('hlr_msix_' + [Guid]::NewGuid().ToString('N'))
$stage = Join-Path $tmp 'stage'      # 묶을 폴더
$priSrc = Join-Path $tmp 'pri'       # 색인용 폴더: 로고와 매니페스트만 (게임 파일 이름이 언어·배율로 오해되지 않게)
try {
    New-Item -ItemType Directory -Force $stage, $priSrc | Out-Null
    # 1) 빌드 복사: 디버그 심볼과 Burst 디버그 정보는 뺀다
    $xd = Get-ChildItem $BuildDir -Directory -Filter '*_BurstDebugInformation_DoNotShip' | ForEach-Object { $_.FullName }
    & robocopy $BuildDir $stage /E /XF *.pdb /XD @($xd) /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy 실패 ($LASTEXITCODE)" }
    # 2) 로고, 고지문
    Copy-Item $Images (Join-Path $stage 'Images') -Recurse
    Copy-Item (Join-Path $msixDir 'THIRD_PARTY_NOTICES.txt') $stage
    # 3) 매니페스트
    $xml = [IO.File]::ReadAllText((Join-Path $msixDir 'AppxManifest.template.xml'), [Text.Encoding]::UTF8)
    $esc = { param($v) [Security.SecurityElement]::Escape([string]$v) }
    $xml = $xml.Replace('{Version}', $Version).Replace('{IdentityName}', (& $esc $id.IdentityName)).Replace('{Publisher}', (& $esc $id.Publisher)).Replace('{PublisherDisplayName}', (& $esc $id.PublisherDisplayName))
    [IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'), $xml, (New-Object Text.UTF8Encoding $false))
    # 4) 크기 배율 로고가 이름으로 풀리도록 resources.pri 생성 (로고 폴더만 색인한 뒤 결과 파일을 묶음 폴더로 복사)
    Copy-Item $Images (Join-Path $priSrc 'Images') -Recurse
    Copy-Item (Join-Path $stage 'AppxManifest.xml') $priSrc
    $cfg = Join-Path $tmp 'priconfig.xml'
    & $makepri createconfig /cf $cfg /dq ko-KR /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'makepri createconfig 실패' }
    & $makepri new /pr $priSrc /cf $cfg /mn (Join-Path $priSrc 'AppxManifest.xml') /of (Join-Path $priSrc 'resources.pri') /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'makepri new 실패' }
    Get-ChildItem $priSrc -Filter 'resources*.pri' | Copy-Item -Destination $stage
    # 5) 묶기
    New-Item -ItemType Directory -Force (Split-Path $Out -Parent) | Out-Null
    Remove-Item $Out -ErrorAction SilentlyContinue
    & $makeappx pack /d $stage /p $Out /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack 실패 ($LASTEXITCODE)" }
}
finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
$mb = [math]::Round((Get-Item $Out).Length / 1MB, 1)
Write-Output "MSIX 완성: $Out (버전 $Version, $mb MB)"
if ($placeholder) {
    Write-Warning "store-identity.json 이 아직 자리표시 값입니다. 이 패키지는 로컬 확인용이며 스토어에 올릴 수 없습니다. Partner Center 의 제품 ID(Product identity) 값으로 바꾼 뒤 다시 만드세요."
}