# 40 – Календарен график (Gantt Chart)

```mermaid
gantt
    title EcoProject – Разработка и план
    dateFormat  YYYY-MM-DD
    axisFormat  %b %d

    section Фаза 1: Основи
    Проектна документация         :done,    doc1,    2026-01-01, 2026-01-14
    Настройка на среда (Docker)   :done,    env1,    2026-01-08, 2026-01-21
    .NET 8 Backend структура      :done,    be1,     2026-01-15, 2026-01-28

    section Фаза 2: Auth & Trails
    JWT Auth + ASP.NET Identity   :done,    auth1,   2026-01-22, 2026-02-04
    Trails endpoint + ETag        :done,    trails1, 2026-01-29, 2026-02-11
    Frontend SPA структура        :done,    fe1,     2026-02-01, 2026-02-18

    section Фаза 3: AI Assistant
    Gemini интеграция             :done,    ai1,     2026-02-10, 2026-02-24
    Safety + Prompt Assembly      :done,    ai2,     2026-02-18, 2026-03-03
    RAG Retrieval + Embeddings    :done,    rag1,    2026-02-24, 2026-03-10
    OpenAI Fallback (Polly)       :done,    ai3,     2026-03-04, 2026-03-14

    section Фаза 4: Features
    Favorites система             :done,    fav1,    2026-03-10, 2026-03-18
    Community Posts               :done,    comm1,   2026-03-14, 2026-03-24
    Admin Panel                   :done,    admin1,  2026-03-20, 2026-03-28
    PWA + Service Worker          :done,    pwa1,    2026-03-22, 2026-04-01

    section Фаза 5: Качество
    Unit & Integration тестове    :done,    test1,   2026-03-15, 2026-04-05
    eco.json обогатяване (322)    :done,    data1,   2026-03-18, 2026-04-01
    UML Диаграми                  :done,    uml1,    2026-04-01, 2026-04-21

    section Фаза 6: Production
    Docker Compose production     :active,  prod1,   2026-04-10, 2026-04-25
    CI/CD GitHub Actions          :active,  cicd1,   2026-04-15, 2026-04-30
    Performance оптимизация       :         perf1,   2026-04-20, 2026-05-05
    Security audit                :         sec1,    2026-04-25, 2026-05-10

    section Фаза 7: Разширения
    Kubernetes deployment         :         k8s1,    2026-05-01, 2026-05-20
    OpenTelemetry + Prometheus    :         obs1,    2026-05-10, 2026-05-25
    Mobile PWA оптимизация        :         mob1,    2026-05-15, 2026-05-30
```

## Описание

**Тип:** Gantt Chart – Проектен план

| Фаза | Период | Статус |
|------|--------|--------|
| Фаза 1: Основи | Яну 2026 | ✅ Завършена |
| Фаза 2: Auth & Trails | Яну-Фев 2026 | ✅ Завършена |
| Фаза 3: AI Assistant | Фев-Мар 2026 | ✅ Завършена |
| Фаза 4: Features | Мар-Апр 2026 | ✅ Завършена |
| Фаза 5: Качество | Мар-Апр 2026 | ✅ Завършена |
| Фаза 6: Production | Апр-Май 2026 | 🔄 В процес |
| Фаза 7: Разширения | Май 2026 | 📅 Планирано |

**Критичен път:** AI Assistant (Фаза 3) → Quality (Фаза 5) → Production (Фаза 6)
