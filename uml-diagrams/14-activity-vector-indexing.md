# Activity Diagram: Векторно индексиране за семантично търсене

Обхват: Сценарий „Администратор стартира пакетно индексиране на embeddings за пътеките; системата обработва на партиди с устойчивост при частични грешки".  
Alt-ветви: неавторизиран (401/403), надвишена квота (429), частична пакетна грешка (продължава), всички партиди неуспешни (връща Failed count).  
Файл: `14-activity-vector-indexing.md` — Mermaid source за draw.io import.

```mermaid
flowchart TD
    START([Начало]) --> AUTH_CHECK

    AUTH_CHECK{"[Authorize(Roles=Admin)]\n+ RateLimit: 3 req/5min"}
    AUTH_CHECK -- Неавторизиран --> ERR_401([401 / 403 Unauthorized])
    AUTH_CHECK -- Квота надвишена --> ERR_429([429 Too Many Requests])
    AUTH_CHECK -- OK --> LOAD_TRAILS

    LOAD_TRAILS["Зареждане на пътеки от DB\n(по trailIds? | limit, sort by Id)"]
    LOAD_TRAILS --> FILTER

    FILTER{"overwriteExisting = true?"}
    FILTER -- Не --> FILTER_PENDING["Филтриране: само пътеки\nбез EmbeddingVector"]
    FILTER -- Да --> SPLIT_BATCHES
    FILTER_PENDING --> SPLIT_BATCHES

    SPLIT_BATCHES{"Пътеки налични?"}
    SPLIT_BATCHES -- Не --> DONE_EMPTY(["200 OK { processed: 0, updated: 0 }"])
    SPLIT_BATCHES -- Да --> INIT_BATCH

    INIT_BATCH["Разделяне на партиди\n(batchSize: 1–50, clamped)"]
    INIT_BATCH --> PROCESS_BATCH

    subgraph BATCH["Обработка на партида"]
        PROCESS_BATCH["buildEmbeddingInput(trail.name, description, location)"]
        PROCESS_BATCH --> CALL_OPENAI["createEmbeddingsAsync(inputList)\nPOST OpenAI /v1/embeddings"]
        CALL_OPENAI --> EMB_OK{Успешен?}
        EMB_OK -- Да --> STORE["Запис: EmbeddingVector, EmbeddingModel,\nEmbeddingUpdatedAt"]
        STORE --> INC_UPDATED["response.Updated++"]
        EMB_OK -- Не --> LOG_FAIL["Логване на грешката\nresponse.Failed += missing"]
        INC_UPDATED --> NEXT_BATCH
        LOG_FAIL --> NEXT_BATCH
    end

    NEXT_BATCH{Още партиди?}
    NEXT_BATCH -- Да --> DELAY
    DELAY["Task.Delay(enrichDelayMs)\n(пауза между партиди)"]
    DELAY --> PROCESS_BATCH
    NEXT_BATCH -- Не --> SAVE_DB

    SAVE_DB["saveChangesAsync()\n(всички embeddings в един commit)"]
    SAVE_DB --> DONE(["202 Accepted\n{ processed, updated, failed, errors[] }"])

    style ERR_401 fill:#ffeded,stroke:#c0392b
    style ERR_429 fill:#ffeded,stroke:#c0392b
    style DONE fill:#e8f2ff,stroke:#2a4b8d
    style LOG_FAIL fill:#fff4df,stroke:#a66a00
```
