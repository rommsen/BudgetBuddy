---
id: budgeting-dkjc5
title: "Hintergrund-Übernahme: laufender und künftige Monate zuerst, dann ältere, unter YNABs Rate-Grenze"
status: doing
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [budgeting-dgrp7]
blocks: []
tags: [uebernahme, hintergrund, rate-limit, fuehrung]
related_adrs: [0071, 0074, 0083, 0084]
related_research: []
prior_art: []
---

## Why
ADR 0084 Punkt 5: Ältere Monate werden nach und nach im Hintergrund übernommen, damit die
Historie keine Lücken behält, solange Roman einen Monat nicht öffnet. budgeting-dgrp7 übernimmt
nur beim Lesen und vor Befehlen. Dieser Task schließt die übrigen Monate unter YNABs Rate-Grenze
(rund 200 Anfragen pro Stunde je Token, rund 97 Monate).

## What
- **Warteschlange** als reine Funktion in `src/Server/Budgeting/Domain.fs`, etwa
  `takeoverQueue currentMonth months takenOver`: laufender Monat (Berliner Kalendermonat), dann
  künftige Monate aus `GET /months` aufsteigend, dann ältere absteigend, ohne die schon
  übernommenen. Fehlt der laufende Monat in der Liste, steht er trotzdem vorn.
- **Schritt mit Ports** (`TakeoverPort`: Settings, `FetchMonths`, übernommene Monate laden,
  `ensureTakenOver` aus dgrp7): liefert `TookOver | NothingLeft | RateLimited | Failed |
  NotConfigured | MonthNotOpen`. Übernommene Monate kommen mit einer Abfrage aus `events`
  (Kontext `budgeting`, Typ `BudgetTakenOverFromYnab`), kein N+1. `ensureTakenOver` prüft unter
  `MonthGuard` zuerst den Stream und ruft YNAB nur ohne Übernahme auf.
- **Takt** als reine Funktion `nextDelay`: nach `TookOver` und `MonthNotOpen` 1 Minute (höchstens
  60 Monatsaufrufe je Stunde), nach `RateLimited` und `Failed` 15 Minuten, nach `NotConfigured` 15
  Minuten (Settings werden dann neu gelesen), nach `NothingLeft` 6 Stunden, danach Liste neu holen.
  `RateLimitExceeded 60` aus `YnabClient` wird nicht als Wartezeit genommen.
- **Dienst** `src/Server/Budgeting/TakeoverWorker.fs` (BackgroundService nach dem Vorbild
  `BankConnectionWorker.fs`) mit injizierter Wartefunktion `delay: TimeSpan -> Async<unit>`.
  Registrierung nur in `Program.fs` (Wächter `findHostedServiceRegistrations`, ADR 0071 Punkt 8).
  Der Serverstart hängt nicht von YNAB ab. Kein gespeicherter Fortschritt: Der Stream ist die
  einzige Quelle, nach einem Neustart beginnt ein neuer Durchlauf.
- Logging: Fehler mit Typname, Wechsel nach `NotConfigured` und 401 einmal je Zustandswechsel,
  keine Tokens.

## Acceptance criteria
- [ ] Domain: `takeoverQueue` ordnet laufend, künftig aufsteigend, älter absteigend, ohne
      Übernommene; laufender Monat außerhalb der Liste steht vorn. `nextDelay` je Ergebnis ein Fall.
- [ ] Schleife mit Fake-Wartefunktion (summiert simulierte Zeit) und gezähltem Monatsport: Über
      eine simulierte Stunde höchstens 60 Monatsaufrufe; nach 429 der nächste Aufruf erst nach 15
      simulierten Minuten; ein vorher durch Lesen übernommener Monat kostet keinen Monatsaufruf und
      bekommt kein zweites Übernahme-Ereignis.
- [ ] Ohne Token oder Budget: kein YNAB-Aufruf, nach 15 Minuten neue Prüfung; mit Token danach
      läuft die Übernahme ohne Neustart an.
- [ ] Neustart mitten im Durchlauf: Der neue Durchlauf übernimmt nur die fehlenden Monate.
- [ ] Ein 404 für einen Monat überspringt nur ihn; die übrigen laufen weiter.
- [ ] Jede Stelle, die den Live-Port anschließt (`Program.fs`, Live-Port-Funktion), ist nach
      Test-Doktrin Punkt 3 aufgelistet und per Verhalten oder vorhandenem Wächter belegt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Quelle: ADR 0084 Punkt 5, Nachtrag zur Verfeinerung von qm5x9 und dgrp7.
- **Deploy zusammen mit budgeting-dgrp7** (entschieden von Roman am 2026-11-05): dgrp7 geht erst
  mit diesem Task in Produktion. Takt 1 Monat je Minute, ebenfalls von Roman entschieden.
- Ein zentraler Zähler aller YNAB-Aufrufe je Token ist nicht Teil dieses Tasks; erst wenn 429 im
  Alltag auftritt.
- Grenze (hingenommen, Roman): Die Streams tragen keine Budget-Id. Ein Wechsel von
  `ynab_default_budget_id` ließe die Übernahmen des alten Budgets gelten. In der README als Grenze
  nennen.
- Live-Dienste nur in Integrationstests hinter `RUN_INTEGRATION_TESTS`.
