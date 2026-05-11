# Activity Diagram: Оркестрация на AI доставчик с многостепенен Fallback

Обхват: Сценарий „Системата избира AI доставчик, изпраща промпт и прилага многостепенна fallback стратегия при грешки".  
Нива: Gemini (основен) → OpenAI (резервен) → Secondary OpenAI модел → Локален детерминистичен отговор.  
Файл: `11-activity-ai-fallback-orchestration.md` — Mermaid source за draw.io import.

```mermaid
flowchart TD
    START([Начало: контекст и промпт са готови]) --> RESOLVE_MODE

    RESOLVE_MODE["resolveAssistantMode()\ncontext_prompt | current"]
    RESOLVE_MODE --> SHADOW_CHECK

    SHADOW_CHECK{PromptTemplateShadowMode = true?}
    SHADOW_CHECK -- Да --> FORCE_CURRENT["Принуди режим: current\n(shadow monitoring)"]
    SHADOW_CHECK -- Не --> BUILD_PROMPT
    FORCE_CURRENT --> BUILD_PROMPT

    BUILD_PROMPT["buildSystemInstruction(mode)\nbuildUserPromptByMode(mode, context, weather, trails)"]
    BUILD_PROMPT --> TRY_PRIMARY

    subgraph PRIMARY["Основен доставчик"]
        TRY_PRIMARY["sendChatRequestAsync(provider, model, prompt)"]
        TRY_PRIMARY --> PRIMARY_OK{Отговорът е успешен?}
        PRIMARY_OK -- Да --> GOT_REPLY
        PRIMARY_OK -- Не --> PRIMARY_ERR["AiProviderException\n(404 | 429 | 503 | 5xx | timeout)"]
    end

    PRIMARY_ERR --> FALLBACK1_CHECK

    subgraph FALLBACK1["Fallback 1: Secondary OpenAI модел"]
        FALLBACK1_CHECK{provider=openai И\nShouldFallbackToSecondaryModel?}
        FALLBACK1_CHECK -- Да --> TRY_SECONDARY["sendChatRequestAsync(fallbackModel, prompt)"]
        TRY_SECONDARY --> F1_OK{Успешен?}
        F1_OK -- Да --> GOT_REPLY
        F1_OK -- Не --> FALLBACK2_CHECK
        FALLBACK1_CHECK -- Не --> FALLBACK2_CHECK
    end

    subgraph FALLBACK2["Fallback 2: Gemini → OpenAI"]
        FALLBACK2_CHECK{provider=gemini И\nShouldFallbackToOpenAiFromGemini?}
        FALLBACK2_CHECK -- Да --> TRY_OPENAI["sendChatRequestAsync(openai, model, prompt)"]
        TRY_OPENAI --> F2_OK{Успешен?}
        F2_OK -- Да --> GOT_REPLY
        F2_OK -- Не --> FALLBACK3_CHECK
        FALLBACK2_CHECK -- Не --> FALLBACK3_CHECK
    end

    subgraph FALLBACK3["Fallback 3: Context Prompt → Current режим"]
        FALLBACK3_CHECK{mode=context_prompt И\nPromptTemplateFailOpen = true?}
        FALLBACK3_CHECK -- Да --> REBUILD_CURRENT["Сглобяване на промпт в режим: current"]
        REBUILD_CURRENT --> TRY_CURRENT["sendChatRequestAsync(provider, model, current_prompt)"]
        TRY_CURRENT --> F3_OK{Успешен?}
        F3_OK -- Да --> GOT_REPLY
        F3_OK -- Не --> LOCAL_FALLBACK
        FALLBACK3_CHECK -- Не --> LOCAL_FALLBACK
    end

    subgraph FALLBACK4["Fallback 4: Локален детерминистичен отговор"]
        LOCAL_FALLBACK["buildLocalFallbackReply(prompt, trails, alternatives, weather)\nШаблонен отговор без AI"]
    end

    LOCAL_FALLBACK --> GOT_REPLY

    GOT_REPLY(["aiReply: string"]) --> EMPTY_CHECK
    EMPTY_CHECK{Отговорът е празен\nИ FailOpen = true?}
    EMPTY_CHECK -- Да --> REBUILD_CURRENT
    EMPTY_CHECK -- Не --> FORMAT

    FORMAT["formatAssistantReply(reply, trails, alternatives)\nNormalize markdown + append trail links"]
    FORMAT --> PERSIST

    PERSIST["saveConversationTurnAsync(session, userMessage, assistantReply)"]
    PERSIST --> END([Край: AssistantChatResponse към клиента])

    style LOCAL_FALLBACK fill:#fff4df,stroke:#a66a00
    style PRIMARY_ERR fill:#ffeded,stroke:#c0392b
    style END fill:#e8f2ff,stroke:#2a4b8d
```
