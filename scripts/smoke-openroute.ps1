param(
    [string]$BaseUrl = "http://127.0.0.1:5218/api",
    [int]$TrailId = 1,
    [switch]$RequireExternalRoute
)

$ErrorActionPreference = "Stop"
$results = @()

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
    $response = Invoke-RestMethod -Method Get -Uri "$BaseUrl/trails/$TrailId/route"
    Add-Result -Step "RouteCall" -Status "PASS" -Detail "HTTP 200"

    $coordCount = if ($null -ne $response.Coordinates) {
        ($response.Coordinates | Measure-Object).Count
    }
    else {
        0
    }

    if ($coordCount -ge 2) {
        Add-Result -Step "Coordinates" -Status "PASS" -Detail "count=$coordCount"
    }
    else {
        Add-Result -Step "Coordinates" -Status "FAIL" -Detail "count=$coordCount"
    }

    if ($RequireExternalRoute) {
        if ($response.IsExternalRoute -eq $true) {
            Add-Result -Step "ExternalRoute" -Status "PASS" -Detail "IsExternalRoute=true"
        }
        else {
            Add-Result -Step "ExternalRoute" -Status "FAIL" -Detail "IsExternalRoute=false"
        }
    }
    else {
        Add-Result -Step "ExternalRoute" -Status "INFO" -Detail "IsExternalRoute=$($response.IsExternalRoute)"
    }
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $detail = if ($statusCode) { "HTTP $statusCode" } else { $_.Exception.Message }
    Add-Result -Step "RouteCall" -Status "FAIL" -Detail $detail
}

$results | Format-Table -AutoSize
$results | ConvertTo-Json -Compress

$failed = ($results | Where-Object { $_.Status -eq "FAIL" }).Count
if ($failed -gt 0) {
    exit 1
}

exit 0
