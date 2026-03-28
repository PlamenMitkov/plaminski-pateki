param(
  [string]$InputPath = "eco.json",
  [switch]$Apply,
  [switch]$OnlyReal,
  [string]$UnresolvedCsvPath = "",
  [int]$MaxTrails = 0,
  [int]$DelayMs = 300
)

$ErrorActionPreference = "Stop"

function Test-MissingValue {
  param([string]$Value)

  if ([string]::IsNullOrWhiteSpace($Value)) { return $true }
  $normalized = $Value.Trim().ToLowerInvariant()
  return $normalized -in @("не е посочена", "варира", "неизвестна", "неизвестно", "n/a", "na", "-")
}

function Convert-ToInvariantString {
  param([double]$Value)
  return $Value.ToString([System.Globalization.CultureInfo]::InvariantCulture)
}

function Try-ParseValidCoordinate {
  param(
    [string]$Raw,
    [bool]$IsLatitude
  )

  if ([string]::IsNullOrWhiteSpace($Raw)) { return $null }

  $normalized = $Raw.Trim().Replace(',', '.')
  $sign = 1

  if ($normalized -match '(?i)[SW]') { $sign = -1 }

  $match = [regex]::Match($normalized, '(-?\d+(?:\.\d+)?)')
  if (-not $match.Success) { return $null }

  $value = 0.0
  if (-not [double]::TryParse($match.Groups[1].Value, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$value)) {
    return $null
  }

  $value = $value * $sign

  if ($IsLatitude) {
    if ($value -lt -90 -or $value -gt 90) { return $null }
  }
  else {
    if ($value -lt -180 -or $value -gt 180) { return $null }
  }

  return [math]::Round($value, 6)
}

function Get-DifficultyNumber {
  param([string]$Difficulty)

  if ([string]::IsNullOrWhiteSpace($Difficulty)) { return 3 }
  $normalized = $Difficulty.Trim().ToLowerInvariant()
  if ($normalized.Contains("лека")) { return 1 }
  if ($normalized.Contains("умерен")) { return 2 }
  if ($normalized.Contains("средно") -or $normalized.Contains("средна")) { return 3 }
  if ($normalized.Contains("трудна")) { return 4 }
  if ($normalized.Contains("тежка") -or $normalized.Contains("екстрем")) { return 5 }
  return 3
}

function Estimate-ElevationGain {
  param(
    [double]$LengthKm,
    [int]$Difficulty
  )

  $multiplier = switch ($Difficulty) {
    { $_ -le 2 } { 35 }
    { $_ -ge 4 } { 75 }
    default { 55 }
  }

  $estimated = [math]::Round($LengthKm * $multiplier, 0, [System.MidpointRounding]::AwayFromZero)
  if ($estimated -lt 80) { $estimated = 80 }
  if ($estimated -gt 1600) { $estimated = 1600 }
  return [int]$estimated
}

function Try-ParseLengthFromText {
  param([string]$Text)

  if ([string]::IsNullOrWhiteSpace($Text)) { return $null }

  $patterns = @(
    '(?i)(?:дължина|маршрут(?:ът)?\s*е\s*дълъг|дълъг)\s*[:\-]?\s*(\d+(?:[\.,]\d+)?)\s*км',
    '(?i)(\d+(?:[\.,]\d+)?)\s*км'
  )

  foreach ($pattern in $patterns) {
    $match = [regex]::Match($Text, $pattern)
    if ($match.Success) {
      $raw = $match.Groups[1].Value.Replace(',', '.')
      $value = 0.0
      if ([double]::TryParse($raw, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$value)) {
        return [math]::Round($value, 2)
      }
    }
  }

  return $null
}

function Try-ParseElevationFromText {
  param([string]$Text)

  if ([string]::IsNullOrWhiteSpace($Text)) { return $null }

  $patterns = @(
    '(?i)(?:денивелац(?:ия|ията)|изкачване|качване|положителна\s*денивелация|асцент)\s*[:\-]?\s*(\d{2,4})\s*м',
    '(?i)(\d{2,4})\s*м\s*(?:денивелац(?:ия|ията)|изкачване|качване)',
    '(?i)(?:денивелац(?:ия|ията)|изкачване|качване|положителна\s*денивелация|асцент|elevation\s*gain|ascent|gain|d\+)\s*[:=\-+]?\s*(\d{2,4})(?:\s*(?:м|m))?',
    '(?i)(\d{2,4})\s*(?:м|m)?\s*(?:elevation\s*gain|ascent|gain|denivelation|денивелац(?:ия|ията)|изкачване|качване)'
  )

  foreach ($pattern in $patterns) {
    $match = [regex]::Match($Text, $pattern)
    if ($match.Success) {
      $raw = $match.Groups[1].Value
      $value = 0
      if ([int]::TryParse($raw, [ref]$value)) {
        if ($value -ge 50 -and $value -le 3000) {
          return $value
        }
      }
    }
  }

  return $null
}

function Get-SourceText {
  param(
    [string]$Url,
    [hashtable]$Cache
  )

  if ([string]::IsNullOrWhiteSpace($Url)) { return $null }
  if ($Cache.ContainsKey($Url)) { return $Cache[$Url] }

  try {
    $response = Invoke-WebRequest -Uri $Url -Method GET -TimeoutSec 12
    $html = [string]$response.Content
    $text = [regex]::Replace($html, '<[^>]+>', ' ')
    $text = [System.Net.WebUtility]::HtmlDecode($text)
    $text = [regex]::Replace($text, '\s+', ' ').Trim()
    $Cache[$Url] = $text
    return $text
  }
  catch {
    $Cache[$Url] = $null
    return $null
  }
}

function Get-SourceTrustInfo {
  param([string]$Source)

  if ([string]::IsNullOrWhiteSpace($Source)) {
    return @{ trust = "unknown"; reason = "empty_source" }
  }

  $s = $Source.Trim().ToLowerInvariant()

  # User-submitted records are treated as trusted by explicit product decision.
  if ($s -match 'потребител|user|community|crowd|ugc') {
    return @{ trust = "trusted"; reason = "user_contributed" }
  }

  if ($s -match 'wikipedia|wikidata') {
    return @{ trust = "review"; reason = "community_encyclopedia" }
  }

  if ($s -match 'gov\.|\.gov|municipality|obshtina|община|park|парк|np\.|natura2000|opoznai\.bg|visit|tourism|official') {
    return @{ trust = "trusted"; reason = "official_or_tourism_portal" }
  }

  if ($s -match '^https?://') {
    return @{ trust = "review"; reason = "generic_web_source" }
  }

  return @{ trust = "review"; reason = "local_or_unclassified_source" }
}

function Try-GeocodeTrail {
  param(
    [string]$Name,
    [string]$NearestTown,
    [string]$Region
  )

  $queryParts = @($Name, $NearestTown, $Region, "България") | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
  $query = ($queryParts -join ", ").Trim()
  if ([string]::IsNullOrWhiteSpace($query)) { return $null }

  $encoded = [uri]::EscapeDataString($query)
  $url = "https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&q=$encoded"

  try {
    $headers = @{ "User-Agent" = "EcoTrailsDataEnricher/1.0 (research-use)" }
    $result = Invoke-RestMethod -Uri $url -Headers $headers -Method GET -TimeoutSec 12
    if ($null -eq $result -or $result.Count -eq 0) { return $null }

    $item = $result[0]
    $lat = 0.0
    $lon = 0.0
    if (-not [double]::TryParse(([string]$item.lat).Replace(',', '.'), [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$lat)) {
      return $null
    }
    if (-not [double]::TryParse(([string]$item.lon).Replace(',', '.'), [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$lon)) {
      return $null
    }

    return @{
      Latitude = [math]::Round($lat, 6)
      Longitude = [math]::Round($lon, 6)
      DisplayName = [string]$item.display_name
    }
  }
  catch {
    return $null
  }
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

$sourceTextCache = @{}
$updatedCoords = 0
$updatedLength = 0
$updatedElevation = 0
$skippedEstimatedElevation = 0
$processed = 0
$unresolvedRows = @()

foreach ($trail in $trails) {
  if ($MaxTrails -gt 0 -and $processed -ge $MaxTrails) { break }
  $processed++

  if (-not $trail.ContainsKey("location")) { continue }
  if (-not $trail["location"].ContainsKey("coordinates")) { $trail["location"]["coordinates"] = @{} }
  $coords = $trail["location"]["coordinates"]

  if (-not $trail.ContainsKey("trail_details")) { $trail["trail_details"] = @{} }
  $details = $trail["trail_details"]

  $name = [string]$trail["name"]
  $nearestTown = [string]$trail["location"]["nearest_town"]
  $region = [string]$trail["location"]["region"]
  $sourceUrl = [string]$trail["source"]
  $sourceTrust = Get-SourceTrustInfo -Source $sourceUrl

  $sourceText = Get-SourceText -Url $sourceUrl -Cache $sourceTextCache

  $latValue = Try-ParseValidCoordinate -Raw ([string]$coords["latitude"]) -IsLatitude $true
  $lonValue = Try-ParseValidCoordinate -Raw ([string]$coords["longitude"]) -IsLatitude $false
  $latMissing = Test-MissingValue -Value ([string]$coords["latitude"])
  $lonMissing = Test-MissingValue -Value ([string]$coords["longitude"])

  $coordsInvalid = ($null -eq $latValue -or $null -eq $lonValue)
  if ($latMissing -or $lonMissing -or $coordsInvalid) {
    $geo = Try-GeocodeTrail -Name $name -NearestTown $nearestTown -Region $region
    if ($null -ne $geo) {
      $coords["latitude"] = Convert-ToInvariantString -Value $geo.Latitude
      $coords["longitude"] = Convert-ToInvariantString -Value $geo.Longitude
      $updatedCoords++

      if (-not $trail.ContainsKey("auto_enrichment")) { $trail["auto_enrichment"] = @{} }
      $trail["auto_enrichment"]["coords_source"] = "nominatim"
      $trail["auto_enrichment"]["coords_updated_at"] = (Get-Date).ToString("yyyy-MM-dd")
      $trail["auto_enrichment"]["coords_note"] = $geo.DisplayName
    }

    Start-Sleep -Milliseconds $DelayMs
  }
  elseif ($null -ne $latValue -and $null -ne $lonValue) {
    $coords["latitude"] = Convert-ToInvariantString -Value $latValue
    $coords["longitude"] = Convert-ToInvariantString -Value $lonValue
  }

  $lengthRaw = [string]$details["length_km"]
  $lengthMissing = Test-MissingValue -Value $lengthRaw
  $resolvedLength = $null

  if ($lengthMissing) {
    $resolvedLength = Try-ParseLengthFromText -Text $sourceText
    if ($null -eq $resolvedLength) {
      $resolvedLength = Try-ParseLengthFromText -Text ([string]$trail["description"])
    }

    if ($null -ne $resolvedLength) {
      $details["length_km"] = Convert-ToInvariantString -Value $resolvedLength
      $updatedLength++

      if (-not $trail.ContainsKey("auto_enrichment")) { $trail["auto_enrichment"] = @{} }
      $trail["auto_enrichment"]["length_source"] = if ($null -ne $sourceText) { "source_page_regex" } else { "description_regex" }
      $trail["auto_enrichment"]["length_updated_at"] = (Get-Date).ToString("yyyy-MM-dd")
    }
  }
  else {
    $parsedLen = 0.0
    if ([double]::TryParse($lengthRaw.Replace(',', '.'), [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$parsedLen)) {
      $resolvedLength = $parsedLen
    }
  }

  $elevationRaw = if ($details.ContainsKey("elevation_gain_m")) { [string]$details["elevation_gain_m"] } else { "" }
  $elevationMissing = Test-MissingValue -Value $elevationRaw

  if ($elevationMissing) {
    $elevation = Try-ParseElevationFromText -Text $sourceText

    if ($null -eq $elevation -and $null -ne $resolvedLength -and -not $OnlyReal) {
      $difficultyNumber = Get-DifficultyNumber -Difficulty ([string]$details["difficulty"])
      $elevation = Estimate-ElevationGain -LengthKm ([double]$resolvedLength) -Difficulty $difficultyNumber

      if (-not $trail.ContainsKey("auto_enrichment")) { $trail["auto_enrichment"] = @{} }
      $trail["auto_enrichment"]["elevation_source"] = "estimated_from_length_and_difficulty"
    }
    elseif ($null -ne $elevation) {
      if (-not $trail.ContainsKey("auto_enrichment")) { $trail["auto_enrichment"] = @{} }
      $trail["auto_enrichment"]["elevation_source"] = "source_page_regex"
    }

    if ($null -ne $elevation) {
      $details["elevation_gain_m"] = [string][int]$elevation
      $updatedElevation++
      $trail["auto_enrichment"]["elevation_updated_at"] = (Get-Date).ToString("yyyy-MM-dd")
    }
    elseif ($OnlyReal -and $null -ne $resolvedLength) {
      $skippedEstimatedElevation++
    }
  }

  $coordsStillMissing = Test-MissingValue -Value ([string]$coords["latitude"]) -or (Test-MissingValue -Value ([string]$coords["longitude"]))
  $lengthStillMissing = Test-MissingValue -Value ([string]$details["length_km"])
  $elevationStillMissing = (-not $details.ContainsKey("elevation_gain_m")) -or (Test-MissingValue -Value ([string]$details["elevation_gain_m"]))

  if ($coordsStillMissing -or $lengthStillMissing -or $elevationStillMissing) {
    $unresolvedRows += [pscustomobject]@{
      id = [string]$trail["id"]
      name = [string]$trail["name"]
      source = [string]$trail["source"]
      source_trust = [string]$sourceTrust.trust
      source_trust_reason = [string]$sourceTrust.reason
      nearest_town = [string]$trail["location"]["nearest_town"]
      region = [string]$trail["location"]["region"]
      missing_coords = $coordsStillMissing
      missing_length_km = $lengthStillMissing
      missing_elevation_gain_m = $elevationStillMissing
      manual_elevation_gain_m = ""
      manual_elevation_source = ""
    }
  }
}

$result = @{
  total = $trails.Count
  processed = $processed
  updated_coords = $updatedCoords
  updated_length = $updatedLength
  updated_elevation = $updatedElevation
  skipped_estimated_elevation = $skippedEstimatedElevation
  unresolved_rows = $unresolvedRows.Count
  changed_any = ($updatedCoords + $updatedLength + $updatedElevation)
}

$resultJson = $result | ConvertTo-Json -Depth 4
Write-Host $resultJson

if ($Apply -and $result.changed_any -gt 0) {
  $outputJson = $data | ConvertTo-Json -Depth 100
  Set-Content -Path $InputPath -Value $outputJson -Encoding UTF8
  Write-Host "Saved updates to $InputPath"
}
else {
  Write-Host "Dry run only. Use -Apply to write changes."
}

if (-not [string]::IsNullOrWhiteSpace($UnresolvedCsvPath)) {
  $parent = Split-Path -Path $UnresolvedCsvPath -Parent
  if (-not [string]::IsNullOrWhiteSpace($parent) -and -not (Test-Path -Path $parent)) {
    New-Item -ItemType Directory -Path $parent | Out-Null
  }

  $unresolvedRows |
    Sort-Object -Property @{ Expression = { if ($_.source_trust -eq "trusted") { 0 } else { 1 } } }, missing_coords, missing_length_km, name |
    Export-Csv -Path $UnresolvedCsvPath -NoTypeInformation -Encoding UTF8
  Write-Host "Saved unresolved report to $UnresolvedCsvPath"
}
