# Примерни приложения (кратък вариант)

Този файл съдържа съкратени примери за:
- Приложение А: JSON структура на геопространствения каталог в EcoTrail
- Приложение Б: Docker Compose конфигурация за инфраструктурната среда
- Приложение В: Структура на системния промпт и софтуерната рамка на ИИ

## ПРИЛОЖЕНИЕ А: JSON структура на геопространствения каталог (пример)

```json
{
  "meta": {
    "version": "1.0",
    "generatedAt": "2026-06-10T09:30:00Z",
    "source": "EcoProject"
  },
  "trails": [
    {
      "id": 101,
      "name": "Витошка панорама",
      "region": "София",
      "difficulty": "medium",
      "distanceKm": 12.4,
      "durationMin": 260,
      "elevationGainM": 740,
      "location": {
        "type": "LineString",
        "coordinates": [
          [23.255, 42.640],
          [23.278, 42.655],
          [23.301, 42.671]
        ]
      },
      "startPoint": {
        "name": "кв. Драгалевци",
        "lat": 42.642,
        "lon": 23.304
      },
      "tags": ["панорама", "гора", "семеен"],
      "status": "active",
      "updatedAt": "2026-05-28T14:12:00Z"
    }
  ]
}
```

## ПРИЛОЖЕНИЕ Б: Docker Compose конфигурация (пример)

```yaml
version: "3.9"

services:
  api:
    build:
      context: .
      dockerfile: EcoTrails.Api/Dockerfile
    container_name: ecotrails-api
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: "Production"
      ConnectionStrings__DefaultConnection: "Host=db;Port=5432;Database=eco;Username=eco;Password=eco_pass"
      Redis__Configuration: "redis:6379"
    depends_on:
      - db
      - redis

  client:
    build:
      context: .
      dockerfile: EcoTrails.Client/Dockerfile
    container_name: ecotrails-client
    ports:
      - "3000:80"
    depends_on:
      - api

  db:
    image: postgres:16
    container_name: ecotrails-db
    environment:
      POSTGRES_DB: "eco"
      POSTGRES_USER: "eco"
      POSTGRES_PASSWORD: "eco_pass"
    volumes:
      - pgdata:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  redis:
    image: redis:7
    container_name: ecotrails-redis
    ports:
      - "6379:6379"

volumes:
  pgdata:
```

## ПРИЛОЖЕНИЕ В: Структура на системния промпт и ИИ рамка (пример)

```yaml
assistantFramework:
  name: "EcoTrail Assistant"
  version: "0.1"

systemPrompt:
  role: "Ти си асистент за еко маршрути в България."
  goals:
    - "Давай кратки, точни и проверими отговори."
    - "При липса на данни, заявявай несигурност."
    - "Предлагай безопасни и реалистични маршрути."
  safetyRules:
    - "Не измисляй факти за затворени/опасни трасета."
    - "Не показвай чувствителни данни (API ключове, токени)."
  responseStyle:
    language: "bg"
    format: "markdown"
    maxLength: "short"

softwarePipeline:
  - step: "intent-detection"
    description: "Класифицира заявката (маршрут, време, трудност, регион)."
  - step: "retrieval"
    description: "Извлича релевантни пътеки от каталога."
  - step: "ranking"
    description: "Подрежда по близост, трудност и предпочитания."
  - step: "response-composition"
    description: "Генерира отговор с препоръки и предупреждения."
  - step: "audit-log"
    description: "Записва кратък лог за прозрачност и диагностика."
```

---

Бележка: Това е примерен, съкратен вариант. При нужда може да се разшири с реалните полета от вашите `eco.json`, `docker-compose.yml` и API/assistant конфигурации.
