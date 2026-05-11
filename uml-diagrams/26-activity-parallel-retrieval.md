# 26 – Activity Diagram: Паралелно извличане (Fork/Join)

```mermaid
flowchart TD
    Start(["Валидиран въпрос\nот Safety Check"])

    Fork[["⑃ FORK\nПаралелни операции"]]

    subgraph Parallel["Паралелно изпълнение"]
        direction LR
        T1["Семантично търсене\n(Embedding + Cosine)\n~50-200ms"]
        T2["Прогноза за времето\n(Open-Meteo REST)\n~100-500ms"]
        T3["Зареждане на\nчат история\n(SQL query)\n~10-50ms"]
    end

    Join[["⑃ JOIN\nОбединяване на резултати"]]

    R1{{"Embedding score\n≥ 0.75?"}}
    R2["Top-5 маршрути\n(семантично)"]
    R3["Text fallback\n(keyword)"]

    Assembly["PromptAssemblyService\nСъставяне на обогатен промпт:\n• Маршрути (контекст)\n• Времето (локация)\n• Чат история (последни 10)"]

    End(["→ Изпращане към AI модел"])

    Start --> Fork
    Fork --> T1
    Fork --> T2
    Fork --> T3
    T1 --> Join
    T2 --> Join
    T3 --> Join
    Join --> R1
    R1 -->|"Да"| R2
    R1 -->|"Не"| R3
    R2 --> Assembly
    R3 --> Assembly
    Assembly --> End

    style Fork fill:#333,color:#fff,stroke:#333
    style Join fill:#333,color:#fff,stroke:#333
    style T1 fill:#dae8fc
    style T2 fill:#d5e8d4
    style T3 fill:#fff2cc
    style Assembly fill:#f5f5f5,stroke:#666
```

## Описание

**Тип:** Activity Diagram – Паралелно извличане с Fork/Join

**Паралелни операции:**

| Задача | Timeout | Зависимост |
|--------|---------|-----------|
| Embedding similarity search | 200ms | eco.json embeddings |
| Open-Meteo weather fetch | 500ms | GPS координати от eco.json |
| Chat history retrieval | 50ms | SQL Server (EF Core) |

**Fork/Join стратегия:**
- `Task.WhenAll()` в .NET за паралелност
- Ако Open-Meteo timeout → продължава без времето (graceful degradation)
- Ако embedding неуспешен → fallback към text search
- Join изчаква всички или max 800ms timeout
