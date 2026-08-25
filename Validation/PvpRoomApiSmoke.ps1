param(
    [string]$DotnetPath = 'D:\dotnet\dotnet.exe',
    [int]$Port = 15100
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$serverDll = Join-Path $projectRoot 'Server\Game.Server\bin\Debug\net10.0\Game.Server.dll'
$artifactRoot = 'D:\SyndicatesAndEmpires\ServerTests'
$runId = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$runRoot = Join-Path $artifactRoot $runId
$dataRoot = Join-Path $runRoot 'data'
$stdoutPath = Join-Path $runRoot 'server.stdout.log'
$stderrPath = Join-Path $runRoot 'server.stderr.log'
$baseUri = "http://127.0.0.1:$Port"

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw "Assertion failed: $message" }
}

function Invoke-JsonApi {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body,
        [string]$Token,
        [int]$ExpectedStatus = 200
    )

    Add-Type -AssemblyName System.Net.Http
    $client = New-Object System.Net.Http.HttpClient
    $client.Timeout = [TimeSpan]::FromSeconds(15)
    $httpMethod = if ($Method -eq 'Post') {
        [System.Net.Http.HttpMethod]::Post
    } else {
        [System.Net.Http.HttpMethod]::Get
    }
    $message = New-Object System.Net.Http.HttpRequestMessage($httpMethod, ($baseUri + $Path))
    try {
        if ($Token) {
            $message.Headers.Authorization =
                New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $Token)
        }
        if ($null -ne $Body) {
            $json = $Body | ConvertTo-Json -Depth 12 -Compress
            $message.Content = New-Object System.Net.Http.StringContent(
                $json, [Text.Encoding]::UTF8, 'application/json')
        }

        $response = $client.SendAsync($message).GetAwaiter().GetResult()
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        Assert-True ([int]$response.StatusCode -eq $ExpectedStatus) (
            "$Method $Path expected=$ExpectedStatus actual=$([int]$response.StatusCode) body=$content")
        if ([string]::IsNullOrWhiteSpace($content)) { return $null }
        return $content | ConvertFrom-Json
    }
    finally {
        if ($null -ne $response) { $response.Dispose() }
        $message.Dispose()
        $client.Dispose()
    }
}

function Start-TestServer {
    $env:PVP_URLS = $baseUri
    $env:PVP_DATA_DIR = $dataRoot
    $env:PVP_MAX_ROOMS = '16'
    $env:DOTNET_CLI_HOME = 'D:\dotnet\cli-home'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    return Start-Process -FilePath $DotnetPath `
        -ArgumentList @($serverDll) `
        -WorkingDirectory $projectRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru
}

function Wait-ForHealth {
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $health = Invoke-WebRequest -Uri "$baseUri/health" -UseBasicParsing -TimeoutSec 2
            if ([int]$health.StatusCode -eq 200) { return }
        }
        catch { Start-Sleep -Milliseconds 250 }
    }
    throw 'PvP test server did not start before timeout.'
}

function Stop-TestServer([System.Diagnostics.Process]$process) {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
}

if (-not (Test-Path -LiteralPath $DotnetPath)) { throw ".NET executable missing: $DotnetPath" }
if (-not (Test-Path -LiteralPath $serverDll)) { throw "Server build missing: $serverDll" }
New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null

$server = $null
try {
    $server = Start-TestServer
    Wait-ForHealth

    $hostSession = Invoke-JsonApi Post '/api/v1/rooms' @{
        displayName = 'Host'
        maxPlayers = 3
    } '' 201
    Assert-True ($hostSession.roomCode -match '^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$') 'invite code format'
    Assert-True ($hostSession.accessToken -match '^[A-F0-9]{64}$') '256-bit session token format'

    Invoke-JsonApi Get "/api/v1/rooms/$($hostSession.roomCode)" $null '' 401 | Out-Null
    Invoke-JsonApi Post "/api/v1/rooms/$($hostSession.roomCode)/start" $null $hostSession.accessToken 409 | Out-Null

    $guest = Invoke-JsonApi Post "/api/v1/rooms/$($hostSession.roomCode)/join" @{
        displayName = 'Guest'
    } '' 201
    Invoke-JsonApi Post "/api/v1/rooms/$($hostSession.roomCode)/join" @{
        displayName = 'Guest'
    } '' 409 | Out-Null
    Invoke-JsonApi Post "/api/v1/rooms/$($hostSession.roomCode)/start" $null $guest.accessToken 403 | Out-Null

    $started = Invoke-JsonApi Post "/api/v1/rooms/$($hostSession.roomCode)/start" $null $hostSession.accessToken 200
    Assert-True ($started.status -eq 'Active') 'room active state'
    Invoke-JsonApi Post "/api/v1/rooms/$($hostSession.roomCode)/join" @{
        displayName = 'LateGuest'
    } '' 409 | Out-Null

    $roomRuns = @([pscustomobject]@{
        HostSession = $hostSession
        GuestSession = $guest
    })
    for ($roomNumber = 2; $roomNumber -le 3; $roomNumber++) {
        $roomHostSession = Invoke-JsonApi Post '/api/v1/rooms' @{
            displayName = "Host$roomNumber"
            maxPlayers = 2
        } '' 201
        $roomGuestSession = Invoke-JsonApi Post "/api/v1/rooms/$($roomHostSession.roomCode)/join" @{
            displayName = "Guest$roomNumber"
        } '' 201
        if ($roomNumber -eq 2) {
            Invoke-JsonApi Post "/api/v1/rooms/$($roomHostSession.roomCode)/join" @{
                displayName = 'OverCapacity'
            } '' 409 | Out-Null
        }
        Invoke-JsonApi Get '/api/v1/rooms/AAAAAA' $null '' 404 | Out-Null
        $roomStarted = Invoke-JsonApi Post "/api/v1/rooms/$($roomHostSession.roomCode)/start" $null $roomHostSession.accessToken 200
        Assert-True ($roomStarted.status -eq 'Active') "room $roomNumber active state"
        $roomRuns += [pscustomobject]@{
            HostSession = $roomHostSession
            GuestSession = $roomGuestSession
        }
    }
    Assert-True (($roomRuns.HostSession.roomCode | Select-Object -Unique).Count -eq 3) 'unique concurrent room codes'

    foreach ($roomRun in $roomRuns) {
        $initial = Invoke-JsonApi Get "/api/v1/rooms/$($roomRun.HostSession.roomCode)/match" $null $roomRun.HostSession.accessToken 200
        Assert-True ($null -ne $initial.world.map) 'authoritative map snapshot'
        Assert-True ($initial.world.map.width -eq 80 -and $initial.world.map.height -eq 48) 'authoritative map size'
        $ownPlayer = $initial.players | Where-Object playerId -eq $roomRun.HostSession.playerId
        $ownUnit = $initial.world.map.units |
            Where-Object ownerCompanyId -eq $roomRun.HostSession.companyId |
            Select-Object -First 1
        Assert-True ($null -ne $ownUnit) 'host starting map unit'

        $target = $null
        foreach ($offset in @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))) {
            $targetX = ($ownUnit.x + $offset[0] + $initial.world.map.width) % $initial.world.map.width
            $targetY = $ownUnit.y + $offset[1]
            if ($targetY -lt 0 -or $targetY -ge $initial.world.map.height) { continue }
            $terrainIndex = $targetY * $initial.world.map.width + $targetX
            if ([int]$initial.world.map.terrain[$terrainIndex] -ne 0) {
                $target = [pscustomobject]@{ x = $targetX; y = $targetY }
                break
            }
        }
        Assert-True ($null -ne $target) 'adjacent land movement target'

        if ($roomRun -eq $roomRuns[0]) {
            $guestState = Invoke-JsonApi Get "/api/v1/rooms/$($roomRun.HostSession.roomCode)/match" $null $roomRun.GuestSession.accessToken 200
            $guestPlayer = $guestState.players | Where-Object playerId -eq $roomRun.GuestSession.playerId
            Invoke-JsonApi Post "/api/v1/rooms/$($roomRun.HostSession.roomCode)/commands" @{
                requestId = "wrong-owner-$($roomRun.GuestSession.playerId)"
                protocolVersion = 1
                matchId = $guestState.matchId
                expectedRevision = $guestState.revision
                commandId = "wrong-owner-command"
                turn = $guestState.turn
                sequence = $guestPlayer.expectedSequence
                kind = 'MoveUnit'
                regionId = 'map'
                targetId = $ownUnit.unitId
                quantity = 0
                limitPrice = 0
                targetX = $target.x
                targetY = $target.y
                action = ''
            } $roomRun.GuestSession.accessToken 409 | Out-Null
        }

        $move = Invoke-JsonApi Post "/api/v1/rooms/$($roomRun.HostSession.roomCode)/commands" @{
            requestId = "move-$($roomRun.HostSession.playerId)"
            protocolVersion = 1
            matchId = $initial.matchId
            expectedRevision = $initial.revision
            commandId = "move-command-$($roomRun.HostSession.playerId)"
            turn = $initial.turn
            sequence = $ownPlayer.expectedSequence
            kind = 'MoveUnit'
            regionId = 'map'
            targetId = $ownUnit.unitId
            quantity = 0
            limitPrice = 0
            targetX = $target.x
            targetY = $target.y
            action = ''
        } $roomRun.HostSession.accessToken 200
        Assert-True $move.accepted 'authoritative movement command accepted'
        $roomRun | Add-Member -NotePropertyName UnitId -NotePropertyValue $ownUnit.unitId
        $roomRun | Add-Member -NotePropertyName TargetX -NotePropertyValue $target.x
        $roomRun | Add-Member -NotePropertyName TargetY -NotePropertyValue $target.y
    }

    foreach ($roomRun in $roomRuns) {
        for ($round = 1; $round -le 5; $round++) {
            foreach ($session in @($roomRun.HostSession, $roomRun.GuestSession)) {
                Start-Sleep -Milliseconds 120
                $match = Invoke-JsonApi Get "/api/v1/rooms/$($roomRun.HostSession.roomCode)/match" $null $session.accessToken 200
                $ownPlayer = $match.players | Where-Object playerId -eq $session.playerId
                Assert-True ($null -ne $ownPlayer) "round $round player state"
                $ready = Invoke-JsonApi Post "/api/v1/rooms/$($roomRun.HostSession.roomCode)/ready" @{
                    requestId = "smoke-$round-$($session.playerId)"
                    protocolVersion = 1
                    matchId = $match.matchId
                    turn = $match.turn
                    expectedRevision = $match.revision
                    lastSequence = $ownPlayer.expectedSequence
                } $session.accessToken 200
                Assert-True $ready.accepted "round $round ready request"
            }
        }
    }

    $statesBeforeRestart = @()
    foreach ($roomRun in $roomRuns) {
        $beforeRestart = Invoke-JsonApi Get "/api/v1/rooms/$($roomRun.HostSession.roomCode)/match" $null $roomRun.HostSession.accessToken 200
        Assert-True ($beforeRestart.turn -eq 6) 'turn after five resolutions'
        $movedUnit = $beforeRestart.world.map.units |
            Where-Object unitId -eq $roomRun.UnitId
        Assert-True (
            $movedUnit.x -eq $roomRun.TargetX -and
            $movedUnit.y -eq $roomRun.TargetY) 'authoritative unit movement result'
        $statesBeforeRestart += $beforeRestart

        $roomFile = Join-Path $dataRoot "rooms\$($roomRun.HostSession.roomCode).room.json"
        $persisted = Get-Content -LiteralPath $roomFile -Raw -Encoding utf8
        Assert-True (-not $persisted.Contains($roomRun.HostSession.accessToken)) 'host token is not persisted in plaintext'
        Assert-True (-not $persisted.Contains($roomRun.GuestSession.accessToken)) 'guest token is not persisted in plaintext'
    }

    Stop-TestServer $server
    $server = Start-TestServer
    Wait-ForHealth
    for ($index = 0; $index -lt $roomRuns.Count; $index++) {
        $roomRun = $roomRuns[$index]
        $afterRestart = Invoke-JsonApi Get "/api/v1/rooms/$($roomRun.HostSession.roomCode)/match" $null $roomRun.HostSession.accessToken 200
        Assert-True ($afterRestart.turn -eq 6) 'turn restored after restart'
        Assert-True ($afterRestart.matchId -eq $statesBeforeRestart[$index].matchId) 'match ID restored after restart'
    }

    Write-Output "PASS PvpRoomApiSmoke rooms=3 playersPerRoom=2 turnsPerRoom=5 restoredTurn=$($afterRestart.turn)"
    Write-Output "artifacts=$runRoot"
}
finally {
    Stop-TestServer $server
}
