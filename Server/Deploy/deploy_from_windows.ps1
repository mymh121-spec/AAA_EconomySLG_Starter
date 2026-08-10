[CmdletBinding()]
param(
    [string]$ServerHost = "101.79.19.253",
    [string]$ServerUser = "economyslg",
    [string]$KeyPath,
    [switch]$SkipTurnVerification
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = (Resolve-Path (Join-Path $scriptDirectory "..\..")).Path
$workspaceRoot = (Resolve-Path (Join-Path $projectRoot "..\..")).Path

if ([string]::IsNullOrWhiteSpace($KeyPath)) {
    $KeyPath = Join-Path $workspaceRoot ".server-access\economyslg_server_ed25519"
}

$archive = Join-Path $workspaceRoot "work\publish\game-server-0.2.0-linux-x64.tar.gz"
$deployScript = Join-Path $scriptDirectory "deploy_user.sh"
$verifyScript = Join-Path $scriptDirectory "verify_remote.sh"
$remote = "${ServerUser}@${ServerHost}"
$appRoot = "/home/economyslg/apps/economy-slg"

foreach ($requiredFile in @($KeyPath, $archive, $deployScript, $verifyScript)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required file was not found: $requiredFile"
    }
}

function Invoke-NativeChecked {
    param(
        [Parameter(Mandatory)]
        [string]$Executable,
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Executable failed with exit code $LASTEXITCODE"
    }
}

Write-Host "[1/4] Preparing the remote deployment directory"
Invoke-NativeChecked "ssh" @(
    "-i", $KeyPath, "-o", "BatchMode=yes", $remote,
    "mkdir -p '$appRoot/incoming'"
)

Write-Host "[2/4] Uploading the server package and deployment scripts"
Invoke-NativeChecked "scp" @(
    "-i", $KeyPath, "-o", "BatchMode=yes", $archive,
    "${remote}:$appRoot/incoming/game-server-0.2.0-linux-x64.tar.gz"
)
Invoke-NativeChecked "scp" @(
    "-i", $KeyPath, "-o", "BatchMode=yes", $deployScript, $verifyScript,
    "${remote}:$appRoot/"
)

Write-Host "[3/4] Validating Bash scripts and deploying 0.2.0"
$remoteDeployCommand = @(
    "chmod 750 '$appRoot/deploy_user.sh' '$appRoot/verify_remote.sh'",
    "bash -n '$appRoot/deploy_user.sh'",
    "bash -n '$appRoot/verify_remote.sh'",
    "bash '$appRoot/deploy_user.sh'"
) -join " && "
Invoke-NativeChecked "ssh" @(
    "-i", $KeyPath, "-o", "BatchMode=yes", $remote,
    $remoteDeployCommand
)

if (-not $SkipTurnVerification) {
    Write-Host "[4/4] Running one authoritative turn on the remote server"
    Invoke-NativeChecked "ssh" @(
        "-i", $KeyPath, "-o", "BatchMode=yes", $remote,
        "bash '$appRoot/verify_remote.sh'"
    )
}
else {
    Write-Host "[4/4] Turn verification skipped"
}

Write-Host "PvP server 0.2.0 deployment completed."
