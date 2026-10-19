---
id: ynab-sync-aj5tv
title: Import prüfen an ADR 0049 angleichen: nur die Kante farbig, deutsche Zahlen und Datumsformate
status: backlog
type: feature
context: ynab-sync
created: 2026-10-19
completed:
depends_on: [design-system-zahv9]
blocks: []
tags: [captured, frontend, ruhige-listen]
related_adrs: [0049]
related_research: []
prior_art: [design-system-q7rkm, ledger-n4vte]
---

## Why
Bestandsaufnahme gegen ADR 0049 (Ruhige Listen) am 2026-10-19. Bericht und Screenshots:
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/ce62b2b7-5777-4228-8b31-afa8d3e4749a/scratchpad/audit/report.md` und `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/ce62b2b7-5777-4228-8b31-afa8d3e4749a/scratchpad/audit/shots/` (Session-Ablage; die für die Umsetzung nötigen Bilder bei
der Verfeinerung ins Repo unter `docs/ux/` übernehmen, Regel "Zielbild liegt im Repo").

## What
Entscheidung Roman (2026-10-19): auch "Import prüfen" wird ruhiger, nicht nur die Formate.
- Beträge und Summen deutsch über `Money.formatAmountText`/`listAmount`; Datum deutsch ohne `CultureInfo` (Fable ignoriert sie). Heute "3,113.30 €", "10. OCTOBER 2026" (S2; `TransactionRow.fs:76-93`, `TransactionList.fs:219,225`).
- Pro Zeile nur die Statuskante farbig: Kategorie-Chip neutral, Checkbox gedämpft; "Kategorie..." für fehlende Kategorie bleibt Orange (S1; `styles.css:1279-1421`, `TransactionRow.fs:43-58`).
- Datum nicht doppelt in Zeile und Gruppenkopf (S3).
- Debug-Block "BudgetBuddy Duplicate Detection" nur bei Verdacht oder hinter "Details", deutsch; "Überspringen" nicht als roter Chip (S6).
- Aufteilen-Sheet: Rest-Betrag ohne Leuchten, Gesamt nicht rot-fett (S7, `SplitSheet.fs:176,205`).
- Prüfen: volle Breite (S4) und Farbe des Importieren-Knopfs (S5).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured on 2026-10-19 aus der Bestandsaufnahme. Raw, unrefined; braucht einen
`modeling`-Durchgang. Befund-Nummern (K, P, R, S, E) beziehen sich auf den Bericht.
