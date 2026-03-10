# 🏔️ Plaminski Pateki (EcoTrails Project)

[![EcoProject CI](https://github.com/PlamenMitkov/plaminski-pateki/actions/workflows/ci.yml/badge.svg)](https://github.com/PlamenMitkov/plaminski-pateki/actions/workflows/ci.yml)
[![GHCR Packages](https://img.shields.io/badge/GHCR-Packages-blue?logo=github)](https://github.com/PlamenMitkov?tab=packages&repo_name=plaminski-pateki)
[![API Image Tag](https://img.shields.io/docker/v/plamenmitkov/ecotrails-api/latest?registry_url=ghcr.io&label=API%20Image)](https://github.com/PlamenMitkov?tab=packages&repo_name=plaminski-pateki)
[![Client Image Tag](https://img.shields.io/docker/v/plamenmitkov/ecotrails-client/latest?registry_url=ghcr.io&label=Client%20Image)](https://github.com/PlamenMitkov?tab=packages&repo_name=plaminski-pateki)

_Docker image таговете се публикуват автоматично от CI при `push` към `main`._

Интерактивна платформа за изследване на екопътеки в България, изградена с ASP.NET Core 10, React + TypeScript и SQL Server. Проектът включва реални данни за 500+ маршрута, интерактивна карта и интелигентен анализ.

## 🎯 Quick Vision

Plaminski Pateki обединява GIS визуализация, филтриране, експорт и хибридна синхронизация на любими маршрути (LocalStorage + Cloud) в един модерен full-stack продукт.

---

## 🧱 Технологичен стек

### Backend
- ⚙️ ASP.NET Core 10 Web API
- 🗄️ Entity Framework Core 10
- 🔐 ASP.NET Core Identity + JWT Authentication
- 📘 Swagger / OpenAPI

### Frontend
- ⚛️ React 18 + TypeScript
- ⚡ Vite
- 🗺️ Leaflet + Marker Clustering (Spiderfy)
- 📊 Recharts (dashboard analytics)

### Database
- 🛢️ SQL Server (SQL Server Express / SQL Server in Docker)

### DevOps & Tooling
- 🐳 Docker + Docker Compose
- 🐍 Python tooling с `uv`

---

## 🏗️ System Architecture

### API Layer
- RESTful контролери за trails, auth и favorites.
- Пагинация, търсене, филтриране по трудност и координати.
- `X-Total-Count` header + paged response metadata.
- HTTP кеширане за списъци с пътеки: `Cache-Control`, `ETag`, `If-None-Match` (`304 Not Modified`).
- Health probes за оркестратори: `/health/live` (liveness) и `/health/ready` (readiness + DB).

### Auth Layer
- Регистрация/вход с JWT.
- Защитени endpoints за favorites sync.
- Cloud-first hybrid модел: локални любими + синхронизация към база след login.

### GIS Layer
- Интерактивна карта с Leaflet.
- Клъстеризация на маркери за висока плътност на данни.
- Spiderfy поведение за припокриващи се координати.

### Data Layer
- SQL Server + EF Core migrations.
- Импорт на реални данни от `eco.json` (500+ маршрута).

---

## 🚀 Quick Start (Docker First)

```bash
# Clone repository
git clone https://github.com/PlamenMitkov/plaminski-pateki.git
cd plaminski-pateki

# Start everything
docker compose up --build
```

### Services
- Frontend: `http://localhost:5173`
- API: `http://localhost:5218`
- Swagger: `http://localhost:5218/swagger`
- Health (liveness): `http://localhost:5218/health/live`
- Health (readiness): `http://localhost:5218/health/ready`
- SQL Server: `localhost:1433`

### OpenAI Assistant setup (gpt-3.5-turbo)

Backend assistant endpoint: `POST /api/assistant/chat`

Assistant chat response now includes alternative recommendations via `suggestedAlternativeIds` and `suggestedAlternatives`.

Semantic enrichment endpoint: `POST /api/assistant/enrich`

Note: enrichment is optional and can be skipped for normal assistant chat usage.
Rate-limit optimization: use `OpenAI.EnrichDelayMs`, `OpenAI.RetryAttempts`, `OpenAI.RetryInitialDelayMs`, and `OpenAI.RetryJitterMs`.

Session persistence endpoints:
- `POST /api/assistant/sessions` (create new chat session)
- `GET /api/assistant/sessions/{sessionId}/messages?limit=80` (load conversation history)
- `GET /api/assistant/sessions/mine?limit=12` (logged-in user's sessions for profile/assistant)
- `DELETE /api/assistant/sessions/{sessionId}` (delete session from profile/assistant)

Security: deleting session requires authenticated user ownership.

Set API key in environment variable before starting API:

```powershell
$env:OPENAI_API_KEY="your_openai_key_here"
```

Default model is configured as `gpt-3.5-turbo` in `EcoTrails.Api/appsettings.json`.

Example semantic enrichment request (process 25 trails):

```bash
curl -X POST http://localhost:5218/api/assistant/enrich \
	-H "Content-Type: application/json" \
	-d '{"limit":25,"overwriteExisting":false}'
```

### OpenTelemetry (traces + metrics)

API-то вече публикува:
- traces: ASP.NET Core requests, HttpClient outbound заявки, EF Core DB операции
- metrics: ASP.NET Core, HttpClient, runtime metrics + custom `external.http.request.duration` и `external.http.requests`

За export към OTLP collector (Grafana/Tempo/Alloy/Collector), задайте:

```powershell
$env:OpenTelemetry__Otlp__Endpoint="http://localhost:4317"
$env:OpenTelemetry__Otlp__Protocol="grpc"
```

Алтернативно в `EcoTrails.Api/appsettings.json`:
- `OpenTelemetry:Otlp:Endpoint`
- `OpenTelemetry:Otlp:Protocol` (`grpc` или `http/protobuf`)

Ако endpoint не е конфигуриран, telemetry се събира локално в процеса, но не се експортира навън.

### One-click start (Windows)

```bat
start.bat
```

Скриптът е в root папката на проекта и стартира целия стек с `docker compose up --build`.

### One-click stop (Windows)

```bat
stop.bat
```

Скриптът е в root папката на проекта и спира контейнерите с `docker compose down`.

---

## 📦 Container Images (GHCR)

При `push` към `main`, GitHub Actions workflow-ът публикува Docker image-и в GitHub Container Registry (GHCR):

- `ghcr.io/<github-owner>/ecotrails-api`
- `ghcr.io/<github-owner>/ecotrails-client`

Публикувани тагове:

- `latest`
- `sha-<commit>`

Примерно теглене на image-и:

```bash
docker pull ghcr.io/<github-owner>/ecotrails-api:latest
docker pull ghcr.io/<github-owner>/ecotrails-client:latest
```

Примерно стартиране локално:

```bash
docker run --rm -p 8080:8080 ghcr.io/<github-owner>/ecotrails-api:latest
docker run --rm -p 5173:80 ghcr.io/<github-owner>/ecotrails-client:latest
```

Ако пакетите са private, първо login към GHCR:

```bash
echo <github_pat> | docker login ghcr.io -u <github-username> --password-stdin
```

### Production deployment with Docker Compose

1) Създай production env файл от шаблона:

```bash
cp .env.production.example .env.production
```

2) Попълни задължително:
- `MSSQL_SA_PASSWORD`
- `JWT_KEY`
- `CORS_ORIGIN_0`
- `GHCR_OWNER`

3) (Опционално) външни API ключове за backend:
- `APP_OPENROUTESERVICE_API_KEY`
- `APP_OPENAI_API_KEY`

4) (Опционално) за telemetry export към Grafana/Tempo/Collector:
- `OTEL_EXPORTER_OTLP_ENDPOINT`
- `OTEL_EXPORTER_OTLP_PROTOCOL` (`grpc` или `http/protobuf`)

5) Изтегли и стартирай production стека:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml pull
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
```

6) Проверка на услугите:
- API health: `http://<host>:<API_PORT>/health/ready`
- Client: `http://<host>:<CLIENT_PORT>`

Helper scripts:

```powershell
./scripts/deploy-prod.ps1 -Action config
./scripts/deploy-prod.ps1 -Action up
./scripts/deploy-prod.ps1 -Action ps
./scripts/deploy-prod.ps1 -Action status
./scripts/deploy-prod.ps1 -Action status -Json
./scripts/deploy-prod.ps1 -Action status -ClientHealthUrl http://127.0.0.1:5173
./scripts/deploy-prod.ps1 -Action logs
./scripts/deploy-prod.ps1 -Action rollback -ApiTag sha-abc1234
./scripts/deploy-prod.ps1 -Action rollback -ApiTag sha-abc1234 -ClientTag sha-def5678
./scripts/deploy-prod.ps1 -Action down
```

```bash
bash ./scripts/deploy-prod.sh config
bash ./scripts/deploy-prod.sh up
bash ./scripts/deploy-prod.sh ps
bash ./scripts/deploy-prod.sh status
bash ./scripts/deploy-prod.sh status .env.production docker-compose.prod.yml "" "" http://127.0.0.1:5218/health/ready true
bash ./scripts/deploy-prod.sh status .env.production docker-compose.prod.yml "" "" http://127.0.0.1:5218/health/ready true http://127.0.0.1:5173
bash ./scripts/deploy-prod.sh logs
bash ./scripts/deploy-prod.sh rollback .env.production docker-compose.prod.yml sha-abc1234
bash ./scripts/deploy-prod.sh rollback .env.production docker-compose.prod.yml sha-abc1234 sha-def5678
bash ./scripts/deploy-prod.sh down
```

`status` изпълнява `compose ps` и проверява API health endpoint (по подразбиране `http://127.0.0.1:5218/health/ready`).
`status` може опционално да проверява и frontend URL, а JSON режимът връща `overallOk`, `compose`, `apiHealth` и `clientHealth`.

CI deployment health gate:
- Workflow job `deploy-health-gate` използва JSON режима на `status` и fail-ва при `overallOk=false`.
- За да е активен gate-ът в GitHub Actions, добави repository secret `DEPLOY_API_HEALTH_URL` (например `https://api.example.com/health/ready`).
- За frontend gate добави и `DEPLOY_CLIENT_HEALTH_URL` (например `https://app.example.com/`). Ако липсва, frontend check се пропуска.

Спиране:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml down
```

---

## 🧪 Smoke Test & Validation

Подробно ръководство за unit/integration тестове и филтриране по test групи: [TESTING.md](TESTING.md)

- ✅ **Auth:** регистрация/вход + JWT валидация.
- ✅ **Sync:** хибридна синхронизация на любими пътеки (LocalStorage ↔ Cloud DB).
- ✅ **Maps:** интерактивно филтриране и визуализация на 500+ локации.
- ✅ **Export:** CSV експорт на филтрирани масиви.
- ✅ **Analytics:** Pie/Bar диаграми се актуализират при промяна на favorite state.

### HTTP caching quick check (`ETag` / `304`)

Проверка за условни заявки към списъка с пътеки:

```powershell
$r1 = Invoke-WebRequest -Uri "http://localhost:5218/api/trails?page=1&pageSize=2&sortBy=name&sortDirection=asc"
$etag = $r1.Headers.ETag
Invoke-WebRequest -Uri "http://localhost:5218/api/trails?page=1&pageSize=2&sortBy=name&sortDirection=asc" -Headers @{ "If-None-Match" = $etag } -SkipHttpErrorCheck
```

Очакван резултат:
- Първа заявка: `200 OK` + `ETag` + `Cache-Control: public,max-age=60`
- Втора заявка (с `If-None-Match`): `304 Not Modified`

Linux/macOS (`bash`) вариант:

```bash
URL="http://localhost:5218/api/trails?page=1&pageSize=2&sortBy=name&sortDirection=asc"
ETAG=$(curl -sSI "$URL" | awk -F': ' 'tolower($1)=="etag" {gsub("\r", "", $2); print $2}')
curl -sSI -H "If-None-Match: $ETAG" "$URL"
```

`httpie` вариант:

```bash
URL="http://localhost:5218/api/trails?page=1&pageSize=2&sortBy=name&sortDirection=asc"
ETAG=$(http --headers GET "$URL" | awk -F': ' 'tolower($1)=="etag" {print $2}' | tr -d '\r')
http --headers GET "$URL" "If-None-Match:$ETAG"
```

### Quick security smoke script

When the API is running locally, execute:

```powershell
./scripts/smoke-auth.ps1
```

Optional custom API base URL:

```powershell
./scripts/smoke-auth.ps1 -BaseUrl "http://127.0.0.1:5218/api"
```

The script checks register/login, `/auth/me`, unauthenticated access rejection, authenticated assistant session creation, and admin-only enrich rejection for non-admin users.

### HTTP cache smoke script

One-command validation for `ETag` / `Cache-Control` / `304 Not Modified`:

```powershell
./scripts/smoke-cache.ps1
```

Optional custom API base URL:

```powershell
./scripts/smoke-cache.ps1 -BaseUrl "http://127.0.0.1:5218/api"
```

Linux/macOS:

```bash
bash ./scripts/smoke-cache.sh
# or with custom base URL
bash ./scripts/smoke-cache.sh http://127.0.0.1:5218/api
```

### Admin-path smoke script

Prerequisite: configure an existing user email under `Admin:Emails` in `EcoTrails.Api/appsettings.json`, then restart the API so role seeding can assign `Admin`.

Run:

```powershell
./scripts/smoke-admin.ps1 -AdminEmail "admin@example.com" -AdminPassword "YourPassword"
```

Optional flags:

```powershell
./scripts/smoke-admin.ps1 -AdminEmail "admin@example.com" -AdminPassword "YourPassword" -BaseUrl "http://127.0.0.1:5218/api" -EnrichLimit 1
```

The script validates admin login, checks the `Admin` role via `/auth/me`, and verifies that `/assistant/enrich` succeeds for an admin token.

### Cleanup temporary test user

Remove a temporary smoke-test account by email:

```powershell
./scripts/cleanup-test-user.ps1 -Email "tempadmin_20260301@example.com"
```

Optional custom SQL connection string:

```powershell
./scripts/cleanup-test-user.ps1 -Email "tempadmin_20260301@example.com" -ConnectionString "Server=.\\SQLEXPRESS;Database=EcoTrailsDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

### One-command admin smoke + cleanup

Run the full end-to-end flow in one command:

```powershell
./scripts/smoke-admin-e2e.ps1
```

What it does automatically:
- Builds solution (unless `-SkipBuild` is used)
- Starts API
- Creates a temporary user
- Restarts API with runtime admin seeding (`Admin__Emails__0`)
- Runs `smoke-admin.ps1`
- Cleans up the temporary user

Optional parameters:

```powershell
./scripts/smoke-admin-e2e.ps1 -TempEmail "tempadmin@example.com" -TempPassword "Passw0rd!" -BaseUrl "http://127.0.0.1:5218/api" -SkipBuild
```

For CI environments without `OpenRouteService:ApiKey`, you can skip only the OpenRoute validation step:

```powershell
./scripts/smoke-admin-e2e.ps1 -SkipBuild -SkipOpenRoute
```

---

## 📁 Project Structure

```text
.
├─ EcoTrails.Api/        # ASP.NET Core Web API + EF Core + Identity/JWT
├─ EcoTrails.Client/     # React + TypeScript + Vite + Leaflet + Recharts
├─ eco.json              # Source dataset for trail import
└─ docker-compose.yml    # Full local orchestration (DB + API + Client)
```

---

## 🗺️ Future Roadmap

- 🌦️ Интеграция с weather API за реална прогноза по пътеки.
- 📱 PWA режим за офлайн достъп в планински условия.
- 🧭 Разширени гео-филтри (денивелация, дължина, сезонност).
- 👥 Role-based admin панел за управление на съдържание.
- ☁️ CI/CD deployment pipeline (build, test, container publish).

---

## 👨‍💻 Author

**Plamen Mitkov**

- GitHub: https://github.com/PlamenMitkov
- Repository: https://github.com/PlamenMitkov/plaminski-pateki
