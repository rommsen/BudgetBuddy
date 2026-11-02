---
id: infrastructure-t5m2b
title: "Browser-Wächter durchsehen: was billiger in F# geht, was doppelt ist, was nur Werkzeug absichert"
status: backlog
type: chore
context: infrastructure
created: 2026-11-02
completed:
depends_on: [budgeting-m3x7f]
blocks: []
tags: [test, waechter, gate]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Roman: 500 node-Tests fühlen sich sehr viel an. Rund 300 stehen in Playwright-Dateien. Die Test-Doktrin (`.claude/rules/test-doctrine.md` Punkt 5) verlangt je Nutzerfluss einen Happy Path und was nur der Browser messen kann. Browser-Wächter sind der langsamste Teil des Gates und die Quelle des Flackerns.

## What
Je Datei prüfen: Was lässt sich billiger in F# belegen, was ist doppelt, was sichert nur ein Hilfswerkzeug ab. Auffällig: `scripts/screenshot-route.test.mjs` mit 32 Fällen, `scripts/budget-page.test.mjs` mit über 30. Die Budget-Seite kommt erst dran, wenn sie steht (nach budgeting-m3x7f), deshalb wartet die ganze Durchsicht darauf.

Vorgehen nach dem Muster von infrastructure-h4q8v: Eine Doppelung gilt erst als belegt, wenn eine Mutante im Produktionscode den billigeren Test allein rot macht. Reine Lesart reicht nicht.

## Acceptance criteria
- [ ] Liste je Datei mit Laufzeit und Empfehlung: behalten, nach F# ziehen, löschen, mit Begründung. Jede Empfehlung "löschen" oder "nach F# ziehen" nennt die Mutante, die den verbleibenden Test rot macht.
- [ ] Die Liste nennt je Datei die festen Wartezeiten und die Uhr-Abhängigkeiten. Stand 2026-11-02: 36-mal `waitForTimeout` in 19 Dateien, obwohl Doktrin Punkt 5 es verbietet.
- [ ] Löschen nur nach Freigabe durch Roman.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Die Laufzeit gehört in die Liste, weil sie über den Nutzen entscheidet: Die sechs Fälle aus infrastructure-h4q8v kosteten seriell rund 10 s von rund 8 Minuten Gate.

Vorsicht bei `RetryTests.fs`: Das dortige Inventar liest Quelltext und ist nach infrastructure-h4q8v die einzige Absicherung der Aufrufstellen. Es ist kein Kandidat zum Aufräumen.
