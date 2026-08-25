param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$playerRoot = Join-Path $repositoryRoot 'Builds\Windows'
$installerRoot = Join-Path $repositoryRoot 'Builds\Installer'
$installerScript = Join-Path $PSScriptRoot 'SyndicatesAndEmpires.iss'

$requiredPaths = @(
    (Join-Path $playerRoot 'SyndicatesAndEmpires.exe'),
    (Join-Path $playerRoot 'SyndicatesAndEmpires_Data'),
    (Join-Path $playerRoot 'MonoBleedingEdge'),
    (Join-Path $playerRoot 'UnityPlayer.dll')
)
foreach ($requiredPath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Windows 플레이어 빌드가 불완전합니다: $requiredPath"
    }
}

$compilerCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
)
$compilerPath = $compilerCandidates |
    Where-Object { $_ -and (Test-Path -LiteralPath $_) } |
    Select-Object -First 1
if (-not $compilerPath) {
    throw 'Inno Setup 6 ISCC.exe를 찾을 수 없습니다.'
}

New-Item -ItemType Directory -Path $installerRoot -Force | Out-Null
$env:SYNDICATES_AND_EMPIRES_INSTALLER_VERSION = $Version
try {
    & $compilerPath /Qp $installerScript
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup 컴파일 실패: 종료 코드 $LASTEXITCODE"
    }
}
finally {
    Remove-Item Env:SYNDICATES_AND_EMPIRES_INSTALLER_VERSION -ErrorAction SilentlyContinue
}

$installerPath = Join-Path $installerRoot "SyndicatesAndEmpires_Setup_$Version.exe"
if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "설치 파일이 생성되지 않았습니다: $installerPath"
}

$hash = Get-FileHash -LiteralPath $installerPath -Algorithm SHA256
$checksumPath = "$installerPath.sha256"
$checksumLine = "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($installerPath))"
Set-Content -LiteralPath $checksumPath -Value $checksumLine -Encoding ascii

Write-Output "installer=$installerPath"
Write-Output "sha256=$($hash.Hash.ToLowerInvariant())"
Write-Output "checksum=$checksumPath"
