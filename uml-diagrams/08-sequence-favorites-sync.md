# Sequence Diagram: Синхронизация на любими пътеки (транзакция)

Обхват: Сценарий „Автентикиран потребител синхронизира своя списък с любими".  
Alt-ветви: неавторизиран (401), транзакционен rollback при грешка в БД.  
Файл: `08-sequence-favorites-sync.md` — Mermaid source за draw.io import.

```mermaid
sequenceDiagram
    autonumber
    actor U as Потребител
    participant C as FavoritesController
    participant R as FavoritesRepository
    participant DB as AppDbContext
    participant TX as Database Transaction

    U->>+C: POST /api/favorites/sync { trailIds: [1, 7, 42] }
    Note over C: [Authorize] — извличане на userId от JWT claims

    alt userId не е намерен
        C-->>U: 401 Unauthorized
    end

    C->>+R: syncFavoritesAsync(userId, trailIds)

    R->>+DB: Trails.where(id IN trailIds).select(id)
    DB-->>-R: validTrailIds: int[]
    Note over R: Невалидни ID-та се отхвърлят мълчаливо

    R->>TX: beginTransactionAsync()

    R->>+DB: UserFavoriteTrails.where(userId).executeDeleteAsync()
    DB-->>-R: deletedCount: int

    R->>+DB: UserFavoriteTrails.addRangeAsync(validTrailIds)
    DB-->>-R: OK

    R->>+DB: saveChangesAsync()
    DB-->>-R: OK

    alt Грешка при запис
        R->>TX: rollbackAsync()
        TX-->>R: rolled back
        R-->>C: Exception
        C-->>U: 500 Internal Server Error
    else Успешен запис
        R->>TX: commitAsync()
        TX-->>R: committed

        R-->>C: syncedIds: int[] (наредени)
        C-->>-U: 200 OK { syncedTrailIds: [1, 7, 42] }
    end
```
