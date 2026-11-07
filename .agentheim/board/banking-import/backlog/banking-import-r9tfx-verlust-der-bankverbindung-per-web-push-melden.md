---
id: banking-import-r9tfx
title: "Verlust der Bankverbindung per Web-Push melden"
status: backlog
type: feature
context: banking-import
created: 2026-11-01
completed:
depends_on: [banking-import-bftq0, infrastructure-jsys4]
blocks: []
tags: [danach, backend, comdirect, session, benachrichtigung, tests]
related_adrs: [0071]
related_research: []
prior_art: []
---

## Why
Ein Verlust der Bankverbindung soll Roman erreichen, ohne dass er die App öffnet. Heute landet er nur
im Server-Log (`ReportLoss` in `livePorts`).

## What
`ReportLoss` ruft zusätzlich zum Log `notify` aus `infrastructure-jsys4` auf, einmal je Verlust, mit
festem Text. Der Grund aus `ReportLoss` trägt Status und `error`-Code und bleibt im Log.

## Acceptance criteria
- [ ] GIVEN `Connected` und der Refresh wird mit `ReloginRequired` abgelehnt THEN genau ein `notify` mit festem Text ohne Bankdaten.
- [ ] GIVEN `Connected` und ein Datenabruf wird auch nach dem Refresh mit 401 oder 403 abgelehnt THEN genau ein `notify`.
- [ ] GIVEN ein unbrauchbarer gespeicherter Wert bei der Wiederaufnahme THEN genau ein `notify`.
- [ ] GIVEN "Bank trennen", geänderte Zugangsdaten oder eine gescheiterte TAN THEN kein `notify`.
- [ ] GIVEN `notify` scheitert THEN der Zustand bleibt unberührt, Log-Eintrag.
- [ ] GIVEN `notify` hängt THEN das Bank-Gate ist frei, ein Abruf läuft durch.
- [ ] `ReportLoss` in `livePorts` auf `ignore` macht mindestens einen Test rot.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
`ReportLoss` läuft bei gehaltenem Gate und ist synchron (`string -> unit`), `notify` ist asynchron und
geht ins Netz. Der Aufruf darf deshalb nicht innerhalb des Gate abgewartet werden.

`ComdirectAuthSession.fs` liegt früh in der Kompilierreihenfolge. Der Live-Anschluss kann nur auf
`notify` zugreifen, wenn das Modul davor liegt.

Deckt den Mutanten `L-report` aus `banking-import-q7ds2` ab. Die übrigen Zusätze dort bleiben offen.

Scheitert der Geräte-Beweis in `infrastructure-jsys4`, entfällt dieser Task.

## Hinweis Triage 2026-11-06
Der Mutant aus banking-import-q7ds2 (ReportLoss auf ignore) wird mit dieser Task durch einen Test auf den Push-Weg abgedeckt.

## Aufgenommen aus banking-import-q7ds2 (Triage 2026-11-06)
**ReportLoss im laufenden Server absichern** (chore, erfasst 2026-10-31)

### Why
Aus der Gegenprobe von banking-import-z4h6p überlebt ein Mutant: `ReportLoss` in `livePorts` auf `ignore`. Der Verlust ginge dann nicht ins Server-Log, die ganze Suite bleibt grün.

### What
Den Mutanten rot machen oder festhalten, dass die Zeile bis `banking-import-bftq0` ungetestet bleibt. Mittel nach Test-Doktrin Punkt 1 und 7.

### Acceptance criteria
- [ ] `ReportLoss` in `livePorts` auf `ignore` macht mindestens einen Test rot, oder der Task hält begründet fest, dass diese Zeile bis `banking-import-bftq0` ungetestet bleibt.
- [ ] Die Mutante ist in einer Kopie ohne `.env` gelaufen, mit sauberer Basis.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

### Notes
Die Mutante steht als `L-report` in `mut.py` der Iteration 4 von banking-import-z4h6p.

Zusatz des Verifiers: Der Mutant `X-bankClock` überlebt ebenfalls (die Uhr `fun () -> DateTimeOffset.UtcNow` in `liveImportPorts` auf `MinValue`). Dieselbe Lücke ist beim `DateTimeOffset.UtcNow` in `testComdirectConnection` zu vermuten, dort lief kein Mutant.

Zusatz aus infrastructure-mngfn: Der Anschluss von `loadAppSettings` in `liveImportPorts.LoadSettings` hat keinen Wächter. Ein eingefügtes `try/with`, das leere Einstellungen liefert, bleibt grün (Mutant `live-loadsettings-swallow`).
