param(
    [string]$BaseUrl = "http://127.0.0.1:5218/api"
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

function Test-EndpointCaching {
    param(
        [string]$Label,
        [string]$RequestPath,
        [bool]$ExpectTotalCountHeader
    )

    $uri = "$BaseUrl/$RequestPath"

    try {
        $first = Invoke-WebRequest -Uri $uri -Method Get
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $detail = if ($statusCode) { "HTTP $statusCode" } else { $_.Exception.Message }
        Add-Result -Step "$Label-FirstRequest" -Status "FAIL" -Detail $detail
        return
    }

    $etag = $first.Headers["ETag"]
    $cacheControl = $first.Headers["Cache-Control"]

    if ($first.StatusCode -eq 200) {
        Add-Result -Step "$Label-Status200" -Status "PASS" -Detail "HTTP 200"
    }
    else {
        Add-Result -Step "$Label-Status200" -Status "FAIL" -Detail "HTTP $($first.StatusCode)"
    }

    if (-not [string]::IsNullOrWhiteSpace($etag)) {
        Add-Result -Step "$Label-ETag" -Status "PASS" -Detail $etag
    }
    else {
        Add-Result -Step "$Label-ETag" -Status "FAIL" -Detail "Missing ETag"
    }

    if (-not [string]::IsNullOrWhiteSpace($cacheControl) -and $cacheControl.Contains("max-age=60")) {
        Add-Result -Step "$Label-CacheControl" -Status "PASS" -Detail $cacheControl
    }
    else {
        $detail = if ([string]::IsNullOrWhiteSpace($cacheControl)) { "Missing Cache-Control" } else { $cacheControl }
        Add-Result -Step "$Label-CacheControl" -Status "FAIL" -Detail $detail
    }

    if ($ExpectTotalCountHeader) {
        $totalCount = $first.Headers["X-Total-Count"]
        if (-not [string]::IsNullOrWhiteSpace($totalCount)) {
            Add-Result -Step "$Label-TotalCount" -Status "PASS" -Detail $totalCount
        }
        else {
            Add-Result -Step "$Label-TotalCount" -Status "FAIL" -Detail "Missing X-Total-Count"
        }
    }

    if ([string]::IsNullOrWhiteSpace($etag)) {
        Add-Result -Step "$Label-NotModified" -Status "FAIL" -Detail "Skipped due to missing ETag"
        return
    }

    $second = Invoke-WebRequest -Uri $uri -Method Get -Headers @{ "If-None-Match" = $etag } -SkipHttpErrorCheck
    if ($second.StatusCode -eq 304) {
        Add-Result -Step "$Label-NotModified" -Status "PASS" -Detail "HTTP 304"
    }
    else {
        Add-Result -Step "$Label-NotModified" -Status "FAIL" -Detail "HTTP $($second.StatusCode)"
    }
}

$base = $BaseUrl.TrimEnd('/')
$BaseUrl = $base

Test-EndpointCaching -Label "Trails" -RequestPath "trails?page=1&pageSize=2&sortBy=name&sortDirection=asc" -ExpectTotalCountHeader $true
Test-EndpointCaching -Label "TrailsSummary" -RequestPath "trails/summary?page=1&pageSize=2&sortBy=name&sortDirection=asc" -ExpectTotalCountHeader $false

$results | Format-Table -AutoSize

$failed = ($results | Where-Object { $_.Status -eq "FAIL" }).Count
if ($failed -gt 0) {
    exit 1
}

exit 0
