# 34 – Архитектура на наблюдаемостта (Observability)

```mermaid
graph TB
    subgraph App["EcoProject Приложение"]
        Frontend_O["Frontend\n(React)"]
        Backend_O[".NET 8 API"]
        DB_O["SQL Server"]
    end

    subgraph Logging["📋 Logging Layer"]
        Serilog["Serilog\n(Structured Logging)"]
        ConsoleLog["Console Sink\n(Docker logs)"]
        FileLog["File Sink\n(logs/app-{date}.log)"]
        SeqLog["Seq / ELK Stack\n(опционално)"]
    end

    subgraph Metrics["📊 Metrics Layer"]
        HealthCheck["ASP.NET Health Checks\nGET /health\n+ DB connectivity\n+ AI provider ping"]
        CustomMetrics["Custom Metrics\n- Rate limit hits\n- AI fallback count\n- Response latency"]
        Prometheus["Prometheus\n(опционално)\n/metrics endpoint"]
    end

    subgraph Tracing["🔍 Tracing Layer"]
        ActivitySource["System.Diagnostics\nActivity Source\n(W3C Trace Context)"]
        OpenTelemetry["OpenTelemetry SDK\n(опционално)"]
        Jaeger["Jaeger / Zipkin\n(опционално)"]
    end

    subgraph Alerting["🔔 Alerting"]
        RateAlert["Rate Limit 429\nАлерт при висок брой"]
        FallbackAlert["AI Fallback\nАлерт при активиране\nна OpenAI fallback"]
        HealthAlert["Health Check Fail\nАлерт при неизправност"]
    end

    Backend_O --> Serilog
    Serilog --> ConsoleLog
    Serilog --> FileLog
    Serilog -.->|"optional"| SeqLog

    Backend_O --> HealthCheck
    Backend_O --> CustomMetrics
    CustomMetrics -.->|"optional"| Prometheus

    Backend_O --> ActivitySource
    ActivitySource -.->|"optional"| OpenTelemetry
    OpenTelemetry -.->|"optional"| Jaeger

    CustomMetrics --> RateAlert
    CustomMetrics --> FallbackAlert
    HealthCheck --> HealthAlert

    Frontend_O -->|"Console errors\nbrowser DevTools"| ConsoleLog

    style Logging fill:#dae8fc,stroke:#6c8ebf
    style Metrics fill:#d5e8d4,stroke:#82b366
    style Tracing fill:#fff2cc,stroke:#d6b656
    style Alerting fill:#f8cecc,stroke:#b85450
```

## Описание

**Тип:** Observability Architecture Diagram

| Аспект | Инструмент | Статус |
|--------|-----------|--------|
| Structured Logging | Serilog + Console/File sinks | ✅ Внедрено |
| Health Checks | ASP.NET Core Health Checks `/health` | ✅ Внедрено |
| Rate Limit Monitoring | Custom middleware counters | ✅ Внедрено |
| Metrics | Prometheus endpoint | ⚠️ Опционално |
| Distributed Tracing | OpenTelemetry + Jaeger | ⚠️ Опционално |
| Log Aggregation | Seq / ELK Stack | ⚠️ Опционално |

**Текущи log нива:** Error (prod) → Warning (staging) → Information (dev) → Debug (local)
