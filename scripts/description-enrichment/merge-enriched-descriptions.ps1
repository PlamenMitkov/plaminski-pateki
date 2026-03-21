<#
.SYNOPSIS
    Merge Gemini-enriched descriptions back into eco.json.

.DESCRIPTION
    Reads `needs-enrichment-combined.csv` (after Gemini has filled it in),
    and for each row updates the matching trail in eco.json with:
      - description          ← enriched_description_bg
      - metadata.short_summary
      - metadata.key_highlights
      - metadata.terrain_and_difficulty
      - suitability           (if filled)
      - best_season           (if filled)
      - safety_warnings       (if filled, from cautions)
      - nearby_amenities      (if filled, from nearby_points_of_interest)
      - metadata.sources_urls
      - metadata.confidence
      - metadata.reviewer_notes

.PARAMETER CsvPath
    Path to the enriched CSV file. Default: needs-enrichment-combined.csv

.PARAMETER EcoJsonPath
    Path to eco.json. Default: eco.json (relative to script location)

.PARAMETER DryRun
    If set, prints what would change but does not write eco.json.

.PARAMETER RequireHumanApproval
    If set, every row must have explicit approval decision in ApprovalCsvPath before it can be applied.

.PARAMETER ApprovalCsvPath
    Path to approval decisions CSV with headers: id,decision,approved_by,approved_at_utc,note

.PARAMETER AllowMissingSources
    If set, rows without sources_urls can still be applied. By default, missing sources are blocked.

.EXAMPLE
    .\merge-enriched-descriptions.ps1
    .\merge-enriched-descriptions.ps1 -DryRun
    .\merge-enriched-descriptions.ps1 -CsvPath "my-enriched.csv"
#>
param(
    [string]$CsvPath            = "$PSScriptRoot\needs-enrichment-combined.csv",
    [string]$EcoJsonPath        = "$PSScriptRoot\..\..\eco.json",
    [switch]$DryRun,
    [switch]$RequireHumanApproval,
    [string]$ApprovalCsvPath    = "$PSScriptRoot\approval-decisions.csv",
    [switch]$AllowMissingSources
)

Set-StrictMode -Version 3

# ── Load files ────────────────────────────────────────────────────────────────
if (-not (Test-Path $CsvPath))     { Write-Error "CSV not found: $CsvPath"; exit 1 }
if (-not (Test-Path $EcoJsonPath)) { Write-Error "eco.json not found: $EcoJsonPath"; exit 1 }

Write-Host "Loading eco.json..." -ForegroundColor Cyan
$eco   = Get-Content $EcoJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$trails = $eco['eco_trails']

Write-Host "Loading CSV: $CsvPath" -ForegroundColor Cyan
$csv = Import-Csv $CsvPath -Encoding UTF8

$approvalById = @{}
if ($RequireHumanApproval) {
    if (-not (Test-Path $ApprovalCsvPath)) {
        Write-Error "Approval CSV not found: $ApprovalCsvPath"
        exit 1
    }

    Write-Host "Loading approval decisions: $ApprovalCsvPath" -ForegroundColor Cyan
    foreach ($entry in (Import-Csv $ApprovalCsvPath -Encoding UTF8)) {
        $approvalById[[string]$entry.id] = $entry
    }
}

# ── Build lookup: id → trail hashtable ────────────────────────────────────────
$lookup = @{}
foreach ($trail in $trails) { $lookup[[string]$trail['id']] = $trail }

# ── Helpers ───────────────────────────────────────────────────────────────────
function HasValue([string]$s) { $s -and $s.Trim() -ne '' -and $s.Trim() -ne 'няма' }

function IsApproved($entry) {
    if ($null -eq $entry) { return $false }

    $decision = [string]$entry.decision
    $approvedBy = [string]$entry.approved_by
    return $decision.Trim().ToLowerInvariant() -eq 'approve' -and (HasValue $approvedBy)
}

# ── Merge ─────────────────────────────────────────────────────────────────────
$updated = 0
$skipped = 0
$notFound = 0
$blockedMissingSources = 0
$blockedMissingApproval = 0
$blockedRejected = 0

foreach ($row in $csv) {
    $id = [string]$row.id

    if (-not $lookup.ContainsKey($id)) {
        Write-Warning "Trail id=$id not found in eco.json — skipped"
        $notFound++
        continue
    }

    $desc = $row.enriched_description_bg.Trim()
    if ($desc.Length -lt 30) {
        Write-Warning "id=$id — enriched_description_bg is empty/too short ($($desc.Length) chars) — skipped"
        $skipped++
        continue
    }

    if (-not $AllowMissingSources -and -not (HasValue ([string]$row.sources_urls))) {
        Write-Warning "id=$id — sources_urls is missing; blocked by governance gate"
        $blockedMissingSources++
        continue
    }

    if ($RequireHumanApproval) {
        $approval = $approvalById[$id]
        if ($null -eq $approval) {
            Write-Warning "id=$id — no approval decision found; blocked by HITL gate"
            $blockedMissingApproval++
            continue
        }

        $decision = ([string]$approval.decision).Trim().ToLowerInvariant()
        if ($decision -eq 'reject') {
            Write-Warning "id=$id — reviewer decision is reject; not applied"
            $blockedRejected++
            continue
        }

        if (-not (IsApproved $approval)) {
            Write-Warning "id=$id — approval record is incomplete (decision/approved_by); blocked by HITL gate"
            $blockedMissingApproval++
            continue
        }
    }

    $trail = $lookup[$id]

    if (-not $DryRun) {
        # Core description
        $trail['description'] = $desc

        # Ensure metadata dict exists
        if (-not $trail.ContainsKey('metadata')) { $trail['metadata'] = @{} }
        $meta = $trail['metadata']

        if (HasValue $row.short_summary_bg)           { $meta['short_summary']          = $row.short_summary_bg.Trim() }
        if (HasValue $row.key_highlights)              { $meta['key_highlights']          = $row.key_highlights.Trim() }
        if (HasValue $row.terrain_and_difficulty)      { $meta['terrain_and_difficulty']  = $row.terrain_and_difficulty.Trim() }
        if (HasValue $row.sources_urls)                { $meta['sources_urls']            = $row.sources_urls.Trim() }
        if (HasValue $row.confidence)                  { $meta['confidence']              = $row.confidence.Trim() }
        if (HasValue $row.reviewer_notes)              { $meta['reviewer_notes']          = $row.reviewer_notes.Trim() }
        $meta['ai_enriched'] = $true
        $meta['ai_enriched_at_utc'] = [DateTime]::UtcNow.ToString('o')

        # Top-level fields — only overwrite if enrichment provides a better value
        if (HasValue $row.suitable_for)                { $trail['suitability']            = $row.suitable_for.Trim() }
        if (HasValue $row.best_season)                 { $trail['best_season']            = $row.best_season.Trim() }
        if (HasValue $row.cautions)                    { $trail['safety_warnings']        = $row.cautions.Trim() }
        if (HasValue $row.nearby_points_of_interest)   { $trail['nearby_amenities']       = $row.nearby_points_of_interest.Trim() }
    }

    Write-Host "  ✓ id=$id ($($desc.Length) chars)" -ForegroundColor Green
    $updated++
}

Write-Host ""
Write-Host "Summary: updated=$updated  skipped(empty)=$skipped  notFound=$notFound  blockedMissingSources=$blockedMissingSources  blockedMissingApproval=$blockedMissingApproval  blockedRejected=$blockedRejected" -ForegroundColor Yellow

if ($DryRun) {
    Write-Host "[DRY RUN] eco.json was NOT written." -ForegroundColor Magenta
    exit 0
}

# ── Backup + write ─────────────────────────────────────────────────────────────
$backupPath = $EcoJsonPath -replace '\.json$', ("-backup-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".json")
Copy-Item $EcoJsonPath $backupPath
Write-Host "Backup saved → $backupPath" -ForegroundColor Cyan

$eco | ConvertTo-Json -Depth 20 -Compress:$false | Set-Content $EcoJsonPath -Encoding UTF8
Write-Host "eco.json updated successfully." -ForegroundColor Green
