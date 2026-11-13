---
id: ledger-ay81j
title: "YNAB gleicht Barbuchungen aus BB (Bob, Quick Add, Neue Buchung) mit älteren von Hand erfassten Buchungen gleichen Betrags ab und verschluckt so echte Zahlungen"
status: backlog
type: bug
context: ledger
created: 2026-11-13
completed:
depends_on: []
blocks: []
tags: [captured, money, ynab-sync]
related_adrs: [0021, 0066]
related_research: []
prior_art: [ledger-xqqnv, ledger-nkwqb]
---

## Why
Probelauf zu ledger-nkwqb (2026-11-13): Eine Barbuchung von Bob (50 €) wurde von YNAB mit einer eine Woche älteren, von
Hand erfassten Buchung gleichen Payees und Betrags abgeglichen. In YNAB fehlte danach eine echte Zahlung, der Bar-Stand war
zu hoch. Ursache: BB schickt Barbuchungen mit `import_id`; YNAB gleicht jede Buchung mit `import_id` wie einen Bank-Import
gegen von Hand erfasste Buchungen gleichen Betrags der letzten Tage ab. Roman: „Ich verstehe überhaupt nicht, warum YNAB
die zusammenlegt, wenn die von mehreren Tagen kommt. Das macht gar keinen Sinn.“ Roman hat den Abgleich von Hand
aufgehoben. Wiederkehrende Barzahlungen gleicher Höhe (etwa wöchentlich) treffen das immer wieder.

## What
Offen, beim Verfeinern klären:
- BB schickt in BB erfasste Buchungen so, dass YNAB nicht abgleicht (etwa ohne `import_id`). Das berührt die Absicherung
  einer verlorenen Antwort über die `import_id` (ledger-xqqnv, 409 heißt angekommen) und ADR 0021.
- Oder BB erkennt einen solchen Abgleich (Beobachtung mit `matched_transaction_id`, ledger-nnd2f lehnt die
  Zusammenlegung ab) und zeigt die Buchung als „in YNAB verschluckt“, mit einem Weg, sie neu zu senden.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured raw, unrefined. Geld betroffen.
