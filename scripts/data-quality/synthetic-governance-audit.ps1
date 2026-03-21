param(
  [string]$InputPath = "eco.json",
  [string]$OutputCsvPath = "scripts/data-quality/synthetic-governance-findings.csv"
)

$ErrorActionPreference = "Stop"

function Is-MissingValue {
  param([string]$Value)

  if ([string]::IsNullOrWhiteSpace($Value)) { return $true }
  $normalized = $Value.Trim().ToLowerInvariant()
  return $normalized -in @("не е посочена", "неизвестна", "неизвестно", "няма", "n/a", "na", "-")
}

function Has-Value {
  param([string]$Value)
  return -not (Is-MissingValue $Value)
}

if (-not (Test-Path -Path $InputPath)) {
  throw "Input file not found: $InputPath"
}

$jsonRaw = Get-Content -Path $InputPath -Raw -Encoding UTF8
$data = $jsonRaw | ConvertFrom-Json -AsHashtable
$trails = $data["eco_trails"]
if ($null -eq $trails) {
  throw "eco_trails array not found in $InputPath"
}

$findings = @()
$syntheticRows = 0
$syntheticWithoutSources = 0
$missingNearestTown = 0
$missingLengthKm = 0
$missingPhotoUrl = 0

foreach ($trail in $trails) {
  $trailId = [string]$trail["id"]
  $trailName = [string]$trail["name"]

  if (-not $trail.ContainsKey("metadata")) {
    $trail["metadata"] = @{}
  }
  $meta = $trail["metadata"]

  if (-not $trail.ContainsKey("location")) {
    $trail["location"] = @{}
  }
  $location = $trail["location"]

  if (-not $trail.ContainsKey("trail_details")) {
    $trail["trail_details"] = @{}
  }
  $details = $trail["trail_details"]

  $hasSyntheticSignals = $false
  if ($meta.ContainsKey("ai_enriched") -and [bool]$meta["ai_enriched"]) { $hasSyntheticSignals = $true }
  if (Has-Value ([string]$meta["confidence"])) { $hasSyntheticSignals = $true }
  if (Has-Value ([string]$meta["short_summary"])) { $hasSyntheticSignals = $true }
  if (Has-Value ([string]$meta["key_highlights"])) { $hasSyntheticSignals = $true }
  if (Has-Value ([string]$meta["reviewer_notes"])) { $hasSyntheticSignals = $true }

  if ($hasSyntheticSignals) {
    $syntheticRows++
    $sources = [string]$meta["sources_urls"]
    if (-not (Has-Value $sources)) {
      $syntheticWithoutSources++
      $findings += [pscustomobject]@{
        category = "synthetic_without_sources"
        severity = "high"
        id = $trailId
        name = $trailName
        details = "Synthetic metadata present but metadata.sources_urls is missing"
      }
    }
  }

  $nearestTown = [string]$location["nearest_town"]
  if (-not (Has-Value $nearestTown)) {
    $missingNearestTown++
    $findings += [pscustomobject]@{
      category = "missing_nearest_town"
      severity = "medium"
      id = $trailId
      name = $trailName
      details = "location.nearest_town is missing"
    }
  }

  $lengthKm = [string]$details["length_km"]
  if (-not (Has-Value $lengthKm)) {
    $missingLengthKm++
    $findings += [pscustomobject]@{
      category = "missing_length_km"
      severity = "medium"
      id = $trailId
      name = $trailName
      details = "trail_details.length_km is missing"
    }
  }

  $photoUrl = [string]$trail["photo_url"]
  if (-not (Has-Value $photoUrl)) {
    $missingPhotoUrl++
    $findings += [pscustomobject]@{
      category = "missing_photo_url"
      severity = "low"
      id = $trailId
      name = $trailName
      details = "photo_url is missing"
    }
  }
}

$parent = Split-Path -Path $OutputCsvPath -Parent
if (-not [string]::IsNullOrWhiteSpace($parent) -and -not (Test-Path -Path $parent)) {
  New-Item -Path $parent -ItemType Directory | Out-Null
}

$findings | Sort-Object severity, category, id | Export-Csv -Path $OutputCsvPath -NoTypeInformation -Encoding UTF8

$summary = [pscustomobject]@{
  total_trails = $trails.Count
  synthetic_rows = $syntheticRows
  synthetic_without_sources = $syntheticWithoutSources
  missing_nearest_town = $missingNearestTown
  missing_length_km = $missingLengthKm
  missing_photo_url = $missingPhotoUrl
  findings_csv = $OutputCsvPath
}

$summary | ConvertTo-Json -Depth 4 | Write-Host
