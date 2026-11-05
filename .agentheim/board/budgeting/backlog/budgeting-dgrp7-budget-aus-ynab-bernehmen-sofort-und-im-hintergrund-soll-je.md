---
id: budgeting-dgrp7
title: "Budget aus YNAB übernehmen (sofort und im Hintergrund), Soll je Monat und Kategorie aus BB falten, YNAB-Änderungen über observeMonth mit Echo-Regel erkennen"
status: backlog
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [budgeting-hyn77]
blocks: [budgeting-eh8xe]
tags: [uebernahme, echo, ynab-spiegel, fuehrung]
related_adrs: [0055, 0065, 0074, 0083, 0084]
related_research: []
prior_art: [budgeting-h2zfn, budgeting-afd8h, budgeting-c5w2n]
---

## Why
Mit ADR 0084 führt BB das Zugewiesen je (Monat, Kategorie). Dafür braucht BB einen Startstand
(Übernahme), ein Soll aus den eigenen Ereignissen und eine Regel, wie es YNAB-Änderungen und
Echos eigener Rückschreibungen erkennt.

## What
- Übernahme: beim Umschalten laufender und künftige Monate sofort, ältere im Hintergrund unter
  der YNAB-Rate-Grenze (rund 200 Anfragen pro Stunde); ein noch nicht übernommener Monat wird beim
  Lesen übernommen. Ein `BudgetTakenOverFromYnab` je Monat unter `MonthGuard`, ohne Kategorien
  `internal`. Idempotent über "Übernahme-Ereignis schon im Stream", nicht über Version 1.
- Soll-Faltung: Zugewiesen je (Monat, Kategorie) aus Übernahme, Entscheidungen und Beobachtungen
  des Monats-Streams, ab dem Übernahme-Ereignis.
- `observeMonth`: eigener Befehl, liest YNAB unter `MonthGuard`, vergleicht nach der Echo-Regel
  (ADR 0084 Punkt 3) und hängt die Ereignisse an. Das Lesen eines Monats ruft ihn danach auf;
  `QueryHandlers.budgetMonthOf` bleibt rein; ein Fehler wird nur geloggt.
- Jeder Befehl auf einem Monat beobachtet zuerst und entscheidet dann.
- Rasterprüfung (10 Milliunits) auf allen Wegen, auch bei der Übernahme.
- `AssignmentWriteBackWithdrawn` dort, wo heute ein Ziel ungesendet geschlossen wird.

## Acceptance criteria
- [ ] Echo-Regel als reine Funktion mit je einem Test pro Tabellenzeile: Soll ohne offene
      Rückschreibung → nichts; Soll mit offener → `AssignmentWrittenBackToYnab`; Baseline mit
      offener → nichts, bleibt offen; anderer Wert → `AssignmentChangeObservedInYnab` (vorher
      BB-Soll, nachher YNAB-Wert) und bei offener Rückschreibung zusätzlich
      `AssignmentWriteBackSupersededInYnab`. "Ausstehend" (festgehalten, nie gesendet) verhält
      sich wie "fehlgeschlagen".
- [ ] Reihenfolgen-Tests unter `MonthGuard` (Ports als Funktionswerte, Uhr als Parameter): Lesen
      vor Senden, Senden vor Lesen, Lesen während eines laufenden Befehls desselben Monats,
      Absturz zwischen Festhalten und Senden. In keiner Reihenfolge entsteht eine Beobachtung, die
      BBs eigene Zuweisung für eine YNAB-Änderung hält.
- [ ] GIVEN YNAB zeigt einen Wert neben dem 10-Milliunits-Raster oder BB hätte einen solchen
      Soll-Wert, WHEN der Monat zweimal gelesen wird, THEN entsteht höchstens beim ersten Lesen
      ein Ereignis (keine Beobachtung in Dauerschleife).
- [ ] Eine Verschiebung in YNAB ergibt genau zwei `AssignmentChangeObservedInYnab`.
- [ ] Übernahme: Zwei gleichzeitige Übernahmen desselben Monats ergeben ein Übernahme-Ereignis;
      ein Monat mit migrierten Altereignissen wird trotzdem übernommen; Kategorien `internal`
      fehlen im Ereignis; `occurred_at` ist der Zeitpunkt der Übernahme.
- [ ] Hintergrund-Übernahme: hält die Rate-Grenze ein (Test mit gezähltem Port und Uhr als
      Parameter), bricht bei 429 ab und setzt später fort; ein vorher gelesener Monat wird dabei
      übernommen und im Hintergrund nicht noch einmal.
- [ ] Scheitert `observeMonth`, liefert das Lesen des Monats trotzdem den Monat (Test mit
      scheiterndem Port), und der Fehler steht im Log.
- [ ] Zuweisen, Verschieben und Targets anwenden rechnen mit dem Soll aus der Faltung; der Bericht
      einer Anwendung und die Historie zeigen Beobachtungen und Übernahmen.
- [ ] Ändert sich ein Feld in `src/Shared` (etwa neue Historienzeilen), werden die Fixtures in
      `scripts/*.test.mjs` im selben Schritt nachgezogen.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Quelle: ADR 0084 Grundsatz, Punkte 3 und 5. Vorbild Echo: `src/Server/Ledger/Domain.fs:900-999`;
  Vorbild Anstoß beim Seitenaufruf: `src/Server/Ledger/CommandHandlers.fs:1028-1034`.
- Live-Dienste nur in Integrationstests hinter `RUN_INTEGRATION_TESTS`.
- Offen, nicht Teil dieses Tasks: Wartezeit beim Lesen hinter einem laufenden Befehl desselben
  Monats. Fällt sie auf, Vergleich über die zuletzt gesehene Position prüfen.
- Sinnvoll vorher, kein Blocker: infrastructure-g4wy8 (Verbindungsabbrüche).
- Fachsprache: "Soll", "Beobachtung" und "Übernahme" bekommen hier einen Eintrag in der Ubiquitous language der budgeting-README (Hinweis aus der Verifikation von budgeting-zkbbd).
