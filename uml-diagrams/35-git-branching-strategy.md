# 35 – Git стратегия и Branching модел

```mermaid
gitGraph
    commit id: "init: project setup"

    branch develop
    checkout develop
    commit id: "feat: JWT auth"
    commit id: "feat: trails endpoint"

    branch feature/ai-assistant
    checkout feature/ai-assistant
    commit id: "feat: GeminiService"
    commit id: "feat: SafetyService"
    commit id: "feat: RAG retrieval"
    checkout develop
    merge feature/ai-assistant id: "merge: AI assistant"

    branch feature/community-posts
    checkout feature/community-posts
    commit id: "feat: CommunityController"
    commit id: "feat: Admin approval"
    checkout develop
    merge feature/community-posts id: "merge: community"

    branch release/v1.0
    checkout release/v1.0
    commit id: "chore: version bump"
    commit id: "fix: rate limit config"
    checkout main
    merge release/v1.0 id: "v1.0.0" tag: "v1.0.0"
    checkout develop
    merge release/v1.0 id: "sync develop"

    branch hotfix/jwt-expiry
    checkout hotfix/jwt-expiry
    commit id: "fix: JWT expiry bug"
    checkout main
    merge hotfix/jwt-expiry id: "hotfix applied" tag: "v1.0.1"
    checkout develop
    merge hotfix/jwt-expiry id: "sync hotfix"
```

## Описание

**Тип:** Git Branching Strategy (Git Flow)

| Клон | Описание | Merge target |
|------|----------|-------------|
| `main` | Production-ready код | – |
| `develop` | Интеграционен клон | main (при release) |
| `feature/*` | Нови функционалности | develop |
| `release/*` | Release подготовка | main + develop |
| `hotfix/*` | Спешни production поправки | main + develop |

**Правила:**
- `main` → само чрез PR с code review
- `feature/*` → branch от `develop`, merge чрез PR
- `hotfix/*` → branch от `main`, merge в `main` AND `develop`
- Semantic versioning: `v{major}.{minor}.{patch}`
- Всеки merge в `main` → автоматичен CI/CD deployment
