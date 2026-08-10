[CmdletBinding()]
param(
    [string]$DestinationDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$sourceProject = (Resolve-Path $PSScriptRoot).Path
$launcher = Join-Path $sourceProject "RUN_MY_PROJECT_GAME.cmd"
if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
    throw "Launcher is missing: $launcher"
}

if ([string]::IsNullOrWhiteSpace($DestinationDirectory)) {
    $DestinationDirectory = [Environment]::GetFolderPath("Desktop")
}
if ([string]::IsNullOrWhiteSpace($DestinationDirectory) -or
    -not (Test-Path -LiteralPath $DestinationDirectory -PathType Container)) {
    throw "Shortcut destination folder could not be resolved."
}

$shortcutName = "$([char]0xACBD)$([char]0xC81C) SLG $([char]0xC2E4)$([char]0xD589).lnk"
$shortcutPath = Join-Path $DestinationDirectory $shortcutName
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $launcher
$shortcut.WorkingDirectory = $sourceProject
$shortcut.Description = "Synchronize and run Economy SLG"

$unityEditor = "C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe"
if (Test-Path -LiteralPath $unityEditor -PathType Leaf) {
    $shortcut.IconLocation = $unityEditor + ",0"
}

$shortcut.Save()
Write-Host "Desktop shortcut created: $shortcutPath"
