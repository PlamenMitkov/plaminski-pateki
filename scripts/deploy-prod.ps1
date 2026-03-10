param(
    [ValidateSet("pull", "up", "down", "restart", "logs", "ps", "config", "rollback", "status")]
    [string]$Action = "up",

    [string]$EnvFile = ".env.production",

    [string]$ComposeFile = "docker-compose.prod.yml",

    [string]$ApiTag = "",

    [string]$ClientTag = "",

    [string]$ApiHealthUrl = "http://127.0.0.1:5218/health/ready",

    [string]$ClientHealthUrl = "",

    [switch]$Json
)

$ErrorActionPreference = "Stop"

Set-Location -Path (Join-Path $PSScriptRoot "..")

if (-not (Test-Path -Path $ComposeFile)) {
    Write-Error "Compose file not found: $ComposeFile"
}

if (-not (Test-Path -Path $EnvFile)) {
    Write-Error "Env file not found: $EnvFile. Create it from .env.production.example"
}

$dockerExists = $null -ne (Get-Command docker -ErrorAction SilentlyContinue)
if (-not $dockerExists) {
    Write-Error "Docker CLI not found. Install Docker Desktop / docker engine first."
}

docker info *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker daemon is not running. Start Docker and retry."
}

$baseArgs = @("compose", "--env-file", $EnvFile, "-f", $ComposeFile)

function Invoke-ComposeWithRollbackOverrides {
    param(
        [string[]]$ComposeActionArgs
    )

    $previousApiTag = $env:API_IMAGE_TAG
    $previousClientTag = $env:CLIENT_IMAGE_TAG

    if (-not [string]::IsNullOrWhiteSpace($ApiTag)) {
        $env:API_IMAGE_TAG = $ApiTag
    }

    if (-not [string]::IsNullOrWhiteSpace($ClientTag)) {
        $env:CLIENT_IMAGE_TAG = $ClientTag
    }

    try {
        & docker @baseArgs @ComposeActionArgs
    }
    finally {
        if ($null -eq $previousApiTag) {
            Remove-Item Env:API_IMAGE_TAG -ErrorAction SilentlyContinue
        }
        else {
            $env:API_IMAGE_TAG = $previousApiTag
        }

        if ($null -eq $previousClientTag) {
            Remove-Item Env:CLIENT_IMAGE_TAG -ErrorAction SilentlyContinue
        }
        else {
            $env:CLIENT_IMAGE_TAG = $previousClientTag
        }
    }
}

switch ($Action) {
    "pull" {
        & docker @baseArgs pull
        break
    }
    "up" {
        & docker @baseArgs pull
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        & docker @baseArgs up -d
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        & docker @baseArgs ps
        break
    }
    "down" {
        & docker @baseArgs down
        break
    }
    "restart" {
        & docker @baseArgs pull
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        & docker @baseArgs up -d --force-recreate
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        & docker @baseArgs ps
        break
    }
    "logs" {
        & docker @baseArgs logs -f --tail 200
        break
    }
    "ps" {
        & docker @baseArgs ps
        break
    }
    "config" {
        & docker @baseArgs config
        break
    }
    "rollback" {
        if ([string]::IsNullOrWhiteSpace($ApiTag) -and [string]::IsNullOrWhiteSpace($ClientTag)) {
            Write-Error "Rollback requires at least one tag: -ApiTag and/or -ClientTag"
        }

        $services = @()
        if (-not [string]::IsNullOrWhiteSpace($ApiTag)) { $services += "api" }
        if (-not [string]::IsNullOrWhiteSpace($ClientTag)) { $services += "client" }

        Invoke-ComposeWithRollbackOverrides -ComposeActionArgs (@("pull") + $services)
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        Invoke-ComposeWithRollbackOverrides -ComposeActionArgs (@("up", "-d", "--force-recreate") + $services)
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        & docker @baseArgs ps
        break
    }
    "status" {
        if (-not $Json) {
            & docker @baseArgs ps
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

            try {
                $response = Invoke-WebRequest -Uri $ApiHealthUrl -Method Get -TimeoutSec 10
                Write-Host "API health check OK: HTTP $($response.StatusCode) ($ApiHealthUrl)"
            }
            catch {
                $statusCode = $_.Exception.Response.StatusCode.value__
                if ($statusCode) {
                    Write-Error "API health check failed: HTTP $statusCode ($ApiHealthUrl)"
                }
                else {
                    Write-Error "API health check failed: $($_.Exception.Message) ($ApiHealthUrl)"
                }
            }

            if (-not [string]::IsNullOrWhiteSpace($ClientHealthUrl)) {
                try {
                    $clientResponse = Invoke-WebRequest -Uri $ClientHealthUrl -Method Get -TimeoutSec 10
                    Write-Host "Client health check OK: HTTP $($clientResponse.StatusCode) ($ClientHealthUrl)"
                }
                catch {
                    $clientStatusCode = $_.Exception.Response.StatusCode.value__
                    if ($clientStatusCode) {
                        Write-Error "Client health check failed: HTTP $clientStatusCode ($ClientHealthUrl)"
                    }
                    else {
                        Write-Error "Client health check failed: $($_.Exception.Message) ($ClientHealthUrl)"
                    }
                }
            }

            break
        }

        $composeItems = @()
        $composeError = ""
        try {
            $composeRaw = & docker @baseArgs ps --format json
            if ($LASTEXITCODE -ne 0) {
                $composeError = "docker compose ps failed with exit code $LASTEXITCODE"
            }
            elseif (-not [string]::IsNullOrWhiteSpace($composeRaw)) {
                $composeItems = $composeRaw | ConvertFrom-Json
            }
        }
        catch {
            $composeError = $_.Exception.Message
        }

        $healthStatusCode = 0
        $healthOk = $false
        $healthError = ""
        try {
            $response = Invoke-WebRequest -Uri $ApiHealthUrl -Method Get -TimeoutSec 10
            $healthStatusCode = [int]$response.StatusCode
            $healthOk = $healthStatusCode -eq 200
            if (-not $healthOk) {
                $healthError = "Unexpected status code: $healthStatusCode"
            }
        }
        catch {
            $healthStatusCode = $_.Exception.Response.StatusCode.value__
            if (-not $healthStatusCode) {
                $healthStatusCode = 0
            }
            $healthError = $_.Exception.Message
        }

        $clientEnabled = -not [string]::IsNullOrWhiteSpace($ClientHealthUrl)
        $clientStatusCode = 0
        $clientOk = $false
        $clientError = ""
        if ($clientEnabled) {
            try {
                $clientResponse = Invoke-WebRequest -Uri $ClientHealthUrl -Method Get -TimeoutSec 10
                $clientStatusCode = [int]$clientResponse.StatusCode
                $clientOk = $clientStatusCode -eq 200
                if (-not $clientOk) {
                    $clientError = "Unexpected status code: $clientStatusCode"
                }
            }
            catch {
                $clientStatusCode = $_.Exception.Response.StatusCode.value__
                if (-not $clientStatusCode) {
                    $clientStatusCode = 0
                }
                $clientError = $_.Exception.Message
            }
        }

        $overallOk = $healthOk -and [string]::IsNullOrWhiteSpace($composeError) -and (-not $clientEnabled -or $clientOk)
        $payload = [ordered]@{
            timestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            action = "status"
            overallOk = $overallOk
            compose = $composeItems
            composeError = $composeError
            apiHealth = [ordered]@{
                url = $ApiHealthUrl
                ok = $healthOk
                statusCode = $healthStatusCode
                error = $healthError
            }
            clientHealth = [ordered]@{
                enabled = $clientEnabled
                url = $ClientHealthUrl
                ok = $clientOk
                statusCode = $clientStatusCode
                error = $clientError
            }
        }

        $payload | ConvertTo-Json -Depth 8
        if (-not $overallOk) { exit 1 }
        break
    }
}

exit $LASTEXITCODE