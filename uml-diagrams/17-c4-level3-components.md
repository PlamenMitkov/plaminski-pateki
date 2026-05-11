# 17 – C4 Level 3: Диаграма на компонентите (Backend API)

```mermaid
graph TB
    Frontend["📱 Frontend SPA\n[React + TypeScript]"]
    DB["🗄️ SQL Server\n[MS SQL Server]"]
    EcoJson["📄 eco.json"]
    Gemini["☁️ Gemini API"]
    OpenAI["☁️ OpenAI API"]
    Weather["☁️ Open-Meteo API"]

    subgraph BackendAPI["Backend API Service [.NET 8 / ASP.NET Core]"]

        subgraph Controllers["Controllers Layer"]
            AuthCtrl["AuthController\nПостана/Изход/Refresh"]
            TrailsCtrl["TrailsController\nМаршрути + Търсене"]
            FavCtrl["FavoritesController\nФаворити CRUD"]
            AssistantCtrl["AssistantController\nАИ чат"]
            CommunityCtrl["CommunityController\nПубликации"]
            AdminCtrl["AdminController\nАдмин операции"]
        end

        subgraph Services["Services Layer"]
            AuthSvc["AuthService\nJWT генерация + хеш"]
            TrailSvc["TrailService\nЧетене + Филтриране"]
            AssistantSvc["AssistantService\nOркестрация на AI"]
            SafetySvc["SafetyService\nПроверка на промпт"]
            AssemblySvc["PromptAssemblyService\nСъставяне на контекст"]
            RetrievalSvc["RetrievalService\nRAG извличане"]
            ComposeSvc["ResponseCompositionService\nФорматиране"]
            WeatherSvc["WeatherService\nМетеорология"]
            HangfireSvc["HangfireJobs\nBackground enrichment"]
        end

        subgraph Repositories["Repositories Layer"]
            TrailRepo["TrailRepository\nCRUD + Вектори"]
            FavRepo["FavoritesRepository\nFavorites CRUD"]
            MsgRepo["MessageRepository\nЧат история"]
            PostRepo["PostRepository\nCommunity CRUD"]
            UserRepo["UserRepository\nASP.NET Identity"]
        end

        subgraph Middleware["Middleware / Cross-cutting"]
            AuthMiddle["JWT Auth Middleware"]
            RateLimit["Rate Limiting\nauth/assistant/enrich"]
            ETagMiddle["ETag Middleware"]
            ExceptionHandler["Global Exception Handler"]
        end
    end

    Frontend -->|"HTTP REST"| AuthCtrl
    Frontend -->|"HTTP REST"| TrailsCtrl
    Frontend -->|"HTTP REST + JWT"| FavCtrl
    Frontend -->|"HTTP REST + JWT"| AssistantCtrl
    Frontend -->|"HTTP REST + JWT"| CommunityCtrl

    AuthCtrl --> AuthSvc
    TrailsCtrl --> TrailSvc
    FavCtrl --> FavRepo
    AssistantCtrl --> AssistantSvc
    CommunityCtrl --> PostRepo

    AssistantSvc --> SafetySvc
    AssistantSvc --> AssemblySvc
    AssistantSvc --> RetrievalSvc
    AssistantSvc --> ComposeSvc
    AssemblySvc --> WeatherSvc

    TrailSvc --> TrailRepo
    AuthSvc --> UserRepo
    AssistantSvc --> MsgRepo

    TrailRepo -->|"EF Core"| DB
    FavRepo -->|"EF Core"| DB
    MsgRepo -->|"EF Core"| DB
    PostRepo -->|"EF Core"| DB
    UserRepo -->|"EF Core"| DB

    TrailSvc -->|"fs.Read"| EcoJson
    RetrievalSvc -->|"fs.Read"| EcoJson

    AssistantSvc -->|"Gemini REST"| Gemini
    AssistantSvc -->|"Fallback OpenAI REST"| OpenAI
    WeatherSvc -->|"Open-Meteo REST"| Weather
    HangfireSvc -->|"Периодично обогатяване"| Gemini

    style Controllers fill:#dae8fc,stroke:#6c8ebf
    style Services fill:#d5e8d4,stroke:#82b366
    style Repositories fill:#fff2cc,stroke:#d6b656
    style Middleware fill:#f8cecc,stroke:#b85450
```

## Описание

**Тип:** C4 Model – Level 3 (Component Diagram) – Backend API Application

| Слой | Компонент | Отговорност |
|------|-----------|-------------|
| Controllers | AuthController | Регистрация, вход, JWT refresh |
| Controllers | TrailsController | Списък, търсене, ETag кеш |
| Controllers | AssistantController | AI чат с rate limiting |
| Controllers | FavoritesController | Управление на любими маршрути |
| Services | AssistantService | Orchestration: Safety → Assembly → Model → Compose |
| Services | RetrievalService | RAG: семантично търсене в eco.json |
| Services | PromptAssemblyService | Съставя контекст: маршрути + времето + история |
| Services | SafetyService | Блокира опасни/нерелевантни промпти |
| Repositories | TrailRepository | Достъп до SQL + векторни полета |
| Middleware | Rate Limiting | Token bucket: 30 req/min за assistant |
