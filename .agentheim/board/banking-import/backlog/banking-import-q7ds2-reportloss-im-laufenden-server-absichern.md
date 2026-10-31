---
id: banking-import-q7ds2
title: "ReportLoss im laufenden Server absichern"
status: backlog
type: chore
context: banking-import
created: 2026-10-31
completed:
depends_on: [banking-import-z4h6p]
blocks: []
tags: [backend, tests, comdirect, session]
related_adrs: [0071]
related_research: []
prior_art: [banking-import-z4h6p]
---

## Why
Aus der Gegenprobe von banking-import-z4h6p überlebt ein Mutant: `ReportLoss` in `livePorts` auf `ignore`. Der Verlust ginge dann nicht ins Server-Log, die ganze Suite bleibt grün.

## What
Den Mutanten rot machen oder festhalten, dass die Zeile bis `banking-import-bftq0` ungetestet bleibt. Mittel nach Test-Doktrin Punkt 1 und 7.

## Acceptance criteria
- [ ] `ReportLoss` in `livePorts` auf `ignore` macht mindestens einen Test rot, oder der Task hält begründet fest, dass diese Zeile bis `banking-import-bftq0` ungetestet bleibt.
- [ ] Die Mutante ist in einer Kopie ohne `.env` gelaufen, mit sauberer Basis.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Die Mutante steht als `L-report` in `mut.py` der Iteration 4 von banking-import-z4h6p.

Zusatz des Verifiers: Der Mutant `X-bankClock` überlebt ebenfalls (die Uhr `fun () -> DateTimeOffset.UtcNow` in `liveImportPorts` auf `MinValue`). Dieselbe Lücke ist beim `DateTimeOffset.UtcNow` in `testComdirectConnection` zu vermuten, dort lief kein Mutant.

Zusatz aus infrastructure-mngfn: Der Anschluss von `loadAppSettings` in `liveImportPorts.LoadSettings` hat keinen Wächter. Ein eingefügtes `try/with`, das leere Einstellungen liefert, bleibt grün (Mutant `live-loadsettings-swallow`).
