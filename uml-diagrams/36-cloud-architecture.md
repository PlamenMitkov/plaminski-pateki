# 36 – Cloud Architecture Диаграма

```mermaid
graph TB
    subgraph Internet["🌐 Internet"]
        User_C["👤 Потребители\n(Global)"]
    end

    subgraph CDN["☁️ CDN / Edge Layer"]
        CloudflareC["Cloudflare\n(DNS + DDoS Protection\n+ SSL Termination)"]
    end

    subgraph CloudProvider["☁️ Cloud Provider (Azure / AWS / VPS)"]

        subgraph LoadBalancer["Load Balancer / Reverse Proxy"]
            Nginx["Nginx / Traefik\n(Port 80/443)\nSSL + Rate Limiting"]
        end

        subgraph AppTier["Application Tier"]
            FrontendC["Frontend Container\n(React SPA / Nginx)\nPort: 80"]
            BackendC1["Backend API\nInstance 1\n.NET 8 / Port: 8080"]
            BackendC2["Backend API\nInstance 2\n(Horizontal Scaling)"]
        end

        subgraph DataTier["Data Tier"]
            SQLC["SQL Server\n(Primary)\nPort: 1433"]
            SQLReplica["SQL Server\n(Read Replica)\n(опционално)"]
            FileStorage["File Storage\neco.json\n(Azure Blob / S3)"]
        end

        subgraph BackgroundTier["Background Jobs"]
            HangfireC["Hangfire Server\n(Trail Enrichment)\nScheduled jobs"]
        end
    end

    subgraph ExternalServices["☁️ External Services (SaaS)"]
        GeminiC["Google Gemini API\n(generativelanguage.googleapis.com)"]
        OpenAIC["OpenAI API\n(api.openai.com)"]
        WeatherC["Open-Meteo API\n(api.open-meteo.com)"]
    end

    User_C -->|"HTTPS"| CloudflareC
    CloudflareC -->|"Reverse Proxy"| Nginx
    Nginx -->|"Static files"| FrontendC
    Nginx -->|"API routing /api/*"| BackendC1
    Nginx -->|"Load balanced"| BackendC2

    BackendC1 -->|"EF Core TCP 1433"| SQLC
    BackendC2 -->|"EF Core TCP 1433"| SQLC
    SQLC -.->|"Replication"| SQLReplica
    BackendC1 -->|"Read eco.json"| FileStorage
    HangfireC -->|"Background jobs"| SQLC
    HangfireC -->|"Enrichment calls"| GeminiC

    BackendC1 -->|"Gemini API"| GeminiC
    BackendC1 -->|"Fallback"| OpenAIC
    BackendC1 -->|"Weather"| WeatherC

    style AppTier fill:#dae8fc,stroke:#6c8ebf
    style DataTier fill:#fff2cc,stroke:#d6b656
    style BackgroundTier fill:#d5e8d4,stroke:#82b366
    style ExternalServices fill:#f8cecc,stroke:#b85450
    style CDN fill:#f3e5f5,stroke:#9C27B0
```

## Описание

**Тип:** Cloud Architecture Diagram

| Tier | Компонент | Мащабируемост |
|------|-----------|--------------|
| CDN/Edge | Cloudflare | Global edge network |
| Load Balancer | Nginx/Traefik | Horizontal scaling ready |
| Frontend | React SPA / Nginx | Stateless, CDN кешируем |
| Backend API | .NET 8 Kestrel | Horizontal (2+ instances) |
| Database | SQL Server Primary/Replica | Vertical + Read replicas |
| Background | Hangfire | Единствен scheduler node |
| External | Gemini, OpenAI, Open-Meteo | SaaS – няма управление |

**Deployment target:** Docker Compose (текущо) → Kubernetes (следваща фаза)
