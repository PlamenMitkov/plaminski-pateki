# 25 – Activity Diagram: AI Response (Пълен поток на AI отговор)

```mermaid
flowchart TD
    Start(["POST /api/assistant/chat\nJWT токен + съобщение"])

    R1["Rate Limit проверка\n(30 tokens/min token bucket)"]
    R1F{{"Лимит надвишен?"}}
    R1Err["429 Too Many Requests\n+ Retry-After header"]

    S1["Safety Check\n(SafetyService)"]
    S1F{{"Небезопасен промпт?"}}
    S1Err["400 Bad Request\n'Промптът е нарушение'"]

    C1["Зареждане на чат история\n(последни 10 съобщения)"]
    C2["RAG Retrieval\n(RetrievalService)"]
    C3["Weather Enrichment\n(WeatherService)"]
    C4["Prompt Assembly\n(PromptAssemblyService)"]

    M1["Изпращане към Gemini Flash\n(HTTP POST + timeout 30s)"]
    M2{{"HTTP 200?"}}
    M3["Fallback: Retry x3\n(exponential backoff)"]
    M4["Fallback: OpenAI\n(gpt-4o-mini)"]
    M5{{"OpenAI 200?"}}
    M6["503 Service Unavailable\n'AI временно недостъпен'"]

    F1["Response Composition\n(ResponseCompositionService)"]
    F2["Добавяне на Schema.org JSON-LD"]
    F3["Запазване в AssistantMessages"]
    F4["200 OK: AI отговор + Citations"]

    Start --> R1
    R1 --> R1F
    R1F -->|"Да"| R1Err
    R1F -->|"Не"| S1
    S1 --> S1F
    S1F -->|"Да"| S1Err
    S1F -->|"Не"| C1
    C1 --> C2
    C2 --> C3
    C3 --> C4
    C4 --> M1
    M1 --> M2
    M2 -->|"Да"| F1
    M2 -->|"Не (429/5xx)"| M3
    M3 --> M4
    M4 --> M5
    M5 -->|"Да"| F1
    M5 -->|"Не"| M6
    F1 --> F2
    F2 --> F3
    F3 --> F4
```

## Описание

**Тип:** Activity Diagram – Пълен поток на AI заявка

**Компоненти по ред на изпълнение:**

1. **Rate Limiting** – Token bucket: 30 req/min, window 1 min (`AssistantRateLimitMiddleware`)
2. **Safety Check** – Regex + AI safety prompt; блокира: injection, violence, OOD теми
3. **Retrieval** – RAG pipeline: embedding → cosine similarity → top-5 маршрути
4. **Assembly** – Структуриран system prompt с контекст, история, времето
5. **Model Execution** – Gemini Flash primary; Polly retry x3; OpenAI fallback
6. **Composition** – Markdown формат + Schema.org HowToStep JSON-LD
7. **Persistence** – Всяко съобщение (user + assistant) записано в SQL Server
