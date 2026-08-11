param(
    [string]$InterfaceVersion = "26.5.0",
    [string]$WindowsVersion = "26.4.0"
)

$ErrorActionPreference = "Stop"
$downloadDirectory = Join-Path $PSScriptRoot "Temp\HiveSdkPackages"
New-Item -ItemType Directory -Force -Path $downloadDirectory | Out-Null

function Receive-HivePackage {
    param(
        [string]$Name,
        [string]$Url
    )

    $archivePath = Join-Path $downloadDirectory $Name
    if (-not (Test-Path -LiteralPath $archivePath)) {
        Write-Host "Downloading: $Name"
        Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $archivePath
    }
    else {
        Write-Host "Using existing file: $Name"
    }

    $extractPath = Join-Path $downloadDirectory ([IO.Path]::GetFileNameWithoutExtension($Name))
    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath -Force
    return $extractPath
}

$baseUrl = "https://hive-fn.qpyou.cn/hivedev/sdk/hive_sdk_v4_with_core/unity"
$interfaceRoot = Receive-HivePackage `
    -Name "Hive_SDK_v4_Unity_Interface_$InterfaceVersion.zip" `
    -Url "$baseUrl/Hive_SDK_v4_Unity_Interface_$InterfaceVersion.zip"
$windowsRoot = Receive-HivePackage `
    -Name "Hive_SDK_v4_Unity_Platform_Windows_$WindowsVersion.zip" `
    -Url "$baseUrl/Hive_SDK_v4_Unity_Platform_Windows_$WindowsVersion.zip"

$packages = @(
    Get-ChildItem -LiteralPath $interfaceRoot -Recurse -Filter "*.unitypackage" |
        Where-Object { $_.Name -notmatch "Sample" }
    Get-ChildItem -LiteralPath $windowsRoot -Recurse -Filter "*.unitypackage" |
        Where-Object { $_.Name -notmatch "Sample" }
)

Write-Host ""
Write-Host "Download and extraction completed."
Write-Host "In Unity, use Import Package > Custom Package in this order:"
$index = 1
foreach ($package in $packages) {
    Write-Host "$index. $($package.FullName)"
    $index++
}
Write-Host ""
Write-Host "After import, follow HIVE_CONNECTION_KO.md to enable HIVE Matchmaking."
Start-Process explorer.exe -ArgumentList $downloadDirectory
