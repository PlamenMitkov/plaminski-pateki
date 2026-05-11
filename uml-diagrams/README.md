# UML Диаграми — EcoProject

Папката съдържа пълен набор от диаграми за дипломна защита и техническа документация.  
**Последна ревизия:** Май 2026 — **40 Mermaid файла** — пълно покритие на всички архитектурни аспекти (C4, Use Case, ER, DFD L0-L3, Activity/Sequence, Deployment, CI/CD, State, Threat Modeling, Testing, DDD, API, Observability, Git, Cloud, User Journey, Wireflow, Gantt).

---

## Конвенции за именуване

| Правило | Пример |
|---------|--------|
| ASCII имена на файлове | `09-sequence-assistant-session-safety.md` |
| Тип диаграма в префикса | `sequence-` / `activity-` / `class-` / `er-` / `dfd-` |
| Пореден номер за подредба | `05-`, `06-`, ... |
| Един файл = един конкретен сценарий | ✅ |
| Текстово съдържание на диаграмите | Български |

---

## Правила за академична коректност (Sequence диаграми)

1. **Обхват (Scope):** Всяка sequence диаграма описва един конкретен процес (напр. „Успешна автентикация" — не „цялата auth система").
2. **Подредба на участниците:** Отляво надясно по реда на включване. Инициаторът е вляво; бази данни и външни API-та са вдясно.
3. **Имена на съобщенията:** Кратки глаголи/методи — `findByEmailAsync(email)`, `createToken(user, roles)`. Не „изпраща данни".
4. **Activation bars:** Правоъгълникът започва при изпращане на съобщение (`->>+`) и приключва при получаване на отговор (`-->>-`).

---

## Mermaid файлове (draw.io import)

**Import в draw.io:** `Extras → Edit Diagram → (избери Mermaid) → поставете съдържанието на блока`

### Модели и архитектура

| Файл | Тип | Описание | Статус |
|------|-----|----------|--------|
| [01-class-diagram.md](01-class-diagram.md) | Class | Domain entities + AI service интерфейси | ✅ Актуален |
| [02-frontend-backend-diagram.md](02-frontend-backend-diagram.md) | Flowchart | Структурен обзор Frontend ↔ Backend ↔ AI ↔ External | ✅ Актуален |
| [03-database-schema-diagram.md](03-database-schema-diagram.md) | Flowchart | Prisma PostgreSQL схема | ✅ Актуален |
| [04-er-diagram.md](04-er-diagram.md) | ER | ER модел (Prisma PostgreSQL) | ✅ Актуален |

### Sequence диаграми — потребителски потоци

| Файл | Тип | Обхват | Статус |
|------|-----|--------|--------|
| [05-sequence-login.md](05-sequence-login.md) | Sequence | Вход: валидни данни → JWT | ✅ Нов |
| [06-sequence-register.md](06-sequence-register.md) | Sequence | Регистрация: нов потребител → JWT | ✅ Нов |
| [07-sequence-trail-search-etag.md](07-sequence-trail-search-etag.md) | Sequence | Търсене на пътеки + ETag кеш (304/200) | ✅ Нов |
| [08-sequence-favorites-sync.md](08-sequence-favorites-sync.md) | Sequence | Синхронизация на любими (SQL транзакция) | ✅ Нов |
| [12-sequence-community-post-create.md](12-sequence-community-post-create.md) | Sequence | Публикация + качване на изображения | ✅ Нов |
| [13-sequence-admin-proposal-approval.md](13-sequence-admin-proposal-approval.md) | Sequence | Одобряване на предложение → нова пътека | ✅ Нов |

### Sequence/Activity диаграми — AI асистент

| Файл | Тип | Обхват | Статус |
|------|-----|--------|--------|
| [09-sequence-assistant-session-safety.md](09-sequence-assistant-session-safety.md) | Sequence | Сесия + Prompt Injection Guard | ✅ Нов |
| [10-sequence-assistant-retrieval-provenance.md](10-sequence-assistant-retrieval-provenance.md) | Sequence | RAG извличане + Provenance верификация + Метео | ✅ Нов |
| [11-activity-ai-fallback-orchestration.md](11-activity-ai-fallback-orchestration.md) | Activity | 4-степенна Fallback оркестрация (Gemini→OpenAI→Local) | ✅ Нов |
| [14-activity-vector-indexing.md](14-activity-vector-indexing.md) | Activity | Пакетно векторно индексиране за семантично търсене | ✅ Нов |

### C4 Model диаграми

| Файл | Тип | Описание |
|------|-----|----------|
| [15-c4-level1-system-context.md](15-c4-level1-system-context.md) | C4-L1 | System Context: Потребител → EcoProject → Gemini/OpenAI |
| [16-c4-level2-containers.md](16-c4-level2-containers.md) | C4-L2 | Containers: Frontend, Backend, SQL Server, eco.json |
| [17-c4-level3-components.md](17-c4-level3-components.md) | C4-L3 | Components: Controllers, Services, Repositories, Middleware |

### Use Case & ER диаграми

| Файл | Тип | Описание |
|------|-----|----------|
| [18-usecase-eco.md](18-usecase-eco.md) | Use Case | Гост/Потребител/Admin + AI Provider – 14 use cases |
| [19-er-hybrid-model.md](19-er-hybrid-model.md) | ER | SQL Server EF Core + eco.json хибриден модел |

### DFD диаграми (Data Flow)

| Файл | Тип | Описание |
|------|-----|----------|
| [20-dfd-level0-context.md](20-dfd-level0-context.md) | DFD-L0 | Контекстна диаграма – черна кутия |
| [21-dfd-level1-subsystems.md](21-dfd-level1-subsystems.md) | DFD-L1 | 4 основни подсистеми (Auth, Trails, AI, Community) |
| [22-dfd-level2-ai-orchestration.md](22-dfd-level2-ai-orchestration.md) | DFD-L2 | Декомпозиция на процес 3.0: Safety→Assembly→Execution→Compose |
| [23-dfd-level3-model-execution.md](23-dfd-level3-model-execution.md) | DFD-L3 | Декомпозиция на процес 3.3: Gemini retry + OpenAI fallback |

### Activity диаграми (AI Pipeline)

| Файл | Тип | Описание |
|------|-----|----------|
| [24-activity-rag-pipeline.md](24-activity-rag-pipeline.md) | Activity | RAG Pipeline: Embedding → Cosine Similarity → Context |
| [25-activity-ai-response.md](25-activity-ai-response.md) | Activity | Пълен AI Response поток: Rate Limit → Safety → RAG → AI → Format |
| [26-activity-parallel-retrieval.md](26-activity-parallel-retrieval.md) | Activity | Паралелно извличане Fork/Join: Embedding + Weather + History |

### Deployment & Infrastructure

| Файл | Тип | Описание |
|------|-----|----------|
| [27-deployment-docker-compose.md](27-deployment-docker-compose.md) | Deployment | Docker Compose: Frontend/Backend/DB контейнери + Volumes |
| [28-cicd-pipeline.md](28-cicd-pipeline.md) | CI/CD | GitHub Actions: Build→Test→Security→Docker→Deploy |

### State & Security диаграми

| Файл | Тип | Описание |
|------|-----|----------|
| [29-state-ai-request-lifecycle.md](29-state-ai-request-lifecycle.md) | State | Жизнен цикъл на AI заявка (14 състояния) |
| [30-threat-modeling-stride.md](30-threat-modeling-stride.md) | Threat | STRIDE анализ: Spoofing, Tampering, DoS, EoP, Info Disclosure |
| [31-testing-strategy-pyramid.md](31-testing-strategy-pyramid.md) | Testing | Пирамида: Unit (13 теста) + Integration (6 теста) + Manual |

### Architecture patterns

| Файл | Тип | Описание |
|------|-----|----------|
| [32-ddd-bounded-context.md](32-ddd-bounded-context.md) | DDD | Bounded Contexts: Identity, Trails, Favorites, AI, Community |
| [33-api-component-diagram.md](33-api-component-diagram.md) | API | Middleware pipeline + Endpoints map |
| [34-observability-architecture.md](34-observability-architecture.md) | Observability | Serilog + Health Checks + Metrics + Tracing |

### Git, Cloud & Infrastructure

| Файл | Тип | Описание |
|------|-----|----------|
| [35-git-branching-strategy.md](35-git-branching-strategy.md) | Git | Git Flow: main, develop, feature/*, release/*, hotfix/* |
| [36-cloud-architecture.md](36-cloud-architecture.md) | Cloud | Cloud architecture: CDN, LB, App tier, Data tier, Background |

### UX & Project Management

| Файл | Тип | Описание |
|------|-----|----------|
| [37-user-journey-map.md](37-user-journey-map.md) | UX | User Journey: Откритие → AI Асистент → Запазване |
| [38-wireflow-ui-ux.md](38-wireflow-ui-ux.md) | Wireflow | UI навигационен поток: всички 9 екрана |
| [39-api-integration-payload-map.md](39-api-integration-payload-map.md) | API | Payload Map: Request/Response формати за всички endpoints |
| [40-gantt-project-timeline.md](40-gantt-project-timeline.md) | Gantt | Проектен timeline: Яну 2026 – Май 2026 (7 фази) |

---

## Draw.io файлове

| Файл | Тип | Описание | Статус |
|------|-----|----------|--------|
| [C4Level1.drawio](C4Level1.drawio) | C4-L1 | System Context (User, Platform, Gemini, OpenAI) | ✅ Актуален |
| [C4Level2.drawio](C4Level2.drawio) | C4-L2 | Container диаграма | 🔄 Проверете за нови services |
| [C4 Level 3  Диаграма на компонентите (Backend API Application).drawio](C4%20Level%203%20%20Диаграма%20на%20компонентите%20(Backend%20API%20Application).drawio) | C4-L3 | Компоненти в Backend API | 🔄 Проверете за extracted services |
| [SequenceDiagramAIqueryRAGFallback.drawio](SequenceDiagramAIqueryRAGFallback.drawio) | Sequence | AI заявка (RAG + Fallback) — PlantUML | ✅ Актуален |
| [ActivityDiagramAIResponce.drawio](ActivityDiagramAIResponce.drawio) | Activity | Обработка на AI отговор | ✅ Актуален |
| [ActivityDiagramComplexAIOrchestrationPipeline.drawio](ActivityDiagramComplexAIOrchestrationPipeline.drawio) | Activity | Комплексна AI оркестрация | ✅ Актуален |
| [ActivityDiagramParalelretrivalForkJoin.drawio](ActivityDiagramParalelretrivalForkJoin.drawio) | Activity | Паралелно извличане (Fork/Join) | ✅ Актуален |
| [DFD Ниво 1 - Основни подсистеми.drawio](DFD%20Ниво%201%20-%20Основни%20подсистеми.drawio) | DFD-L1 | Основни подсистеми | ⚠️ Дублиран с DFD L1 по-долу — изберете канонична версия |
| [Data Flow Diagram (DFD Level 1) - Обработка на данни.drawio](Data%20Flow%20Diagram%20(DFD%20Level%201)%20-%20Обработка%20на%20данни.drawio) | DFD-L1 | Обработка на данни | ⚠️ Дублиран с DFD L1 по-горе — изберете канонична версия |
| [DFD Ниво 2 - Декомпозиция на Процес 3.0 (AI Оркестрация).drawio](DFD%20Ниво%202%20-%20Декомпозиция%20на%20Процес%203.0%20(AI%20Оркестрация).drawio) | DFD-L2 | AI Оркестрация | ✅ Актуален |
| [DFD Ниво 3 - Декомпозиция на Процес 3.3 (Моделна екзекуция и Fallback).drawio](DFD%20Ниво%203%20-%20Декомпозиция%20на%20Процес%203.3%20(Моделна%20екзекуция%20и%20Fallback).drawio) | DFD-L3 | Fallback оркестрация | ✅ Актуален |
| [RAG Pipeline  Обогатяване на AI Контекста.drawio](RAG%20Pipeline%20%20Обогатяване%20на%20AI%20Контекста.drawio) | Pipeline | RAG обогатяване | ✅ Актуален |
| [ClassDiagramBackEnd.drawio](ClassDiagramBackEnd.drawio) | Class | Backend класове | 🔄 Проверете за extracted assistant services |
| [ClassDiagramFrontend.drawio](ClassDiagramFrontend.drawio) | Class | Frontend компоненти | 🔄 Вероятно актуален |
| [ER Диаграма  Хибриден модел на данните (SQL + JSON).drawio](ER%20Диаграма%20%20Хибриден%20модел%20на%20данните%20(SQL%20+%20JSON).drawio) | ER | SQL + JSON хибриден модел | ✅ Актуален |
| [UsecaseEco.drawio](UsecaseEco.drawio) | Use Case | Случаи на употреба | 🔄 Добавете Community/Admin/Offline случаи |
| [Threat Modeling  Вектори на атака и защитни слоеве.drawio](Threat%20Modeling%20%20Вектори%20на%20атака%20и%20защитни%20слоеве.drawio) | Threat | Security posture | ✅ Актуален |
| [Коригирана Стратегия за тестване (Testing Pyramid).drawio](Коригирана%20Стратегия%20за%20тестване%20(Testing%20Pyramid).drawio) | Testing | QA стратегия | ✅ Актуален |
| [DeploymentDockerCompose.drawio](DeploymentDockerCompose.drawio) | Deployment | Docker Compose топология | ✅ Актуален |
| [Диаграма на разположението (Deployment Diagram)..drawio](Диаграма%20на%20разположението%20(Deployment%20Diagram)..drawio) | Deployment | Deployment диаграма | ✅ Актуален |
| [Диаграма на състоянията  Жизнен цикъл на AI заявката.drawio](Диаграма%20на%20състоянията%20%20Жизнен%20цикъл%20на%20AI%20заявката.drawio) | State | Жизнен цикъл на AI заявка | ✅ Актуален |
| [Архитектура на наблюдаемостта (Observability & Telemetry Diagram).drawio](Архитектура%20на%20наблюдаемостта%20(Observability%20&%20Telemetry%20Diagram).drawio) | Observability | Telemetry & Logging | ℹ️ Аспирационна — валидирайте спрямо реалния стек |
| [Cloud Architecture  Готовност за мащабиране (Production Scale).drawio](Cloud%20Architecture%20%20Готовност%20за%20мащабиране%20(Production%20Scale).drawio) | Cloud | Cloud scaling | ℹ️ Аспирационна |
| [Domain-Driven Design (DDD) Карта на контекстите (Bounded Context Map.drawio](Domain-Driven%20Design%20(DDD)%20Карта%20на%20контекстите%20(Bounded%20Context%20Map.drawio) | DDD | Bounded Context Map | ✅ Актуален |
| [Диаграма на API Интеграцията и Формата на данните (Payload Map).drawio](Диаграма%20на%20API%20Интеграцията%20и%20Формата%20на%20данните%20(Payload%20Map).drawio) | API | Payload схеми | ✅ Актуален |
| [CI CD Pipeline  Автоматизирано тестване и билдване.drawio](CI%20CD%20Pipeline%20%20Автоматизирано%20тестване%20и%20билдване.drawio) | CI/CD | Pipeline | ✅ Актуален |
| [Диаграма на Git стратегиите (GitFlow   Branching Strategy).drawio](Диаграма%20на%20Git%20стратегиите%20(GitFlow%20%20Branching%20Strategy).drawio) | Git | Branching strategy | ✅ Актуален |
| [User Journey Map (Пътят на потребителя - Swimlanes).drawio](User%20Journey%20Map%20(Пътят%20на%20потребителя%20-%20Swimlanes).drawio) | UX | User Journey | ℹ️ UX-фокусиран |
| [Wireflow Диаграма (UI UX + Логика).drawio](Wireflow%20Диаграма%20(UI%20UX%20+%20Логика).drawio) | Wireflow | UI/UX flow | ℹ️ UX-фокусиран |
| [Контекстна диаграма (DFD Level 0) - EcoProject.drawio](Контекстна%20диаграма%20(DFD%20Level%200)%20-%20EcoProject.drawio) | DFD-L0 | Context диаграма | ✅ Актуален |
| [Календарен график за изпълнение на проекта (Gantt Chart).drawio](Календарен%20график%20за%20изпълнение%20на%20проекта%20(Gantt%20Chart).drawio) | Gantt | Проектен план | ℹ️ Управленски |

---

## Статус легенда

| Символ | Значение |
|--------|---------|
| ✅ Актуален | Отговаря на текущата архитектура |
| 🔄 Проверете | Вероятно частично остарял — изисква ревизия |
| ⚠️ Проблем | Naming typo, дублиране или структурен проблем |
| ℹ️ Аспирационна | Документира цели/планове, не реален код |

---

## Препоръчан основен пакет (дипломна защита)

1. [01-class-diagram.md](01-class-diagram.md) — Domain model + AI service interfaces
2. [02-frontend-backend-diagram.md](02-frontend-backend-diagram.md) — Архитектурен обзор
3. [C4Level1.drawio](C4Level1.drawio) — System context
4. [09-sequence-assistant-session-safety.md](09-sequence-assistant-session-safety.md) — AI Session + Safety
5. [10-sequence-assistant-retrieval-provenance.md](10-sequence-assistant-retrieval-provenance.md) — RAG + Provenance
6. [11-activity-ai-fallback-orchestration.md](11-activity-ai-fallback-orchestration.md) — Fallback оркестрация
9. [SequenceDiagramAIqueryRAGFallback.drawio](SequenceDiagramAIqueryRAGFallback.drawio) — Пълна AI sequence (draw.io)
8. [DFD Ниво 3 - Декомпозиция на Процес 3.3 (Моделна екзекуция и Fallback).drawio](DFD%20Ниво%203%20-%20Декомпозиция%20на%20Процес%203.3%20(Моделна%20екзекуция%20и%20Fallback).drawio) — DFD процес
9. [Threat Modeling  Вектори на атака и защитни слоеве.drawio](Threat%20Modeling%20%20Вектори%20на%20атака%20и%20защитни%20слоеве.drawio) — Security
10. [Коригирана Стратегия за тестване (Testing Pyramid).drawio](Коригирана%20Стратегия%20за%20тестване%20(Testing%20Pyramid).drawio) — QA

---

## Правило за поддръжка

> При добавяне на нов endpoint или service → обновете поне един Mermaid файл или draw.io диаграма, свързана с новия поток, и актуализирайте статуса в таблиците по-горе.
