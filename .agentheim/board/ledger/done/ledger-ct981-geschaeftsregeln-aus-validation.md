---
id: ledger-ct981
title: "Bot-Geschäftsregeln aus Validation.fs in die Ledger-Domain verschieben (Tagesgrenze, Kategorie-Auflösung, Zeitzone)"
status: done
type: refactor
context: ledger
created: 2026-10-29
completed:
depends_on: [infrastructure-ky2kh]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
`src/Server/Validation.fs:160-229`, `:336-372` enthalten Geschäftsregeln und `failwith` (Audit B1 M4, M5, B2, C B14).

## What
Regeln als pure Funktionen in die Ledger-Domain, Zeitzone als Abhängigkeit, kein `failwith`.

## Acceptance criteria
- [ ] Validation.fs enthält nur Eingabeprüfung.
- [ ] Bestehende Bot-Tests bleiben grün, Domain-Wächter aus M6 ist grün.

## Notes
Migrationsschritt M15. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.


## Outcome

Verhalten unverändert, nur Platzierung und Abhängigkeiten (Audit B1 M4/M5, B2 B4/B5, C B14).

- Neu `src/Server/Ledger/Bot/Domain.fs` (Modul `Ledger.Bot.Domain`, rein, wird vom Domain-Wächter aus M6 mitgeprüft): `BotDirection`, `BotPreviewInput`, `botBackdateDays` und neu `checkBotBookingDate today date` (Datumsfenster, gleiche Meldungen wie bisher), `berlinOffsetAt`/`berlinDayOf`/`berlinDayBoundsUtc` mit der Zeitzone als erstem Parameter, `botDailyMaxSumEur`/`botDailyMaxCount`/`checkBotDailyLimit` (500 EUR und 10 Buchungen unverändert), `selectableBotCategories`, `BotCategoryResolution`, `resolveBotCategory`.
- `src/Server/Validation.fs`: nur noch `BotFieldError`, die verbotenen Konto-Felder, `parseBotPreviewRequest` und `checkBotAmount`. `parseBotPreviewRequest` bekommt statt `today` die Datumsregel `checkDate: DateTime -> Result<unit, string>` hineingereicht; ihr Fehler wird wie bisher zum gesammelten Feldfehler `date` (Reihenfolge und Sammeln der Feldfehler gleich). Kein `failwith`, kein Lazy, keine Zeitzonendatenbank mehr.
- `src/Server/ServerConfig.fs`: `tryResolveBerlinZone` (aus Validation verschoben). `src/Server/Program.fs` löst die Zone einmal auf (Abbruch mit Hinweis wie bisher) und gibt sie an `Api.webApp berlinZone`, das an `BotApi.liveDeps berlinZone`. `BotDeps` hat das neue Feld `BerlinZone`; Vorschau und Commit rechnen Kalendertag und Tagesgrenzen damit.
- Tests: neu `src/Tests/BotDomainTests.fs` (Datumsfenster direkt, dazu die verschobenen Tests zu Kategorie-Auflösung, Tagesgrenze und Berliner Tag mit Zone als Parameter). `src/Tests/BotValidationTests.fs` prüft die Vorschau über die Domain-Regel (`parseAt`) und neu, dass die Datumsregel von außen kommt und ein Fensterfehler mit anderen Feldfehlern gesammelt wird. `BotApiTests.fs` und `RemotingPipelineTests.fs` reichen die Zone hinein.
- `docs/bot-api.md`: Ort der Konstanten nachgezogen.
- `./verify.sh`: PASS, dotnet 2605 grün (vorher 2600), node 310 grün.
