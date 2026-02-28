# 🏔️ Plaminski Pateki (EcoTrails Project)

Интерактивна платформа за изследване на екопътеки в България, изградена с ASP.NET Core 10, React + TypeScript и SQL Server. Проектът включва реални данни за 500+ маршрута, интерактивна карта и интелигентен анализ.

## 🎯 Quick Vision

Plaminski Pateki обединява GIS визуализация, филтриране, експорт и хибридна синхронизация на любими маршрути (LocalStorage + Cloud) в един модерен full-stack продукт.

---

## 🧱 Технологичен стек

### Backend
- ⚙️ ASP.NET Core 10 Web API
- 🗄️ Entity Framework Core 10
- 🔐 ASP.NET Core Identity + JWT Authentication
- 📘 Swagger / OpenAPI

### Frontend
- ⚛️ React 18 + TypeScript
- ⚡ Vite
- 🗺️ Leaflet + Marker Clustering (Spiderfy)
- 📊 Recharts (dashboard analytics)

### Database
- 🛢️ SQL Server (SQL Server Express / SQL Server in Docker)

### DevOps & Tooling
- 🐳 Docker + Docker Compose
- 🐍 Python tooling с `uv`

---

## 🏗️ System Architecture

### API Layer
- RESTful контролери за trails, auth и favorites.
- Пагинация, търсене, филтриране по трудност и координати.
- `X-Total-Count` header + paged response metadata.

### Auth Layer
- Регистрация/вход с JWT.
- Защитени endpoints за favorites sync.
- Cloud-first hybrid модел: локални любими + синхронизация към база след login.

### GIS Layer
- Интерактивна карта с Leaflet.
- Клъстеризация на маркери за висока плътност на данни.
- Spiderfy поведение за припокриващи се координати.

### Data Layer
- SQL Server + EF Core migrations.
- Импорт на реални данни от `eco.json` (500+ маршрута).

---

## 🚀 Quick Start (Docker First)

```bash
# Clone repository
git clone https://github.com/PlamenMitkov/plaminski-pateki.git
cd plaminski-pateki

# Start everything
docker compose up --build
```

### Services
- Frontend: `http://localhost:5173`
- API: `http://localhost:5218`
- Swagger: `http://localhost:5218/swagger`
- SQL Server: `localhost:1433`

### OpenAI Assistant setup (gpt-3.5-turbo)

Backend assistant endpoint: `POST /api/assistant/chat`

Assistant chat response now includes alternative recommendations via `suggestedAlternativeIds` and `suggestedAlternatives`.

Semantic enrichment endpoint: `POST /api/assistant/enrich`

Note: enrichment is optional and can be skipped for normal assistant chat usage.
Rate-limit optimization: use `OpenAI.EnrichDelayMs`, `OpenAI.RetryAttempts`, `OpenAI.RetryInitialDelayMs`, and `OpenAI.RetryJitterMs`.

Session persistence endpoints:
- `POST /api/assistant/sessions` (create new chat session)
- `GET /api/assistant/sessions/{sessionId}/messages?limit=80` (load conversation history)
- `GET /api/assistant/sessions/mine?limit=12` (logged-in user's sessions for profile/assistant)
- `DELETE /api/assistant/sessions/{sessionId}` (delete session from profile/assistant)

Security: deleting session requires authenticated user ownership.

Set API key in environment variable before starting API:

```powershell
$env:OPENAI_API_KEY="your_openai_key_here"
```

Default model is configured as `gpt-3.5-turbo` in `EcoTrails.Api/appsettings.json`.

Example semantic enrichment request (process 25 trails):

```bash
curl -X POST http://localhost:5218/api/assistant/enrich \
	-H "Content-Type: application/json" \
	-d '{"limit":25,"overwriteExisting":false}'
```

### One-click start (Windows)

```bat
start.bat
```

Скриптът е в root папката на проекта и стартира целия стек с `docker compose up --build`.

### One-click stop (Windows)

```bat
stop.bat
```

Скриптът е в root папката на проекта и спира контейнерите с `docker compose down`.

---

## 🧪 Smoke Test & Validation

- ✅ **Auth:** регистрация/вход + JWT валидация.
- ✅ **Sync:** хибридна синхронизация на любими пътеки (LocalStorage ↔ Cloud DB).
- ✅ **Maps:** интерактивно филтриране и визуализация на 500+ локации.
- ✅ **Export:** CSV експорт на филтрирани масиви.
- ✅ **Analytics:** Pie/Bar диаграми се актуализират при промяна на favorite state.

---

## 📁 Project Structure

```text
.
├─ EcoTrails.Api/        # ASP.NET Core Web API + EF Core + Identity/JWT
├─ EcoTrails.Client/     # React + TypeScript + Vite + Leaflet + Recharts
├─ eco.json              # Source dataset for trail import
└─ docker-compose.yml    # Full local orchestration (DB + API + Client)
```

---

## 🗺️ Future Roadmap

- 🌦️ Интеграция с weather API за реална прогноза по пътеки.
- 📱 PWA режим за офлайн достъп в планински условия.
- 🧭 Разширени гео-филтри (денивелация, дължина, сезонност).
- 👥 Role-based admin панел за управление на съдържание.
- ☁️ CI/CD deployment pipeline (build, test, container publish).

---

## 👨‍💻 Author

**Plamen Mitkov**

- GitHub: https://github.com/PlamenMitkov
- Repository: https://github.com/PlamenMitkov/plaminski-pateki
