# 15 – C4 Level 1: Диаграма на системния контекст

```mermaid
graph LR
    User["👤 Турист / Потребител\n[Person]"]
    EcoProject["🌿 EcoProject Platform\n[Software System]\n\nИнтелигентна WEB система\nза еко туристически маршрути"]
    Gemini["☁️ Gemini API\n[External System]\nПървичен AI доставчик"]
    OpenAI["☁️ OpenAI API\n[External System]\nРезервен AI доставчик"]

    User -->|"Задава въпроси, разглежда\nмаршрути, управлява профил\n[HTTPS]"| EcoProject
    EcoProject -->|"AI отговори, списък маршрути,\nкарта, персонализирано съдържание"| User
    EcoProject -->|"Обогатени промптове\n(Контекст + Въпрос)\n[REST/gRPC]"| Gemini
    Gemini -->|"Генериран текст (Отговор)"| EcoProject
    EcoProject -->|"Изпраща заявки при срив\n[REST]"| OpenAI
    OpenAI -->|"Резервен AI отговор"| EcoProject

    style User fill:#08427b,color:#fff,stroke:#073b6f
    style EcoProject fill:#1168bd,color:#fff,stroke:#0e5aa5
    style Gemini fill:#999,color:#fff,stroke:#888
    style OpenAI fill:#999,color:#fff,stroke:#888
```

## Описание

**Тип:** C4 Model – Level 1 (System Context)

| Елемент | Тип | Описание |
|---------|-----|----------|
| Турист / Потребител | Person | Краен потребител – регистриран или анонимен посетител |
| EcoProject Platform | Software System | Цялата система – Frontend + Backend + данни |
| Gemini API | External System | Google AI – първичен LLM доставчик (Gemini Flash) |
| OpenAI API | External System | OpenAI – резервен LLM при недостъпност на Gemini |

**Ключови потоци:**
- Потребителят взаимодейства само с EcoProject Platform
- Platform оркестрира комуникацията с AI провайдъри
- Fallback логика: Gemini → OpenAI при HTTP 429/5xx
