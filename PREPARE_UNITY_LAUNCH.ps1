[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$runningEditors = @(Get-Process Unity -ErrorAction SilentlyContinue)
if ($runningEditors.Count -gt 0) {
    Write-Host "Close every open Unity Editor before launching this project."
    exit 10
}

$editorLicensingPathPart =
    "\Editor\Data\Resources\Licensing\Client\"
$licensingClients = @(
    Get-Process "Unity.Licensing.Client" -ErrorAction SilentlyContinue)

foreach ($client in $licensingClients) {
    $clientPath = $null
    try {
        $clientPath = $client.Path
    } catch {
        continue
    }

    if (-not [string]::IsNullOrWhiteSpace($clientPath) -and
        $clientPath.IndexOf(
            $editorLicensingPathPart,
            [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Stop-Process -Id $client.Id -Force -ErrorAction SilentlyContinue
    }
}

Start-Sleep -Milliseconds 500
exit 0
