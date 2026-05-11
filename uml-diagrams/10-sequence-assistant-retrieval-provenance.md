# Sequence Diagram: Извличане на контекст и верификация на произход (RAG + Provenance)

Обхват: Сценарий „Системата извлича релевантни пътеки чрез хибридно търсене, проверява произхода на данните и добавя метеорологичен контекст".  
Alt-ветви: липса на embeddings (graceful degrade), карантинен домейн (изключване), недостъпно метео API (null fallback).  
Файл: `10-sequence-assistant-retrieval-provenance.md` — Mermaid source за draw.io import.

```mermaid
sequenceDiagram
    autonumber
    participant S as AssistantService
    participant RS as AssistantRetrievalService
    participant PP as AssistantProvenancePolicyService
    participant WS as AssistantWeatherContextService
    participant VS as IVectorService
    participant DB as AppDbContext (SQL Server)
    participant GEO as Geocoding API
    participant WA as Open-Meteo API

    Note over S: Входни данни: sanitizedMessage, userId, history

    %% 1. Хибридно векторно + пълнотекстово търсене
    S->>+RS: findRelevantTrailsAsync(sanitizedMessage, request)
    RS->>+VS: createEmbeddingAsync(sanitizedMessage)
    VS-->>-RS: queryVector: float[]
    RS->>+DB: SELECT trails ORDER BY vector_similarity DESC LIMIT 10
    Note over DB: Хибридно: векторна прилика + BM25 текстово съвпадение
    DB-->>-RS: Trail[] (ранкирани)
    alt Няма trails с embeddings
        RS-->>S: [] (празен списък — graceful degrade)
    end
    RS-->>-S: relevantTrails: AssistantTrailContext[]

    S->>+RS: getAlternativeTrailsAsync(sanitizedMessage, excludeIds)
    RS->>+DB: SELECT trails WHERE id NOT IN excludeIds ORDER BY similarity
    DB-->>-RS: Trail[]
    RS-->>-S: alternatives: AssistantTrailContext[]

    %% 2. Verifikация на произход
    S->>+PP: buildContextAsync(relevantTrails, alternatives)
    PP->>+DB: SELECT TrailEnrichmentSnapshots WHERE TrailId IN trailIds
    DB-->>-PP: TrailEnrichmentSnapshot[]
    loop За всяка пътека
        PP->>PP: checkDomain(snapshot.sourceUrl)
        alt Домейнът е в TrustedSourceDomainAllowList
            PP->>PP: markAsVerified(trail)
        else Домейнът е в QuarantinedSourceDomains
            PP->>PP: excludeFromContext(trail)
            Note over PP: Карантинираните източници се изключват
        else RequireVerifiedSourceForContext = false
            PP->>PP: includeWithWarning(trail)
        end
    end
    PP-->>-S: AssistantProvenanceContextResult { trails, alternatives, hasReliabilityWarning }

    %% 3. Метеорологичен контекст (само при нужда)
    S->>+WS: buildWeatherContextAsync(sanitizedMessage, trails)
    WS->>WS: detectWeatherIntent(message)
    Note over WS: Ключови думи: "времето", "температур", "дъжд", "сняг"
    alt Няма метеорологично намерение
        WS-->>S: null
    end
    WS->>WS: extractLocation(message, trails)
    WS->>+GEO: geocodeAsync(locationName)
    GEO-->>-WS: lat, lon
    alt GEO API недостъпно
        WS-->>S: null (graceful degrade)
    end
    WS->>+WA: getCurrentWeatherAsync(lat, lon)
    WA-->>-WS: WeatherData
    alt Weather API недостъпно
        WS-->>S: null (graceful degrade)
    end
    WS->>WS: formatWeatherContext(WeatherData)
    WS-->>-S: weatherContext: string

    Note over S: Продължава към сглобяване на промпта\n(вж. 11-activity-ai-fallback-orchestration.md)
```
