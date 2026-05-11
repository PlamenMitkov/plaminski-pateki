# Sequence Diagram: Регистрация на потребител

Обхват: Сценарий „Нов потребител се регистрира и получава JWT токен".  
Alt-ветви: надвишена квота (429), дублиран имейл (409), слаба парола (400).  
Файл: `06-sequence-register.md` — Mermaid source за draw.io import.

```mermaid
sequenceDiagram
    autonumber
    actor U as Потребител
    participant RL as RateLimitMiddleware
    participant C as AuthController
    participant UM as UserManager
    participant JWT as JwtTokenService
    participant DB as База данни

    U->>+RL: POST /api/auth/register { email, password }
    RL->>RL: verifyQuota("auth": 10 req/min)
    alt Квотата е надвишена
        RL-->>U: 429 Too Many Requests (Retry-After: N сек.)
    end
    RL->>+C: forward(request)

    C->>+UM: findByEmailAsync(email)
    UM->>+DB: SELECT WHERE Email = email
    DB-->>-UM: AppUser | null
    UM-->>-C: AppUser | null
    alt Имейлът вече съществува
        C-->>U: 409 Conflict "Потребител с този имейл вече съществува"
    end

    C->>+UM: createAsync(newUser, password)
    Note over UM: Хешира паролата (PBKDF2)\nи валидира изискванията
    UM->>+DB: INSERT INTO AspNetUsers
    DB-->>-UM: IdentityResult
    UM-->>-C: IdentityResult
    alt Невалидна парола (IdentityResult.Failed)
        C-->>U: 400 Bad Request [ IdentityError[] ]
    end

    C->>+UM: getRolesAsync(newUser)
    UM-->>-C: roles: string[]

    C->>+JWT: createToken(newUser, roles)
    JWT->>JWT: encodeClaimsHS256(sub, email, roles, exp)
    JWT-->>-C: jwtToken: string

    C-->>-RL: 200 OK { token, userId, email }
    RL-->>-U: 200 OK { token, userId, email }
```
