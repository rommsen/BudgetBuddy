---
id: ledger-m23nz
title: Kassenbuch-Filterleiste und Summenzeile nach ADR 0049 nachziehen
status: backlog
type: feature
context: ledger
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
- Kategorie-Popover 400 bis 460px breit, kein Umbruch (K5, `Ledger/View.fs:1047`).
- Payee-Zeilen 14px mit `truncate`, Trefferzahl mit Tausenderpunkt (K8).
- Zeitraum, Memo und Betrag kompakt, "Übernehmen" ohne Leuchten, Datumsfelder deutsch (K7, K9).
- Summenzeile ohne Karte, neutral über `Money.listAmount`, beim Laden kein "+0,00 €" in Grün (K10, `Ledger/View.fs:1158`).
- Abstand zwischen Kopf-Untertitel und "KONTEN" (K12, `PageHeader.fs:111`).
- Memo-Spalte in "Alle Konten" mit Mindestbreite (K13, `Ledger/View.fs:787`).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured on 2026-10-19 aus der Bestandsaufnahme. Raw, unrefined; braucht einen
`modeling`-Durchgang. Befund-Nummern (K, P, R, S, E) beziehen sich auf den Bericht.
