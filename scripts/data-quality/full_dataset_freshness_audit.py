from __future__ import annotations

import argparse
import csv
import json
import re
import unicodedata
from collections import Counter
from dataclasses import dataclass
from datetime import date, datetime
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_INPUT_CANDIDATES = [REPO_ROOT / "ecoupdated.json", REPO_ROOT / "eco.json"]
DEFAULT_OUTPUT_DIR = REPO_ROOT / "scripts" / "data-quality" / "reports"
PLACEHOLDER_SOURCE = "Екопътека.md файл"
GENERIC_DESCRIPTION_PREFIXES = (
    "екопътека във българия",
    "екопътека в българия",
    "екопътека",
)
STOPWORDS = {
    "алея",
    "балкан",
    "българия",
    "вековете",
    "връх",
    "върхът",
    "екопътека",
    "етап",
    "здравето",
    "информационна",
    "историческа",
    "кръгов",
    "културен",
    "линеен",
    "манастир",
    "маршрут",
    "маршрутът",
    "обиколна",
    "парк",
    "планина",
    "планините",
    "площадка",
    "подход",
    "поход",
    "преход",
    "пътека",
    "пътят",
    "разходка",
    "река",
    "спортно",
    "стътека",
    "стътеки",
    "тека",
    "трасе",
    "туристическа",
    "хижа",
}
PLACEHOLDER_VALUES = {"", "-", "n/a", "na", "няма", "не е посочена", "неизвестна", "неизвестно"}
EMAIL_REGION_HINTS = {
    "plovdiv": "Plovdiv",
    "varna": "Varna",
    "smolyan": "Smolyan",
    "burgas": "Burgas",
    "sofia": "Sofia",
    "vidin": "Vidin",
}


@dataclass(frozen=True)
class Finding:
    severity: str
    category: str
    trail_id: int
    trail_name: str
    region: str
    nearest_town: str
    details: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run full freshness audit for eco dataset.")
    parser.add_argument("--input", dest="input_path", help="Input dataset path. Defaults to ecoupdated.json, then eco.json.")
    parser.add_argument(
        "--output-dir",
        dest="output_dir",
        default=str(DEFAULT_OUTPUT_DIR),
        help="Directory for CSV/JSON reports.",
    )
    return parser.parse_args()


def resolve_input_path(explicit_path: str | None) -> Path:
    if explicit_path:
        path = Path(explicit_path).resolve()
        if not path.exists():
            raise FileNotFoundError(f"Input file not found: {path}")
        return path

    for candidate in DEFAULT_INPUT_CANDIDATES:
        if candidate.exists():
            return candidate

    raise FileNotFoundError("No dataset file found. Checked ecoupdated.json and eco.json.")


def load_trails(input_path: Path) -> list[dict[str, Any]]:
    payload = json.loads(input_path.read_text(encoding="utf-8-sig"))
    trails = payload.get("eco_trails")
    if not isinstance(trails, list):
        raise ValueError(f"eco_trails array not found in {input_path}")
    return trails


def normalize_text(value: Any) -> str:
    text = str(value or "").strip().lower()
    if not text:
        return ""

    normalized = unicodedata.normalize("NFKD", text)
    normalized = "".join(ch for ch in normalized if not unicodedata.combining(ch))
    normalized = normalized.replace("\u2013", " ").replace("\u2014", " ")
    normalized = re.sub(r"[^\w\s]", " ", normalized, flags=re.UNICODE)
    normalized = re.sub(r"\s+", " ", normalized)
    return normalized.strip()


def normalize_placeholder(value: Any) -> str:
    return normalize_text(value)


def has_value(value: Any) -> bool:
    return normalize_placeholder(value) not in PLACEHOLDER_VALUES


def tokenize_name(name: str) -> list[str]:
    normalized = normalize_text(name)
    tokens = [token for token in normalized.split() if len(token) >= 5 and token not in STOPWORDS and not token.isdigit()]
    return tokens


def parse_date(value: Any) -> date | None:
    text = str(value or "").strip()
    if not text:
        return None

    for fmt in ("%Y-%m-%d", "%Y-%m-%dT%H:%M:%S", "%Y-%m-%dT%H:%M:%S.%f", "%Y-%m-%dT%H:%M:%S%z"):
        try:
            return datetime.strptime(text, fmt).date()
        except ValueError:
            continue

    try:
        return datetime.fromisoformat(text.replace("Z", "+00:00")).date()
    except ValueError:
        return None


def detect_email_region_mismatch(email: str, region: str) -> str | None:
    normalized_email = normalize_text(email)
    normalized_region = normalize_text(region)
    if not normalized_email or not normalized_region:
        return None

    for hint, expected in EMAIL_REGION_HINTS.items():
        if hint in normalized_email and hint not in normalized_region:
            return f"contact email suggests {expected}, but record region is '{region}'"

    return None


def build_findings(trails: list[dict[str, Any]], audit_date: date) -> list[Finding]:
    findings: list[Finding] = []

    for trail in trails:
        trail_id = int(trail.get("id") or 0)
        trail_name = str(trail.get("name") or "").strip()
        location = trail.get("location") or {}
        details = trail.get("trail_details") or {}
        metadata = trail.get("metadata") or {}
        contact_info = trail.get("contact_info") or {}

        region = str(location.get("region") or "").strip()
        nearest_town = str(location.get("nearest_town") or "").strip()
        description = str(trail.get("description") or "").strip()
        short_summary = str(trail.get("short_summary") or "").strip()
        metadata_short_summary = str(metadata.get("short_summary") or "").strip()
        source = str(trail.get("source") or "").strip()
        sources_urls = str(metadata.get("sources_urls") or "").strip()
        photo_url = str(trail.get("photo_url") or "").strip()
        length_km = str(details.get("length_km") or "").strip()
        last_verified_raw = metadata.get("last_verified")
        contact_email = str(contact_info.get("email") or "").strip()

        if not has_value(nearest_town):
            findings.append(Finding("medium", "missing_nearest_town", trail_id, trail_name, region, nearest_town, "location.nearest_town is missing"))

        if not has_value(length_km):
            findings.append(Finding("medium", "missing_length_km", trail_id, trail_name, region, nearest_town, "trail_details.length_km is missing"))

        if not has_value(photo_url):
            findings.append(Finding("low", "missing_photo_url", trail_id, trail_name, region, nearest_town, "photo_url is missing"))

        if not has_value(source):
            findings.append(Finding("high", "missing_primary_source", trail_id, trail_name, region, nearest_town, "source is missing"))
        elif normalize_text(source) == normalize_text(PLACEHOLDER_SOURCE):
            findings.append(Finding("high", "placeholder_source", trail_id, trail_name, region, nearest_town, "source uses placeholder value"))

        if metadata.get("ai_enriched") and not has_value(sources_urls):
            findings.append(Finding("high", "synthetic_without_sources", trail_id, trail_name, region, nearest_town, "ai_enriched is true but metadata.sources_urls is missing"))

        normalized_description = normalize_text(description)
        if not normalized_description:
            findings.append(Finding("high", "missing_description", trail_id, trail_name, region, nearest_town, "description is empty"))
        else:
            if len(description) < 140:
                findings.append(Finding("medium", "short_description", trail_id, trail_name, region, nearest_town, f"description is only {len(description)} chars"))

            if normalized_description.startswith(GENERIC_DESCRIPTION_PREFIXES):
                findings.append(Finding("high", "generic_description", trail_id, trail_name, region, nearest_town, "description starts with generic placeholder wording"))

            name_tokens = tokenize_name(trail_name)
            searchable_text = " ".join(
                item for item in [description, short_summary, metadata_short_summary] if str(item).strip()
            )
            normalized_searchable_text = normalize_text(searchable_text)
            matched_tokens = [token for token in name_tokens if token in normalized_searchable_text]

            if name_tokens and not matched_tokens:
                findings.append(
                    Finding(
                        "high",
                        "name_not_reflected_in_content",
                        trail_id,
                        trail_name,
                        region,
                        nearest_town,
                        f"none of the core name tokens appear in description/summary: {', '.join(name_tokens[:4])}",
                    )
                )

        normalized_short_summary = normalize_text(short_summary)
        if not normalized_short_summary:
            findings.append(Finding("medium", "missing_short_summary", trail_id, trail_name, region, nearest_town, "short_summary is empty"))

        if metadata_short_summary and short_summary and normalize_text(metadata_short_summary) != normalize_text(short_summary):
            findings.append(Finding("low", "summary_mismatch", trail_id, trail_name, region, nearest_town, "metadata.short_summary differs from short_summary"))

        verified_date = parse_date(last_verified_raw)
        if last_verified_raw and verified_date is None:
            findings.append(Finding("medium", "invalid_last_verified", trail_id, trail_name, region, nearest_town, f"metadata.last_verified is not parseable: {last_verified_raw}"))
        elif verified_date is None:
            findings.append(Finding("medium", "missing_last_verified", trail_id, trail_name, region, nearest_town, "metadata.last_verified is missing"))
        else:
            age_days = (audit_date - verified_date).days
            if age_days > 90:
                findings.append(Finding("medium", "stale_verification", trail_id, trail_name, region, nearest_town, f"last_verified is {age_days} days old"))

        email_mismatch = detect_email_region_mismatch(contact_email, region)
        if email_mismatch:
            findings.append(Finding("high", "contact_email_region_mismatch", trail_id, trail_name, region, nearest_town, email_mismatch))

    return findings


def write_csv(output_path: Path, findings: list[Finding]) -> None:
    fieldnames = ["severity", "category", "trail_id", "trail_name", "region", "nearest_town", "details"]
    with output_path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for finding in findings:
            writer.writerow(
                {
                    "severity": finding.severity,
                    "category": finding.category,
                    "trail_id": finding.trail_id,
                    "trail_name": finding.trail_name,
                    "region": finding.region,
                    "nearest_town": finding.nearest_town,
                    "details": finding.details,
                }
            )


def build_summary(input_path: Path, trails: list[dict[str, Any]], findings: list[Finding], audit_date: date, csv_path: Path) -> dict[str, Any]:
    by_severity = Counter(item.severity for item in findings)
    by_category = Counter(item.category for item in findings)
    affected_trails = {item.trail_id for item in findings}

    top_affected = Counter(item.trail_id for item in findings).most_common(25)
    trail_lookup = {int(trail.get("id") or 0): str(trail.get("name") or "") for trail in trails}

    return {
        "audit_date": audit_date.isoformat(),
        "input_path": str(input_path),
        "total_trails": len(trails),
        "affected_trails": len(affected_trails),
        "total_findings": len(findings),
        "severity_counts": dict(sorted(by_severity.items())),
        "category_counts": dict(sorted(by_category.items())),
        "top_affected_trails": [
            {"trail_id": trail_id, "trail_name": trail_lookup.get(trail_id, ""), "findings": count}
            for trail_id, count in top_affected
        ],
        "csv_report": str(csv_path),
    }


def main() -> None:
    args = parse_args()
    input_path = resolve_input_path(args.input_path)
    output_dir = Path(args.output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    trails = load_trails(input_path)
    audit_date = date.today()
    findings = build_findings(trails, audit_date)
    findings.sort(key=lambda item: (item.severity, item.category, item.trail_id))

    timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    csv_path = output_dir / f"full-freshness-findings-{timestamp}.csv"
    json_path = output_dir / f"full-freshness-summary-{timestamp}.json"
    latest_csv_path = output_dir / "full-freshness-findings-latest.csv"
    latest_json_path = output_dir / "full-freshness-summary-latest.json"

    write_csv(csv_path, findings)
    latest_csv_path.write_text(csv_path.read_text(encoding="utf-8"), encoding="utf-8")

    summary = build_summary(input_path, trails, findings, audit_date, csv_path)
    json_payload = json.dumps(summary, ensure_ascii=False, indent=2)
    json_path.write_text(json_payload, encoding="utf-8")
    latest_json_path.write_text(json_payload, encoding="utf-8")

    print(json_payload)


if __name__ == "__main__":
    main()