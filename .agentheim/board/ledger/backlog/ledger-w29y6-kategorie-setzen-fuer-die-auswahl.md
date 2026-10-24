---
id: ledger-w29y6
title: "Kategorie setzen für die Auswahl: Sammel-Befehl mit einem Sammel-PATCH nach YNAB, Schaltfläche Kategorie setzen in der Auswahl-Leiste mit demselben Picker, Toast mit Zahl der geänderten und übersprungenen Buchungen"
status: backlog
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [design-system-001, ledger-k2rbn, ledger-w4vte, design-system-gffps, ledger-sfab0]
blocks: []
tags: [frontend, backend, kassenbuch, kategorie, v2, auswahl]
related_adrs: [0050, 0053, 0055]
related_research: []
prior_art: [ledger-nhwrt, ledger-r1d2f, ledger-th36h]
---

## Why
Einstieg für viele Buchungen (ADR 0053, Punkt 2): "Kategorie setzen" in der Auswahl-Leiste,
mit demselben Picker wie im Detail. Muster: "Geprüft für die Auswahl" (`ledger-nhwrt`).

## What
1. **Befehl** `setEntryCategoryBatch: ReviewMarkBatchSelection -> YnabCategoryId option -> Async<Result<CategoryBatchOutcome, string>>`.
   Die Auswahl löst `resolveReviewMarkBatchSelection` auf (ADR 0051, Punkt 3, sortierungsfrei);
   der Name der Auswahl-Union und der Auflösung bleibt, ein Umbenennen ist nicht Teil dieser Task.
2. **Je Buchung ein `EntryRecategorized`** (Actor `UserInBb`) für jede, deren Ziel vom Ist
   abweicht. **Übersprungen** (mit Grund gezählt, kein Ereignis): in YNAB gelöscht, mit
   Split-Zeilen, Übertrag ohne Kategorie, Ziel gleich Ist. Ohne YNAB-Verknüpfung: Ereignis
   plus Fehlschlag wie im Einzelweg.
3. **Ein Sammel-PATCH** (`YnabClient.updateCategoryBatch`, `PATCH .../transactions`,
   je Buchung `id` und `category_id`). Ein Fehler trifft den ganzen Stapel: je geänderter
   Buchung `EntryCategorizationWriteBackFailed` mit Baseline. Von YNAB nicht als gespeichert
   genannte Ids werden einzeln als Fehlschlag geführt (wie `reviewMarkNotSavedReason`).
   Dieselbe Schreib-Sperre wie im Einzelweg.
4. **Leiste.** Neben "Geprüft" steht "Kategorie setzen" (Leiste am PC, Leiste am Handy im
   Auswahl-Modus). Es öffnet den Picker (Popover am PC, Sheet am Handy); die Auswahl bleibt
   während der Wahl bestehen. Nach Erfolg endet die Auswahl.
5. **Toast:** "n Buchungen nach <Kategorie> verschoben" und, falls nötig, ", m übersprungen
   (Split, Übertrag, gelöscht)"; Warnung bei Fehlschlägen. Die `OrderStale`-Regel aus
   `ledger-sfab0` gilt wie im Einzelweg, sobald eine geladene Zeile ihren Platz oder den
   Filter verlässt. Available wird nicht fortgeschrieben (zu viele Buchungen, verschiedene
   Monate); der Picker lädt beim nächsten Öffnen nach (`ledger-kwwxz`, Stand älter als zwei
   Minuten) und der Client stößt nach dem Sammel-Befehl einen Abruf an.

## Acceptance criteria
- [ ] Handler-Test: `BatchPicked` mit drei Buchungen, eine gleich Ziel, eine mit Splits:
      genau ein Ereignis je geänderter Buchung, ein einziger Sammel-PATCH mit den geänderten,
      Antwort zählt geändert und übersprungen mit Grund.
- [ ] Handler-Test: `BatchAllExcept` löst den ganzen Treffer ohne Ausnahmen auf, auch nie
      geladene Buchungen, mit `LedgerSort.defaultSort` (ADR 0051).
- [ ] Handler-Test: Sammel-PATCH scheitert: je geänderter Buchung steht ein Fehlschlag mit
      Baseline im Log, die Kategorien bleiben in BB; nicht genannte Ids werden einzeln
      Fehlschlag.
- [ ] JSON-Vertragstest `buildCategoryBatchBody` (`id`, `category_id`, `null` für ohne
      Kategorie) und PATCH-Test mit Fake-`HttpClient`.
- [ ] Reducer-Test: "Kategorie setzen" ist nur bei nicht leerer Auswahl wirksam und ruft den
      Befehl mit der Auswahl; nach Erfolg endet die Auswahl (auch `SelectionMode`), Zeilen
      tragen die neue Kategorie, `OrderStale` wie im Einzelweg.
- [ ] Ansichts-Test: Die Leiste rendert beide Sammelaktionen am PC und am Handy; der Toast
      nennt Zahl und übersprungene Buchungen.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run test:scripts` grün. Die BC-README ist nachgezogen.

## Notes
Entworfen am 2026-10-24 (Sammel-Modeling) als Scheibe (d). Ziel-Lage: backlog (wartet auf
`ledger-w4vte`, `ledger-sfab0`). Dateien: Shared `Domain.fs`/`Api.fs`, `src/Server/Api.fs`,
`YnabClient.fs`, `Ledger/CommandHandlers.fs`, `Ledger/Events.fs` (nur Nutzung), Client
`Ledger/Types.fs`, `State.fs`, `View.fs`, `src/Client/State.fs`. Überschneidung mit
`ledger-k2rbn` (Server), `ledger-sfab0` und `ledger-w4vte` (Client).
