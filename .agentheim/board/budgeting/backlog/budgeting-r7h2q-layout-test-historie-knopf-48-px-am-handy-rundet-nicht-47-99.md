---
id: budgeting-r7h2q
title: "[layout]-Test Historie-Knopf 48 px am Handy rundet nicht: 47,99999 px ließ ihn einmal rot werden"
status: backlog
type: chore
context: budgeting
created: 2026-11-04
completed:
depends_on: [budgeting-a3k7m]
blocks: []
tags: [test, browser, flaky]
related_adrs: []
related_research: []
prior_art: [budgeting-c6vn4]
---

## Why
Im Gate-Lauf zu budgeting-g2ty9 war der Test "[layout] Handy 375 px: die Suche teilt ihre Zeile nur mit Targets anwenden, der Knopf Historie steht im Kopf mit 48 px" (`scripts/budget-page.test.mjs`) einmal rot: der Knopf maß 47,99999237 px Höhe bei gebrochener Lage (top 125,915). Einzeln und im zweiten Gate-Lauf war er grün. Ein roter Gate-Lauf ohne echten Fehler kostet einen Durchlauf.

## What
Die Prüfung der Trefferfläche vergleicht mit einer Toleranz unter einem Pixel (etwa `Math.round` oder `>= 47.5`), wie andere `[layout]`-Tests es tun.

## Acceptance criteria
- [ ] Die 48-px-Prüfung im Test verträgt Rundung unter einem Pixel.
- [ ] Ein Knopf mit 44 px Höhe macht den Test weiter rot (Gegenprobe).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
## Notes
Zweites Auftreten 2026-11-04 im Verifier-Lauf zu budgeting-f4k9n: erster `./verify.sh` rot mit 47,99999 px, die Datei allein dreimal gelaufen (einmal rot, zweimal grün), zweiter Gate-Lauf grün. Prüfstelle `scripts/budget-page.test.mjs:943`. Laut CLAUDE.md ("Fehler, der schon einmal vorkam") ein Fall für den `learn`-Skill.
