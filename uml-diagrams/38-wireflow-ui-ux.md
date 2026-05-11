# 38 – Wireflow Диаграма: UI/UX навигационен поток

```mermaid
flowchart TD
    HomePage["🏠 Начална страница\n─────────────\n[Списък маршрути]\n[Търсене]\n[Карта бутон]"]

    MapPage["🗺️ Карта\n─────────────\nLeaflet карта\nМаркери по региони\nКлик = детайл"]

    TrailDetail["🏔️ Детайл маршрут\n─────────────\nОписание + GPS\nТрудност, Дължина\n[Добави в любими]"]

    SearchResults["🔍 Резултати от търсене\n─────────────\nФилтър: регион/трудност\nСортиране по дължина"]

    LoginPage["🔐 Вход / Регистрация\n─────────────\nEmail + Парола\nJWT автоматично"]

    Dashboard["📊 Моят профил\n─────────────\nЛюбими маршрути\nПубликации\n[Изход]"]

    FavoritesPage["⭐ Любими\n─────────────\nСписък запазени\n[Премахни] [Карта]"]

    ChatPage["🤖 AI Асистент\n─────────────\nЧат интерфейс\nИстория на разговора\nCitations бутони"]

    CommunityPage["👥 Общност\n─────────────\nПостове за маршрути\n[Нова публикация]"]

    AdminPage["👑 Админ панел\n[Role: Admin only]\n─────────────\nОдобрение на предложения\nУправление потребители"]

    HomePage -->|"Карта бутон"| MapPage
    HomePage -->|"Търсене"| SearchResults
    HomePage -->|"Клик маршрут"| TrailDetail
    MapPage -->|"Клик маркер"| TrailDetail
    SearchResults -->|"Клик резултат"| TrailDetail

    TrailDetail -->|"Добави любими\n(изисква вход)"| LoginPage
    HomePage -->|"Вход бутон"| LoginPage
    LoginPage -->|"Успешен вход"| Dashboard

    Dashboard -->|"Любими"| FavoritesPage
    Dashboard -->|"AI Чат"| ChatPage
    Dashboard -->|"Общност"| CommunityPage
    Dashboard -.->|"[Admin only]"| AdminPage

    FavoritesPage -->|"Клик маршрут"| TrailDetail
    ChatPage -->|"Citation клик"| TrailDetail
    CommunityPage -->|"Клик маршрут"| TrailDetail

    style HomePage fill:#dae8fc,stroke:#6c8ebf
    style ChatPage fill:#d5e8d4,stroke:#82b366
    style AdminPage fill:#f8cecc,stroke:#b85450
    style LoginPage fill:#fff2cc,stroke:#d6b656
```

## Описание

**Тип:** Wireflow Diagram – UI/UX навигационен поток

| Страница | Достъп | Ключова функция |
|----------|--------|----------------|
| Начална страница | Публичен | Списък + търсене |
| Карта | Публичен | Географска визуализация |
| Детайл маршрут | Публичен | Пълна информация |
| Вход/Регистрация | Публичен | JWT auth |
| Моят профил | Auth required | Dashboard |
| Любими | Auth required | Запазени маршрути |
| AI Асистент | Auth required | Чат + RAG |
| Общност | Mixed | UGC posts |
| Админ панел | Role: Admin | Модерация |

**Ключови UX потоци:**
1. Анонимен → разглежда → харесва → регистрира се → добавя в любими
2. Регистриран → задава въпрос на AI → получава отговор → кликва citation → детайл маршрут
