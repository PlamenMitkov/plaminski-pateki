param(
  [string]$InputPath = "eco.json",
  [string]$OutputDir = "scripts/data-quality/reports"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $OutputDir)) {
  New-Item -Path $OutputDir -ItemType Directory | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$snapshotCsv = Join-Path $OutputDir ("synthetic-governance-findings-" + $timestamp + ".csv")

$scriptPath = Join-Path $PSScriptRoot "synthetic-governance-audit.ps1"
if (-not (Test-Path -Path $scriptPath)) {
  throw "Missing script: $scriptPath"
}

# Run audit script to generate snapshot CSV.
& $scriptPath -InputPath $InputPath -OutputCsvPath $snapshotCsv

$latestCsv = Join-Path $OutputDir "synthetic-governance-findings-latest.csv"
Copy-Item -Path $snapshotCsv -Destination $latestCsv -Force

$findings = Import-Csv -Path $snapshotCsv -Encoding UTF8
$highSeverity = @($findings | Where-Object { ([string]$_.severity).Trim().ToLowerInvariant() -eq "high" }).Count
$mediumSeverity = @($findings | Where-Object { ([string]$_.severity).Trim().ToLowerInvariant() -eq "medium" }).Count
$lowSeverity = @($findings | Where-Object { ([string]$_.severity).Trim().ToLowerInvariant() -eq "low" }).Count

$summaryObj = [pscustomobject]@{
  total_findings = @($findings).Count
  high_severity_findings = $highSeverity
  medium_severity_findings = $mediumSeverity
  low_severity_findings = $lowSeverity
  generated_at_utc = [DateTime]::UtcNow.ToString("o")
}

$summaryObj | Add-Member -NotePropertyName "snapshot_csv" -NotePropertyValue $snapshotCsv -Force
$summaryObj | Add-Member -NotePropertyName "latest_csv" -NotePropertyValue $latestCsv -Force

$summaryPath = Join-Path $OutputDir ("synthetic-governance-summary-" + $timestamp + ".json")
$summaryObj | ConvertTo-Json -Depth 5 | Set-Content -Path $summaryPath -Encoding UTF8

$latestSummary = Join-Path $OutputDir "synthetic-governance-summary-latest.json"
Copy-Item -Path $summaryPath -Destination $latestSummary -Force

$summaryObj | ConvertTo-Json -Depth 5 | Write-Host
