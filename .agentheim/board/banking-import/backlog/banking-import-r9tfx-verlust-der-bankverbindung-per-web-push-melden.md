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
tags: [backend, comdirect, session, benachrichtigung]
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
