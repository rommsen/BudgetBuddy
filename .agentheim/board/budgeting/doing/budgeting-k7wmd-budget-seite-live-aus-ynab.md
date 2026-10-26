---
id: budgeting-k7wmd
title: "Budget-Seite: Monat (dieser oder nächster), Zu verteilen und Kategorien nach Gruppen mit Zugewiesen, Aktivität und Verfügbar, live aus YNAB, am Handy und am PC"
status: doing
type: feature
context: budgeting
created: 2026-10-26
completed:
depends_on: [design-system-001]
blocks: [budgeting-h2zfn]
tags: [budgeting, ui, ynab-read, mobile]
related_adrs: [0063, 0055]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-r4tkq]
---

## Why
Das Budget ist die nächste große Fähigkeit (ADR 0063). Die Seite ist die Fläche, auf der
`budgeting-h2zfn` "Targets anwenden" sitzt, und für sich nutzbar, weil sie Roman den Stand
zeigt. Sie liest live aus YNAB und speichert keine Beträge. Damit kann BB YNAB nicht
unerklärt widersprechen (Scheiter-Kriterium der Vision).

## What
Seite "Budget" im Client, über die Navigation erreichbar.
- Monat wählbar: dieser Monat (Vorgabe) und nächster Monat, als Umschalter (Budgetieren am
  30. für den 1.).
- **Zu verteilen** als große Zahl. Negativ wird einfach negativ angezeigt, kein Warnen,
  kein Sperren (Roman geht bewusst ins Minus, bis das Kindergeld am 15. kommt).
- Kategorien nach Kategoriegruppe mit Zugewiesen, Aktivität, Verfügbar. Beträge über
  `Money`. Handy: Zeilen oder Karten ohne horizontales Scrollen. PC: Tabelle.
- Server: neuer Lesepfad in `YnabClient` für `GET /budgets/{id}/months/{YYYY-MM-01}`, der
  `to_be_budgeted` und je Kategorie `budgeted`, `activity`, `balance`, Gruppe sowie
  `goal_under_funded` und `goal_snoozed_at` dekodiert (die beiden letzten braucht
  `budgeting-h2zfn`). Ausgeblendete und gelöschte Kategorien und Gruppen werden nicht
  angezeigt (Verhalten prüfen, im Outcome festhalten).
- Struktur nach dem Vorbild von `src/Server/Ledger/` und `src/Client/Components/Ledger/`:
  eigener Ordner für `budgeting`, eigene Shared-Typen, neuer Fable.Remoting-Vertrag in
  `src/Shared/Api.fs`.
- Kein Schreiben nach YNAB, keine Persistenz, keine Kategorie-Suche.

## Acceptance criteria
- [ ] `YnabClient` liest den Monat und liefert ein Modell mit `ToBeBudgeted` und je Kategorie Id, Name, Gruppenname, Zugewiesen, Aktivität, Verfügbar, Fehlbetrag (option) und `Pausiert` (bool).
- [ ] Decoder-Test mit festem JSON ohne Netz: Milliunits, `goal_under_funded` null, 0 und positiv, `goal_snoozed_at` gesetzt und null.
- [ ] Ein reiner Domain-Baustein gruppiert nach Kategoriegruppe in YNAB-Reihenfolge und lässt ausgeblendete und gelöschte Kategorien weg. Tests: leere Gruppe, mehrere Gruppen, Reihenfolge.
- [ ] Die API nimmt "dieser" oder "nächster" Monat und rechnet zu `YYYY-MM-01` um (Test Dezember zu Januar). Ein ungültiger Monat wird an der Grenze abgelehnt (Validation-Test).
- [ ] YNAB-Fehler (auch 429) kommen als lesbare Meldung an. Die Seite nutzt `RemoteData` und `ErrorDisplay` mit Wiederholen.
- [ ] Die Seite zeigt Monat, Umschalter, Zu verteilen und Gruppen mit Kategorien. Negatives Zu verteilen erscheint ohne Warnhinweis und ohne Sperre.
- [ ] Nur DS-Komponenten und Tokens, keine Inline-Farben, gemessen an `standards/frontend/styleguide.md`.
- [ ] Die Seite ist über die Navigation erreichbar, auch am Handy.
- [ ] Der bestehende Aufrufer von `monthToBeBudgetedDecoder` (Ready to Assign im Kategorie-Picker) funktioniert unverändert.
- [ ] Zu verteilen und Verfügbar stimmen im aktuellen Monat mit YNAB überein (Stichprobe, Datum und Beispielkategorien im Outcome). [human-eye]
- [ ] Lesbar am Handy-Format (etwa 390 px) und am PC, am Handy ohne horizontales Scrollen. [human-eye]
- [ ] Lesende Probe, im Outcome dokumentiert: enthält `to_be_budgeted` des nächsten Monats den Rest des laufenden Monats? (GET gegen Romans Budget erlaubt, nie PATCH, POST oder DELETE.) Ergebnis per README_DELTA ins `budgeting`-README.
- [ ] `dotnet build` und `dotnet test` grün, kein Zugriff auf die Produktiv-Datenbank, Eintrag in `diary/development.md`.

## Notes
- Muster: `monthToBeBudgetedDecoder` und der Aufruf um `src/Server/YnabClient.fs` Zeile 58 und 219 (liest heute nur `to_be_budgeted` aus `months/current`).
- Spike: `.agentheim/knowledge/research/ynab-api-targets-zuweisung-2026-10-25.md`, Abschnitt 4. Die Pfade `/budgets/` und `/plans/` gehen beide; BB nutzt `/budgets/`.
- Aktivität kommt bewusst von YNAB. Die Naht `ledger` zu `budgeting` folgt erst, wenn BB selbst rechnet (Entwurf ADR 0064 in `budgeting-h2zfn`).
- Kategorie-Suche am Handy: eigenes Backlog-Item, zusammen mit "Geld verschieben" zu schneiden.
- Kein Design-Board (Roman, 2026-10-26): aus DS-Komponenten nach Styleguide, Feinschliff am echten Stand.
