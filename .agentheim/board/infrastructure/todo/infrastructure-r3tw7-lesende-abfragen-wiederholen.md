---
id: infrastructure-r3tw7
title: "Alle übrigen lesenden Abfragen laufen über Retry.reading, damit ein Verbindungsabbruch im Kassenbuch keine Fehlerseite mehr zeigt"
status: todo
type: bug
context: infrastructure
created: 2026-10-31
completed:
depends_on: []
blocks: []
tags: [retry, netzwerk, firefox, kassenbuch, regression]
related_adrs: []
related_research: []
prior_art: [infrastructure-y6atr, infrastructure-awstw]
---

## Why

Roman, 2026-10-31: Auch im Kassenbuch erscheinen ständig Fehlerseiten, die nach dem Tipp auf "Erneut versuchen" sofort weg sind. infrastructure-y6atr hat nur vier Start-Abfragen geschützt, die Buchungsliste selbst nicht.

## What

Diese lesenden Aufrufe laufen über `Retry.reading`:

- `src/Client/Components/Ledger/State.fs`: `getLedgerAccountBalances`, `getLedgerEntriesPage` (zwei Stellen), `getEntryHistory`, `getLedgerPayeeSuggestions`, `getPayeeCategorySuggestions`, `getBudgetAccounts`, `getSettings`, `getLedgerEntry`
- `src/Client/State.fs`: `getCategories`, `getRecentQuickAddTemplates`
- `src/Client/Components/Rules/State.fs`: `getAllRules`

Schreibende Aufrufe bleiben ungeschützt. `SyncFlow/State.fs` gehört zu ynab-sync-k3vq8.

## Acceptance criteria

- [ ] Kaputt-Szenario, GIVEN der erste `getLedgerEntriesPage`-Aufruf bricht ab (`route.abort`, Status 0), WHEN das Kassenbuch lädt, THEN erscheint die Liste ohne Fehlerseite. Der Browser-Wächter ist vor dem Fix rot und nennt im Kommentar diesen Bug.
- [ ] Intakt-Szenario: Scheitern alle drei Versuche, erscheint die Fehlerseite mit "Erneut versuchen" wie bisher.
- [ ] Alle oben genannten Aufrufe laufen über `Retry.reading`, kein schreibender Aufruf.
- [ ] `.agentheim/knowledge/contexts/infrastructure/README.md`, Bullet "Lesende Abfrage mit Wiederholung", nennt den neuen Umfang.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes

Die Ursache der Abbrüche klärt infrastructure-g4wy8. Dieser Task lindert die Wirkung und hängt nicht daran.
