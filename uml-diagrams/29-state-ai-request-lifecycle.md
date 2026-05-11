# 29 – State Diagram: Жизнен цикъл на AI заявка

```mermaid
stateDiagram-v2
    [*] --> Получена : POST /api/assistant/chat

    Получена --> ПроверкаЛимит : rate limit check
    ПроверкаЛимит --> Отхвърлена429 : лимит надвишен
    ПроверкаЛимит --> ПроверкаБезопасност : лимит ОК

    Отхвърлена429 --> [*]

    ПроверкаБезопасност --> Блокирана400 : небезопасен промпт
    ПроверкаБезопасност --> Извличане : безопасен промпт

    Блокирана400 --> [*]

    Извличане --> СъставянеПромпт : контекст готов
    Извличане --> БезКонтекст : не намерен контекст

    БезКонтекст --> СъставянеПромпт : general prompt

    СъставянеПромпт --> ИзпращанеGemini : промпт готов

    ИзпращанеGemini --> УспешенОтговор : HTTP 200
    ИзпращанеGemini --> Retry : HTTP 429/5xx (опит 1)
    Retry --> ИзпращанеGemini : backoff 1s/2s/4s
    Retry --> FallbackOpenAI : изчерпан retry (>3)

    FallbackOpenAI --> УспешенОтговор : HTTP 200
    FallbackOpenAI --> НеуспешнаЗаявка503 : OpenAI грешка

    НеуспешнаЗаявка503 --> [*]

    УспешенОтговор --> Форматиране : суров текст
    Форматиране --> Записване : Schema.org + markdown
    Записване --> Изпратена200 : SQL записан

    Изпратена200 --> [*]

    note right of Извличане
        Паралелно:
        - RAG embedding search
        - Weather API
        - Chat history
    end note

    note right of Retry
        Polly AiProviderFallbackPolicy:
        Retry x3 + Circuit Breaker
    end note
```

## Описание

**Тип:** State Diagram – Жизнен цикъл на AI заявка

| Състояние | Описание | Следващо |
|-----------|----------|----------|
| Получена | HTTP заявка пристигна | Rate limit check |
| Отхвърлена429 | Rate limit надвишен | [*] → 429 |
| Блокирана400 | Safety violation | [*] → 400 |
| Извличане | RAG паралелно извличане | Съставяне |
| ИзпращанеGemini | HTTP POST към Gemini | Успех / Retry |
| Retry | Exponential backoff (Polly) | Gemini retry / Fallback |
| FallbackOpenAI | OpenAI gpt-4o-mini | Успех / 503 |
| Форматиране | ResponseCompositionService | Записване |
| Изпратена200 | HTTP 200 с отговор | [*] |
