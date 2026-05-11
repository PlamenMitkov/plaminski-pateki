# Sequence Diagram: Сесия на AI асистент и проверка за безопасност

Обхват: Сценарий „Клиентът изпраща съобщение; системата извлича/създава сесия и проверява за prompt injection".  
Alt-ветви: неавторизиран (401), надвишена квота (429), достъп до чужда сесия (403), блокиран prompt injection.  
Файл: `09-sequence-assistant-session-safety.md` — Mermaid source за draw.io import.

```mermaid
sequenceDiagram
    autonumber
    actor U as Потребител
    participant RL as RateLimitMiddleware
    participant C as AssistantController
    participant S as AssistantService
    participant SO as AssistantSessionOrchestrationService
    participant PS as AssistantPromptSafetyService
    participant MR as AssistantMessageRepository
    participant DB as База данни

    U->>+RL: POST /api/assistant/chat { sessionId?, message, history[] }
    Note over RL: TokenBucket: 30 токена/мин, без опашка

    alt Квотата е надвишена
        RL-->>U: 429 Too Many Requests (Retry-After: N сек.)
    end

    RL->>+C: forward(request)
    Note over C: [Authorize] — извличане на userId от JWT claims

    alt userId не е намерен
        C-->>U: 401 Unauthorized
    end

    C->>+S: generateReplyAsync(request, userId)

    %% 1. Сесия
    S->>+SO: getOrCreateSessionAsync(sessionId?, message, userId)
    alt Нова сесия (sessionId = null)
        SO->>+DB: INSERT AssistantChatSession (GUID, userId)
        DB-->>-SO: session
    else Съществуваща сесия
        SO->>+DB: SELECT AssistantChatSession WHERE SessionId = sessionId
        DB-->>-SO: session | null
        alt Сесията принадлежи на друг потребител
            SO-->>S: AccessDeniedException
            S-->>C: 403 Forbidden
            C-->>U: 403 Forbidden
        end
    end
    SO-->>-S: AssistantChatSession

    %% 2. История
    S->>+MR: getRecentMessagesAsync(sessionInternalId, limit: 20)
    MR->>+DB: SELECT TOP 20 AssistantChatEntries ORDER BY CreatedAt DESC
    DB-->>-MR: AssistantChatEntry[]
    MR-->>-S: history: AssistantChatEntry[]

    %% 3. Проверка за prompt injection
    S->>+PS: isPotentialPromptInjection(message)
    PS->>PS: matchInjectionPatterns(message)
    Note over PS: Regex: "ignore instructions",\n"system prompt", "jailbreak", etc.
    PS-->>-S: isInjection: bool

    alt Инжекция открита и PromptInjectionBlockOnDetect = true
        S->>PS: sanitizePrompt(message)
        PS-->>S: sanitizedMessage
        S->>+MR: saveBlockedMessageAsync(session, message)
        MR->>DB: INSERT AssistantChatEntry (role: "assistant", blocked)
        DB-->>MR: OK
        MR-->>-S: OK
        S-->>-C: BlockedReply "Заявката съдържа потенциално опасни инструкции"
        C-->>-RL: 200 OK { reply: blocked message, isBlocked: true }
        RL-->>-U: 200 OK { reply: blocked message, isBlocked: true }
    end

    Note over S: Продължава към извличане на контекст\n(вж. 10-sequence-assistant-retrieval-provenance.md)
    S-->>C: (context retrieval stage)
```
