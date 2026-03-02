param(
    [string]$BaseUrl = "http://127.0.0.1:5218/api",
    [string]$TempEmail = "",
    [string]$TempPassword = "Passw0rd!",
    [switch]$SkipBuild,
    [switch]$SkipOpenRoute
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$apiProjectPath = Join-Path $repoRoot "EcoTrails.Api"
$smokeAdminScript = Join-Path $PSScriptRoot "smoke-admin.ps1"
$smokeOpenRouteScript = Join-Path $PSScriptRoot "smoke-openroute.ps1"
$cleanupScript = Join-Path $PSScriptRoot "cleanup-test-user.ps1"

if ([string]::IsNullOrWhiteSpace($TempEmail)) {
    $TempEmail = "tempadmin_$([Guid]::NewGuid().ToString('N').Substring(0, 10))@example.com"
}

function Wait-ApiReady {
    param([string]$Url)

    $rootUrl = $Url.TrimEnd('/')
    if ($rootUrl.ToLower().EndsWith('/api')) {
        $rootUrl = $rootUrl.Substring(0, $rootUrl.Length - 4)
    }

    $healthUrl = "$rootUrl/swagger/index.html"

    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing $healthUrl -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                return $true
            }
        }
        catch {
            Start-Sleep -Milliseconds 700
        }
    }

    return $false
}

function Start-ApiProcess {
    param([string]$AdminEmail)

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = "dotnet"
    $startInfo.Arguments = "run --project `"$apiProjectPath`""
    $startInfo.WorkingDirectory = [string]$repoRoot
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true

    if (-not [string]::IsNullOrWhiteSpace($AdminEmail)) {
        $startInfo.EnvironmentVariables["Admin__Emails__0"] = $AdminEmail
    }

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    [void]$process.Start()

    return $process
}

function Stop-ApiProcess {
    param([System.Diagnostics.Process]$Process)

    if ($null -eq $Process) {
        return
    }

    if (-not $Process.HasExited) {
        try {
            $Process.Kill($true)
            $null = $Process.WaitForExit(5000)
        }
        catch {}
    }

    $Process.Dispose()
}

$apiProcess = $null
$exitCode = 0

try {
    if (-not $SkipBuild) {
        Write-Host "[1/7] Building solution..."
        dotnet build "$repoRoot/EcoProject.sln" | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed."
        }
    }

    Write-Host "[2/7] Starting API (without admin env) to create temp user..."
    $apiProcess = Start-ApiProcess
    if (-not (Wait-ApiReady -Url $BaseUrl)) {
        throw "API did not become ready in time (initial start)."
    }

    Write-Host "[3/7] Creating temp user: $TempEmail"
    $registerBody = @{ email = $TempEmail; password = $TempPassword } | ConvertTo-Json
    try {
        Invoke-RestMethod -Method Post -Uri "$BaseUrl/auth/register" -ContentType "application/json" -Body $registerBody | Out-Null
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -ne 409) {
            throw
        }
    }

    Stop-ApiProcess -Process $apiProcess
    $apiProcess = $null

    Write-Host "[4/7] Restarting API with admin seeding env for $TempEmail..."
    $apiProcess = Start-ApiProcess -AdminEmail $TempEmail
    if (-not (Wait-ApiReady -Url $BaseUrl)) {
        throw "API did not become ready in time (admin-seeded start)."
    }

    Write-Host "[5/7] Running admin smoke script..."
    & $smokeAdminScript -AdminEmail $TempEmail -AdminPassword $TempPassword -BaseUrl $BaseUrl
    if ($LASTEXITCODE -ne 0) {
        $exitCode = $LASTEXITCODE
        throw "Admin smoke script failed."
    }

    if ($SkipOpenRoute) {
        Write-Host "[6/7] Skipping OpenRoute smoke script (SkipOpenRoute=true)."
    }
    else {
        Write-Host "[6/7] Running OpenRoute smoke script..."
        & $smokeOpenRouteScript -BaseUrl $BaseUrl -TrailId 1 -RequireExternalRoute
        if ($LASTEXITCODE -ne 0) {
            $exitCode = $LASTEXITCODE
            throw "OpenRoute smoke script failed."
        }
    }
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)"
    if ($exitCode -eq 0) {
        $exitCode = 1
    }
}
finally {
    Write-Host "[7/7] Cleaning up temp user..."
    try {
        & $cleanupScript -Email $TempEmail
    }
    catch {
        Write-Host "Cleanup warning: $($_.Exception.Message)"
        if ($exitCode -eq 0) {
            $exitCode = 1
        }
    }

    Stop-ApiProcess -Process $apiProcess
}

if ($exitCode -eq 0) {
    Write-Host "E2E admin smoke flow PASSED for temp user: $TempEmail"
}

exit $exitCode
