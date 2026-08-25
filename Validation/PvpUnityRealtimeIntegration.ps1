param(
    [string]$DotnetPath = 'D:\dotnet\dotnet.exe',
    [string]$UnityPath = 'D:\Unity\Editor\6000.3.21f1\Editor\Unity.exe',
    [string]$ArtifactRoot = 'D:\SyndicatesAndEmpires\UnityTests',
    [int]$Port = 5327
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' +
    [Guid]::NewGuid().ToString('N').Substring(0, 8)
$artifactDirectory = Join-Path $ArtifactRoot "realtime-integration-$runId"
$dataDirectory = Join-Path $artifactDirectory 'server-data'
$serverOut = Join-Path $artifactDirectory 'server.stdout.log'
$serverErr = Join-Path $artifactDirectory 'server.stderr.log'
$unityLog = Join-Path $artifactDirectory 'unity-playmode.log'
$results = Join-Path $artifactDirectory 'unity-playmode-results.xml'
New-Item -ItemType Directory -Path $dataDirectory -Force | Out-Null

$env:DOTNET_CLI_HOME = 'D:\dotnet\cli-home'
$env:NUGET_PACKAGES = 'D:\dotnet\packages'
$env:PVP_DATA_DIR = $dataDirectory
$env:PVP_URLS = "http://127.0.0.1:$Port"
$env:PVP_TURN_TIMEOUT_SECONDS = '120'
$server = Start-Process -FilePath $DotnetPath `
    -ArgumentList @(
        'run',
        '--project',
        (Join-Path $repositoryRoot 'Server\Game.Server\Game.Server.csproj'),
        '--no-build') `
    -WorkingDirectory $repositoryRoot `
    -WindowStyle Hidden `
    -RedirectStandardOutput $serverOut `
    -RedirectStandardError $serverErr `
    -PassThru

try {
    $baseUri = "http://127.0.0.1:$Port"
    $healthy = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $health = Invoke-WebRequest `
                -Uri "$baseUri/health" `
                -UseBasicParsing `
                -TimeoutSec 2
            if ($health.StatusCode -eq 200) {
                $healthy = $true
                break
            }
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }
    if (-not $healthy) {
        throw 'PvP integration server did not become healthy.'
    }

    $hostSession = Invoke-RestMethod `
        -Method Post `
        -Uri "$baseUri/api/v1/rooms" `
        -ContentType 'application/json' `
        -Body (@{ displayName = 'Unity Host'; maxPlayers = 2 } |
            ConvertTo-Json -Compress)
    $guestSession = Invoke-RestMethod `
        -Method Post `
        -Uri "$baseUri/api/v1/rooms/$($hostSession.roomCode)/join" `
        -ContentType 'application/json' `
        -Body (@{ displayName = 'Unity Guest' } | ConvertTo-Json -Compress)
    $headers = @{ Authorization = "Bearer $($hostSession.accessToken)" }
    Invoke-RestMethod `
        -Method Post `
        -Uri "$baseUri/api/v1/rooms/$($hostSession.roomCode)/start" `
        -Headers $headers | Out-Null

    $env:PVP_UNITY_INTEGRATION_ENDPOINT = $baseUri
    $env:PVP_UNITY_INTEGRATION_ROOM = $hostSession.roomCode
    $env:PVP_UNITY_INTEGRATION_TOKEN = $hostSession.accessToken
    $arguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath',
        $repositoryRoot,
        '-runTests',
        '-testPlatform',
        'PlayMode',
        '-testResults',
        $results,
        '-logFile',
        $unityLog)
    $unity = Start-Process -FilePath $UnityPath `
        -ArgumentList $arguments `
        -WindowStyle Hidden `
        -PassThru
    while (-not $unity.HasExited) {
        Start-Sleep -Seconds 1
        $unity.Refresh()
    }
    $unity.WaitForExit()
    if ($unity.ExitCode -ne 0) {
        Get-Content -LiteralPath $unityLog -Tail 200
        throw "Unity PlayMode integration exited with $($unity.ExitCode)."
    }
    Select-String -LiteralPath $results -Pattern '<test-run' |
        ForEach-Object { $_.Line.Trim() }
    Write-Output "artifacts=$artifactDirectory"
}
finally {
    Remove-Item Env:PVP_UNITY_INTEGRATION_ENDPOINT -ErrorAction SilentlyContinue
    Remove-Item Env:PVP_UNITY_INTEGRATION_ROOM -ErrorAction SilentlyContinue
    Remove-Item Env:PVP_UNITY_INTEGRATION_TOKEN -ErrorAction SilentlyContinue
    if ($null -ne $server -and -not $server.HasExited) {
        Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
        $server.WaitForExit()
    }
}
