---
id: design-system-006
title: Konsolidierung roher Html.button → Button-DS-Komponente (SyncFlow/Rules-Views)
status: backlog
type: refactor
context: design-system
created: 2026-06-18
completed:
commit:
depends_on: [design-system-001]
blocks: []
tags: [frontend, design-system, refactor, drift, button, consolidation]
related_adrs: [0005]
related_research: []
prior_art: [design-system-002]
---

## Why
Split aus dem Drift-Audit `design-system-002`. Das Audit fand ~34 rohe `Html.button` in den
fachlichen View-Schichten:

- `SyncFlow/Views/QuickAdd.fs` (7), `SplitSheet.fs` (7), `TransactionList.fs` (7),
  `TransactionRow.fs` (8), `View.fs` (1), `InlineRuleForm.fs` (2), `Rules/View.fs` (2).

Ein Teil davon ist **echter Drift** (eine simple Aktion, die `Button.primary/secondary/
ghost/danger` 1:1 abdeckt). Ein anderer Teil ist **bewusst custom**: Click-Commit-Buttons
in Sheets/Pickern (ADR 0005), Swipe-Zeilen-Aktionen, `action-chip`/`tx-create-rule-btn`-
Chips, Kategorie-Badges-als-Button. Die lassen sich **nicht** ohne Verhaltensrisiko auf
`Button` heben — sie wurden in 002 deshalb ausgegliedert statt blind refactored.

## What
1. Pro Datei jeden rohen `Html.button` einstufen: **(a) 1:1-Button-Kandidat** oder
   **(b) bewusst custom** (Click-Commit/Swipe/Chip → bleibt, ggf. kurz kommentieren *warum*).
2. Nur die (a)-Fälle auf die `Button`-DS-Komponente heben — Variante, Icon, Loading-State
   aus dem bestehenden Styling ableiten, **kein** Look-/Verhaltens-Change.
3. Tests bleiben grün; mobil + Desktop stichprobenhaft visuell bestätigen (insb. dass
   Click-Commit/Swipe-Elemente NICHT angefasst wurden).

## Acceptance criteria
- [ ] Einstufungs-Liste (Datei → button → Kandidat/custom + Grund) im Task oder Diary.
- [ ] Alle 1:1-Kandidaten nutzen `Button`; custom-Elemente unverändert (Begründung notiert).
- [ ] Kein Verhaltens-/Layout-Regress; `dotnet build` + `dotnet test` grün; Diary aktualisiert.

## Notes
Abgrenzung wie in 002: kein Redesign, keine neuen Komponenten — nur Anheben aufs DS.
ADR 0005 ist hart bindend für alle Sheet-/Picker-/Click-Commit-Buttons (nicht anfassen,
außer der Lift erhält das Verhalten beweisbar 1:1).
