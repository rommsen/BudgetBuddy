---
id: ledger-be1yh
title: "Seitenversatz verrutscht, wenn bei aktivem Chip Zu prüfen eine Zeile die Liste verlässt"
status: done
type: bug
context: ledger
created: 2026-10-19
depends_on: [ledger-r777c]
blocks: []
tags: [frontend, kassenbuch]
related_adrs: [0050]
related_research: []
prior_art: [ledger-r777c]
---

## Why
Mit `ledger-r777c` verlässt eine Zeile bei aktivem Chip "Zu prüfen" die geladene Liste, sobald
sie als geprüft markiert ist (`Components.Ledger.State.applyReviewMarkResult`). Die Liste lädt
seitenweise über `NextPage`. Der Server zählt den Versatz auf der gefilterten Menge neu: nach dem
Entfernen einer Zeile aus einer schon geladenen Seite rutscht die erste Zeile der nächsten Seite
um eins nach vorn, und die Buchung, die vorher an der Seitengrenze stand, wird beim Nachladen
übersprungen.

## What
Beim Nachladen den Versatz an die Zahl der lokal entfernten Zeilen anpassen (oder die nächste
Seite über einen Anker statt über einen Seitenzeiger holen), sodass keine Buchung ausgelassen
wird. Betrifft nur den Fall mit aktivem Chip und mehr als einer geladenen Seite.

## Acceptance criteria
- [ ] Reducer-Test: zwei geladene Seiten, aktiver Chip, eine Zeile der ersten Seite wird geprüft,
      die nächste Nachladeanfrage wird so gestellt, dass keine Buchung fehlt. Der Test
      scheitert vor dem Fix und trägt einen Kommentar, welchen Fehler er verhindert
      (Bug-Fix-Protokoll in CLAUDE.md).
- [ ] Ohne aktiven Chip und ohne lokal entfernte Zeilen bleibt die Nachladeanfrage wie bisher
      (Regressionstest).
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Entdeckt beim Umsetzen von `ledger-r777c`. In der Praxis selten: braucht mehr als 100 zu prüfende
Buchungen in einem Filter.

Befund bei der Verfeinerung (2026-10-19): die Liste lädt über eine Seitennummer
(`NextPage = page + 1`, `src/Client/Components/Ledger/State.fs`), `applyReviewMarkResult`
entfernt die Zeile lokal und senkt `TotalCount`. Dieselbe Verschiebung tritt später auch
bei `ledger-th36h` (Wisch) und `ledger-nhwrt` (Stapel) auf; der Fix sollte an der
Nachladeanfrage ansetzen, nicht an einer einzelnen Aktion.

## Outcome

Der Seitenversatz beim Nachladen stimmt jetzt auch, wenn Zeilen lokal aus der geladenen Liste verschwunden sind. `LedgerEntryPageQuery` hat ein neues Feld `LocallyRemoved` (Default 0 = bisheriges Verhalten). Der Server beginnt die Seite bei `max 0 (Page * PageSize - LocallyRemoved)`; `Persistence.loadEntriesPage` nimmt dafür einen Versatz statt eines Seitenindex. Im Client zählt das neue Modellfeld `LocallyRemoved` (direkt nach `NextPage`) jede Zeile hoch, die `applyReviewMarkResult` aus der Liste nimmt, und `resetEntries` setzt es zurück. Die Anfrage entsteht in der neuen reinen Funktion `State.buildPageQuery`. Spätere Aktionen, die Zeilen lokal entfernen (Wisch in th36h, Stapel in nhwrt), müssen nur `LocallyRemoved` erhöhen.

Schlüsseldateien: src/Client/Components/Ledger/State.fs, src/Client/Components/Ledger/Types.fs, src/Shared/Domain.fs, src/Server/Ledger/QueryHandlers.fs, src/Server/Ledger/Persistence.fs, src/Tests/LedgerPagingTests.fs (neu, 6 Tests: 5 Client, 1 Server).

Zahlen: `dotnet build` 0 Warnungen, 0 Fehler; `dotnet test` 1391 bestanden, 0 Fehler, 7 übersprungen (YNAB-Integration); `npm run build` grün; `npm run guard:classes` grün; `npm run test:scripts` 134/134 (ein erster Lauf scheiterte am Warten auf data-app-ready, der Wiederholungslauf war grün).
