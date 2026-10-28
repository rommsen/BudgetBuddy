---
id: ledger-ssmp3
title: "Bobs Grenzen gelten nur pro Buchung: Datumsfenster, Tagesgrenze nach Summe und Anzahl, Meldung je Bob-Commit (Security-Review M1)"
status: done
type: feature
context: ledger
created: 2026-10-28
completed:
depends_on: [ledger-ybm5x]
blocks: [ledger-bbkgs]
tags: [security, bob]
related_adrs: [0066, 0067, 0068]
related_research: []
prior_art: []
---

## Why
Bobs Grenzen gelten heute nur je Buchung. Ein per Prompt-Injection gesteuerter Bob kann beliebig viele Buchungen bis 500 € anlegen, als Ausgabe oder Einnahme und bis zu fünf Jahre rückdatiert (Security-Review M1).

## What
Umsetzung von ADR 0068 Punkte 2 und 3:

- **Tagesgrenze:** je Kalendertag (Europe/Berlin) höchstens 500 € Summe (Beträge ohne Vorzeichen, Ausgaben und Einnahmen zusammen) und höchstens 10 gebuchte Bob-Commits. Gezählt werden die im Kassenbuch gespeicherten Buchungen mit Actor `Bob` nach dem Zeitpunkt des Commits (Ereignis-Zeitstempel), damit ein Neustart nichts zurücksetzt. Werte als Konstanten im Code.
- Geprüft wird in der Vorschau (frühe Rückmeldung an Bob) und noch einmal im Commit (entscheidend, weil mehrere Vorschauen offen sein können). Über der Grenze: 422 mit Code `daily_limit_reached` und einer Meldung, die Summe und Anzahl des Tages nennt. Ein wiederholter Commit derselben Vorschau zählt nicht doppelt und liefert weiter das erste Ergebnis.
- **Datumsfenster:** Buchungsdatum zwischen heute minus 31 Tage und heute (Europe/Berlin); sonst 400 mit Feld `date`. Die bisherige Fünf-Jahres-Grenze in `Validation.fs` (`parseBotPreviewRequest`) entfällt für Bob; die Prüfung für Quick Add bleibt unverändert.
- Keine Benachrichtigung und kein Flag (ADR 0068 Punkt 5). Bob-Buchungen kommen in YNAB ohne `approved` an und stehen dort unbestätigt; das bleibt so.

## Acceptance criteria
- [ ] Tagesgrenze nach Summe: Bei bereits 450 € (450000 Milliunits) Bob-Buchungen heute wird eine Vorschau über 60 € mit 422 `daily_limit_reached` abgelehnt, eine über 50 € angenommen (Integrationstest, In-Memory-SQLite).
- [ ] Tagesgrenze nach Anzahl: Nach 10 gebuchten Bob-Commits heute wird die 11. Vorschau und, falls vorher angelegt, der 11. Commit mit 422 abgelehnt.
- [ ] Einnahmen zählen mit ihrem Betrag ohne Vorzeichen zur Summe (Test mit gemischten Richtungen).
- [ ] Buchungen von gestern (Europe/Berlin) und Buchungen anderer Herkunft (Quick Add, Import) zählen nicht (Test).
- [ ] Die Zählung kommt aus dem gespeicherten Kassenbuch: nach einem neuen `BotPreviewStore` (Neustart) gilt die Grenze weiter (Test).
- [ ] Zwei offene Vorschauen, die zusammen die Grenze überschreiten: der erste Commit bucht, der zweite wird mit 422 abgelehnt (Test, auch bei gleichzeitigen Commits höchstens bis zur Grenze).
- [ ] Ein wiederholter Commit derselben Vorschau zählt nicht doppelt und liefert das erste Ergebnis (bestehender Idempotenz-Test bleibt grün, plus Test an der Grenze).
- [ ] Datumsfenster: heute und heute minus 31 Tage werden angenommen, heute minus 32 Tage und morgen mit 400 Feld `date` abgelehnt; die Quick-Add-Validierung ist unverändert (bestehende Tests grün).
- [ ] Die Tagesbestimmung nutzt Europe/Berlin und funktioniert im Container (Test mit einem Zeitpunkt kurz nach Mitternacht Berliner Zeit, der in UTC noch auf dem Vortag liegt).
- [ ] `docs/bot-api.md` nennt Tagesgrenze, Datumsfenster und den Code `daily_limit_reached`; die ledger-README (Bot-Vorschau, Bot-Commit) ebenso.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün.

## Notes
Herkunft: Security-Review der Homelab-Sitzung (homelab-58) am 2026-10-28, erfasst per quick-capture, verfeinert mit Roman am 2026-10-28. Befund M1, dritter in der Reihenfolge. ADR 0068 Punkte 2, 3 und 5.
Entscheidungen Roman (2026-10-28): Tagesgrenze 500 € und 10 Buchungen, Datumsfenster 31 Tage, keine Meldung je Commit (die YNAB-Prüfliste reicht), kein Flag (Flags gehören "Lena fragen", ADR 0056).
Geprüft bei der Verfeinerung: `YnabClient.buildManualTransactionBodyWithFlag` sendet kein `approved`, YNAB legt die Buchung deshalb unbestätigt an. ADR 0066 und 0067 verweisen in ihren Folgen schon auf ADR 0068.
Hängt an ledger-ybm5x, weil beide `ServerConfig.fs`/`BotApi.fs` ändern.

## Outcome

ADR 0068 Punkte 2, 3 und 5 umgesetzt (Punkt 4, Drosselung und Kategorien-Cache, bleibt ledger-bbkgs).

- `src/Server/Validation.fs`: Konstanten `botBackdateDays = 31`, `botDailyMaxSumEur = 500m`, `botDailyMaxCount = 10`. Die Fünf-Jahres-Grenze in `parseBotPreviewRequest` ist durch das 31-Tage-Fenster ersetzt (Feld `date`, 400); Quick-Add-Validierung unverändert. Neue reine Funktionen `berlinOffsetAt`, `berlinOffsetByEuRules`, `berlinDayOf`, `berlinDayBoundsUtc`, `checkBotDailyLimit` (Meldung nennt Summe und Anzahl des Tages).
- `src/Server/Ledger/Persistence.fs`: `loadBobBookedAmountsBetween fromUtc toUtc` liest `CashExpenseAdded` mit Actor `Bob` im Zeitraum aus `ledger_entry_events`. Gezählt wird also, was gespeichert ist; Quick Add, Import und andere Tage zählen nicht.
- `src/Server/BotApi.fs`: Vorschau prüft `daily_limit_reached` (422) nach der Betragsprüfung, `today` für die Datumsprüfung ist der Berliner Tag. Der Commit prüft erneut und bucht unter einer prozessweiten Sperre (`bookingGate`, SemaphoreSlim), damit gleichzeitige Commits verschiedener Vorschauen die Grenze nicht gemeinsam überschreiten. `CommitOnce` liefert jetzt `BotCommitOutcome` (`BotBooked` oder `BotDailyLimitReached`); eine Wiederholung derselben Vorschau liefert das erste Ergebnis (auch eine Ablehnung), zählt nie doppelt. Der Test für gleichzeitige Commits wurde ohne Sperre kurz rot gesehen.
- `docs/bot-api.md`: Tagesgrenze, Datumsfenster, Code `daily_limit_reached`.
- Europe/Berlin im Container: `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` hat keinen `InvariantGlobalization`-Eintrag im Dockerfile (und TimeZoneInfo hängt ohnehin nicht an ICU), ob das Image tzdata enthält, ließ sich offline nicht prüfen. Deshalb fällt `berlinOffsetAt` ohne gefundene Zeitzone auf die EU-Sommerzeitregeln zurück (`berlinOffsetByEuRules`); ein Test vergleicht beide an den Umstellungsgrenzen. Das Dockerfile blieb unangetastet. Wer den Zonenpfad sicher will, kann `tzdata` im Runtime-Stage per `apk add` ergänzen, nötig ist es nicht.
- Tests (17 neu): 10 in `BotApiTests.fs` (Summe, Anzahl, gemischte Richtungen, gestern und Quick Add, neuer Store, zwei offene Vorschauen, sechs gleichzeitige Commits genau drei gebucht, Wiederholung an der Grenze, Datumsfenster, Berliner Tag nach Mitternacht), 7 in `BotValidationTests.fs` (Grenzwerte, Berliner Tag Winter und Sommer, Tagesgrenzen in UTC, 23 und 25 Stunden an Umstellungstagen, Rückfall gleich Zeitzonendatenbank, 31/32-Tage-Grenze). Jeder API-Test nutzt einen eigenen Berliner Tag, weil die In-Memory-Datenbank über Tests geteilt ist.
- Läufe aus dem Worktree: `dotnet build` 0 Warnungen, `dotnet test` 2489 grün (7 übersprungen, Integrationstests ohne .env), `npm run build`, `guard:classes`, `guard:tokens` grün, `npm run test:scripts` 281 von 281.
- Kein Flag und keine Benachrichtigung (Punkt 5): Bob-Buchungen bleiben in YNAB unbestätigt.

### README von Hand
Die drei Ubiquitous-language-Änderungen (zwei replace, ein append) sind im README_DELTA-Block und gehen durch das Gate (Exit 0). Weitere Stellen mit Bot-Bezug (Zeilen 24, 36, 226, 229, 259, 333, 405) widersprechen der Änderung nicht.
