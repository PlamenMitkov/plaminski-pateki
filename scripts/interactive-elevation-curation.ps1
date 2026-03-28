param(
  [string]$WorklistPath = "scripts/elevation-trusted-worklist.csv",
  [string]$SearchWorklistPath = "scripts/elevation-search-worklist.csv",
  [int]$StartIndex = 0,
  [int]$MaxItems = 5,
  [switch]$OpenBrowser
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $WorklistPath)) {
  throw "Worklist not found: $WorklistPath"
}

if (-not (Test-Path -Path $SearchWorklistPath)) {
  throw "Search worklist not found: $SearchWorklistPath"
}

$work = Import-Csv -Path $WorklistPath
$search = Import-Csv -Path $SearchWorklistPath

$searchById = @{}
foreach ($s in $search) {
  $searchById[[string]$s.id] = $s
}

$pending = @($work | Where-Object { [string]::IsNullOrWhiteSpace([string]$_.manual_elevation_gain_m) })
if ($pending.Count -eq 0) {
  Write-Host "No pending rows in $WorklistPath"
  exit 0
}

$end = [math]::Min($StartIndex + $MaxItems, $pending.Count)
if ($StartIndex -ge $pending.Count) {
  Write-Host "StartIndex is out of range. pending=$($pending.Count)"
  exit 0
}

for ($i = $StartIndex; $i -lt $end; $i++) {
  $row = $pending[$i]
  $id = [string]$row.id
  $searchRow = if ($searchById.ContainsKey($id)) { $searchById[$id] } else { $null }

  Write-Host ""
  Write-Host "[$($i + 1)/$($pending.Count)] id=$id"
  Write-Host "name=$([string]$row.name)"

  $urls = @()
  if ($null -ne $searchRow) {
    $urls = @(
      [string]$searchRow.bing_opoznai,
      [string]$searchRow.bing_denivelation,
      [string]$searchRow.bing_wikiloc,
      [string]$searchRow.bing_komoot,
      [string]$searchRow.bing_alltrails
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
  }

  if ($urls.Count -gt 0) {
    Write-Host "search links:"
    for ($u = 0; $u -lt $urls.Count; $u++) {
      Write-Host ("  [{0}] {1}" -f ($u + 1), $urls[$u])
    }

    if ($OpenBrowser) {
      Start-Process $urls[0] | Out-Null
    }
  }
  else {
    Write-Host "No search URLs for this row."
  }

  $valRaw = Read-Host "Enter elevation gain in meters (or empty to skip)"
  if ([string]::IsNullOrWhiteSpace($valRaw)) {
    continue
  }

  $meters = 0
  if (-not [int]::TryParse($valRaw.Trim(), [ref]$meters)) {
    Write-Host "Invalid number, skipping."
    continue
  }

  if ($meters -lt 50 -or $meters -gt 3000) {
    Write-Host "Out of allowed range 50..3000, skipping."
    continue
  }

  $src = Read-Host "Enter source URL used for this value"
  if ([string]::IsNullOrWhiteSpace($src)) {
    Write-Host "Source URL is required, skipping."
    continue
  }

  foreach ($w in $work) {
    if ([string]$w.id -eq $id) {
      $w.manual_elevation_gain_m = [string]$meters
      $w.manual_elevation_source = [string]$src
      break
    }
  }

  Write-Host "Saved candidate for id=$id"
}

$work | Export-Csv -Path $WorklistPath -NoTypeInformation -Encoding UTF8
Write-Host "Updated $WorklistPath"
