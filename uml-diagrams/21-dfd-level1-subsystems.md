# 21 – DFD Level 1: Основни подсистеми

```mermaid
graph TB
    User(["👤 Потребител"])
    AI(["🤖 AI Доставчици"])
    SQLDB[("🗄️ SQL Server\nБаза данни")]
    EcoFile[("📄 eco.json\nМаршрутни данни")]

    subgraph System["EcoProject – Ниво 1 Декомпозиция"]
        P1["1.0\nАутентикация\nи Профил"]
        P2["2.0\nИзвличане\nна маршрути"]
        P3["3.0\nAI Оркестрация\nи Отговор"]
        P4["4.0\nОбщност\nи Публикации"]
    end

    User -->|"Данни за вход/регистрация"| P1
    P1 -->|"JWT токен, профил"| User
    P1 <-->|"Четене/Запис потребители"| SQLDB

    User -->|"Заявка за маршрути/търсене"| P2
    P2 -->|"Резултати: маршрути, карта"| User
    P2 <-->|"Четене маршрути"| EcoFile
    P2 <-->|"Любими маршрути"| SQLDB

    User -->|"Чат съобщение"| P3
    P3 -->|"AI отговор + Citations"| User
    P3 <-->|"Чат история"| SQLDB
    P3 <-->|"Извличане на контекст"| EcoFile
    P3 <-->|"AI промпт / отговор"| AI

    User -->|"Публикация / коментар"| P4
    P4 -->|"Одобрено съдържание"| User
    P4 <-->|"Community Posts"| SQLDB

    style P1 fill:#dae8fc,stroke:#6c8ebf
    style P2 fill:#d5e8d4,stroke:#82b366
    style P3 fill:#fff2cc,stroke:#d6b656
    style P4 fill:#f8cecc,stroke:#b85450
```

## Описание

**Тип:** DFD Level 1 – Основни подсистеми

| Процес | Описание | Входни данни | Изходни данни |
|--------|----------|-------------|--------------|
| 1.0 Аутентикация | JWT auth, ASP.NET Identity | Credentials | JWT + Refresh токен |
| 2.0 Извличане | Маршрути, търсене, карта | Search params | Trail списък, GeoJSON |
| 3.0 AI Оркестрация | Safety → RAG → LLM → Format | Чат съобщение | AI отговор + Citations |
| 4.0 Общност | Community Posts, одобрение | Публикация | Одобрено съдържание |

**Хранилища:**
- SQL Server: Потребители, Favorites, Messages, Posts
- eco.json: 322 маршрута (статичен файл, чете се от процеси 2.0 и 3.0)
