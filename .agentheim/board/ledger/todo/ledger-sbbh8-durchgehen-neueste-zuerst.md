---
id: ledger-sbbh8
title: "Durchgehen ordnet neueste zuerst: nach den wartenden Duplikaten kommen die Buchungen nach Datum absteigend, damit nach „Später“ nicht wieder dieselben alten vorne stehen"
status: todo
type: feature
context: ledger
created: 2026-11-20
completed:
depends_on: []
blocks: []
tags: [durchgehen, pruefen, reihenfolge]
related_adrs: []
related_research: []
prior_art: [ledger-v6s7c]
---

## Why
Roman (2026-11-20): „Im Prüfen-Feature sollten neue Transaktionen zuerst kommen, sonst muss man immer wieder durch
die, die man auf später gewählt hat, durch.“ Heute steht die Schlange nach Datum aufsteigend (Entscheidung
2026-11-11, ledger-v6s7c). „Später“ merkt sich nichts, also stehen beim nächsten Öffnen wieder dieselben alten
Buchungen vorne. Roman hat beim Erfassen „Neueste zuerst“ gewählt, „Später“ bleibt ohne Gedächtnis.

## What
`reviewQueueOrder` (`src/Shared/Domain.fs`) ordnet die Schlange des Durchgehens neu:

1. wartende Duplikate zuerst (unverändert, sie halten YNAB auf),
2. danach nach Buchungsdatum absteigend (neueste zuerst),
3. bei gleichem Datum unverändert die kleinere EntryId zuerst.

Innerhalb der wartenden Duplikate gilt dieselbe Datumsrichtung (neueste zuerst). „Später“ bleibt, wie es ist: kein
gespeicherter Zustand, die Buchung kommt beim nächsten Öffnen an ihrer Datumsstelle wieder.

## Acceptance criteria
- [ ] Reine Funktion `reviewQueueOrder`: Fälle in `src/Tests/LedgerReviewQueueTests.fs` auf die neue Richtung
      umgestellt (leer; zwei Daten, neueres zuerst; Gleichstand nach EntryId; wartende vor dem Rest, je neueste
      zuerst). Gegenprobe: mit der alten Richtung `e.Date` aufsteigend wird mindestens ein Fall rot.
- [ ] `getReviewQueue` liefert die Folge über den echten Lesepfad (bestehender Handler-Test nachgezogen, falls er die
      Folge prüft).
- [ ] Kommentare an `reviewQueueOrder` und in `src/Tests/LedgerReviewQueueTests.fs` nennen die neue Richtung.
- [ ] README ledger: der Satz zur Reihenfolge der Schlange nennt „neueste zuerst“ (Roman, 2026-11-20) statt
      „älteste zuerst“; Delta mit `npm run check:readme-delta` geprüft.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.

## Notes
- Keine sichtbare Änderung an Karte oder Layout, nur die Folge. Kein Styleguide-Bezug.
- Dass Duplikate vorne bleiben, war nicht Teil von Romans Wunsch und bleibt deshalb wie entschieden.
- Fahrplan „Stand“ nennt „Durchgehen älteste zuerst“ als Entscheidung vom 2026-11-11; beim Abhaken dort ergänzen,
  dass sie am 2026-11-20 geändert wurde.
