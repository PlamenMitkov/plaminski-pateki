# 16 – C4 Level 2: Диаграма на контейнерите

```mermaid
graph TB
    User["👤 Потребител\n[Person]"]

    subgraph EcoProject["EcoProject Platform [Software System]"]
        Frontend["📱 Frontend Application\n[React 18, TypeScript, Vite]\n\nSPA + PWA с Service Worker\nLeaflet карта, Schema.org JSON-LD"]
        Backend["⚙️ Backend API Service\n[.NET 8, C#, ASP.NET Core]\n\nREST API, JWT Auth, EF Core\nHangfire, Rate Limiting"]
        DB["🗄️ User Database\n[MS SQL Server]\n\nПотребители, Favorites,\nCommunity Posts, Embeddings"]
        EcoJson["📄 Eco Trails Data\n[eco.json, static file]\n\n322 маршрута с\nGeoJSON координати"]
    end

    Gemini["☁️ Gemini API\n[External System]"]
    OpenAI["☁️ OpenAI API\n[External System]"]
    Weather["☁️ Open-Meteo API\n[External System]"]

    User -->|"HTTPS / SPA навигация"| Frontend
    User -->|"REST API заявки (JWT)"| Backend
    Frontend -->|"HTTP/REST API вътрешен routing"| Backend
    Backend -->|"EF Core / ADO.NET\nTCP 1433"| DB
    Backend -->|"Чете статични данни\nfs.ReadAllText"| EcoJson
    Backend -->|"Обогатени промптове\n(Gemini Flash)"| Gemini
    Gemini -->|"AI отговор / грешка"| Backend
    Backend -->|"Fallback заявки\n(gpt-4o-mini)"| OpenAI
    OpenAI -->|"Резервен AI отговор"| Backend
    Backend -->|"Прогноза за времето\nпо GPS координати"| Weather
    Weather -->|"Метеорологични данни"| Backend

    style User fill:#08427b,color:#fff
    style Frontend fill:#438dd5,color:#fff
    style Backend fill:#438dd5,color:#fff
    style DB fill:#438dd5,color:#fff
    style EcoJson fill:#438dd5,color:#fff
    style Gemini fill:#999,color:#fff
    style OpenAI fill:#999,color:#fff
    style Weather fill:#999,color:#fff
```

## Описание

**Тип:** C4 Model – Level 2 (Container Diagram)

| Контейнер | Технология | Порт | Описание |
|-----------|-----------|------|----------|
| Frontend | React 18 + TS + Vite | 3000 (dev) / 80 (prod) | SPA с PWA, офлайн кеш, карта |
| Backend API | .NET 8 Kestrel | 8080 / 443 | REST API, Hangfire background jobs |
| User Database | MS SQL Server | 1433 | EF Core Code-First миграции |
| Eco Trails Data | JSON file (eco.json) | – | 322 статични маршрута |
| Gemini API | Google AI REST/gRPC | 443 | Първичен LLM (Gemini 2.0 Flash) |
| OpenAI API | OpenAI REST | 443 | Fallback LLM (gpt-4o-mini) |
| Open-Meteo API | REST | 443 | Безплатна прогноза за времето |
