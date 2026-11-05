---
id: infrastructure-hwfkn
title: "Prüfen, ob die Sperre um test:scripts noch nötig ist: ihr Grund, der 5-Sekunden-Start von vite preview, ist seit r5k2w behoben"
status: backlog
type: spike
context: infrastructure
created: 2026-11-05
completed:
depends_on: []
blocks: []
tags: [verify, gate, laufzeit, worktree, test-scripts]
related_adrs: []
related_research: []
prior_art: [infrastructure-dvy29, infrastructure-r5k2w, infrastructure-eq27s]
---

## Why
`verify.sh` serialisiert Schritt 6 über ein Lock im gemeinsamen git-Verzeichnis. Laut Kopfkommentar, weil `vite preview` in `export-design-system.mjs` unter Last nicht in den fest eingestellten 5 s startete. Seit infrastructure-r5k2w steht `PREVIEW_START_TIMEOUT_MS` auf 60 s. Bei drei parallelen Worktrees warten die Gate-Läufe trotzdem nacheinander je rund 3 Minuten auf Schritt 6, das ist der spürbarste Teil der Wartezeit bei mehreren Tickets.

## What
Drei Gate-Läufe gleichzeitig ohne Sperre (in drei Worktrees) mehrfach laufen lassen und messen: Dauer je Lauf, rote oder abgebrochene Tests. Vergleich mit drei Läufen mit Sperre. Ergebnis: Sperre entfernen, behalten oder durch etwas Milderes ersetzen (etwa höchstens zwei gleichzeitig, oder geringere Parallelität je Lauf, wenn andere Läufe aktiv sind).

Stop-Loss: Wenn sich mitten im Spike zeigt, dass die Abhilfe schon bekannt und billig ist, wird sie notiert und der Spike endet dort.

## Acceptance criteria
- [ ] Messung mit und ohne Sperre, je mindestens drei Durchgänge mit drei gleichzeitigen Läufen, im Outcome.
- [ ] Entscheidung mit Begründung im Outcome; eine Folgeaufgabe, falls die Sperre geändert werden soll.
- [ ] Der Kopfkommentar von `verify.sh` nennt den aktuellen Grund für die Sperre oder ihren Wegfall.

## Notes
Hängt inhaltlich mit infrastructure-hh19s zusammen: höhere Parallelität je Lauf und mehrere Läufe gleichzeitig konkurrieren um dieselben Kerne. Sinnvoll erst nach infrastructure-shsvf messen, weil der Export-Test dann keinen eigenen Build mehr macht.
