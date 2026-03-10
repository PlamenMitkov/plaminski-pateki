#!/usr/bin/env bash
set -euo pipefail

ACTION="${1:-up}"
ENV_FILE="${2:-.env.production}"
COMPOSE_FILE="${3:-docker-compose.prod.yml}"
API_TAG="${4:-}"
CLIENT_TAG="${5:-}"
API_HEALTH_URL="${6:-http://127.0.0.1:5218/health/ready}"
JSON_OUTPUT="${7:-false}"
CLIENT_HEALTH_URL="${8:-}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${ROOT_DIR}"

if [[ ! -f "${COMPOSE_FILE}" ]]; then
  echo "Compose file not found: ${COMPOSE_FILE}" >&2
  exit 1
fi

if [[ ! -f "${ENV_FILE}" ]]; then
  echo "Env file not found: ${ENV_FILE}. Create it from .env.production.example" >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker CLI not found. Install Docker first." >&2
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  echo "Docker daemon is not running." >&2
  exit 1
fi

base_cmd=(docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}")

case "${ACTION}" in
  pull)
    "${base_cmd[@]}" pull
    ;;
  up)
    "${base_cmd[@]}" pull
    "${base_cmd[@]}" up -d
    "${base_cmd[@]}" ps
    ;;
  down)
    "${base_cmd[@]}" down
    ;;
  restart)
    "${base_cmd[@]}" pull
    "${base_cmd[@]}" up -d --force-recreate
    "${base_cmd[@]}" ps
    ;;
  logs)
    "${base_cmd[@]}" logs -f --tail 200
    ;;
  ps)
    "${base_cmd[@]}" ps
    ;;
  config)
    "${base_cmd[@]}" config
    ;;
  rollback)
    if [[ -z "${API_TAG}" && -z "${CLIENT_TAG}" ]]; then
      echo "Rollback requires at least one tag argument: <api-tag> and/or <client-tag>" >&2
      echo "Usage: bash ./scripts/deploy-prod.sh rollback .env.production docker-compose.prod.yml <api-tag> <client-tag>" >&2
      exit 1
    fi

    services=()
    rollback_env=()

    if [[ -n "${API_TAG}" ]]; then
      services+=(api)
      rollback_env+=("API_IMAGE_TAG=${API_TAG}")
    fi

    if [[ -n "${CLIENT_TAG}" ]]; then
      services+=(client)
      rollback_env+=("CLIENT_IMAGE_TAG=${CLIENT_TAG}")
    fi

    env "${rollback_env[@]}" "${base_cmd[@]}" pull "${services[@]}"
    env "${rollback_env[@]}" "${base_cmd[@]}" up -d --force-recreate "${services[@]}"
    "${base_cmd[@]}" ps
    ;;
  status)
    if [[ "${JSON_OUTPUT,,}" == "true" ]]; then
      compose_json="[]"
      compose_error=""
      if ! compose_json="$("${base_cmd[@]}" ps --format json 2>/dev/null)"; then
        compose_error="docker compose ps failed"
        compose_json="[]"
      fi
      if [[ -z "${compose_json}" ]]; then
        compose_json="[]"
      fi

      status_code="000"
      health_ok="false"
      health_error=""

      if command -v curl >/dev/null 2>&1; then
        status_code="$(curl -sS -o /dev/null -w '%{http_code}' "${API_HEALTH_URL}" || true)"
        if [[ "${status_code}" == "200" ]]; then
          health_ok="true"
        else
          health_error="HTTP ${status_code}"
        fi
      else
        health_error="curl not found"
      fi

      if [[ ! "${status_code}" =~ ^[0-9]+$ ]]; then
        status_code="0"
      elif [[ "${status_code}" == "000" ]]; then
        status_code="0"
      fi

      client_enabled="false"
      client_status_code="0"
      client_ok="false"
      client_error=""

      if [[ -n "${CLIENT_HEALTH_URL}" ]]; then
        client_enabled="true"
        if command -v curl >/dev/null 2>&1; then
          client_status_code="$(curl -sS -o /dev/null -w '%{http_code}' "${CLIENT_HEALTH_URL}" || true)"
          if [[ "${client_status_code}" == "200" ]]; then
            client_ok="true"
          else
            client_error="HTTP ${client_status_code}"
          fi
        else
          client_error="curl not found"
        fi
      fi

      if [[ ! "${client_status_code}" =~ ^[0-9]+$ ]]; then
        client_status_code="0"
      elif [[ "${client_status_code}" == "000" ]]; then
        client_status_code="0"
      fi

      overall_ok="false"
      if [[ "${health_ok}" == "true" && -z "${compose_error}" && ( "${client_enabled}" != "true" || "${client_ok}" == "true" ) ]]; then
        overall_ok="true"
      fi

      timestamp_utc="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
      printf '{"timestampUtc":"%s","action":"status","overallOk":%s,"compose":%s,"composeError":"%s","apiHealth":{"url":"%s","ok":%s,"statusCode":%s,"error":"%s"},"clientHealth":{"enabled":%s,"url":"%s","ok":%s,"statusCode":%s,"error":"%s"}}\n' \
        "${timestamp_utc}" \
        "${overall_ok}" \
        "${compose_json}" \
        "${compose_error}" \
        "${API_HEALTH_URL}" \
        "${health_ok}" \
        "${status_code:-0}" \
        "${health_error}" \
        "${client_enabled}" \
        "${CLIENT_HEALTH_URL}" \
        "${client_ok}" \
        "${client_status_code:-0}" \
        "${client_error}"

      if [[ "${overall_ok}" != "true" ]]; then
        exit 1
      fi
      exit 0
    fi

    "${base_cmd[@]}" ps

    if command -v curl >/dev/null 2>&1; then
      status_code="$(curl -sS -o /dev/null -w '%{http_code}' "${API_HEALTH_URL}" || true)"
      if [[ "${status_code}" == "200" ]]; then
        echo "API health check OK: HTTP 200 (${API_HEALTH_URL})"
      else
        echo "API health check failed: HTTP ${status_code:-<unreachable>} (${API_HEALTH_URL})" >&2
        exit 1
      fi
    else
      echo "curl not found; skipped API health check (${API_HEALTH_URL})." >&2
    fi

    if [[ -n "${CLIENT_HEALTH_URL}" ]]; then
      if command -v curl >/dev/null 2>&1; then
        client_status_code="$(curl -sS -o /dev/null -w '%{http_code}' "${CLIENT_HEALTH_URL}" || true)"
        if [[ "${client_status_code}" == "200" ]]; then
          echo "Client health check OK: HTTP 200 (${CLIENT_HEALTH_URL})"
        else
          echo "Client health check failed: HTTP ${client_status_code:-<unreachable>} (${CLIENT_HEALTH_URL})" >&2
          exit 1
        fi
      else
        echo "curl not found; skipped Client health check (${CLIENT_HEALTH_URL})." >&2
      fi
    fi
    ;;
  *)
    echo "Unknown action: ${ACTION}" >&2
    echo "Allowed actions: pull | up | down | restart | logs | ps | config | rollback | status" >&2
    exit 1
    ;;
esac