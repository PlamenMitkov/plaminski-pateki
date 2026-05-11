# 19 – ER Диаграма: Хибриден модел (SQL Server + eco.json)

```mermaid
erDiagram
    AspNetUsers {
        string Id PK
        string UserName
        string Email
        string PasswordHash
        string NormalizedEmail
        bool EmailConfirmed
        string RefreshToken
        datetime RefreshTokenExpiry
    }

    AspNetRoles {
        string Id PK
        string Name
        string NormalizedName
    }

    AspNetUserRoles {
        string UserId FK
        string RoleId FK
    }

    Favorites {
        int Id PK
        string UserId FK
        int TrailId
        string TrailName
        datetime CreatedAt
    }

    CommunityPosts {
        int Id PK
        string UserId FK
        string Title
        string Content
        string TrailName
        datetime CreatedAt
        datetime UpdatedAt
        bool IsApproved
        string ApprovedByUserId FK
    }

    AssistantMessages {
        int Id PK
        string UserId FK
        string Role
        string Content
        string SourceCitation
        string ModelUsed
        datetime CreatedAt
    }

    TrailProposals {
        int Id PK
        string UserId FK
        string Name
        string Description
        string Location
        string Difficulty
        string Status
        datetime CreatedAt
    }

    EcoJsonTrail {
        int id PK
        string name
        string region
        string difficulty
        float length_km
        int duration_min
        string description
        string nearestTown
        string source
        float lat
        float lng
        string[] tags
        float[] embedding
    }

    AspNetUsers ||--o{ AspNetUserRoles : "има роли"
    AspNetRoles ||--o{ AspNetUserRoles : "назначена на"
    AspNetUsers ||--o{ Favorites : "запазва"
    AspNetUsers ||--o{ CommunityPosts : "публикува"
    AspNetUsers ||--o{ AssistantMessages : "изпраща"
    AspNetUsers ||--o{ TrailProposals : "предлага"
    CommunityPosts }o--|| AspNetUsers : "одобрена от"
    Favorites }|..|| EcoJsonTrail : "референцира (TrailId)"
    AssistantMessages }|..o| EcoJsonTrail : "цитира маршрут"
```

## Описание

**Тип:** ER Диаграма – Хибриден модел

### Хибридна архитектура на данните

| Хранилище | Технология | Данни |
|-----------|-----------|-------|
| SQL Server (EF Core) | Релационна БД | Потребители, Favorites, Posts, Messages |
| eco.json (статичен файл) | JSON (322 записа) | Маршрути с GPS, описания, тагове, embeddings |

### Ключови особености
- `Favorites.TrailId` → референцира `id` от `eco.json` (не Foreign Key в SQL)
- `AssistantMessages.SourceCitation` → JSON низ с имена на маршрути от eco.json
- `EcoJsonTrail.embedding` → float[1536] вектор за семантично търсене (cosine similarity)
- ASP.NET Core Identity управлява `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`
- `CommunityPosts.IsApproved` + `ApprovedByUserId` → workflow за одобрение от Admin
