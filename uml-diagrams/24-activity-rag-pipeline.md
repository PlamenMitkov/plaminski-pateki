# 24 – Activity Diagram: RAG Pipeline (Retrieval-Augmented Generation)

```mermaid
flowchart TD
    Start(["Потребителски въпрос\nполучен от AssistantController"])

    A1["Нормализиране на въпрос\n(lowercase, trim)"]
    A2["Генериране на embedding\n(text-embedding-ada-002)"]
    A3["Cosine Similarity Search\nспрямо eco.json embeddings"]
    A4{{"Score ≥ 0.75?"}}
    A5["Вземане на Top-5\nсемантично съвпадащи маршрути"]
    A6["Text Match Fallback\n(keyword search в name/tags/region)"]
    A7{{"Намерени резултати?"}}
    A8["Вземане на Top-3\nпо текстово съвпадение"]
    A9["Съставяне на Retrieval Context\n(маршрути + GPS + описания)"]
    A10["Добавяне на метеорология\n(Open-Meteo за маршрутните координати)"]
    A11["Изграждане на System Prompt\nс инжектиран контекст"]
    A12["Запазване на контекст\nв AssistantMessages (history)"]
    A13["→ Изпращане към AI модел\n(Процес 3.3)"]
    NoResults["Отговор без контекст:\n'Не намерих релевантни маршрути'"]

    Start --> A1
    A1 --> A2
    A2 --> A3
    A3 --> A4
    A4 -->|"Да"| A5
    A4 -->|"Не"| A6
    A5 --> A9
    A6 --> A7
    A7 -->|"Да"| A8
    A7 -->|"Не"| NoResults
    A8 --> A9
    A9 --> A10
    A10 --> A11
    A11 --> A12
    A12 --> A13

    style A2 fill:#dae8fc
    style A3 fill:#dae8fc
    style A5 fill:#d5e8d4
    style A9 fill:#d5e8d4
    style A11 fill:#fff2cc
    style NoResults fill:#f8cecc
```

## Описание

**Тип:** Activity Diagram – RAG (Retrieval-Augmented Generation) Pipeline

| Стъпка | Компонент | Технология |
|--------|-----------|-----------|
| Embedding generation | `RetrievalService` | text-embedding-ada-002 (или локален модел) |
| Cosine similarity search | `TrailSearchTextMatcher` | Dot product / normalized vectors |
| Text fallback | `TrailSearchTextMatcher` | LINQ full-text search върху eco.json |
| Context assembly | `PromptAssemblyService` | StringBuilder с структуриран контекст |
| Weather injection | `WeatherService` | Open-Meteo REST за GPS координати |
| History persistence | `MessageRepository` | EF Core → SQL Server |

**Threshold:** Score 0.75 → висока семантична релевантност; под 0.75 се прилага text fallback.
