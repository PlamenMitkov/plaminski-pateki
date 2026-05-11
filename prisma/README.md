# Prisma + PostgreSQL setup

This project can ingest trail data from `ecoupdated.json` into PostgreSQL using Prisma.

## 1) Configure database connection

Set `DATABASE_URL` in your `.env`:

`postgresql://USER:PASSWORD@localhost:5432/ecoproject?schema=public`

## 2) Generate Prisma client

```bash
npm run prisma:generate
```

## 3) Create tables

Use migrations (recommended):

```bash
npm run prisma:migrate
```

Or push schema directly without creating migration history:

```bash
npm run prisma:push
```

## 4) Import trails from JSON

Default source file is `ecoupdated.json` in project root:

```bash
npm run prisma:seed
```

Optional custom input file:

```bash
ECO_JSON_PATH=./eco.json npm run prisma:seed
```

PowerShell example:

```powershell
$env:ECO_JSON_PATH = "./eco.json"
npm run prisma:seed
```

## Notes

- The seed process clears existing rows in `Trail` before importing.
- Relations are normalized into separate tables: location, details, transportation, accessibility, seasonal info, contact info, rating, metadata, and attractions.
