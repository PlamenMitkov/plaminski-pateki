# Trail Data Architecture Proposal

## Scope

This proposal answers five concrete needs:

1. Separate canonical static trail data from assistant-derived enrichment and live external overlays.
2. Preserve provenance and confidence for every update.
3. Add structured user ratings and correction submissions.
4. Allow the assistant to suggest updates without directly mutating canonical data.
5. Keep the design compatible with the current backend model in `EcoTrails.Api`.

## Current Backend Review

### What already exists

- `Trail` is the primary relational entity and already supports search/filter fields used by the API and client.
- `TrailEnrichmentSnapshot` already stores cached JSON payloads for offline enrichment with freshness timestamps.
- `CommunityTrailPost` already supports authenticated trail feedback posts and image uploads.
- `TrailsController` already exposes `offline-enrichment` and admin `data-quality` style endpoints.
- `TrailOfflineEnrichmentService` already does 24-hour caching and external source preview fetching.

### What is missing for the next phase

- `Trail` is too flat for provenance-heavy data. It mixes canonical facts and inferred values without field-level lineage.
- `TrailEnrichmentSnapshot` stores opaque JSON blobs, which is good for cache snapshots but not enough for approval workflows.
- `CommunityTrailPost` is free-form only. It has no structured rating, correction type, field target, confidence, or moderation state.
- `EcoJsonImportService` imports only a subset of `eco.json` into SQL. Important fields like `length_km`, source provenance, and editorial metadata remain outside the typed DB model.

## Recommended Data Layers

Use one logical trail document with four top-level layers:

1. `static`: curated canonical facts that change rarely.
2. `derived`: assistant or editor produced summaries, normalized tags, inferred hints, and validation status.
3. `dynamic`: live or cached overlays from external providers.
4. `governance`: provenance, review state, confidence, and change history.

The SQL `Trail` table can remain the search/index table, while the richer JSON payload becomes the canonical content document for editing and assistant reasoning.

## Proposed JSON Schema

```json
{
  "$schema_version": "trail-document/v1",
  "trail_id": 217,
  "slug": "dyavolskoto-garlo",
  "static": {
    "name": "Дяволското гърло",
    "description_bg": "...canonical long description...",
    "location": {
      "country": "BG",
      "region": "Смолян",
      "nearest_town": "Триград",
      "mountain_area": "Родопи",
      "coordinates": {
        "latitude": 41.615,
        "longitude": 24.379,
        "coordinate_source": "editor_verified"
      }
    },
    "trail_details": {
      "difficulty_text": "умерена",
      "difficulty_level": "Moderate",
      "length_km": 1.1,
      "duration_minutes": 60,
      "elevation_gain_m": 140,
      "max_altitude_m": 1458,
      "route_type": "out-and-back"
    },
    "media": {
      "primary_photo_url": "https://...",
      "gallery": []
    },
    "amenities": {
      "water_sources": false,
      "parking_available": true,
      "public_transport": "bus_to_trigrad"
    },
    "accessibility": {
      "wheelchair_accessible": false,
      "stroller_friendly": false,
      "bicycle_allowed": false,
      "suitable_for_kids": true
    },
    "source_refs": [
      {
        "type": "official_page",
        "url": "https://...",
        "title": "...",
        "publisher": "...",
        "trust_level": "high"
      }
    ]
  },
  "derived": {
    "short_summary_bg": "...",
    "key_highlights_bg": [
      "..."
    ],
    "terrain_and_difficulty_bg": "...",
    "seasonality": {
      "best_months": [
        "май",
        "юни",
        "септември"
      ],
      "winter_accessible": false,
      "weather_dependent": true
    },
    "safety": {
      "warnings_bg": [
        "Хлъзгави участъци след дъжд."
      ],
      "required_gear_bg": [
        "туристически обувки",
        "вода"
      ]
    },
    "quality": {
      "confidence": "high",
      "field_confidence": {
        "location.region": "high",
        "trail_details.length_km": "medium"
      },
      "verified_fields": [
        "location.coordinates",
        "location.region"
      ],
      "open_issues": [
        "length_km_needs_manual_check"
      ]
    },
    "assistant": {
      "embedding_model": "text-embedding-3-small",
      "embedding_updated_at_utc": "2026-03-20T18:00:00Z",
      "last_summary_model": "gemini-1.5-flash"
    }
  },
  "dynamic": {
    "weather_alerts": {
      "provider": "NIMH",
      "source_url": "https://www.meteo.bg/bg/forecasts/warnings",
      "fetched_at_utc": "2026-03-20T18:05:00Z",
      "expires_at_utc": "2026-03-21T18:05:00Z",
      "alerts": [
        "..."
      ]
    },
    "source_preview": {
      "title": "...",
      "description": "...",
      "fetched_at_utc": "2026-03-20T18:03:00Z"
    },
    "route_overlay": {
      "provider": "OpenRouteService",
      "generated_at_utc": "2026-03-20T18:04:00Z",
      "is_estimated_end": true,
      "coordinates": []
    },
    "live_status": {
      "trail_closed": null,
      "closure_note": null,
      "hazard_level": null
    }
  },
  "community": {
    "rating": {
      "average": 4.6,
      "count": 21,
      "last_updated_utc": "2026-03-20T17:00:00Z"
    },
    "recent_feedback_summary": {
      "positive_tags": [
        "добра маркировка"
      ],
      "warning_tags": [
        "хлъзгаво"
      ]
    }
  },
  "governance": {
    "document_status": "active",
    "created_at_utc": "2026-03-12T10:00:00Z",
    "updated_at_utc": "2026-03-20T18:06:00Z",
    "last_verified_at_utc": "2026-03-20T18:06:00Z",
    "provenance": [
      {
        "field": "trail_details.length_km",
        "value": 1.1,
        "source_type": "user_submission",
        "source_ref": "community-feedback/981",
        "review_status": "approved",
        "reviewed_by": "admin",
        "reviewed_at_utc": "2026-03-20T18:06:00Z"
      }
    ]
  }
}
```

## Relational Mapping Recommendation

Keep SQL for operational queries and add typed support for structured corrections:

### Keep and extend `Trail`

- Keep: `Id`, `Name`, `Description`, `Location`, `Region`, `Difficulty`, `DifficultyLevel`, `DurationInHours`, `ElevationGain`, `Latitude`, `Longitude`.
- Add: `LengthKm`, `PrimarySourceUrl`, `CanonicalPayloadJson`, `CanonicalPayloadVersion`, `LastVerifiedAtUtc`.

### Repurpose `TrailEnrichmentSnapshot`

Keep it for cached or generated read models, but distinguish snapshot kinds:

- `SnapshotType`: `offline_cache`, `assistant_proposal`, `editor_export`, `external_overlay`.
- `ExpiresAtUtc` for dynamic snapshots.
- `GeneratedBy` for pipeline traceability.

### Add structured feedback entities

Recommended new tables:

1. `TrailRating`
2. `TrailFieldSuggestion`
3. `TrailVerificationDecision`

Suggested shape:

```json
{
  "TrailRating": {
    "Id": 0,
    "TrailId": 0,
    "AppUserId": "...",
    "OverallRating": 5,
    "DifficultyAccuracyRating": 4,
    "SignageRating": 5,
    "SceneryRating": 5,
    "SafetyRating": 4,
    "VisitedAtUtc": "2026-03-20T00:00:00Z",
    "CreatedAtUtc": "2026-03-20T18:10:00Z"
  },
  "TrailFieldSuggestion": {
    "Id": 0,
    "TrailId": 0,
    "AppUserId": "...",
    "FieldPath": "trail_details.length_km",
    "ProposedValueJson": "1.1",
    "EvidenceText": "Measured on device / source page link",
    "SourceUrl": "https://...",
    "Confidence": "medium",
    "Status": "pending_review",
    "CreatedAtUtc": "2026-03-20T18:11:00Z"
  },
  "TrailVerificationDecision": {
    "Id": 0,
    "TrailFieldSuggestionId": 0,
    "Decision": "approved",
    "ReviewerUserId": "...",
    "DecisionNotes": "Matched official source",
    "CreatedAtUtc": "2026-03-20T18:12:00Z"
  }
}
```

## Assistant Learning and Validation Flow

The assistant should not behave like an unsupervised self-editing model. It should operate as a proposal engine with human or rule-based verification.

### Recommended flow

1. User submits review, rating, or correction.
2. API stores raw user text in `CommunityTrailPost` and structured fields in `TrailRating` and/or `TrailFieldSuggestion`.
3. Assistant reads the submission plus current trail document.
4. Assistant emits a normalized candidate patch, field confidence, and evidence summary.
5. System runs validation rules.
6. If confidence is high and rule-safe, mark as `auto-approvable` but do not directly rewrite canonical data without an approval record.
7. Admin or trusted reviewer approves or rejects.
8. Approved change updates the canonical trail document and appends a provenance event.
9. A new `TrailEnrichmentSnapshot` or canonical payload version is stored for traceability.

### Validation rules that should exist before auto-approval

- `length_km` must be numeric and within a sane range, for example $0 < length_km < 200$.
- `region` must match a controlled vocabulary.
- `coordinates` changes must stay inside Bulgaria bounds.
- `difficulty` changes must remain consistent with duration and elevation when provided.
- `source_url` must be HTTPS unless explicitly whitelisted.
- The same field should require extra review when multiple users disagree in a short time window.

## Ratings Design

Current `CommunityTrailPost` is not enough for ratings analytics. Ratings should be typed and queryable.

Recommended user-facing rating payload:

```json
{
  "trailId": 217,
  "overallRating": 5,
  "difficultyAccuracyRating": 4,
  "signageRating": 4,
  "sceneryRating": 5,
  "safetyRating": 4,
  "wouldRecommend": true,
  "visitedAtUtc": "2026-03-15T00:00:00Z",
  "comment": "Маркировката е добра, но след дъжд е хлъзгаво."
}
```

This gives the assistant structured signals instead of trying to infer everything from free text.

## External Data Integrations

### What is safe to integrate now

1. `NIMH / meteo.bg`: current warning page is already aligned with the existing offline enrichment approach. Treat it as a scraped official source with cache and expiry.
2. `OpenRouteService`: already used for route overlays. Keep it in the `dynamic.route_overlay` layer only.
3. Source page previews from curated URLs in `eco.json`: already implemented and should remain cached rather than canonical.

### Tourism and rescue sources

Based on the current review, I did not verify a documented public API for these sources:

1. `visitbulgaria.com` / official tourism portal
2. `redcross.bg`
3. Bulgarian mountain rescue related sites checked during this session

Recommendation:

- Model these as `connector candidates`, not as guaranteed APIs.
- Prefer official machine-readable feeds if they appear later.
- Until then, only ingest from explicitly approved pages, with caching, robots/terms review, and provenance labels such as `official_page_scrape` or `manual_editor_link`.

### Connector policy

- `api_connector`: documented, stable JSON/XML/feed endpoint.
- `page_connector`: approved HTML extraction with cache and failure tolerance.
- `manual_registry`: editor-curated link list when no stable machine-readable source exists.

## Practical Implementation Order

1. Add `LengthKm` and `PrimarySourceUrl` to `Trail`.
2. Add typed `TrailRating` and `TrailFieldSuggestion` tables.
3. Keep `CommunityTrailPost` for free-form narrative posts and image uploads.
4. Add `SnapshotType` and `ExpiresAtUtc` to `TrailEnrichmentSnapshot`.
5. Introduce a canonical JSON payload per trail for `static + derived + governance`.
6. Keep `dynamic` data ephemeral and cached, not imported into canonical data without approval.
7. Add admin review endpoints for pending suggestions and approved changes.

## Why This Fits The Existing Codebase

- It preserves the current `Trail` query model used by `TrailsController`.
- It reuses `TrailEnrichmentSnapshot` instead of replacing it.
- It extends `CommunityTrailPost` rather than forcing ratings into unstructured text.
- It matches the current offline enrichment pattern already implemented in `TrailOfflineEnrichmentService`.
- It gives the assistant a controlled write path through proposals and approvals instead of direct self-modification.