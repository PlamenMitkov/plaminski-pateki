# Sequence Diagram: Търсене на пътеки с ETag кеш

Обхват: Сценарий „Клиентът търси пътеки; при cache hit се връща 304; при miss се зарежда от БД".  
Alt-ветви: ETag съвпада (304 Not Modified), cache hit (без DB заявка), cache miss (DB + cache write).  
Файл: `07-sequence-trail-search-etag.md` — Mermaid source за draw.io import.

```mermaid
sequenceDiagram
    autonumber
    actor U as Клиент
    participant C as TrailsController
    participant MC as IMemoryCache
    participant R as TrailRepository
    participant DB as AppDbContext (SQL Server)

    U->>+C: GET /api/trails?search=витоша&difficulty=2&page=1&pageSize=10

    C->>C: buildCacheKey(queryParams + version)
    C->>C: buildETag(cacheKey)
    Note over C: ETag = SHA-256 хеш на cache ключа

    alt If-None-Match съвпада с ETag
        C-->>-U: 304 Not Modified
    else If-None-Match не съвпада
        C->>+MC: tryGetValue(cacheKey)
        alt Cache hit
            MC-->>C: PagedResponse~Trail~
            C-->>-U: 200 OK [X-Total-Count, ETag, Cache-Control: public]
        else Cache miss
            MC-->>C: null (cache miss)

            C->>+R: getPagedTrailsAsync(TrailQueryParameters)
            activate R
            R->>R: buildFilteredQuery(search, difficulty, coords, duration, elevation)
            R->>+DB: CountAsync() + Skip/Take
            DB-->>-R: totalCount, Trail[]
            R-->>C: PagedResponse { Items, TotalCount, Page, PageSize }
            deactivate R

            C->>+MC: set(cacheKey, result, TTL: 10 min)
            MC-->>C: OK

            C-->>-U: 200 OK { items[], totalCount } [ETag, X-Total-Count, Cache-Control: public]
        end
    end
```
