from __future__ import annotations

import csv
import json
import re
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
ECO_JSON_PATH = REPO_ROOT / "eco.json"
OUTPUT_DIR = REPO_ROOT / "scripts" / "data-quality"
PLACEHOLDER_SOURCE = "Екопътека.md файл"
PLovdiv_REVIEW_BBOX = {
    "min_lat": 41.7,
    "max_lat": 42.4,
    "min_lon": 23.8,
    "max_lon": 25.4,
}
URL_PATTERN = re.compile(r"https?://\S+")


def normalize_list(value: object) -> list[str]:
    if value is None:
        return []

    if isinstance(value, list):
        return [str(item).strip() for item in value if str(item).strip()]

    if isinstance(value, str):
        text = value.strip()
        if not text:
            return []

        urls = URL_PATTERN.findall(text)
        if urls:
            return urls

        parts = [item.strip() for item in re.split(r"[\n;,]+", text) if item.strip()]
        return parts or [text]

    text = str(value).strip()
    return [text] if text else []


def parse_float(value: object) -> float | None:
    if value is None:
        return None

    text = str(value).strip().replace(",", ".")
    if not text:
        return None

    try:
        return float(text)
    except ValueError:
        return None


def source_status(source: str, metadata_sources: list[str]) -> str:
    if source and source != PLACEHOLDER_SOURCE:
        return "source"
    if metadata_sources:
        return "metadata_only"
    return "missing"


def importance_score(trail: dict) -> tuple[int, list[str]]:
    metadata = trail.get("metadata") or {}
    details = trail.get("trail_details") or {}

    source = str(trail.get("source") or "").strip()
    metadata_sources = normalize_list(metadata.get("sources_urls"))
    attractions = normalize_list(trail.get("attractions"))
    nearest_town = str(((trail.get("location") or {}).get("nearest_town") or "")).strip()
    description = str(trail.get("description") or "").strip()
    photo_url = str(trail.get("photo_url") or "").strip()
    duration = str(details.get("duration") or "").strip()
    max_altitude = str(details.get("max_altitude_m") or "").strip()

    score = 0
    reasons: list[str] = []

    if source and source != PLACEHOLDER_SOURCE:
        score += 30
        reasons.append("source")

    if metadata_sources:
        score += min(len(metadata_sources) * 10, 20)
        reasons.append(f"{len(metadata_sources)} metadata source(s)")

    if photo_url:
        score += 10
        reasons.append("photo")

    if nearest_town:
        score += 10
        reasons.append("nearest_town")

    if duration:
        score += 10
        reasons.append("duration")

    if max_altitude:
        score += 5
        reasons.append("max_altitude")

    if attractions:
        score += 10
        reasons.append("attractions")

    if len(description) >= 220:
        score += 5
        reasons.append("rich_description")

    return score, reasons


def priority_tier(score: int) -> str:
    if score >= 60:
        return "P1"
    if score >= 40:
        return "P2"
    return "P3"


def load_trails() -> list[dict]:
    data = json.loads(ECO_JSON_PATH.read_text(encoding="utf-8"))
    return data.get("eco_trails", [])


def build_length_worklist(trails: list[dict]) -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []

    for trail in trails:
        details = trail.get("trail_details") or {}
        length_km = str(details.get("length_km") or "").strip()
        if length_km:
            continue

        metadata = trail.get("metadata") or {}
        metadata_sources = normalize_list(metadata.get("sources_urls"))
        score, reasons = importance_score(trail)
        location = trail.get("location") or {}
        coordinates = location.get("coordinates") or {}
        source = str(trail.get("source") or "").strip()
        row = {
            "priority_tier": priority_tier(score),
            "importance_score": score,
            "id": int(trail["id"]),
            "name": trail.get("name", ""),
            "nearest_town": str(location.get("nearest_town") or "").strip(),
            "current_region": str(location.get("region") or "").strip(),
            "latitude": str(coordinates.get("latitude") or "").strip(),
            "longitude": str(coordinates.get("longitude") or "").strip(),
            "source_status": source_status(source, metadata_sources),
            "metadata_source_count": len(metadata_sources),
            "photo_present": "yes" if str(trail.get("photo_url") or "").strip() else "no",
            "attractions_count": len(normalize_list(trail.get("attractions"))),
            "duration": str(details.get("duration") or "").strip(),
            "max_altitude_m": str(details.get("max_altitude_m") or "").strip(),
            "review_basis": "; ".join(reasons),
            "next_action": "Fill length_km from source page or manual editorial review.",
        }
        rows.append(row)

    rows.sort(
        key=lambda row: (
            row["priority_tier"],
            -int(row["importance_score"]),
            -int(row["metadata_source_count"]),
            row["name"],
        )
    )
    return rows


def is_outside_plovdiv_review_bbox(latitude: float | None, longitude: float | None) -> bool:
    if latitude is None or longitude is None:
        return False

    return not (
        PLovdiv_REVIEW_BBOX["min_lat"] <= latitude <= PLovdiv_REVIEW_BBOX["max_lat"]
        and PLovdiv_REVIEW_BBOX["min_lon"] <= longitude <= PLovdiv_REVIEW_BBOX["max_lon"]
    )


def build_region_worklist(trails: list[dict]) -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []

    for trail in trails:
        location = trail.get("location") or {}
        coordinates = location.get("coordinates") or {}
        current_region = str(location.get("region") or "").strip()
        if current_region != "Пловдив":
            continue

        latitude_text = str(coordinates.get("latitude") or "").strip()
        longitude_text = str(coordinates.get("longitude") or "").strip()
        latitude = parse_float(latitude_text)
        longitude = parse_float(longitude_text)
        if not is_outside_plovdiv_review_bbox(latitude, longitude):
            continue

        metadata = trail.get("metadata") or {}
        metadata_sources = normalize_list(metadata.get("sources_urls"))
        row = {
            "review_priority": "P1",
            "id": int(trail["id"]),
            "name": trail.get("name", ""),
            "current_region": current_region,
            "nearest_town": str(location.get("nearest_town") or "").strip(),
            "latitude": latitude_text,
            "longitude": longitude_text,
            "metadata_source_count": len(metadata_sources),
            "reason": "Coordinates fall outside the generous Plovdiv review bbox (41.7-42.4 lat, 23.8-25.4 lon).",
            "next_action": "Review region from coordinates, source URL, and nearest_town before replacing the value.",
        }
        rows.append(row)

    rows.sort(key=lambda row: (row["name"], int(row["id"])))
    return rows


def write_csv(path: Path, rows: list[dict[str, object]]) -> None:
    if not rows:
        path.write_text("", encoding="utf-8", newline="")
        return

    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    trails = load_trails()
    length_rows = build_length_worklist(trails)
    region_rows = build_region_worklist(trails)

    write_csv(OUTPUT_DIR / "length-km-missing-worklist.csv", length_rows)
    write_csv(OUTPUT_DIR / "region-plovdiv-suspicious-worklist.csv", region_rows)

    print(
        "Generated quality worklists:",
        f"length_km_missing={len(length_rows)}",
        f"plovdiv_region_suspects={len(region_rows)}",
    )


if __name__ == "__main__":
    main()