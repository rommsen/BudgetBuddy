---
id: ledger-k9tq7
title: Kassenbuch lädt fehlende Settings nach, statt beim Laden der Kategorien still aufzugeben (sonst bleibt der Kategorie-Picker leer bis zum Neuladen)
status: backlog
type: bug
context: ledger
created: 2026-10-24
completed:
depends_on: []
blocks: []
tags: [captured, kategorie, picker]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Live-Befund 2026-10-24: Am PC zeigte der Kategorie-Picker im Kassenbuch-Detail nur "Ohne
Kategorie", ohne Kategorien und ohne Fußzeile "Stand …". Die Kategorien wurden in dieser
Sitzung nie geladen, weil das Laden der Settings beim Seitenaufruf gescheitert war (siehe
infrastructure-y6atr).

## What
`LoadCategories` im Kassenbuch gibt still auf, wenn die Settings nicht `Success` sind
(`src/Client/State.fs`, Zweig `LedgerMsg LoadCategories`, `| _ -> model, Cmd.none`). Danach
kommen nie Kategorien, auch nicht über `EnsureCategoriesFresh` beim Öffnen des Pickers.
Fix: fehlende Settings nachladen und die Kategorien danach holen. Mit Regressionstest.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-24: raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Der bestehende Nachhol-Weg in `syncCategoriesCmd` (nach `SettingsLoaded`)
greift nur, wenn `Ledger.Categories = NotAsked` und die Settings später doch noch ankommen.
