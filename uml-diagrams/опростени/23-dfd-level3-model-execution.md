# 23 – DFD Level 3: Декомпозиция на Процес 3.3 (Моделна Екзекуция и Fallback)

```mermaid
flowchart TD
    Start(["Обогатен Промпт\n(от Процес 3.2)"])

    P331["3.3.1\nИзбор на Модел\n(GeminiService)"]
    P332["3.3.2\nИзпращане към\nGemini Flash API"]
    P333{{"HTTP Статус?"}}
    P334["3.3.3\nПарсване и\nВалидация на Отговор"]
    P335["3.3.4\nFallback Решение\n(Backoff / OpenAI)"]
    P336{{"Retry Exhausted?<br/>max 3 пъти"}}
    P337["3.3.5\nИзпращане към\nOpenAI gpt-4o-mini"]
    P338{{"OpenAI Статус?"}}
    P339["⚠️ ServiceUnavailableException\n503 към клиента"]
    P340["Успешен AI отговор\n(суров текст)"]

    Start --> P331
    P331 --> P332
    P332 --> P333
    P333 -->|"200 OK"| P334
    P334 --> P340
    P333 -->|"429 / 5xx"| P335
    P335 --> P336
    P336 -->|"Не (опит < 3)"| P332
    P336 -->|"Да"| P337
    P337 --> P338
    P338 -->|"200 OK"| P334
    P338 -->|"Грешка"| P339

    P340 --> End(["→ Процес 3.4\nФорматиране"])
```

## Описание

**Тип:** DFD Level 3 – Декомпозиция на Процес 3.3 (Моделна Екзекуция)

| Под-процес | Описание |
|-----------|----------|
| 3.3.1 Избор на модел | Проверява кеш, избира Gemini Flash като primary |
| 3.3.2 Изпращане | HTTP POST към Gemini REST API с timeout 30s |
| 3.3.3 Парсване | Валидира response структурата, извлича text |
| 3.3.4 Fallback | Exponential backoff: 1s → 2s → 4s между retry |
| 3.3.5 OpenAI fallback | При изчерпан retry – gpt-4o-mini с адаптиран промпт |

**Backoff стратегия:** `AiProviderFallbackPolicy` (Polly) с:
- Retry count: 3
- Wait: 1s, 2s, 4s (exponential)
- Circuit breaker: 5 грешки за 30s → open circuit за 60s
