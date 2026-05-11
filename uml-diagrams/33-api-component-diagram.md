# 33 – API Component Diagram (EcoTrails .NET 8 Web API)

```mermaid
graph TB
    Client["🌐 Frontend Client\n(React + TypeScript)"]
    External["☁️ Външни клиенти\n(Postman, Mobile)"]

    subgraph API["EcoTrails.Api [.NET 8 ASP.NET Core]"]

        subgraph MiddlewarePipeline["Middleware Pipeline (ред на изпълнение)"]
            MW1["1. CORS Middleware\n(AllowAnyOrigin в dev)"]
            MW2["2. HTTPS Redirection"]
            MW3["3. Static Files\n(/wwwroot)"]
            MW4["4. Authentication\n(JWT Bearer)"]
            MW5["5. Authorization"]
            MW6["6. Rate Limiting\n(SlidingWindow / TokenBucket)"]
            MW7["7. ETag Middleware\n(Cache validation)"]
            MW8["8. Exception Handler\n(Global error catching)"]
        end

        subgraph Endpoints["API Endpoints"]
            AuthEP["🔐 /api/auth\nPOST register, login\nPOST refresh, logout"]
            TrailsEP["🏔️ /api/trails\nGET list, GET search\nGET {id}"]
            FavEP["⭐ /api/favorites\n[Auth Required]\nGET, POST, DELETE"]
            AssistantEP["🤖 /api/assistant\n[Auth + RateLimit]\nPOST chat\nGET history"]
            CommunityEP["👥 /api/community\nGET posts\n[Auth] POST create"]
            AdminEP["👑 /api/admin\n[Role: Admin]\nPUT approve\nGET proposals"]
            HealthEP["🩺 /health\nGET (public)"]
            SummaryEP["📊 /api/trails/summary\nGET aggregated stats"]
        end

        subgraph OpenApi["API Documentation"]
            Swagger["Swagger / OpenAPI 3.0\n/openapi/v1.json\n/swagger/index.html"]
        end
    end

    Client -->|"REST + JSON\nJWT Bearer header"| MW1
    External -->|"REST + JSON"| MW1
    MW1 --> MW2 --> MW3 --> MW4 --> MW5 --> MW6 --> MW7 --> MW8
    MW8 --> AuthEP
    MW8 --> TrailsEP
    MW8 --> FavEP
    MW8 --> AssistantEP
    MW8 --> CommunityEP
    MW8 --> AdminEP
    MW8 --> HealthEP
    MW8 --> SummaryEP

    style MiddlewarePipeline fill:#fff2cc,stroke:#d6b656
    style Endpoints fill:#dae8fc,stroke:#6c8ebf
    style OpenApi fill:#d5e8d4,stroke:#82b366
```

## Описание

**Тип:** API Component Diagram

| Endpoint Group | Auth | Rate Limit | Описание |
|----------------|------|-----------|----------|
| `/api/auth` | Публичен | auth: 10/min | Регистрация + JWT |
| `/api/trails` | Публичен | – | Маршрути + ETag кеш |
| `/api/favorites` | JWT Required | – | CRUD любими маршрути |
| `/api/assistant` | JWT Required | assistant: 30/min | AI чат с RAG |
| `/api/community` | Mixed | – | Community posts |
| `/api/admin` | Role: Admin | – | Одобрение + управление |
| `/health` | Публичен | – | Health check endpoint |

**Middleware ред:** Всяка заявка минава през целия pipeline в определен ред преди да достигне контролера.
