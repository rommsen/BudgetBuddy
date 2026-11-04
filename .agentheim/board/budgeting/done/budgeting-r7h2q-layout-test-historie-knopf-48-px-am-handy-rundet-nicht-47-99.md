---
id: budgeting-r7h2q
title: "[layout]-Test Historie-Knopf 48 px am Handy rundet nicht: 47,99999 px ließ ihn einmal rot werden"
status: done
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

## Outcome

Die Prüfung der Trefferfläche im Test "[layout] Handy 375 px: die Suche teilt ihre Zeile nur mit Targets anwenden, der Knopf Historie steht im Kopf mit 48 px" (`scripts/budget-page.test.mjs`) vergleicht jetzt `Math.round(at.history.width) >= 48 && Math.round(at.history.height) >= 48`. Das folgt der Konvention der Datei (`Math.round` in mehreren [layout]-Tests, sonst Toleranzen von 0,5 px); einen gemeinsamen Helfer dafür gibt es nicht. Ein Kommentar mit der Task-Kennung nennt den Grund. Produktionscode bleibt unverändert.

Gegenprobe: den gemessenen Wert vor der Prüfung vorübergehend überschrieben und die Datei laufen lassen, danach den Test byteweise wiederhergestellt (`cmp` gegen die Sicherung).
- `at.history.height = 44`: der Test ist rot ("Historie hat 48 px Trefferfläche").
- `at.history.height = 47.99999237` (der Wert aus dem roten Gate-Lauf): der Test ist grün, Datei 32 von 32 grün.

Gate: `./verify.sh` im Worktree mit `VERIFY: PASS` (dotnet 3781 grün, node 300 grün, Build und beide Wächter grün), Log unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/78d1d4cc-38a9-4142-849d-286626b8b6e6/scratchpad/verify-budgeting-r7h2q.log`.

Hinweis: In derselben Datei prüfen weitere 48-px-Stellen ohne Rundung (etwa Zeilen 438, 509, 521, 525, 531, 1223). Sie sind bisher nicht als wackelig aufgefallen und wurden außerhalb des Auftrags nicht angefasst. Laut Task-Notiz ist das zweite Auftreten ein Fall für den `learn`-Skill, das entscheidet der Conductor.
