---
id: infrastructure-t5m2b
title: "Browser-Wächter durchsehen: was billiger in F# geht, was doppelt ist, was nur Werkzeug absichert"
status: backlog
type: chore
context: infrastructure
created: 2026-11-02
completed:
depends_on: []
blocks: []
tags: [test, waechter, gate]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Roman: 500 node-Tests fühlen sich sehr viel an. Rund 300 stehen in Playwright-Dateien. Die Test-Doktrin (`.claude/rules/test-doctrine.md` Punkt 5) verlangt je Nutzerfluss einen Happy Path und was nur der Browser messen kann. Browser-Wächter sind der langsamste Teil des Gates und die Quelle des Flackerns.

## What
Je Datei prüfen: Was lässt sich billiger in F# belegen, was ist doppelt, was sichert nur ein Hilfswerkzeug ab. Auffällig: `scripts/screenshot-route.test.mjs` mit 32 Fällen, `scripts/budget-page.test.mjs` mit über 30. Die Budget-Seite kommt erst dran, wenn sie steht (nach budgeting-m3x7f).

## Acceptance criteria
- [ ] Liste je Datei mit Empfehlung: behalten, nach F# ziehen, löschen, mit Begründung.
- [ ] Löschen nur nach Freigabe durch Roman.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
