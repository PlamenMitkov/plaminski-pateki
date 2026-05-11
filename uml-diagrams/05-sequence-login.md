# Sequence Diagram: Успешна автентикация (Вход)

Обхват: Сценарий „Потребителят въвежда валидни данни и получава JWT токен".  
Alt-ветви: надвишена квота (429), непознат имейл (401), невалидна парола (401).  
Файл: `05-sequence-login.md` — Mermaid source за draw.io import.

```mermaid
sequenceDiagram
    autonumber
    actor U as Потребител
    participant RL as RateLimitMiddleware
    participant C as AuthController
    participant UM as UserManager
    participant JWT as JwtTokenService

    U->>+RL: POST /api/auth/login { email, password }
    RL->>RL: verifyQuota("auth": 10 req/min)
    alt Квотата е надвишена
        RL-->>U: 429 Too Many Requests (Retry-After: N сек.)
    end
    RL->>+C: forward(request)

    C->>+UM: findByEmailAsync(email)
    UM-->>-C: AppUser | null
    alt Потребителят не е намерен
        C-->>U: 401 Unauthorized "Невалидни данни за вход"
    end

    C->>+UM: checkPasswordAsync(user, password)
    UM-->>-C: isValid: bool
    alt Невалидна парола
        C-->>U: 401 Unauthorized "Невалидни данни за вход"
    end

    C->>+UM: getRolesAsync(user)
    UM-->>-C: roles: string[]

    C->>+JWT: createToken(user, roles)
    JWT->>JWT: encodeClaimsHS256(sub, email, roles, exp)
    JWT-->>-C: jwtToken: string

    C-->>-RL: 200 OK { token, userId, email }
    RL-->>-U: 200 OK { token, userId, email }
```
