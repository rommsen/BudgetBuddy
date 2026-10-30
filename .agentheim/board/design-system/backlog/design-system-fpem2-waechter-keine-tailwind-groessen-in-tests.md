---
id: design-system-fpem2
title: "Wächter: .NET-Tests prüfen keine Tailwind-Größen per Klassenstring"
status: backlog
type: chore
context: design-system
created: 2026-10-30
completed:
depends_on: []
blocks: []
tags: [captured, harness, tests]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Muster: Bei UI-Tasks mit Größenvorgaben sichern Worker die Trefferfläche im .NET-Test über Klassenstrings ab, weil .NET kein Layout messen kann. Der Test bleibt grün, auch wenn der Knopf die Klasse nie bekommt. Vorgekommen in 897b13c (`ButtonSizeTests.fs`, Audit B1 N7, C B19, in infrastructure-hdf7j gelöscht) und im Nachspiel von design-system-j6dnj bei der Messung nach M12 (`baseline.md`). Die Prosa-Regel `.claude/rules/test-doctrine.md` Punkt 6 hat nicht gegriffen.

## What
Ein Wächter in `src/Tests` sucht in `src/Tests/*.fs` nach Tailwind-Größen als Literal (etwa `min-h-[48px]`, `w-[..px]`), mit eingebautem Gegenbeweis (Doktrin Punkt 7). Bestandsstellen ersetzen oder mit Begründung auf eine Ausnahmeliste setzen: `AuswahlTests.fs:99`, `ButtonSizeTests.fs:17` (`iconOnlyClass` prüft eine Verzweigung, bleibt), `LedgerSortSheetTests.fs:138`, `LedgerViewTests.fs:1257`, `RuhigeListenTests.fs:122`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-30 aus dem `learn`-Skill, raw, unrefined. Needs a `modeling` refine pass before it can be promoted.
