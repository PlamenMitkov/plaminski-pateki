# Elevation Source Priority

Use this order when filling `manual_elevation_gain_m` in `scripts/elevation-trusted-worklist.csv`.

## 1) Official / institutional pages (highest trust)
- Municipality tourism pages (e.g. `*.bg` municipality domains)
- Nature park / protected area official pages
- `teteven.bg`, `opoznai.bg` trail pages with explicit denivelation text

## 2) Structured trail platforms (good trust, verify route match)
- `wikiloc.com` (route details often include elevation gain)
- `komoot.com` (ascent field)
- `alltrails.com` (elevation gain field)
- `bgmountains.org` (when route matches exactly)

## 3) Community/blog pages (review required)
- Travel blogs, forums, media posts
- Use only when trail name, location, and route length clearly match

## Search pattern
- `<trail name> денивелация`
- `<trail name> elevation gain`
- `site:opoznai.bg <trail name>`
- `site:wikiloc.com <trail name>`
- `site:komoot.com <trail name>`
- `site:alltrails.com <trail name>`

## Input rules
- Fill both columns:
  - `manual_elevation_gain_m`
  - `manual_elevation_source` (exact URL)
- Prefer values in meters (`m`).
- If multiple sources disagree, choose the official one.
- If uncertain, leave empty.

## Apply command
- Dry run:
  - `./scripts/apply-elevation-overrides.ps1 -InputPath eco.json -OverridesCsvPath scripts/elevation-trusted-worklist.csv -StrictValidation`
- Persist:
  - `./scripts/apply-elevation-overrides.ps1 -InputPath eco.json -OverridesCsvPath scripts/elevation-trusted-worklist.csv -StrictValidation -Apply`
