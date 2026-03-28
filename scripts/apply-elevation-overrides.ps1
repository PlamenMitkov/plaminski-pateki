param(
  [string]$InputPath = "eco.json",
  [string]$OverridesCsvPath = "scripts/unresolved-all.csv",
  [string]$ValidationReportPath = "scripts/elevation-validation-report.csv",
  [switch]$StrictValidation,
  [switch]$Apply
)

$ErrorActionPreference = "Stop"

function Test-MissingValue {
  param([string]$Value)

  if ([string]::IsNullOrWhiteSpace($Value)) { return $true }
  $normalized = $Value.Trim().ToLowerInvariant()
  return $normalized -in @("не е посочена", "варира", "неизвестна", "неизвестно", "n/a", "na", "-")
}

function Try-ParseLengthKm {
  param([string]$LengthRaw)

  if (Test-MissingValue -Value $LengthRaw) { return $null }

  $normalized = $LengthRaw.Trim().Replace(',', '.')
  $parsed = 0.0
  if ([double]::TryParse($normalized, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$parsed)) {
    if ($parsed -gt 0) { return [math]::Round($parsed, 2) }
  }

  return $null
}

function Get-ElevationValidation {
  param(
    [int]$ElevationM,
    [nullable[double]]$LengthKm
  )

  if ($ElevationM -lt 50 -or $ElevationM -gt 3000) {
    return @{ status = "invalid"; reason = "out_of_range_50_3000"; meters_per_km = "" }
  }

  if ($null -eq $LengthKm) {
    return @{ status = "review"; reason = "missing_length"; meters_per_km = "" }
  }

  $ratio = [math]::Round(($ElevationM / $LengthKm), 1)
  if ($ratio -gt 350) {
    return @{ status = "review"; reason = "too_steep_gt_350m_per_km"; meters_per_km = [string]$ratio }
  }
  if ($ratio -lt 8) {
    return @{ status = "review"; reason = "too_flat_lt_8m_per_km"; meters_per_km = [string]$ratio }
  }

  return @{ status = "pass"; reason = "plausible"; meters_per_km = [string]$ratio }
}

if (-not (Test-Path -Path $InputPath)) {
  throw "Input file not found: $InputPath"
}

if (-not (Test-Path -Path $OverridesCsvPath)) {
  throw "Overrides CSV not found: $OverridesCsvPath"
}

$jsonRaw = Get-Content -Path $InputPath -Raw -Encoding UTF8
$data = $jsonRaw | ConvertFrom-Json -AsHashtable
$trails = $data["eco_trails"]
if ($null -eq $trails) {
  throw "eco_trails array not found in $InputPath"
}

$rows = Import-Csv -Path $OverridesCsvPath
$updated = 0
$skippedInvalid = 0
$skippedMissingTrail = 0
$skippedAlreadySet = 0
$skippedReview = 0
$processedRows = 0
$validationRows = @()

foreach ($row in $rows) {
  $valueRaw = [string]$row.manual_elevation_gain_m
  if ([string]::IsNullOrWhiteSpace($valueRaw)) { continue }

  $processedRows++

  $value = 0
  if (-not [int]::TryParse($valueRaw.Trim(), [ref]$value)) {
    $skippedInvalid++
    $validationRows += [pscustomobject]@{
      id = [string]$row.id
      name = [string]$row.name
      manual_elevation_gain_m = $valueRaw
      length_km = ""
      meters_per_km = ""
      validation_status = "invalid"
      validation_reason = "non_integer_value"
      will_apply = $false
    }
    continue
  }

  $trail = $null
  $id = [string]$row.id
  if (-not [string]::IsNullOrWhiteSpace($id)) {
    $trail = $trails | Where-Object { ([string]$_["id"]) -eq $id } | Select-Object -First 1
  }

  if ($null -eq $trail) {
    $name = [string]$row.name
    if (-not [string]::IsNullOrWhiteSpace($name)) {
      $trail = $trails | Where-Object { ([string]$_["name"]) -eq $name } | Select-Object -First 1
    }
  }

  if ($null -eq $trail) {
    $skippedMissingTrail++
    $validationRows += [pscustomobject]@{
      id = [string]$row.id
      name = [string]$row.name
      manual_elevation_gain_m = [string]$value
      length_km = ""
      meters_per_km = ""
      validation_status = "invalid"
      validation_reason = "trail_not_found"
      will_apply = $false
    }
    continue
  }

  if (-not $trail.ContainsKey("trail_details")) { $trail["trail_details"] = @{} }
  $details = $trail["trail_details"]
  $existing = if ($details.ContainsKey("elevation_gain_m")) { [string]$details["elevation_gain_m"] } else { "" }

  if (-not (Test-MissingValue -Value $existing)) {
    $skippedAlreadySet++
    $validationRows += [pscustomobject]@{
      id = [string]$trail["id"]
      name = [string]$trail["name"]
      manual_elevation_gain_m = [string]$value
      length_km = [string]$details["length_km"]
      meters_per_km = ""
      validation_status = "skipped"
      validation_reason = "already_has_elevation"
      will_apply = $false
    }
    continue
  }

  $lengthKm = Try-ParseLengthKm -LengthRaw ([string]$details["length_km"])
  $validation = Get-ElevationValidation -ElevationM $value -LengthKm $lengthKm

  $shouldApply = $true
  if ($validation.status -eq "invalid") {
    $skippedInvalid++
    $shouldApply = $false
  }
  elseif ($validation.status -eq "review" -and $StrictValidation) {
    $skippedReview++
    $shouldApply = $false
  }

  $validationRows += [pscustomobject]@{
    id = [string]$trail["id"]
    name = [string]$trail["name"]
    manual_elevation_gain_m = [string]$value
    length_km = if ($null -ne $lengthKm) { [string]$lengthKm } else { "" }
    meters_per_km = [string]$validation.meters_per_km
    validation_status = [string]$validation.status
    validation_reason = [string]$validation.reason
    will_apply = $shouldApply
  }

  if (-not $shouldApply) {
    continue
  }

  $details["elevation_gain_m"] = [string]$value

  if (-not $trail.ContainsKey("auto_enrichment")) { $trail["auto_enrichment"] = @{} }
  $trail["auto_enrichment"]["elevation_source"] = if (-not [string]::IsNullOrWhiteSpace([string]$row.manual_elevation_source)) { [string]$row.manual_elevation_source } else { "manual_verified_csv" }
  $trail["auto_enrichment"]["elevation_updated_at"] = (Get-Date).ToString("yyyy-MM-dd")

  $updated++
}

$result = @{
  total_trails = $trails.Count
  csv_rows = $rows.Count
  rows_with_manual_value = $processedRows
  updated_elevation = $updated
  skipped_invalid_value = $skippedInvalid
  skipped_missing_trail = $skippedMissingTrail
  skipped_already_set = $skippedAlreadySet
  skipped_review_due_to_strict = $skippedReview
  validation_report_rows = $validationRows.Count
}

$resultJson = $result | ConvertTo-Json -Depth 4
Write-Host $resultJson

if ($Apply -and $updated -gt 0) {
  $outputJson = $data | ConvertTo-Json -Depth 100
  Set-Content -Path $InputPath -Value $outputJson -Encoding UTF8
  Write-Host "Saved updates to $InputPath"
}
else {
  Write-Host "Dry run only. Use -Apply to write changes."
}

if (-not [string]::IsNullOrWhiteSpace($ValidationReportPath)) {
  $parent = Split-Path -Path $ValidationReportPath -Parent
  if (-not [string]::IsNullOrWhiteSpace($parent) -and -not (Test-Path -Path $parent)) {
    New-Item -ItemType Directory -Path $parent | Out-Null
  }

  $validationRows | Export-Csv -Path $ValidationReportPath -NoTypeInformation -Encoding UTF8
  Write-Host "Saved validation report to $ValidationReportPath"
}
