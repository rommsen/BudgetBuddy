---
id: ledger-wj0b8
title: "Einstellungen: Satz unter „Quick-Add-Konto“ lautet „Vorbelegt bei „+ Buchung“ ohne Kontofilter“"
status: todo
type: chore
context: ledger
created: 2026-11-14
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [einstellungen, microcopy, frontend]
related_adrs: []
related_research: []
prior_art: [ledger-ea70s]
---

## Why
Seit ledger-ea70s gibt es Quick Add nicht mehr als Seite. Der Satz unter der Einstellung „Quick-Add-Konto“ sagt
„Konto für manuell erfasste Transaktionen (z. B. Bar)“ und beschreibt die Wirkung nicht mehr.

## What
Entschieden (Roman, 2026-11-14, wörtlich): Der Satz lautet „Vorbelegt bei „+ Buchung“ ohne Kontofilter“. Der Name
„Quick-Add-Konto“ bleibt.

## Acceptance criteria
- [ ] `src/Client/Components/Settings/View.fs:149`: die Beschreibung der Zeile „Quick-Add-Konto“ ist „Vorbelegt bei
      „+ Buchung“ ohne Kontofilter“.
- [ ] `src/Client/Components/Settings/View.fs:309` („Quick-Add-Konto (z. B. Bar)“) ist geprüft: passt es zum neuen Satz,
      bleibt es, sonst wird es angeglichen; das Outcome nennt die Entscheidung.
- [ ] Ein vorhandener Test oder Fixture, der den alten Satz erwartet, ist nachgezogen; kein neuer Test nur für den Text
      (Doktrin Punkt 6).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
