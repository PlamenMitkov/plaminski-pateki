# 32 – Domain-Driven Design (DDD): Bounded Contexts

```mermaid
graph TB
    subgraph IdentityContext["🔐 Identity & Auth Context"]
        direction TB
        User_Agg["Aggregate: User\n─────────────\n+ Id (string)\n+ Email\n+ PasswordHash\n+ RefreshToken\n+ Role"]
        AuthService_DDD["Domain Service:\nAuthService\n─────────────\nGenerateJwt()\nHashPassword()\nValidateRefresh()"]
    end

    subgraph TrailsContext["🏔️ Trails Context"]
        direction TB
        Trail_VO["Value Object: Trail\n─────────────\n+ Id (int)\n+ Name\n+ Region\n+ Difficulty\n+ GeoLocation\n+ Embedding (float[])"]
        TrailSearch_DS["Domain Service:\nTrailSearchTextMatcher\n─────────────\nSearchByText()\nSearchByEmbedding()"]
    end

    subgraph FavoritesContext["⭐ Favorites Context"]
        direction TB
        Favorite_Agg["Aggregate: Favorite\n─────────────\n+ Id\n+ UserId (ref)\n+ TrailId (ref to eco.json)\n+ TrailName\n+ CreatedAt"]
    end

    subgraph AssistantContext["🤖 AI Assistant Context"]
        direction TB
        Session_Agg["Aggregate: AssistantSession\n─────────────\n+ UserId\n+ Messages[]"]
        Message_Ent["Entity: AssistantMessage\n─────────────\n+ Role (user/assistant)\n+ Content\n+ SourceCitation\n+ ModelUsed"]
        AIOrchestration_DS["Domain Service:\nAssistantOrchestration\n─────────────\nSafetyCheck()\nAssemblePrompt()\nExecuteWithFallback()\nCompose()"]
    end

    subgraph CommunityContext["👥 Community Context"]
        direction TB
        Post_Agg["Aggregate: CommunityPost\n─────────────\n+ Id\n+ AuthorId (ref)\n+ Content\n+ IsApproved\n+ ApprovedBy"]
        Proposal_Ent["Entity: TrailProposal\n─────────────\n+ UserId\n+ Status (Pending/Approved/Rejected)"]
    end

    subgraph SharedKernel["🔗 Shared Kernel"]
        EcoJsonRepo["eco.json Repository\n(Read-only Trail data)"]
        WeatherSvc_SK["WeatherService\n(Infrastructure)"]
    end

    IdentityContext -->|"UserID → shared across\nbounded contexts"| FavoritesContext
    IdentityContext -->|"UserID → Auth claims"| AssistantContext
    IdentityContext -->|"UserID → Author"| CommunityContext
    TrailsContext -->|"TrailId (int)\nby reference only"| FavoritesContext
    TrailsContext -->|"Trail embeddings\nfor RAG search"| AssistantContext
    SharedKernel -->|"eco.json data source"| TrailsContext
    SharedKernel -->|"Weather for context"| AssistantContext

    style IdentityContext fill:#fce4ec,stroke:#E91E63
    style TrailsContext fill:#e8f5e9,stroke:#4CAF50
    style FavoritesContext fill:#fff3e0,stroke:#FF9800
    style AssistantContext fill:#e3f2fd,stroke:#2196F3
    style CommunityContext fill:#f3e5f5,stroke:#9C27B0
    style SharedKernel fill:#f5f5f5,stroke:#666
```

## Описание

**Тип:** DDD Bounded Context Map

| Контекст | Aggregate Root | Описание |
|----------|---------------|----------|
| Identity & Auth | User | ASP.NET Identity + JWT |
| Trails | Trail (Value Object) | eco.json static data |
| Favorites | Favorite | User ↔ Trail N:M |
| AI Assistant | AssistantSession | AI orchestration pipeline |
| Community | CommunityPost | UGC + moderation |
| Shared Kernel | eco.json + Weather | Read-only инфраструктура |

**Интеграция между контексти:** Само чрез ID референции – няма директни обектни зависимости между Bounded Contexts.
