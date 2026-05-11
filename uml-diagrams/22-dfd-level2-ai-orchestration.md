# 22 – DFD Level 2: Декомпозиция на Процес 3.0 (AI Оркестрация)

```mermaid
graph TB
    User(["👤 Потребител"])
    P2(["2.0 Извличане\n(Retrieval)"])
    AI(["🤖 AI Доставчици"])

    subgraph P3["Процес 3.0 – AI Оркестрация (декомпозиция)"]
        P31["3.1\nПроверка за\nСигурност (Safety)"]
        P32["3.2\nСъставяне на\nКонтекст (Assembly)"]
        P33["3.3\nМоделна Екзекуция\nи Fallback"]
        P34["3.4\nФорматиране на\nОтговора (Composition)"]
    end

    User -->|"Чат съобщение (Въпрос)"| P31
    P31 -->|"Валидиран (безопасен) въпрос"| P32
    P32 -->|"Заявка за данни"| P2
    P2 -->|"Маршрути и локално време"| P32
    P32 -->|"Обогатен Промпт\n(Контекст + Въпрос)"| P33

    P33 -->|"Изпращане към API"| AI
    AI -->|"AI Отговор / Грешка"| P33
    P33 -->|"Суров текст от успешен модел"| P34

    P34 -->|"Отговор със Schema.org метаданни"| User

    style P31 fill:#fff,stroke:#333
    style P32 fill:#fff,stroke:#333
    style P33 fill:#ffffd0,stroke:#d6b656
    style P34 fill:#fff,stroke:#333
    style P3 fill:#f5f5f5,stroke:#666,stroke-width:2px
```

## Описание

**Тип:** DFD Level 2 – Декомпозиция на Процес 3.0 (AI Оркестрация)

| Под-процес | Клас / Сервиз | Описание |
|-----------|---------------|----------|
| 3.1 Safety | `SafetyService` | Блокира опасни/нерелевантни промпти |
| 3.2 Assembly | `PromptAssemblyService` | Конструира обогатен промпт с контекст |
| 3.3 Execution | `AssistantService` | Извиква Gemini Flash → при грешка OpenAI |
| 3.4 Composition | `ResponseCompositionService` | Добавя Schema.org JSON-LD и citations |

**Ключови правила:**
- Процес 3.1 блокира при: заплахи, offensive съдържание, prompt injection
- Процес 3.2 включва: top-5 маршрути по cosine similarity + прогноза за времето
- Процес 3.3 прилага exponential backoff при HTTP 429/503 от Gemini
- Процес 3.4 форматира markdown + JSON-LD за Schema.org `HowToStep`
