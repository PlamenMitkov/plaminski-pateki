# Sequence Diagram: Създаване на публикация в общността (с качване на изображения)

Обхват: Сценарий „Автентикиран потребител публикува пост с изображения; системата валидира и записва файловете".  
Alt-ветви: неавторизиран (401), твърде голям файл (400), твърде много изображения (400), невалиден тип файл (400), невалиден TrailId (400).  
Файл: `12-sequence-community-post-create.md` — Mermaid source за draw.io import.

```mermaid
sequenceDiagram
    autonumber
    actor U as Потребител
    participant C as CommunityPostsController
    participant FS as FileSystem (wwwroot)
    participant DB as AppDbContext

    U->>+C: POST /api/communityposts/mine (multipart/form-data)\n{ title, content, trailId?, postType, images[] }
    Note over C: [Authorize] — извличане на userId от JWT claims

    alt userId не е намерен
        C-->>U: 401 Unauthorized
    end

    C->>C: validateTitle(title: 4–180 chars)
    C->>C: validateContent(content: 12–6000 chars)
    alt Невалидни полета
        C-->>U: 400 Bad Request (ValidationError[])
    end

    C->>C: validateImages(images[])
    alt Брой изображения > 4
        C-->>U: 400 Bad Request "Може да качиш до 4 снимки"
    end
    loop За всяко изображение
        C->>C: checkContentType(image)
        alt Невалиден тип (не JPG/PNG/WEBP)
            C-->>U: 400 Bad Request "Разрешени са само JPG, PNG и WEBP"
        end
        C->>C: checkSize(image)
        alt Размер > 7 MB
            C-->>U: 400 Bad Request "Всяка снимка трябва да е до 7MB"
        end
    end

    alt trailId е подаден
        C->>+DB: Trails.findAsync(trailId)
        DB-->>-C: Trail | null
        alt Trail не е намерен
            C-->>U: 400 Bad Request "Невалиден TrailId"
        end
    end

    C->>+DB: CommunityTrailPosts.addAsync(newPost)
    Note over DB: PostType, ProposalStatus=Pending (ако е TrailProposal)
    DB-->>-C: savedPost

    loop За всяко изображение
        C->>+FS: saveFileAsync("wwwroot/uploads/community/{userId}/{guid}.ext")
        FS-->>-C: imageUrl: string
        C->>+DB: CommunityTrailPostImages.addAsync({ postId, imageUrl, storagePath })
        DB-->>-C: OK
    end

    C->>+DB: saveChangesAsync()
    DB-->>-C: OK

    C-->>-U: 200 OK { id, title, content, postType, images[], createdAtUtc }
```
