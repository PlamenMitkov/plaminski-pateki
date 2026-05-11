# Диаграма: Архитектура Frontend–Backend (Обзор)

Обхват: Структурна обзорна диаграма на комуникационните канали между слоевете на приложението.
Файл: `02-frontend-backend-diagram.md` — Mermaid source за draw.io import.

```mermaid
flowchart LR
    subgraph Клиент["Клиентски слой"]
        U["Потребител (Браузър)"]
        FE["EcoTrails.Client\nReact + TypeScript + Vite\n(PWA / Service Worker)"]
    end

    subgraph API["EcoTrails.Api — ASP.NET Core .NET 8"]
        AUTH["Auth + JWT\n(ASP.NET Identity)"]
        TRAILS["Trails Endpoints\n+ ETag Cache\n+ Offline Enrichment"]
        FAVS["Favorites Endpoints\n(Transactional Sync)"]
        ASSIST["Assistant Endpoints\n(Rate: 30 tokens/min)"]
        COMMUNITY["Community Posts\n(Multipart Upload)"]
        ADMIN["Admin Panel Endpoints\n(Role: AdminPanel)"]
        QUALITY["Data Quality Endpoints\n(Role: Admin)"]
        HANGFIRE["Hangfire\n(Background Jobs)"]
    end

    subgraph AI["AI доставчици"]
        GEMINI["Gemini Flash API\n(основен модел)"]
        OPENAI["OpenAI API\n(резервен модел)"]
    end

    subgraph External["Външни услуги"]
        GEO["OpenRouteService\n(Геокодиране)"]
        WEATHER["Open-Meteo API\n(Метеорологични данни)"]
    end

    subgraph Data["Слой за данни"]
        EF["EF Core + AppDbContext"]
        SQL[("SQL Server\n(Trails, Users, Sessions,\nCommunity, Vectors)")]
        PG[("PostgreSQL\n(eco.json импорт\nчрез Prisma)")]
    end

    U --> FE
    FE -->|"JWT Bearer"| AUTH
    FE --> TRAILS
    FE --> FAVS
    FE -->|"JWT Bearer"| ASSIST
    FE -->|"JWT Bearer"| COMMUNITY
    FE -->|"Role: Admin"| ADMIN

    ASSIST -->|"основен"| GEMINI
    ASSIST -->|"fallback"| OPENAI
    ASSIST --> WEATHER
    TRAILS --> GEO
    TRAILS --> HANGFIRE
    COMMUNITY --> QUALITY
    ADMIN --> QUALITY

    API --> EF
    EF --> SQL

    SCRIPTS["Prisma Import Scripts"] --> PG

    classDef client fill:#e8f2ff,stroke:#2a4b8d,color:#0f1f3d
    classDef apibox fill:#fff4df,stroke:#a66a00,color:#4a2f00
    classDef aibox fill:#f0fff0,stroke:#2f7d32,color:#103816
    classDef extbox fill:#fff0f5,stroke:#8b0057,color:#3d0020
    classDef databox fill:#eaf7ea,stroke:#2f7d32,color:#103816
    class U,FE client
    class AUTH,TRAILS,FAVS,ASSIST,COMMUNITY,ADMIN,QUALITY,HANGFIRE apibox
    class GEMINI,OPENAI aibox
    class GEO,WEATHER extbox
    class EF,SQL,PG,SCRIPTS databox
```
