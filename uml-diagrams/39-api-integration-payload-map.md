# 39 – Диаграма на API Интеграцията (Payload Map)

```mermaid
graph LR
    subgraph Client["Frontend (React)"]
        FE_Auth["Auth Request\n{email, password}"]
        FE_Trails["Trails Request\n?region=&difficulty="]
        FE_Chat["Chat Request\n{message: string}"]
        FE_Fav["Favorites Request\n{trailId, trailName}"]
    end

    subgraph BackendAPI["Backend API (.NET 8)"]

        subgraph AuthFlow["Auth Flow"]
            AuthReq["POST /api/auth/login\nBody: {email, password}"]
            AuthResp["Response 200:\n{accessToken, refreshToken,\nexpiresIn, userId}"]
        end

        subgraph TrailsFlow["Trails Flow"]
            TrailsReq["GET /api/trails\n?region=Rila&difficulty=Medium\nETag: {etag-value}"]
            TrailsResp["Response 200:\n[{id, name, region, difficulty,\nlength_km, lat, lng, tags}]\nETag: {new-etag}"]
            Trails304["Response 304:\nNot Modified (cached)"]
        end

        subgraph ChatFlow["Chat Flow (AI)"]
            ChatReq["POST /api/assistant/chat\nAuthorization: Bearer {JWT}\nBody: {message: string}"]
            ChatResp["Response 200:\n{role: 'assistant',\ncontent: string,\nsources: [{name, region}],\nmodelUsed: 'gemini-2.0-flash',\nschemaOrg: {...HowToStep}}"]
            ChatRate["Response 429:\n{error: 'Rate limit exceeded',\nretryAfter: 60}"]
        end

        subgraph FavFlow["Favorites Flow"]
            FavReq["POST /api/favorites\nAuthorization: Bearer {JWT}\nBody: {trailId: int, trailName: string}"]
            FavResp["Response 201 Created:\n{id, userId, trailId,\ntrailName, createdAt}"]
        end
    end

    subgraph ExternalAPIs["External APIs"]
        GeminiPayload["Gemini REST\nPOST /v1beta/models/gemini-2.0-flash\n:generateContent\n{contents: [{role, parts}],\ngenerationConfig: {temperature: 0.7}}"]
        OpenAIPayload["OpenAI REST\nPOST /v1/chat/completions\n{model: 'gpt-4o-mini',\nmessages: [{role, content}]}"]
        WeatherPayload["Open-Meteo REST\nGET /v1/forecast\n?latitude=42.5&longitude=23.3\n&current_weather=true"]
    end

    FE_Auth --> AuthReq
    AuthReq --> AuthResp

    FE_Trails --> TrailsReq
    TrailsReq --> TrailsResp
    TrailsReq --> Trails304

    FE_Chat --> ChatReq
    ChatReq --> ChatResp
    ChatReq --> ChatRate
    ChatReq --> GeminiPayload
    GeminiPayload -.->|"fallback"| OpenAIPayload
    ChatReq --> WeatherPayload

    FE_Fav --> FavReq
    FavReq --> FavResp

    style AuthFlow fill:#fce4ec,stroke:#E91E63
    style TrailsFlow fill:#e8f5e9,stroke:#4CAF50
    style ChatFlow fill:#e3f2fd,stroke:#2196F3
    style FavFlow fill:#fff3e0,stroke:#FF9800
    style ExternalAPIs fill:#f3e5f5,stroke:#9C27B0
```

## Описание

**Тип:** API Integration / Payload Map

| Endpoint | Method | Auth | Request | Response |
|----------|--------|------|---------|----------|
| `/api/auth/login` | POST | – | `{email, password}` | `{accessToken, refreshToken}` |
| `/api/trails` | GET | – | Query params + ETag | Trail array + ETag |
| `/api/assistant/chat` | POST | JWT | `{message}` | `{content, sources, schemaOrg}` |
| `/api/favorites` | POST | JWT | `{trailId, trailName}` | Created favorite |

**Gemini API формат:**
- `contents[].parts[].text` → промпт
- `generationConfig.temperature: 0.7` → баланс между creativity и accuracy
- `generationConfig.maxOutputTokens: 1024`

**Schema.org в AI Response:** `@type: HowToStep` за структурирани маршрутни инструкции
