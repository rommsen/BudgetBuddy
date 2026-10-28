---
id: ledger-ssmp3
title: "Bobs Grenzen gelten nur pro Buchung: Datumsfenster, Tagesgrenze nach Summe und Anzahl, Meldung je Bob-Commit (Security-Review M1)"
status: todo
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
