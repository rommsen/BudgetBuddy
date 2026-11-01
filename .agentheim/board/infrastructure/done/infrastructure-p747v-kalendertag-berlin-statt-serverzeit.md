---
id: infrastructure-p747v
title: "Serverzeit: Quick Add, Import und YNAB-Abfragen rechnen mit dem Berliner Kalendertag statt mit DateTime.Today"
status: done
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

## Outcome

Alle Stellen der Tabelle sind umgestellt, `grep -rn 'DateTime.Today' src/Server` trifft nur noch einen Kommentar in `src/Server/Api.fs`. `VERIFY: PASS` (dotnet 3221 grün, node 419 grün). README-Delta gegen `check:readme-delta` geprüft: Exit 0.

- `src/Server/Validation.fs`: `validateManualTransaction` bekommt `today` als ersten Parameter.
- `src/Server/YnabClient.fs`: `getAccountTransactions` und `createTransactions` bekommen `today`. Neue reine Funktion `accountTransactionsQuery` baut das `since_date`.
- `src/Server/Api.fs`: neu `liveBerlinZone` und `liveBerlinDay` (Quick Add, beide `getAccountTransactions`-Aufrufe, `liveImportPorts.Today`) sowie `prepareForImport` (normaler und erzwungener Import).

Abweichung vom Vorbild `budgetApiWith`: `ynabApi`, `syncApi` und `liveImportPorts` bleiben Modulwerte, die Zone wird für sie in `liveBerlinZone` (lazy) ein zweites Mal aufgelöst, statt von `webApp` durchgereicht zu werden. Grund: die Tests und `StructureGuardTests` hängen an diesen Werten, und zwei parallele Tasks ändern dieselbe Datei. `Program.fs` bricht weiterhin beim Start ab, wenn die Zone fehlt. `berlinToday` musste nicht umziehen, weil die Validierung den Tag als Wert bekommt.

ynab-r9k4t spielt nicht hinein: der Decoder liest YNAB-Daten seit dem Fix als Kalendertage, das `since_date` rechnet im selben Kalender.

Regressionstests: rot gesehen, indem die neuen Parameter zuerst ignoriert wurden und intern noch `DateTime.Today` galt (Assertion-Fehler, kein Kompilierfehler). Rot waren Quick Add am Berliner Tag, die Fünf-Jahres-Grenze, `accountTransactionsQuery`, `prepareForImport` mit der Buchung vom Berliner Tag und der Wächter. Die beiden Intakt-Tests (Folgetag bleibt Zukunft) waren schon davor grün. Hinweis: der rote Lauf hing am Datum des Laufs (2026-11-01); nach dem Fix sind die Tests vom Laufdatum unabhängig.

Live-Verdrahtung (Test-Doktrin 3):
- Rückkehr von `DateTime.Today` oder `DateTime.Now` an irgendeiner Stelle: fällt im neuen Wächter `serverZoneDayGuardTests` in `src/Tests/StructureGuardTests.fs` (mit Gegenbeweisen). Er ist ein Strukturtest, weil der Testlauf selbst auf Europe/Berlin steht und kein Verhaltenstest den Unterschied sieht.
- `prepareForImport`, `validateManualTransaction`, `accountTransactionsQuery`: per Verhalten belegt.

Offene Mutanten, kein Test macht sie rot:
- `liveBerlinDay` rechnet mit `DateTime.UtcNow.Date` statt `berlinDayOf`.
- Eine der Aufrufstellen in `addManualTransaction`, den beiden `getAccountTransactions`-Aufrufen, `liveImportPorts.Today` oder den beiden `prepareForImport`-Aufrufen reicht einen anderen Tag oder eine andere Uhr durch, die nicht `DateTime.Today`/`DateTime.Now` ist (etwa `DateTime.UtcNow.Date`).

`createTransactions` hat keinen Aufrufer mehr im Repo. Der Parameter ist trotzdem umgestellt, weil die Tabelle die Stelle nennt; das Entfernen liegt als Backlog-Eintrag bei.
