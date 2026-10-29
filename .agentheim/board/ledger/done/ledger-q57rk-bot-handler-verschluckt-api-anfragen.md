---
id: ledger-q57rk
title: "Hotfix: Bot-Handler beendete im choose jede /api/-Anfrage mit leerem 200, die App lud nichts mehr"
status: done
type: bug
context: ledger
created: 2026-10-29
completed: 2026-10-29
depends_on: []
blocks: []
tags: [bob, hotfix, production]
related_adrs: [0066]
related_research: []
prior_art: [ledger-dtrn2]
---

## Why
Nach dem Deploy am 2026-10-29 um 08:03 Uhr lud BudgetBuddy keine Daten mehr. Roman meldete Toasts "Failed to load settings: JSON.parse: unexpected end of data", "Http error (0)" für getLenaFlagSettings und im Kassenbuch "Konten konnten nicht geladen werden" sowie "Lauf-Status nicht lesbar".

## What
Die Logs auf docker-host zeigten für jede `/api/`-Anfrage `200 0 -` in rund 0,03 ms, ohne Content-Type und ohne Exception. Fable.Remoting lief also gar nicht. Ursache: `BotApi.handler` steht in `Api.webApp` im `choose` vor allen Remoting-Handlern und rief für Pfade außerhalb von `/bot` `next ctx` auf. Im `choose` heißt das "erledigt", die Anfrage endete mit leerem Rumpf. Richtig ist `skipPipeline` (None), damit `choose` den nächsten Handler probiert.

Der Fehler steckte seit b4b5cc3 (2026-10-28, Einführung der Bot-Schnittstelle) im Code. Der Testhelfer `call` in `BotApiTests.fs` wertete den Aufruf von `next` als "weitergereicht" und hielt das falsche Verhalten damit als richtig fest.

## Acceptance criteria
- [x] Pfade außerhalb von `/bot` (z. B. `/api/...`, `/botanik`) erreichen im `choose` den Handler nach dem Bot-Handler, für GET und POST (Regressionstest, vor dem Fix rot).
- [x] Der Testhelfer wertet nur `None` als "nicht beansprucht".
- [x] Bot-Pfade verhalten sich unverändert (Drossel, 401, 404 JSON, Routen).
- [x] `dotnet build` und `dotnet test` grün.
- [x] Deployt; Container mit neuem Image healthy.

## Notes
Gefunden und behoben in einer Sitzung ohne work-Lauf, auf Romans Meldung hin. Kein README_DELTA (keine neue Sprache). In Produktion bestätigt (2026-10-29, 15:45): nach dem Neuladen antworteten alle `/api/`-Aufrufe (Settings, Konten, Buchungsseite, YNAB-Kategorien, Spiegel-Lauf) mit 200 und `application/json`, kein `Fable.Remoting ERROR`.

## Outcome

`src/Server/BotApi.fs`: `handler` gibt für Nicht-Bot-Pfade `skipPipeline` zurück statt `next ctx` (Kommentar nennt den Grund). `src/Tests/BotApiTests.fs`: Helfer `call` prüft das Rückgabeergebnis (None → -1) statt eines `next`-Aufrufs; neuer Test "im choose vor Fable.Remoting erreichen /api/-Anfragen den nächsten Handler" baut `choose [ handler; setStatusCode 299 ]` und prüft GET/POST `/api/...` und `/botanik`. Vor dem Fix scheiterten der neue Test und zwei Durchreich-Prüfungen der bestehenden Tests, danach alle grün.

Läufe: dotnet build 0 Warnungen; dotnet test 2502 bestanden, 6 übersprungen, 0 fehlgeschlagen. `npm run test:scripts` nicht gelaufen (keine Skript-Änderung). Commit 2a5c229, per `scripts/deploy.sh` ausgerollt (Roman hat den Deploy selbst gestartet); Hook meldete "Health status: starting", wenige Sekunden später `healthy` mit Image `7313474b…`.

Lehre: Ein Giraffe-Handler, der in einem `choose` einen Pfad nicht beansprucht, muss `skipPipeline` liefern. Ein isolierter Test mit eigenem `next` kann den Unterschied nicht sehen; den Handler im `choose` prüfen.
