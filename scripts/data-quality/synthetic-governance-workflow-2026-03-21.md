# Synthetic Governance Workflow (HITL)

Date: 2026-03-21
Scope: AI-enriched trail text and metadata updates before publication.

## 1. Inputs
- Candidate enrichment CSV: `scripts/description-enrichment/manual-ai-enrichment.csv` (or another approved batch file).
- Reviewer decisions CSV: `scripts/description-enrichment/approval-decisions.csv` (template: `approval-decisions.template.csv`).
- Target dataset: `eco.json`.

## 2. Reviewer Decision Contract
Decision file columns:
- `id`: trail id
- `decision`: `approve` or `reject`
- `approved_by`: reviewer identity (email or username)
- `approved_at_utc`: ISO-8601 UTC timestamp
- `note`: optional reason/comment

Rules:
- `approve` requires non-empty `approved_by`.
- Missing decision row blocks apply for that trail.
- `reject` prevents apply and should include a rationale in `note`.

## 3. Pre-Apply Validation (Mandatory)
Run merge in dry-run mode with governance gates enabled:

```powershell
./scripts/description-enrichment/merge-enriched-descriptions.ps1 `
  -CsvPath ./scripts/description-enrichment/manual-ai-enrichment.csv `
  -ApprovalCsvPath ./scripts/description-enrichment/approval-decisions.csv `
  -RequireHumanApproval `
  -DryRun
```

Expected:
- `blockedMissingApproval = 0`
- `blockedMissingSources = 0` (unless explicitly allowed with `-AllowMissingSources`)

## 4. Apply Phase (Controlled)
Only after dry-run is clean:

```powershell
./scripts/description-enrichment/merge-enriched-descriptions.ps1 `
  -CsvPath ./scripts/description-enrichment/manual-ai-enrichment.csv `
  -ApprovalCsvPath ./scripts/description-enrichment/approval-decisions.csv `
  -RequireHumanApproval
```

Safety behavior:
- Script creates timestamped backup of `eco.json` before writing.
- Applied rows are marked with `metadata.ai_enriched=true` and `metadata.ai_enriched_at_utc`.

## 5. Governance Audit (Repeatable)
Run data-quality audit after every apply or daily in CI/ops:

```powershell
./scripts/data-quality/run-synthetic-governance-audit.ps1 -InputPath ./eco.json
```

Artifacts:
- Snapshot CSV: `scripts/data-quality/reports/synthetic-governance-findings-<timestamp>.csv`
- Latest CSV: `scripts/data-quality/reports/synthetic-governance-findings-latest.csv`
- Snapshot summary: `scripts/data-quality/reports/synthetic-governance-summary-<timestamp>.json`
- Latest summary: `scripts/data-quality/reports/synthetic-governance-summary-latest.json`

## 6. SLA and Ownership
- Reviewer SLA: 1 business day for `approve/reject` decision.
- Data owner SLA: 1 business day to remediate high-severity findings (`synthetic_without_sources`).
- Weekly cadence: review medium/low findings (`missing_nearest_town`, `missing_length_km`, `missing_photo_url`).

## 7. Escalation
Escalate to project owner if any condition persists > 2 cycles:
- `synthetic_without_sources > 0`
- Dry-run still shows blocked approvals for release-bound batch
- Same trail appears in high-severity findings for two consecutive snapshots
