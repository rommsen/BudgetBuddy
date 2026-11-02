---
id: budgeting-r7t3k
title: "Budget-Seite: Rückkehr aus dem Kassenbuch und Schließen von Targets ohne Sprung"
status: backlog
type: bug
context: budgeting
created: 2026-11-02
completed:
depends_on: [budgeting-p4n6r]
blocks: []
tags: [budget, inspektor]
related_adrs: []
related_research: []
prior_art: [budgeting-p4n6r]
---

## Why
budgeting-p4n6r beruhigt nur das Blättern. Die Rückkehr aus dem Kassenbuch (`PageEntered`, `ReturnedFromLedger`) und `CloseTargets` setzen weiter `Month = Loading`: Liste und Inspektor verschwinden kurz, Fokus und Scrollposition gehen verloren.

## What
Beide Wege lassen einen geladenen Monat stehen und lesen ihn still neu, wie beim Blättern. Scheitert das Lesen, gilt das Verhalten von `applyReloaded`.

## Acceptance criteria
- [ ] (Browser) GIVEN Kategorie im Tab Buchungen WHEN Rückkehr aus dem Kassenbuch THEN Liste und Inspektor bleiben im DOM, kein Spinner.
- [ ] (Browser) GIVEN Targets angewendet WHEN Dialog schließt THEN die Liste bleibt stehen und zeigt danach die neuen Beträge.
- [ ] (Client-`update`) Die Buchungen werden nach der Rückkehr neu geholt.