# 18 – Use Case диаграма: EcoProject

```mermaid
graph LR
    Guest["👤 Нерегистриран\nпотребител (Гост)"]
    User["👤 Регистриран\nпотребител"]
    Admin["👤 Администратор"]
    AI["🤖 AI Провайдър\n(Gemini / OpenAI)"]

    subgraph EcoProject["EcoProject – Use Cases"]

        subgraph Public["Публичен достъп"]
            UC_List["UC1: Преглед на\nмаршрути"]
            UC_Search["UC2: Търсене на\nмаршрути"]
            UC_Map["UC3: Карта с\nмаркери"]
            UC_Login["UC4: Вход / Регистрация"]
        end

        subgraph Registered["Регистриран достъп"]
            UC_Fav["UC5: Управление\nна Любими"]
            UC_Chat["UC6: Чат с\nAI асистент"]
            UC_Post["UC7: Публикуване\nв общността"]
            UC_Profile["UC8: Управление\nна профил"]
        end

        subgraph AIInternal["Вътрешни AI процеси"]
            UC_Safety["UC9: Проверка\nза безопасност"]
            UC_Retrieval["UC10: RAG Извличане\nна контекст"]
            UC_Weather["UC11: Прогноза\nза времето"]
        end

        subgraph AdminUC["Администраторски функции"]
            UC_Proposal["UC12: Одобрение\nна предложения"]
            UC_Enrich["UC13: Обогатяване\nна маршрути"]
            UC_UserMgmt["UC14: Управление\nна потребители"]
        end
    end

    Guest --> UC_List
    Guest --> UC_Search
    Guest --> UC_Map
    Guest --> UC_Login

    User --> UC_List
    User --> UC_Search
    User --> UC_Map
    User --> UC_Fav
    User --> UC_Chat
    User --> UC_Post
    User --> UC_Profile

    Admin --> UC_Proposal
    Admin --> UC_Enrich
    Admin --> UC_UserMgmt
    Admin --> UC_List

    UC_Chat -->|"<<include>>"| UC_Safety
    UC_Chat -->|"<<include>>"| UC_Retrieval
    UC_Chat -->|"<<extend>>"| UC_Weather

    UC_Chat --> AI
    UC_Enrich --> AI

    style Public fill:#e8f4f8,stroke:#2196F3
    style Registered fill:#e8f8e8,stroke:#4CAF50
    style AIInternal fill:#fff3e0,stroke:#FF9800
    style AdminUC fill:#fce4ec,stroke:#E91E63
```

## Описание

**Тип:** Use Case диаграма

| Актьор | Роля | Достъпни UC |
|--------|------|-------------|
| Гост | Анонимен потребител | UC1–UC4 (само четене) |
| Регистриран потребител | Автентикиран с JWT | UC1–UC8 |
| Администратор | Role: Admin | UC12–UC14 + всички останали |
| AI Провайдър | Gemini Flash / gpt-4o-mini | Асинхронни отговори |

**Релации:**
- `UC6 <<include>> UC9` – Всеки чат задължително минава Safety проверка
- `UC6 <<include>> UC10` – Всеки чат извлича RAG контекст
- `UC6 <<extend>> UC11` – Времето се добавя само при маршрутни запитвания
