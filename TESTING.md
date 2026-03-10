# Testing Guide

## Run all tests

From workspace root:

- `dotnet test EcoProject.sln`

## CI test command

Recommended CI command from repository root:

- `dotnet test EcoProject.sln --configuration Release --no-build --verbosity minimal`

Typical CI sequence:

- `dotnet restore EcoProject.sln`
- `dotnet build EcoProject.sln --configuration Release`
- `dotnet test EcoProject.sln --configuration Release --no-build --verbosity minimal`

## Run only API tests project

- `dotnet test EcoTrails.Api.Tests/EcoTrails.Api.Tests.csproj`

## Run specific integration groups

Use `--filter` with class name:

- Trails endpoints: `dotnet test EcoTrails.Api.Tests/EcoTrails.Api.Tests.csproj --filter "FullyQualifiedName~TrailsEndpointTests"`
- Trails summary VM endpoint: `dotnet test EcoTrails.Api.Tests/EcoTrails.Api.Tests.csproj --filter "FullyQualifiedName~TrailsSummaryEndpointTests"`
- Favorites endpoints: `dotnet test EcoTrails.Api.Tests/EcoTrails.Api.Tests.csproj --filter "FullyQualifiedName~FavoritesEndpointTests"`
- Assistant authorization: `dotnet test EcoTrails.Api.Tests/EcoTrails.Api.Tests.csproj --filter "FullyQualifiedName~AssistantAuthorizationTests"`
- Assistant enrich rate limit: `dotnet test EcoTrails.Api.Tests/EcoTrails.Api.Tests.csproj --filter "FullyQualifiedName~AssistantRateLimitTests"`
- Assistant chat rate limit: `dotnet test EcoTrails.Api.Tests/EcoTrails.Api.Tests.csproj --filter "FullyQualifiedName~AssistantChatRateLimitTests"`
- Auth rate limit: `dotnet test EcoTrails.Api.Tests/EcoTrails.Api.Tests.csproj --filter "FullyQualifiedName~AuthRateLimitTests"`
- Health endpoints: `dotnet test EcoTrails.Api.Tests/EcoTrails.Api.Tests.csproj --filter "FullyQualifiedName~HealthEndpointTests"`

## Run specific repository/unit groups

- Trail repository: `dotnet test EcoTrails.Api.Tests/EcoTrails.Api.Tests.csproj --filter "FullyQualifiedName~TrailRepositoryTests"`
- Favorites repository: `dotnet test EcoTrails.Api.Tests/EcoTrails.Api.Tests.csproj --filter "FullyQualifiedName~FavoritesRepositoryTests"`
- Assistant message repository: `dotnet test EcoTrails.Api.Tests/EcoTrails.Api.Tests.csproj --filter "FullyQualifiedName~AssistantMessageRepositoryTests"`

## Current coverage focus

The test suite currently validates:

- Repository behavior (filtering, sorting, paging, valid/invalid ID handling)
- ViewModel endpoint contract for trails summary
- Favorites sync/get behavior, including per-user isolation and unauthorized requests
- Assistant admin authorization (forbidden for non-admin, allowed for admin)
- Rate limiting behavior for:
  - `auth` policy
  - `assistant-enrich` policy
  - `assistant` token bucket policy
- `Retry-After` header presence and format on `429 TooManyRequests`
- Trails list HTTP caching behavior:
  - `ETag` and `Cache-Control` headers on `GET /api/trails`
  - `304 Not Modified` when `If-None-Match` matches
- Health probes behavior:
  - `GET /health/live` returns `200`
  - `GET /health/ready` returns `200` when DB check passes

## Manual check for HTTP caching

Use PowerShell from workspace root (API running on `http://localhost:5218`):

```powershell
$url = "http://localhost:5218/api/trails?page=1&pageSize=2&sortBy=name&sortDirection=asc"
$first = Invoke-WebRequest -Uri $url
$etag = $first.Headers.ETag

$second = Invoke-WebRequest -Uri $url -Headers @{ "If-None-Match" = $etag } -SkipHttpErrorCheck

$first.StatusCode
$first.Headers["Cache-Control"]
$etag
$second.StatusCode
```

Expected output:
- First status code: `200`
- `Cache-Control`: `public,max-age=60`
- `ETag`: non-empty value
- Second status code: `304`

Linux/macOS (`bash`) equivalent:

```bash
url="http://localhost:5218/api/trails?page=1&pageSize=2&sortBy=name&sortDirection=asc"
etag=$(curl -sSI "$url" | awk -F': ' 'tolower($1)=="etag" {gsub("\r", "", $2); print $2}')

curl -sSI "$url"
curl -sSI -H "If-None-Match: $etag" "$url"
```

Expected behavior remains the same: first response `200` with `ETag` and `Cache-Control`, second response `304`.

`httpie` equivalent:

```bash
url="http://localhost:5218/api/trails?page=1&pageSize=2&sortBy=name&sortDirection=asc"
etag=$(http --headers GET "$url" | awk -F': ' 'tolower($1)=="etag" {print $2}' | tr -d '\r')

http --headers GET "$url"
http --headers GET "$url" "If-None-Match:$etag"
```

## One-command cache smoke scripts

PowerShell:

```powershell
./scripts/smoke-cache.ps1
./scripts/smoke-cache.ps1 -BaseUrl "http://127.0.0.1:5218/api"
```

Bash:

```bash
bash ./scripts/smoke-cache.sh
bash ./scripts/smoke-cache.sh http://127.0.0.1:5218/api
```

Both scripts validate:
- `GET /api/trails` headers and conditional request (`304`)
- `GET /api/trails/summary` headers and conditional request (`304`)

## Notes about integration test environment

Integration tests run in a testing host configuration that:

- Uses in-memory database provider
- Skips startup data initialization and background services (Hangfire)
- Uses a test authentication handler via headers:
  - `X-Test-UserId` for authenticated identity
  - `X-Test-Roles` for role-based authorization (comma-separated)
