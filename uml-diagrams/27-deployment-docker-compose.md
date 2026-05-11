# 27 – Deployment Diagram: Docker Compose инфраструктура

```mermaid
graph TB
    subgraph ClientDevice["💻 Потребителско устройство"]
        Browser["🌐 Web Browser\n(Chrome, Safari, Firefox)"]
    end

    subgraph DockerHost["🖥️ Сървърна среда (Docker Host)"]

        subgraph DockerNet["Docker Compose Network (eco-network)"]

            subgraph FrontendContainer["Frontend Container"]
                Vite["Nginx / Vite Preview\nPort: 80 (prod) / 3000 (dev)\nImage: node:20-alpine"]
            end

            subgraph BackendContainer["Backend Container"]
                Kestrel[".NET Kestrel Server\nPort: 8080 / 443\nImage: mcr.microsoft.com/dotnet/aspnet:8.0"]
            end

            subgraph DbContainer["Database Container"]
                MSSQL[("SQL Server 2022\nPort: 1433\nImage: mcr.microsoft.com/mssql/server:2022")]
            end
        end

        subgraph Volumes["Docker Volumes"]
            SqlVol["sqlserver_data\n(SQL Data Persistence)"]
            EcoVol["./eco.json\n(Bind Mount → /app/eco.json)"]
        end
    end

    subgraph ExternalAPIs["☁️ Външни API услуги"]
        GeminiAPI["Gemini API\ngenerativelanguage.googleapis.com"]
        OpenAIAPI["OpenAI API\napi.openai.com"]
        WeatherAPI["Open-Meteo API\napi.open-meteo.com"]
    end

    Browser -->|"HTTP/HTTPS :80/:443"| FrontendContainer
    Browser -->|"REST API :8080"| BackendContainer
    Vite -->|"Internal proxy /api/*"| Kestrel
    Kestrel -->|"TCP 1433\nEF Core"| MSSQL

    MSSQL -.->|"Persists to"| SqlVol
    Kestrel -.->|"Reads static data"| EcoVol

    Kestrel -->|"HTTPS 443"| GeminiAPI
    Kestrel -->|"HTTPS 443"| OpenAIAPI
    Kestrel -->|"HTTPS 443"| WeatherAPI

    style FrontendContainer fill:#dae8fc,stroke:#6c8ebf
    style BackendContainer fill:#d5e8d4,stroke:#82b366
    style DbContainer fill:#fff2cc,stroke:#d6b656
    style Volumes fill:#f5f5f5,stroke:#666
    style ExternalAPIs fill:#f8cecc,stroke:#b85450
```

## Описание

**Тип:** Deployment Diagram – Docker Compose инфраструктура

| Контейнер | Image | Port | Volumes |
|-----------|-------|------|---------|
| frontend | node:20-alpine / nginx:alpine | 80, 3000 | – |
| backend | mcr.../aspnet:8.0 | 8080, 443 | eco.json bind mount |
| db | mcr.../mssql/server:2022 | 1433 | sqlserver_data volume |

**Docker Compose файлове:**
- `docker-compose.yml` – Development среда
- `docker-compose.prod.yml` – Production с HTTPS и оптимизации

**Мрежа:** `eco-network` (bridge) – контейнерите комуникират по hostname (frontend, backend, db)
