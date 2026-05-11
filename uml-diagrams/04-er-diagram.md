# ER Diagram (Prisma PostgreSQL)

```mermaid
erDiagram
    TRAIL {
        int id PK
        int source_trail_id UK
        string name
        string description
        string short_summary
        string source_url
        string photo_url
        datetime ingested_at
    }

    TRAIL_LOCATION {
        int id PK
        int trail_id FK UK
        string region
        string nearest_town
        float latitude
        float longitude
    }

    TRAIL_DETAILS {
        int id PK
        int trail_id FK UK
        float length_km
        string duration_text
        string difficulty_text
        string route_type
        string established_year
    }

    TRAIL_TRANSPORTATION {
        int id PK
        int trail_id FK UK
        string public_transport
        boolean parking_available
    }

    TRAIL_ACCESSIBILITY {
        int id PK
        int trail_id FK UK
        boolean wheelchair_accessible
        boolean stroller_friendly
        boolean bicycle_allowed
    }

    TRAIL_SEASONAL_INFO {
        int id PK
        int trail_id FK UK
        string[] best_months
        boolean winter_accessible
        boolean weather_dependent
    }

    TRAIL_CONTACT_INFO {
        int id PK
        int trail_id FK UK
        string phone
        string email
        string website
    }

    TRAIL_RATING {
        int id PK
        int trail_id FK UK
        float average_score
        int total_reviews
        datetime last_updated
    }

    TRAIL_METADATA {
        int id PK
        int trail_id FK UK
        datetime last_verified
        string data_source
        string status
    }

    TRAIL_ATTRACTION {
        int id PK
        int trail_id FK
        string name
        string type
    }

    TRAIL ||--|| TRAIL_LOCATION : has
    TRAIL ||--|| TRAIL_DETAILS : has
    TRAIL ||--|| TRAIL_TRANSPORTATION : has
    TRAIL ||--|| TRAIL_ACCESSIBILITY : has
    TRAIL ||--|| TRAIL_SEASONAL_INFO : has
    TRAIL ||--|| TRAIL_CONTACT_INFO : has
    TRAIL ||--|| TRAIL_RATING : has
    TRAIL ||--|| TRAIL_METADATA : has
    TRAIL ||--o{ TRAIL_ATTRACTION : includes
```
