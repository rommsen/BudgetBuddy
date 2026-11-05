---
id: infrastructure-hh19s
title: "Parallelität von test:scripts neu messen: 4 Dateien gleichzeitig auf einem Rechner mit 10 Kernen"
status: backlog
type: chore
context: infrastructure
created: 2026-11-05
completed:
depends_on: []
blocks: []
tags: [verify, gate, laufzeit, test-scripts, playwright]
related_adrs: []
related_research: []
prior_art: [infrastructure-eq27s]
---

## Why
`TEST_CONCURRENCY` in `scripts/run-guards.mjs` steht auf 4, gemessen bei infrastructure-eq27s mit 278 Tests. Heute sind es 300 Tests, Schritt 6 braucht 180 s (2026-11-05), davon 157 s `node --test`. Die Maschine hat 10 Kerne und 16 GB. Den Standardwert von Node (Kerne minus 1) hat eq27s nicht gemessen.

## What
Gesamtlauf mit 4, 6 und 8 Dateien parallel messen, je dreimal. Den schnellsten Wert setzen, der dreimal grün und ohne Hänger läuft. Den Einfluss auf zwei gleichzeitige Gate-Läufe (zwei Worktrees) mit betrachten, siehe infrastructure-hwfkn.

## Acceptance criteria
- [ ] Messtabelle (Parallelität, drei Laufzeiten, grün ja/nein) im Outcome.
- [ ] `TEST_CONCURRENCY` steht auf dem gewählten Wert, Kommentar mit Messung und Datum.
- [ ] Der gewählte Wert lief dreimal hintereinander grün.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Untergrenze ist der längste Einzeltest (`[flow:design-system-export]`, 37 s heute, nach infrastructure-shsvf kürzer). Zeitabhängige Wächter (ledger-read-retry, siehe infrastructure-v3tqk) werden unter mehr Last eher wackelig; im Outcome nennen, falls einer auffällt.
