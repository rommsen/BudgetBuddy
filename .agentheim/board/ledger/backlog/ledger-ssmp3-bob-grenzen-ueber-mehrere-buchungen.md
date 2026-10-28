---
id: ledger-ssmp3
title: "Bobs Grenzen gelten nur pro Buchung: Datumsfenster, Tagesgrenze nach Summe und Anzahl, Meldung je Bob-Commit (Security-Review M1)"
status: backlog
type: feature
context: ledger
created: 2026-10-28
completed:
depends_on: []
blocks: []
tags: [captured, security, bob]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Ein per Prompt-Injection gesteuerter Bob kann beliebig viele Buchungen bis 500 € anlegen, auch als Einnahme und bis zu fünf Jahre rückdatiert.

## What
Heute prüft BB nur jede einzelne Buchung (Obergrenze, Konto). Das Datum darf bis fünf Jahre zurückliegen (`Validation.fs`, `AddYears(-5)`).

Vorschläge aus dem Review:
- Datumsfenster von etwa 31 Tagen.
- Tagesgrenze nach Summe und nach Anzahl der Commits.
- ntfy-Meldung je Bob-Commit.
- Eventuell `flag_color` für Buchungen mit Herkunft Bob.
- Prüfen, ob YNAB Bob-Buchungen ohne `approved` als unbestätigt anlegt (`buildManualTransactionBodyWithFlag` in `YnabClient.fs`, ab ca. Zeile 1051).
- ADR 0066 (Folgen, ca. Zeile 42) und ADR 0067 (ca. Zeile 34) klingen enger, als der Code ist; anpassen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-28, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Security-Review der Homelab-Sitzung (homelab-58) zu Bobs `/bot/`-Zugang; laut homelab-58 hat Roman dem Verteilen zugestimmt. Gewünschte Reihenfolge: M4 (infrastructure-z9b5w), M2 (ledger-ybm5x), M1 (ledger-ssmp3), M3 (ledger-bbkgs), dann N1 (ledger-dtrn2). Dieser Fund: M1.
