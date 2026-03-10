#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${1:-http://127.0.0.1:5218/api}"
BASE_URL="${BASE_URL%/}"
FAILURES=0

check_endpoint() {
  local label="$1"
  local path="$2"
  local expect_total_count="$3"
  local url="${BASE_URL}/${path}"

  local headers_file
  headers_file="$(mktemp)"

  curl -sS -D "$headers_file" -o /dev/null "$url"

  local status
  status="$(awk 'tolower($1) ~ /^http\// {code=$2} END {print code}' "$headers_file")"
  local etag
  etag="$(awk -F': ' 'tolower($1)=="etag" {gsub("\r", "", $2); print $2}' "$headers_file")"
  local cache_control
  cache_control="$(awk -F': ' 'tolower($1)=="cache-control" {gsub("\r", "", $2); print $2}' "$headers_file")"
  local total_count
  total_count="$(awk -F': ' 'tolower($1)=="x-total-count" {gsub("\r", "", $2); print $2}' "$headers_file")"

  if [[ "$status" == "200" ]]; then
    echo "[PASS] ${label}-Status200: HTTP 200"
  else
    echo "[FAIL] ${label}-Status200: HTTP ${status:-<missing>}"
    FAILURES=$((FAILURES + 1))
  fi

  if [[ -n "$etag" ]]; then
    echo "[PASS] ${label}-ETag: $etag"
  else
    echo "[FAIL] ${label}-ETag: Missing ETag"
    FAILURES=$((FAILURES + 1))
  fi

  if [[ "$cache_control" == *"max-age=60"* ]]; then
    echo "[PASS] ${label}-CacheControl: $cache_control"
  else
    echo "[FAIL] ${label}-CacheControl: ${cache_control:-Missing Cache-Control}"
    FAILURES=$((FAILURES + 1))
  fi

  if [[ "$expect_total_count" == "true" ]]; then
    if [[ -n "$total_count" ]]; then
      echo "[PASS] ${label}-TotalCount: $total_count"
    else
      echo "[FAIL] ${label}-TotalCount: Missing X-Total-Count"
      FAILURES=$((FAILURES + 1))
    fi
  fi

  if [[ -n "$etag" ]]; then
    local second_headers
    second_headers="$(mktemp)"
    curl -sS -D "$second_headers" -o /dev/null -H "If-None-Match: $etag" "$url"

    local second_status
    second_status="$(awk 'tolower($1) ~ /^http\// {code=$2} END {print code}' "$second_headers")"
    if [[ "$second_status" == "304" ]]; then
      echo "[PASS] ${label}-NotModified: HTTP 304"
    else
      echo "[FAIL] ${label}-NotModified: HTTP ${second_status:-<missing>}"
      FAILURES=$((FAILURES + 1))
    fi

    rm -f "$second_headers"
  else
    echo "[FAIL] ${label}-NotModified: Skipped due to missing ETag"
    FAILURES=$((FAILURES + 1))
  fi

  rm -f "$headers_file"
}

check_endpoint "Trails" "trails?page=1&pageSize=2&sortBy=name&sortDirection=asc" "true"
check_endpoint "TrailsSummary" "trails/summary?page=1&pageSize=2&sortBy=name&sortDirection=asc" "false"

if [[ "$FAILURES" -gt 0 ]]; then
  echo "\nCache smoke test failed with ${FAILURES} issue(s)."
  exit 1
fi

echo "\nCache smoke test passed."
