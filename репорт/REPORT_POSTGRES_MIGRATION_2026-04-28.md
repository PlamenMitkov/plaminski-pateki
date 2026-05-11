# Отчет: Миграция на данни от JSON към PostgreSQL

Дата: 2026-04-28
Проект: EcoProject

## Цел

Локална миграция на данните от `ecoupdated.json` към релационна PostgreSQL база чрез Prisma, без cloud deployment.

## Какво беше направено

1. Подготвена Prisma релационна схема за данните от eco_trails.
2. Добавени свързани таблици за нормализация:
   - Trail
   - TrailLocation
   - TrailDetails
   - TrailTransportation
   - TrailAccessibility
   - TrailSeasonalInfo
   - TrailContactInfo
   - TrailRating
   - TrailMetadata
   - TrailAttraction
3. Създаден seed/import скрипт за четене на `ecoupdated.json` и запис в PostgreSQL.
4. Добавени npm команди за Prisma workflow:
   - prisma:generate
   - prisma:migrate
   - prisma:push
   - prisma:seed
   - db:reset
5. Адаптация за Prisma 7:
   - махнат `url` от `datasource` в schema.prisma
   - добавен Prisma adapter за PostgreSQL в seed скрипта
   - добавен `migrations.seed` в prisma.config.ts
6. Добавени инструкции в Prisma README за локално пускане.

## Редактирани/добавени файлове

- Обновен: `prisma/schema.prisma`
- Добавен: `prisma/seed.mjs`
- Обновен: `package.json`
- Обновен: `prisma.config.ts`
- Добавен: `prisma/README.md`

## Изпълнени команди и резултат

1. `npx prisma validate`  
   - Първоначално: грешка за Prisma 7 datasource `url` в schema.
   - След корекция: успешно.

2. `npm run prisma:generate`  
   - Успешно генериран Prisma Client.

3. `npm run prisma:push`  
   - PostgreSQL база `ecoproject` създадена/синхронизирана.

4. `npm run prisma:seed`  
   - Успешен импорт: `Imported 522 trails from ecoupdated.json`.

## Краен статус

- Локалната релационна база е готова и заредена с данни.
- Не е необходим cloud deployment на този етап.
- API все още е на EF Core/SQL Server (не е превключен към PostgreSQL).

## Следващи възможни стъпки

1. Валидация на импорта с SQL справки (counts по региони/трудност).
2. Синхронизиране на API към PostgreSQL (по желание).
3. Подготовка на deployment checklist за облак, когато е нужно.
