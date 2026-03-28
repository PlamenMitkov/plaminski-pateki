param(
  [string]$InputPath = "eco.json",
  [string]$OverridesCsvPath = "scripts/coords-missing-worklist.csv",
  [switch]$Apply
)

$ErrorActionPreference = "Stop"

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
$processedRows = 0

foreach ($row in $rows) {
  $latRaw = [string]$row.manual_latitude
  $lonRaw = [string]$row.manual_longitude

  if ([string]::IsNullOrWhiteSpace($latRaw) -or [string]::IsNullOrWhiteSpace($lonRaw)) {
    continue
  }

  $processedRows++

  $lat = 0.0
  $lon = 0.0
  $okLat = [double]::TryParse($latRaw.Trim().Replace(',', '.'), [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$lat)
  $okLon = [double]::TryParse($lonRaw.Trim().Replace(',', '.'), [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$lon)

  if (-not $okLat -or -not $okLon -or $lat -lt -90 -or $lat -gt 90 -or $lon -lt -180 -or $lon -gt 180) {
    $skippedInvalid++
    continue
  }

  $id = [string]$row.id
  $trail = $null
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
    continue
  }

  if (-not $trail.ContainsKey("location")) { $trail["location"] = @{} }
  if (-not $trail["location"].ContainsKey("coordinates")) { $trail["location"]["coordinates"] = @{} }

  $trail["location"]["coordinates"]["latitude"] = ([math]::Round($lat, 6)).ToString([System.Globalization.CultureInfo]::InvariantCulture)
  $trail["location"]["coordinates"]["longitude"] = ([math]::Round($lon, 6)).ToString([System.Globalization.CultureInfo]::InvariantCulture)

  if (-not $trail.ContainsKey("auto_enrichment")) { $trail["auto_enrichment"] = @{} }
  $trail["auto_enrichment"]["coords_source"] = if (-not [string]::IsNullOrWhiteSpace([string]$row.manual_coords_source)) { [string]$row.manual_coords_source } else { "manual_verified_csv" }
  $trail["auto_enrichment"]["coords_updated_at"] = (Get-Date).ToString("yyyy-MM-dd")

  $updated++
}

$result = @{
  total_trails = $trails.Count
  csv_rows = $rows.Count
  rows_with_manual_coords = $processedRows
  updated_coords = $updated
  skipped_invalid = $skippedInvalid
  skipped_missing_trail = $skippedMissingTrail
}

Write-Host ($result | ConvertTo-Json -Depth 4)

if ($Apply -and $updated -gt 0) {
  $outputJson = $data | ConvertTo-Json -Depth 100
  Set-Content -Path $InputPath -Value $outputJson -Encoding UTF8
  Write-Host "Saved updates to $InputPath"
}
else {
  Write-Host "Dry run only. Use -Apply to write changes."
}
