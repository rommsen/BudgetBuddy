---
id: ledger-hg5m5
title: "Regel-Angebot nach einer Kategorie-Änderung im Kassenbuch: Anbieten, aus Payee und neuer Kategorie eine Kategorisierungsregel anzulegen"
status: backlog
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [design-system-001, ledger-sfab0]
blocks: []
tags: [frontend, kassenbuch, kategorie, regeln, entwurf]
related_adrs: [0055]
related_research: []
prior_art: [ledger-k2rbn]
---

## Why
Wer eine Kategorie von Hand ändert, wiederholt das oft für denselben Payee. Roman hat am
2026-10-24 entschieden: kein Regel-Angebot in der Scheibe "Kategorie ändern", aber als
eigenen Entwurf erfassen.

## What
Grobe Form, zu schärfen:
1. Nach einer erfolgreichen Änderung einer Buchung mit Payee bietet das Kassenbuch an
   ("Regel für <Payee> anlegen?"), im Toast oder in der Zeile des Details.
2. Das Angebot ruft den bestehenden Regel-Weg (`RulesApi.createRule`, BC `categorization`)
   mit Muster aus dem Payee (Contains) und der neuen Kategorie; Roman sieht und bestätigt
   das Muster vor dem Anlegen.
3. Fragen: Regeln wirken nur auf neue Importe (`categorization`), nicht auf Kassenbuch-
   Buchungen; soll das Angebot anzeigen, wie viele vorhandene Buchungen betroffen wären?
   Nicht bei Sammelaktionen? Doppelte Regeln erkennen?

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Entworfen am 2026-10-24 (Sammel-Modeling) als Scheibe (f). Ziel-Lage: backlog. Context
`ledger`, weil Auslöser und Oberfläche im Kassenbuch liegen; `categorization` liefert den
unveränderten Regel-Weg. Dateien: Ledger-Client `Types.fs`, `State.fs`, `View.fs`, ggf.
`src/Client/State.fs` (Brücke zu `RulesMsg`). Überschneidung mit `ledger-sfab0` und `-3`.
