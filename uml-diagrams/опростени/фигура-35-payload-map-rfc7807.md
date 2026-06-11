# Фигура 35: Payload Map (JSON DTO договори и RFC 7807)

Комуникационна карта на API формати и пример за стандартна грешка.

```mermaid
flowchart LR
    subgraph Client["PWA Client"]
        A["Auth DTO"]
        T["Trail Query DTO"]
        M["Assistant Message DTO"]
        F["Favorite DTO"]
    end

    subgraph Api["EcoTrails API"]
        A1["200 Auth Tokens"]
        T1["200 Trails / 304 Not Modified"]
        M1["200 AI Response DTO"]
        E1["4xx/5xx ProblemDetails RFC 7807"]
    end

    A --> A1
    T --> T1
    M --> M1
    M --> E1
    F --> E1
```

Пример за RFC 7807 системна грешка:

```json
{
  "type": "https://ecotrails.bg/errors/ai-provider-unavailable",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "Основната когнитивна услуга (Gemini API) не отговори в рамките на дефинирания timeout. Задействана е fallback политика.",
  "instance": "/api/v1/assistant/session/3f8a/message"
}
```
