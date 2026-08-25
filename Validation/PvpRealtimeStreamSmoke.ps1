param(
    [string]$DotnetPath = 'D:\dotnet\dotnet.exe',
    [string]$ArtifactRoot = 'D:\SyndicatesAndEmpires\ServerTests',
    [int]$Port = 5317
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' +
    [Guid]::NewGuid().ToString('N').Substring(0, 8)
$artifactDirectory = Join-Path $ArtifactRoot $runId
$dataDirectory = Join-Path $artifactDirectory 'data'
$stdoutLog = Join-Path $artifactDirectory 'server.stdout.log'
$stderrLog = Join-Path $artifactDirectory 'server.stderr.log'
New-Item -ItemType Directory -Path $dataDirectory -Force | Out-Null

$env:DOTNET_CLI_HOME = 'D:\dotnet\cli-home'
$env:NUGET_PACKAGES = 'D:\dotnet\packages'
$env:PVP_DATA_DIR = $dataDirectory
$env:PVP_URLS = "http://127.0.0.1:$Port"
$env:PVP_TURN_TIMEOUT_SECONDS = '15'

$server = Start-Process -FilePath $DotnetPath `
    -ArgumentList @(
        'run',
        '--project',
        (Join-Path $repositoryRoot 'Server\Game.Server\Game.Server.csproj'),
        '--no-build') `
    -WorkingDirectory $repositoryRoot `
    -WindowStyle Hidden `
    -RedirectStandardOutput $stdoutLog `
    -RedirectStandardError $stderrLog `
    -PassThru

try {
    $healthy = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $response = Invoke-WebRequest `
                -Uri "http://127.0.0.1:$Port/health" `
                -UseBasicParsing `
                -TimeoutSec 2
            if ($response.StatusCode -eq 200) {
                $healthy = $true
                break
            }
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }
    if (-not $healthy) {
        throw 'PvP test server did not become healthy.'
    }

    & $DotnetPath run `
        --project (Join-Path $repositoryRoot `
            'Validation\PvpRealtimeStreamSmoke\PvpRealtimeStreamSmoke.csproj') `
        -- "http://127.0.0.1:$Port"
    if ($LASTEXITCODE -ne 0) {
        throw "PvpRealtimeStreamSmoke exited with $LASTEXITCODE."
    }
    Write-Output "artifacts=$artifactDirectory"
}
finally {
    if ($null -ne $server -and -not $server.HasExited) {
        Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
        $server.WaitForExit()
    }
}
