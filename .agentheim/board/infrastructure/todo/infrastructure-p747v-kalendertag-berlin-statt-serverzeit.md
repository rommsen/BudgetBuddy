---
id: infrastructure-p747v
title: "Serverzeit: Quick Add, Import und YNAB-Abfragen rechnen mit dem Berliner Kalendertag statt mit DateTime.Today"
status: todo
type: bug
context: infrastructure
created: 2026-11-01
completed:
depends_on: []
blocks: []
tags: [zeitzone, uhr, bug]
related_adrs: []
related_research: []
prior_art: [infrastructure-b85yv]
---

## Why
Der Produktions-Container läuft auf UTC (am 2026-11-01 geprüft: `date` im Container zeigt UTC, `compose.yaml` und `Dockerfile` setzen kein `TZ`). `DateTime.Today` liefert dort zwischen Mitternacht und 01:00 Uhr Berliner Zeit (Sommerzeit bis 02:00) noch den Vortag. Das Budget ist seit budgeting-et7z8 repariert (`Server.Api.berlinToday`), die übrigen Stellen nicht. Am sichtbarsten trifft es Quick Add: eine Buchung von 00:30 Uhr mit heutigem Datum wird als Zukunft abgelehnt.

## What
Alle Stellen unter `src/Server`, die den Kalendertag aus `DateTime.Today` nehmen, bekommen den Berliner Tag als Parameter (`CLAUDE.md`: Zeit und Zeitzone kommen als Parameter, Test-Doktrin 17). Die bestehende Regel ist `Ledger.Bot.Domain.berlinDayOf`.

| Stelle | Wofür |
|---|---|
| `src/Server/Validation.fs:161-162` | Quick Add: Datum nicht in der Zukunft, nicht älter als 5 Jahre |
| `src/Server/Api.fs:740` | `Today` des Import-Laufs |
| `src/Server/Api.fs:993`, `:1143` | `prepareTransactions` beim Import und erzwungenen Import |
| `src/Server/YnabClient.fs:1036` | `prepareTransactions`: Buchungen in der Zukunft werden übersprungen |
| `src/Server/YnabClient.fs:2080` | `since_date` der Transaktionsabfrage |

## Acceptance criteria
- [ ] kaputt: GIVEN es ist 2026-11-02 00:30 Berliner Zeit (2026-11-01 23:30 UTC) und Quick Add trägt das Datum 2026-11-02 WHEN die Eingabe geprüft wird THEN ist sie gültig. Regressionstest ist vor dem Fix rot.
- [ ] intakt: GIVEN dieselbe Uhrzeit und das Datum 2026-11-03 WHEN die Eingabe geprüft wird THEN kommt "Date cannot be in the future".
- [ ] kaputt: GIVEN dieselbe Uhrzeit und eine Import-Buchung mit Datum 2026-11-02 WHEN `prepareTransactions` läuft THEN wird sie nicht als Zukunft übersprungen. Regressionstest ist vor dem Fix rot.
- [ ] intakt: GIVEN eine Import-Buchung mit Datum nach dem Berliner Tag WHEN `prepareTransactions` läuft THEN wird sie weiter übersprungen.
- [ ] Jede Stelle der Tabelle ist umgestellt, jede Live-Verdrahtung ist per Verhalten belegt oder als offene Mutante genannt (Test-Doktrin 3). `grep -rn 'DateTime.Today' src/Server` trifft keinen Produktionscode mehr.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- `TZ=Europe/Berlin` im Container wäre ein Einzeiler, ersetzt den Parameter aber nicht: Tests laufen ohnehin mit dieser Zone (`src/Tests/test.runsettings`) und zeigen den Fehler deshalb nicht. Der Tag bleibt ein Parameter.
- Vorbild ist `budgetApiWith` und `berlinToday` in `src/Server/Api.fs`. `berlinToday` darf an einen gemeinsamen Ort wandern, wenn die Validierung ihn braucht.
- Ob `prior_art`-Bug ynab-r9k4t (since_date) hier hineinspielt, beim Umstellen von `YnabClient.fs:2080` prüfen.
