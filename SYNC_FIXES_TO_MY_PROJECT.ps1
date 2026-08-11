[CmdletBinding()]
param(
    [string]$TargetProject = "C:\Users\andrew\My project",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$sourceProject = (Resolve-Path $PSScriptRoot).Path
if (-not (Test-Path -LiteralPath (Join-Path $TargetProject "ProjectSettings\ProjectVersion.txt"))) {
    throw "Target is not a Unity project: $TargetProject"
}

$resolvedTargetProject = (Resolve-Path $TargetProject).Path
if ([string]::Equals($sourceProject, $resolvedTargetProject, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Source and target Unity projects must be different."
}
$TargetProject = $resolvedTargetProject

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupRoot = Join-Path $TargetProject ("_codex_backup-" + $stamp)

$relativeFiles = New-Object 'System.Collections.Generic.List[string]'
$relativeFiles.Add("RUN_SINGLE_PLAYER.cmd")
$relativeFiles.Add("RUN_WINDOWS_EXE.cmd")
$relativeFiles.Add("PREPARE_UNITY_LAUNCH.ps1")
$relativeFiles.Add("PREPARE_HIVE_CONNECTION_SDK.cmd")
$relativeFiles.Add("PREPARE_HIVE_CONNECTION_SDK.ps1")
$relativeFiles.Add("HIVE_CONNECTION_KO.md")
$relativeFiles.Add("LAND_SEA_MOVEMENT_KO.md")
$relativeFiles.Add("GAME_SCOPE_REVIEW_KO.md")
$relativeFiles.Add("MAP_INTERACTION_KO.md")
$relativeFiles.Add("WORLD_MAP_RULES_KO.md")
$relativeFiles.Add("NEXT_STEPS_KO.txt")
$relativeFiles.Add("README.md")
$relativeFiles.Add("Packages\manifest.json")

$sourceGameRoot = Join-Path $sourceProject "Assets\Game"
Get-ChildItem -LiteralPath $sourceGameRoot -Recurse -File | ForEach-Object {
    $relativeFile = $_.FullName.Substring($sourceProject.Length + 1)
    $targetFile = Join-Path $TargetProject $relativeFile

    # Preserve GUIDs already imported by the destination project. Copy a meta
    # file only when the corresponding destination meta file does not exist.
    if ($_.Extension -ne ".meta" -or
        -not (Test-Path -LiteralPath $targetFile -PathType Leaf)) {
        $relativeFiles.Add($relativeFile)
    }
}

$copiedCount = 0
$unchangedCount = 0

foreach ($relativeFile in $relativeFiles) {
    $sourceFile = Join-Path $sourceProject $relativeFile
    $targetFile = Join-Path $TargetProject $relativeFile
    if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) {
        throw "Source file is missing: $sourceFile"
    }

    $targetExists = Test-Path -LiteralPath $targetFile -PathType Leaf
    if ($targetExists) {
        $sourceHash = (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash
        $targetHash = (Get-FileHash -LiteralPath $targetFile -Algorithm SHA256).Hash
        if ($sourceHash -eq $targetHash) {
            $unchangedCount++
            continue
        }
    }

    if ($DryRun) {
        $copiedCount++
        continue
    }

    $targetParent = Split-Path -Parent $targetFile
    New-Item -ItemType Directory -Force -Path $targetParent | Out-Null

    if ($targetExists) {
        $backupFile = Join-Path $backupRoot $relativeFile
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backupFile) | Out-Null
        Copy-Item -LiteralPath $targetFile -Destination $backupFile -Force
    }

    Copy-Item -LiteralPath $sourceFile -Destination $targetFile -Force
    $copiedCount++
}

if ($DryRun) {
    Write-Host "Dry run completed. No files were changed."
} else {
    Write-Host "Unity game files were synchronized successfully."
}
Write-Host "Copied or updated: $copiedCount"
Write-Host "Already current: $unchangedCount"
if (Test-Path -LiteralPath $backupRoot) {
    Write-Host "Backup location: $backupRoot"
}
Write-Host "Open the destination project and select Game > Run Game."
