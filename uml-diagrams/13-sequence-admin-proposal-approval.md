# Sequence Diagram: Одобряване на предложение за пътека от администратор

Обхват: Сценарий „Администратор одобрява пост-предложение; системата извършва AI преглед и създава нова пътека атомарно".  
Alt-ветви: пост не е намерен (404), не е предложение (400), вече одобрено/отхвърлено (409), AI недостъпен (heuristic fallback).  
Файл: `13-sequence-admin-proposal-approval.md` — Mermaid source за draw.io import.

```mermaid
sequenceDiagram
    autonumber
    actor A as Администратор
    participant C as AdminPanelController
    participant RS as TrailProposalReviewService
    participant AI as AiProviderClient
    participant DB as AppDbContext
    participant LOG as Logger

    A->>+C: POST /api/adminpanel/{postId}/approve\n{ name?, location?, region?, difficultyLevel?, ... }
    Note over C: [Authorize(Roles="AdminPanel")]

    C->>+DB: CommunityTrailPosts.findAsync(postId)
    DB-->>-C: CommunityTrailPost | null
    alt Пост не е намерен
        C-->>A: 404 Not Found
    end

    alt PostType != TrailProposal
        C-->>A: 400 Bad Request "Постът не е предложение за пътека"
    end

    alt ProposalStatus == Approved
        C-->>A: 409 Conflict "Предложението вече е одобрено"
    else ProposalStatus == Rejected
        C-->>A: 409 Conflict "Предложението е отхвърлено"
    end

    %% AI преглед
    C->>+RS: evaluateAsync(post)
    RS->>+AI: sendRequestAsync(systemPrompt, postContent, temperature: 0.1)
    Note over AI: Системен промпт: "Анализатор за верификация\nна туристически сигнали"
    alt AI отговори успешно
        AI-->>-RS: { isLikelyTrailProposal, reliabilityScore, suggestedName, suggestedLocation, suggestedRegion, suggestedDifficultyLevel, warnings }
    else AI недостъпен (Exception)
        RS->>RS: buildHeuristicReview(post.content)
        Note over RS: Ключови думи: "екопътека", "км", "час",\n"кота", "координат" → score 0-100
        RS-->>C: heuristicReview
    end
    RS-->>-C: CommunityPostAiReviewResponse

    %% Определяне на финалните атрибути
    C->>C: resolveTrailAttributes(overrides, aiReview, post)
    Note over C: Приоритет: admin override > AI suggestion > post content

    %% Атомарно записване
    C->>+DB: Trails.addAsync(newTrail)
    DB-->>-C: Trail { id }

    C->>+DB: post.TrailId = trail.id\npost.ProposalStatus = Approved\npost.ReviewedAtUtc = now
    DB-->>-C: OK

    C->>+DB: saveChangesAsync()
    Note over DB: Пътека + статус на поста в една транзакция
    DB-->>-C: OK

    C->>LOG: log("Trail proposal {postId} approved. New trail id: {trailId}")

    C-->>-A: 200 OK { trailId, trailName }
```
