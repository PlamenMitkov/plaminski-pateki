param(
    [Parameter(Mandatory = $true)]
    [string]$AdminEmail,

    [Parameter(Mandatory = $true)]
    [string]$AdminPassword,

    [string]$BaseUrl = "http://127.0.0.1:5218/api",

    [int]$EnrichLimit = 1
)

$ErrorActionPreference = "Stop"
$results = @()
$token = $null

function Add-Result {
    param(
        [string]$Step,
        [string]$Status,
        [string]$Detail
    )

    $script:results += [pscustomobject]@{
        Step = $Step
        Status = $Status
        Detail = $Detail
    }
}

try {
    $loginBody = @{ email = $AdminEmail; password = $AdminPassword } | ConvertTo-Json
    $loginResponse = Invoke-RestMethod -Method Post -Uri "$BaseUrl/auth/login" -ContentType "application/json" -Body $loginBody
    $token = $loginResponse.token

    if ([string]::IsNullOrWhiteSpace($token)) {
        Add-Result -Step "AdminLogin" -Status "FAIL" -Detail "Token missing"
    }
    else {
        Add-Result -Step "AdminLogin" -Status "PASS" -Detail "Token received"
    }
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $detail = if ($statusCode) { "HTTP $statusCode" } else { $_.Exception.Message }
    Add-Result -Step "AdminLogin" -Status "FAIL" -Detail $detail
}

$hasAdminRole = $false
try {
    $meResponse = Invoke-RestMethod -Method Get -Uri "$BaseUrl/auth/me" -Headers @{ Authorization = "Bearer $token" }
    $roles = if ($meResponse.roles) { @($meResponse.roles) } else { @() }
    $hasAdminRole = $roles | Where-Object { $_.ToString().ToLower() -eq 'admin' } | Measure-Object | Select-Object -ExpandProperty Count

    if ($hasAdminRole -gt 0) {
        Add-Result -Step "AdminRole" -Status "PASS" -Detail ("roles=" + ($roles -join ','))
    }
    else {
        Add-Result -Step "AdminRole" -Status "FAIL" -Detail ("roles=" + ($(if ($roles.Count -gt 0) { $roles -join ',' } else { '<none>' })))
    }
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $detail = if ($statusCode) { "HTTP $statusCode" } else { $_.Exception.Message }
    Add-Result -Step "AdminRole" -Status "FAIL" -Detail $detail
}

try {
    $body = @{ limit = $EnrichLimit; overwriteExisting = $false } | ConvertTo-Json
    Invoke-RestMethod -Method Post -Uri "$BaseUrl/assistant/enrich" -Headers @{ Authorization = "Bearer $token" } -ContentType "application/json" -Body $body | Out-Null

    if ($hasAdminRole -gt 0) {
        Add-Result -Step "AssistantEnrichAdmin" -Status "PASS" -Detail "HTTP 200"
    }
    else {
        Add-Result -Step "AssistantEnrichAdmin" -Status "WARN" -Detail "HTTP 200 but admin role not detected in /auth/me"
    }
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 403) {
        Add-Result -Step "AssistantEnrichAdmin" -Status "FAIL" -Detail "HTTP 403"
    }
    else {
        $detail = if ($statusCode) { "HTTP $statusCode" } else { $_.Exception.Message }
        Add-Result -Step "AssistantEnrichAdmin" -Status "FAIL" -Detail $detail
    }
}

$results | Format-Table -AutoSize

$failedCount = ($results | Where-Object { $_.Status -eq "FAIL" }).Count
if ($failedCount -gt 0) {
    exit 1
}

exit 0
