---
id: ledger-r777c
title: "Prüfvermerk in BB setzen und nach YNAB zurückschreiben (die eine schreibende Fähigkeit vor v2)"
status: backlog
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-kdnnz, design-system-001]
blocks: []
tags: [frontend, backend, kassenbuch, schreibend, scheibe]
related_adrs: [0028, 0012, 0021]
related_research: []
prior_art: [ledger-qvt84, ledger-tdnpk]
---

## Why
Romans häufigster Ablauf am Handy: neue Buchungen durchsehen und jede als geprüft
markieren. Das ist der Grund, aus dem er die YNAB-App öffnet. Solange das Kassenbuch ihn
nicht übernimmt, bleibt es eine Sicht, die er nicht braucht.

**ADR 0028** erklärt den Prüfvermerk zur einzigen schreibenden Fähigkeit, die vor v2 nach
BB wandert: ein Feld, idempotent, ohne Budget-Wirkung, mit trivialer Konfliktauflösung.
Kategorie, Split und Lena-Zuweisung bleiben v2.

In YNAB ist der Ablauf umständlich: die Übersicht "Transactions to Review" kann nur
approve, ein zweiter Wisch nimmt es wieder zurück. Für den vollen Vermerk muss Roman ins
Konto wechseln und dort zweimal wischen. Dieselbe Geste bedeutet an zwei Orten
Verschiedenes. Im Kassenbuch soll **eine** Geste genügen.

## What
1. **Schreibender Weg:** BB setzt und entfernt den Prüfvermerk einer Buchung, schreibt ihn
   als Ereignis in die Historie (Actor `UserInBb`) und **zurück nach YNAB**. Der
   Rückschreibe-Weg ist auf dieses eine Feld zugeschnitten, kein allgemeiner Update-Weg
   (ADR 0028, Punkt 1).
2. **Geste:** Wisch nach rechts am Handy (`Swipe.SwipeableRow`), Taste G oder die
   Auswahl-Leiste am PC. Genau eine Geste, keine zweite Stufe. Wisch nach links bleibt
   frei.
3. **Rückmeldung:** die Zeile verlässt die Liste, wenn der Chip "Zu prüfen" aktiv ist;
   Toast mit "Rückgängig". Der Chip zählt herunter.
4. **Mehrfach:** Aktion "Geprüft" in der Auswahl-Leiste setzt den Vermerk für die ganze
   Auswahl.
5. **Konflikt:** weicht YNAB ab, gewinnt der jüngere Stand, ohne Abweichungs-Sicht (ADR
   0028, Punkt 3).
6. **Scheitert der Push**, bleibt der Vermerk in BB stehen und die Buchung trägt "noch
   nicht in YNAB" mit Grund, wie beim Import (ADR 0021). Kein Rollback.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12 nach der Entscheidung in ADR 0028. Brief:
`docs/ux/kassenbuch/briefs.md`, Ablauf 1.

Setzt `ledger-kdnnz` voraus: ohne den beobachteten Prüfvermerk gibt es keinen Zustand, den
BB ändern könnte, und keinen Chip "Zu prüfen".

Beim Modellieren festzurren: ob "geprüft" technisch YNABs `approved` ist. `cleared` trägt
für Roman keine Information, weil importierte Buchungen immer schon bei der Bank sind.
