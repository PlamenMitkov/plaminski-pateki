param(
    [string]$BaseUrl = "http://127.0.0.1:5218/api"
)

$ErrorActionPreference = "Stop"

$email = "smoke_$([Guid]::NewGuid().ToString('N').Substring(0, 10))@example.com"
$password = "Passw0rd!"
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

function Invoke-ExpectedFailure {
    param(
        [ScriptBlock]$Action,
        [int]$ExpectedStatus,
        [string]$Step
    )

    try {
        & $Action | Out-Null
        Add-Result -Step $Step -Status "FAIL" -Detail "Unexpected success"
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq $ExpectedStatus) {
            Add-Result -Step $Step -Status "PASS" -Detail "HTTP $statusCode"
        }
        else {
            Add-Result -Step $Step -Status "FAIL" -Detail "HTTP $statusCode"
        }
    }
}

try {
    $registerBody = @{ email = $email; password = $password } | ConvertTo-Json
    $registerResponse = Invoke-RestMethod -Method Post -Uri "$BaseUrl/auth/register" -ContentType "application/json" -Body $registerBody
    Add-Result -Step "Register" -Status "PASS" -Detail $registerResponse.email
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $detail = if ($statusCode) { "HTTP $statusCode" } else { $_.Exception.Message }
    Add-Result -Step "Register" -Status "FAIL" -Detail $detail
}

try {
    $loginBody = @{ email = $email; password = $password } | ConvertTo-Json
    $loginResponse = Invoke-RestMethod -Method Post -Uri "$BaseUrl/auth/login" -ContentType "application/json" -Body $loginBody
    $token = $loginResponse.token

    if ([string]::IsNullOrWhiteSpace($token)) {
        Add-Result -Step "Login" -Status "FAIL" -Detail "Token missing"
    }
    else {
        Add-Result -Step "Login" -Status "PASS" -Detail "Token received"
    }
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $detail = if ($statusCode) { "HTTP $statusCode" } else { $_.Exception.Message }
    Add-Result -Step "Login" -Status "FAIL" -Detail $detail
}

try {
    $meResponse = Invoke-RestMethod -Method Get -Uri "$BaseUrl/auth/me" -Headers @{ Authorization = "Bearer $token" }
    $roles = if ($meResponse.roles) { $meResponse.roles -join "," } else { "<none>" }
    Add-Result -Step "AuthMe" -Status "PASS" -Detail "roles=$roles"
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $detail = if ($statusCode) { "HTTP $statusCode" } else { $_.Exception.Message }
    Add-Result -Step "AuthMe" -Status "FAIL" -Detail $detail
}

Invoke-ExpectedFailure -Step "AssistantSessionAnon" -ExpectedStatus 401 -Action {
    Invoke-RestMethod -Method Post -Uri "$BaseUrl/assistant/sessions" -ContentType "application/json" -Body "{}"
}

try {
    $sessionResponse = Invoke-RestMethod -Method Post -Uri "$BaseUrl/assistant/sessions" -Headers @{ Authorization = "Bearer $token" } -ContentType "application/json" -Body "{}"
    if ([string]::IsNullOrWhiteSpace($sessionResponse.sessionId)) {
        Add-Result -Step "AssistantSessionAuth" -Status "FAIL" -Detail "No sessionId"
    }
    else {
        $short = $sessionResponse.sessionId.Substring(0, [Math]::Min(8, $sessionResponse.sessionId.Length))
        Add-Result -Step "AssistantSessionAuth" -Status "PASS" -Detail $short
    }
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $detail = if ($statusCode) { "HTTP $statusCode" } else { $_.Exception.Message }
    Add-Result -Step "AssistantSessionAuth" -Status "FAIL" -Detail $detail
}

Invoke-ExpectedFailure -Step "AssistantEnrichNonAdmin" -ExpectedStatus 403 -Action {
    Invoke-RestMethod -Method Post -Uri "$BaseUrl/assistant/enrich" -Headers @{ Authorization = "Bearer $token" } -ContentType "application/json" -Body '{"limit":1}'
}

$results | Format-Table -AutoSize

$failed = ($results | Where-Object { $_.Status -eq "FAIL" }).Count
if ($failed -gt 0) {
    exit 1
}

exit 0
