---
id: ledger-wj0b8
title: "Einstellungen: Satz unter „Quick-Add-Konto“ lautet „Vorbelegt bei „+ Buchung“ ohne Kontofilter“"
status: done
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

## Outcome

Die Lese-Ansicht der YNAB-Einstellungen beschreibt die Zeile „Quick-Add-Konto“ jetzt mit „Vorbelegt bei „+ Buchung“ ohne Kontofilter“ (`src/Client/Components/Settings/View.fs:149`). Die Anführungszeichen sind typografisch („ “).

Entscheidung zu `src/Client/Components/Settings/View.fs:309`: Das Label „Quick-Add-Konto (z. B. Bar)“ der Kontoauswahl im Bearbeiten-Modus bleibt. Es nennt ein Beispiel für das passende Konto und widerspricht dem neuen Satz nicht. Der neue Satz beschreibt die Wirkung, das Label hilft bei der Auswahl.

Kein Test, keine Playwright-Fixture und kein README-Satz zitierte den alten Satz (Suche über `src`, `scripts`, `standards` und die Ledger-README). Deshalb wurde nichts nachgezogen und kein neuer Test geschrieben (Doktrin Punkt 6).

`./verify.sh`: `VERIFY: PASS` (dotnet 5009 grün, node 340 grün, beide Builds und beide Wächter grün).

Verifier: PASS (Iteration 1).
