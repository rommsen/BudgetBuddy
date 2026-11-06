---
id: budgeting-dgrp7
title: "BB führt das Zugewiesen: Übernahme beim Lesen und vor jedem Befehl, Soll-Faltung mit bestätigtem Stand, observeMonth mit Echo-Regel, Befehle auf das Soll"
status: done
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [budgeting-hyn77, budgeting-qm5x9]
blocks: [budgeting-eh8xe, budgeting-dkjc5]
tags: [uebernahme, echo, ynab-spiegel, fuehrung]
related_adrs: [0055, 0065, 0069, 0074, 0083, 0084]
related_research: []
prior_art: [budgeting-h2zfn, budgeting-afd8h, budgeting-c5w2n, budgeting-hyn77]
---

## Why
Mit ADR 0084 führt BB das Zugewiesen je (Monat, Kategorie). Dafür braucht BB einen Startstand
(Übernahme), ein Soll aus den eigenen Ereignissen und eine Regel, wie es Zuweisungen in YNAB von
Echos eigener Rückschreibungen unterscheidet. Ab diesem Task rechnen Zuweisen, Verschieben und
Targets anwenden mit BBs Soll statt mit YNABs Zugewiesen. Umgeschaltet wird mit dem Deploy.

## What
- **Ereignisse** (`src/Server/Budgeting/Events.fs`): `BudgetTakenOverFromYnab` (Monat, Kategorien
  mit Id, Name, Zugewiesen) und `AssignmentChangeObservedInYnab` (Monat, Kategorie, Name,
  `previousAssigned` = Soll, `assigned` = YNAB-Wert), beide Actor `YnabMirror`, Codec und Eintrag
  in `decodePayload`, Schema-Version 1.
- **Faltung** (rein, `Domain.fs` oder eigenes Modul ohne I/O): je (Monat, Kategorie)
  `{ Assigned (Soll); Confirmed; Open: Guid option }` ab dem jüngsten `BudgetTakenOverFromYnab`
  des Monats-Streams; alles davor ist Historie und wirkt nicht.

  | Ereignis | Wirkung |
  |---|---|
  | `BudgetTakenOverFromYnab` | je Kategorie Soll = Confirmed = Wert, Open = None |
  | Entscheidung (Zuweisen, Verschieben je Seite, Anwenden je Zeile) | Soll = `assigned`, Open = dieses Ereignis |
  | `AssignmentWrittenBackToYnab` für Open | Confirmed = Soll, Open = None |
  | `AssignmentWriteBackFailed` | keine Wirkung |
  | `AssignmentChangeObservedInYnab` | Soll = Confirmed = `assigned`, Open = None |
  | `AssignmentWriteBackSupersededInYnab` | keine eigene Wirkung |
  | `AssignmentWriteBackWithdrawn` für Open | Soll = Confirmed, Open = None |
  | Ergebnis zu einer anderen als Open | keine Wirkung |
  | Kategorie ohne Eintrag | Soll = Confirmed = 0 |

  Invariante: Soll ≠ Confirmed nur mit offener Rückschreibung, und deren `assigned` = Soll.
- **Echo-Regel** (rein, je Kategorie, Vorbild `src/Server/Ledger/Domain.fs:895-999`): YNAB = Soll
  → ohne Open nichts, mit Open `AssignmentWrittenBackToYnab` (zuerst geprüft). YNAB = Confirmed
  mit Open → nichts, bleibt offen. Sonst `AssignmentChangeObservedInYnab` und bei Open zusätzlich
  `AssignmentWriteBackSupersededInYnab`. Eine Kategorie, die in der YNAB-Antwort fehlt, ergibt
  kein Ereignis.
- **Eine offene Rückschreibung je (Monat, Kategorie)**: Eine Entscheidung auf einer Kategorie mit
  Open hängt im selben Append nach sich `AssignmentWriteBackWithdrawn` für die alte an (die
  Entscheidung zuerst, damit `first_position` einer Befehls-Id auf sie zeigt). Weist Roman bei
  offener Rückschreibung denselben Wert noch einmal zu, entsteht nach `planAssign` nichts; die
  Markierung führt zu "erneut senden".
- **Übernahme** (`ensureTakenOver`, unter `MonthGuard`): liegt noch kein Übernahme-Ereignis im
  Stream, aus dem gerade gelesenen YNAB-Monat eins anhängen. Alle Kategorien außer `internal`,
  auch ausgeblendete und gelöschte. Werte ungerundet. Offene Rückschreibungen migrierter
  Entscheidungen des Monats werden im selben Append mit `AssignmentWriteBackWithdrawn`
  geschlossen. `occurred_at` ist der Zeitpunkt der Übernahme.
- **`observeMonth port now monthKey`** (`CommandHandlers.fs`): unter `MonthGuard` ein
  `FetchMonth`, `ensureTakenOver`, Echo-Regel, Ereignisse in einem Append ohne Befehls-Id.
  Liefert den rohen YNAB-Monat und die Faltung danach.
- **Lesen** (`Api.budgetMonthFor`, `QueryHandlers.budgetMonth`): ruft `observeMonth` auf und baut
  die Antwort aus dessen Rohstand und der Faltung mit der reinen Abbildung (`toBudgetMonth`
  erweitert). Ein YNAB-Aufruf je Lesen. Scheitert `FetchMonth`, scheitert das Lesen wie heute.
  Scheitert nur das Anhängen, wird geloggt und der Monat aus dem Rohstand geliefert. Zugewiesen
  zeigt das Soll; bei offener Rückschreibung trägt die Kategorie eine Markierung "noch nicht in
  YNAB" (neues Optionsfeld in `BudgetCategory`) mit dem Weg zu "erneut senden". Aktivität,
  Verfügbar und Zu verteilen bleiben YNABs Werte, ohne Korrektur um die Differenz. Die
  Parallelrechnung (`runUnderfundedComparison`) läuft weiter auf dem gelieferten Monat.
- **Befehle** (`moveMoney`, `assignMoney`, `apply`, `preview`, `setTargetPauseWith`): Reihenfolge
  unter `MonthGuard`: (1) Befehls-Id prüfen (qm5x9), (2) ein `FetchMonth`, (3) Übernahme falls
  nötig, (4) Echo, (5) Append der Übernahme und Beobachtungen, (6) planen auf der Faltung plus
  den Live-Werten, (7) Append der Entscheidung (mit Id) und gegebenenfalls `Withdrawn`,
  (8) senden. Scheitern (2), (3) oder (5): außen `Error`, keine Entscheidung. Beobachtungen bleiben
  auch bei abgelehnter Entscheidung. `OldAssigned` und `previousAssigned` kommen aus dem Soll.
  `preview` wendet die Echo-Regel nur im Speicher an.
- **Targets anwenden**: Fehlbetrag bleibt bis budgeting-eh8xe `goal_under_funded`. Hat eine
  Kategorie mit Fehlbetrag eine offene Rückschreibung, lehnt Anwenden ab, mit einer Meldung, die
  die Kategorien nennt und auf "erneut senden" verweist. Nichts wird festgehalten oder gesendet.
- **Erneut senden** (`retryFailedWith` 577): erst `observeMonth`, dann eine reine Funktion
  `planRetry` für die verbliebenen offenen Zeilen (Paarregel der Verschiebung nach ADR 0069
  Punkt 3, Abbruch bei 429, Kategorie fehlt → `Failed "Kategorie in YNAB nicht gefunden"`).
  `decideRetry` und `RetryDecision` entfallen; eine Gegenseite, deren Partner in YNAB ersetzt
  wurde, wird mit `Withdrawn` geschlossen und ihr Soll fällt auf Confirmed.
- **Raster**: Gesendete Werte liegen auf 10 Milliunits (heute schon in `planMove`, `planAssign`,
  `planTargetApplication`). Übernahme und Beobachtung übernehmen YNAB-Werte ungerundet (Nachtrag
  ADR 0084 Punkt 3).
- **Historie**: Beobachtungen und Übernahmen erscheinen in der Historie je Kategorie und der
  allgemeinen. Eine durch eine spätere BB-Entscheidung zurückgezogene Zeile wird anders benannt als
  "in YNAB geändert", abgeleitet aus dem Stream (auf sie folgt eine Entscheidung derselben
  Kategorie), ohne neuen `LineOutcome`-Fall.

## Acceptance criteria
Domain-Ebene (rein):
- [ ] Echo-Regel, je Tabellenzeile ein Test: Soll ohne Open → nichts; Soll mit Open →
      `AssignmentWrittenBackToYnab`; Confirmed mit Open → nichts; anderer Wert ohne Open →
      Beobachtung (vorher Soll, nachher YNAB); anderer Wert mit Open → Beobachtung und Superseded.
      "Ausstehend" (festgehalten, nie gesendet) verhält sich wie "fehlgeschlagen".
- [ ] Faltung, je Ereigniszeile der Tabelle ein Fall; Ereignisse vor dem Übernahme-Ereignis und
      Ergebnisse zu einer ersetzten Entscheidung ändern nichts.
- [ ] A (10 → 20) offen, dann B (20 → 30): B und `Withdrawn(A)` in einem Append; Echo mit YNAB 10
      → nichts; YNAB 30 → `WrittenBack` nur für B; `planRetry` für A sendet nichts.
- [ ] Eine Verschiebung in YNAB ergibt genau zwei `AssignmentChangeObservedInYnab`, beim
      nächsten Vergleich keins.
- [ ] YNAB-Wert neben dem 10-Milliunits-Raster: höchstens beim ersten Vergleich ein Ereignis,
      beim zweiten keins.

Handler-Ebene (In-Memory-SQLite, Fake-YNAB als Port, Uhr als Parameter, Sperre im Test haltbar).
Reihenfolgen als Aufzählung durch die echten Handler, Orakel unabhängig vom Code: (O1) die
Beobachtungen sind genau die externen Änderungen des Fakes, deren Endwert vom Soll davor abweicht;
(O2) am Ende je Kategorie entweder keine offene Rückschreibung und Soll = YNAB, oder genau eine
offene mit `assigned` = Soll; (O3) kein PATCH trägt einen Wert ungleich dem Soll beim Senden.
- [ ] Aufzählung mindestens: Lesen vor Befehl; Befehl vor Lesen; `observeMonth` wartet an der
      gehaltenen Sperre eines laufenden Befehls; Absturz zwischen Festhalten und Senden, dann
      Lesen (offen, keine Beobachtung), dann erneut senden (`WrittenBack`); Absturz, dann externe
      Änderung auf einen dritten Wert; PATCH angekommen, Antwort verloren; Verschiebung mit
      gescheiterter Quelle und externer Änderung am Ziel; Verschiebung mit geschriebener Quelle und
      gescheitertem Ziel; Wiederholung mit bekannter Befehls-Id nach Absturz vor dem Senden (kein
      YNAB-Aufruf, keine Beobachtung). In keiner Reihenfolge verletzt das Ergebnis O1 bis O3.
- [ ] Übernahme: zwei gleichzeitige Übernahmen desselben Monats (Lesen und Befehl) ergeben ein
      Übernahme-Ereignis; ein Monat mit migrierten Altereignissen wird übernommen und seine offenen
      migrierten Rückschreibungen werden `Withdrawn`; `internal` fehlt, ausgeblendete und gelöschte
      Kategorien sind dabei; `occurred_at` ist die Uhr des Tests.
- [ ] Übernahme im selben Befehl: ein `FetchMonth`, keine Beobachtung, `previousAssigned` der
      Entscheidung ist der übernommene Wert.
- [ ] YNAB nicht erreichbar an einem Monat ohne Übernahme: Zuweisen, Verschieben, Anwenden und
      Pause liefern außen `Error`, `events` bleibt unverändert.
- [ ] Targets anwenden mit offener Rückschreibung auf einer Kategorie mit Fehlbetrag: abgelehnt mit
      Nennung der Kategorie, nichts festgehalten, nichts gesendet.
- [ ] Lesen mit scheiterndem Anhängen (Port oder Datenbank wirft): Der Monat kommt trotzdem, der
      Fehler steht im Log. Lesen mit scheiterndem `FetchMonth`: Fehler wie heute.
- [ ] Lesen braucht genau einen `FetchMonth` (Port zählt).
- [ ] Lesen bei offener Rückschreibung: Zugewiesen ist das Soll, die Markierung ist gesetzt;
      ohne offene Rückschreibung fehlt sie.

Abfrage-Ebene:
- [ ] Die Historie je Kategorie und die allgemeine zeigen Beobachtung und Übernahme; eine durch
      spätere Entscheidung zurückgezogene Zeile ist von "in YNAB geändert" unterscheidbar.

Gate:
- [ ] Ändert sich ein Typ in `src/Shared` (Markierung offener Rückschreibung, Historienzeilen),
      werden die Fixtures in `scripts/*.test.mjs` im selben Schritt nachgezogen; neue Felder sind
      Optionsfelder.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Quelle: ADR 0084 Grundsatz, Punkte 3 und 5, Nachtrag zur Verfeinerung von qm5x9 und dgrp7.
  Vorbild Echo und "die erste Baseline bleibt": `src/Server/Ledger/Domain.fs:558-590` und
  `895-999`.
- Fundstellen: `planTargetApplication` `Domain.fs:78`, `isOpenLine` 278, `decideRetry` 296,
  `planAssign` 379, `retryFailedWith` `CommandHandlers.fs:577-654`, `sendInOrder` 238-255,
  `budgetMonthFor` `src/Server/Api.fs` ca. 1733.
- Entscheidungen von Roman (2026-11-05): Soll mit Markierung anzeigen, Verfügbar und Zu verteilen
  unkorrigiert; Targets anwenden bei offener Rückschreibung ablehnen; Umschalten mit dem Deploy
  ohne Setting; Raster nur für gesendete Werte; ersetzte Zeile aus dem Stream ableiten; gleicher
  Wert bei offener Rückschreibung erzeugt nichts.
- **Deploy nur zusammen mit budgeting-dkjc5** (Hintergrund-Übernahme, entschieden von Roman). Nach
  dem work-Lauf von dgrp7 nicht pushen und deployen, bis dkjc5 auf `main` ist.
- Rücksprung: Code vor diesem Task kann `BudgetTakenOverFromYnab` und
  `AssignmentChangeObservedInYnab` nicht lesen (`loadBudgetEventsOn` liefert `Error`). Vor dem
  Deploy die Produktions-Datenbank sichern; ein Revert geht nur mit Rückspielen der Sicherung.
- Hingenommen: Eine fehlgeschlagene Rückschreibung A kommt verspätet doch in YNAB an, nachdem B sie
  ersetzt hat; YNAB zeigt dann As Wert, BB hält das für eine Beobachtung. Gleiche Lücke wie im
  Kassenbuch.
- Offen, nicht Teil dieses Tasks: Wartezeit beim Lesen hinter einem laufenden Befehl desselben
  Monats. Fällt sie auf, Vergleich über die zuletzt gesehene Position prüfen.
- Live-Dienste nur in Integrationstests hinter `RUN_INTEGRATION_TESTS`.
- Sinnvoll vorher, kein Blocker: infrastructure-g4wy8 (Verbindungsabbrüche).
- Fachsprache (README-Delta, Ubiquitous language der budgeting-README): **Soll**, **Bestätigter
  Stand**, **Offene Rückschreibung** (höchstens eine je Monat und Kategorie), **Übernahme**,
  **Beobachtung**, **Echo**. Den Eintrag "Budget-Bewegung" um "Was Roman direkt in YNAB zuweist,
  taucht dort nicht auf" bereinigen (gilt nach diesem Task nicht mehr), ebenso "Liest die Beträge
  live aus YNAB" bei Budget-Seite und "Fehlbetrag ... Zugewiesen wird altes Zugewiesen plus
  Fehlbetrag" (jetzt Soll). Hinweis aus der Verifikation von budgeting-zkbbd.

## Outcome

BB führt das Zugewiesen je (Monat, Kategorie) nach ADR 0084 (Nachtrag zur Verfeinerung von qm5x9 und dgrp7). `./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 4071 grün, 13 opt-in übersprungen nur "(requires .env)"; node 310 grün).

**Ereignisse** (`src/Server/Budgeting/Events.fs`): `BudgetTakenOverFromYnab` (`month`, `categories` mit Id, Name, `assigned`) und `AssignmentChangeObservedInYnab` (`month`, `categoryId`, `categoryName`, `previousAssigned`, `assigned`), Actor `YnabMirror`, Schema-Version 1, Codec und `decodePayload`. `EventTypes.fromYnab` für die Historie.

**Reine Regeln** (`src/Server/Budgeting/Assignments.fs`, neues Modul ohne I/O): `foldMonth` (Tabelle aus dem Task, ab dem jüngsten Übernahme-Ereignis, `AssignmentState` mit `Assigned`/`Confirmed`/`Open` in `Domain.fs`), `takeoverEvents` (alle Kategorien außer `internal`, ungerundet, offene Rückschreibungen migrierter Entscheidungen mit `AssignmentWriteBackWithdrawn`), `echoOf`/`echoEvents` (Echo-Regel, fehlende Kategorie ergibt nichts), `observe` (Übernahme, dann Echo, Faltung danach), `withdrawalsFor` (eine offene Rückschreibung je Kategorie) und `planRetry` (Paarregel ADR 0069 Punkt 3, Kategorie fehlt). `Domain.toBudgetMonthWith` zeigt das Soll und setzt `BudgetCategory.OpenWriteBack`; `planTargetApplication` lehnt bei offener Rückschreibung auf einer Kategorie mit Fehlbetrag ab (`TargetsOpenWriteBackText`, nennt die Kategorien, verweist auf "erneut senden"). `decideRetry` und `RetryDecision` sind entfallen.

**Handler** (`src/Server/Budgeting/CommandHandlers.fs`): `observeMonthWith connect port now monthKey` / `observeMonth` (unter `MonthGuard` ein `FetchMonth`, Übernahme, Echo, ein Append ohne Befehls-Id; Ergebnis `ObservedMonth` oder `ObserveFailure` mit `FetchFailed`/`NotRecorded`). `moveMoney`, `assignMoney`, `apply`, `setTargetPauseWith` laufen über `preparedMonth` in der Reihenfolge Befehls-Id, `FetchMonth`, Übernahme, Echo, Append, planen auf dem Soll, Entscheidung samt `Withdrawn` (Entscheidung zuerst, `recordDecision` hat dafür den Parameter `withdrawn`), senden. Scheitert Lesen oder Festhalten: außen `Error` (`LocalReadFailed ObservationNotRecordedText`), keine Entscheidung. `preview` liegt unter der Sperre und wendet das Echo nur im Speicher an. `retryFailedWith` beobachtet zuerst und sendet nur Zeilen, deren Entscheidung noch die offene Rückschreibung der Kategorie ist.

**Lesen** (`QueryHandlers.budgetMonthWith save connect port now monthKey`, `Api.budgetMonthFor ... now`): ein YNAB-Aufruf über `observeMonthWith`; scheitert nur das Festhalten, wird geloggt und der Monat aus YNABs Stand geliefert. `budgetMonthOf`/`budgetMonthOfWith` sind entfallen (kein Aufrufer mehr).

**Historie** (`Persistence.queryMovements`, `src/Shared/Budgeting.fs`): `MovementOrigin` hat `ObservedInYnab` und `TakenOverFromYnab` (`isFromYnab`), `HistoryMovement.ReplacedBy: Guid option` (abgeleitet aus dem Stream: jüngste Entscheidung derselben Kategorie vor dem Zurückziehen ist eine andere), `HistoryStatus.HistoryReplacedInBb` ("in BB ersetzt"), `RunStatus.RunReplacedInBb` und `RunFromYnab` (ohne Badge). Texte: "aus YNAB übernommen, 100,00 €", "in YNAB zugewiesen", "Aus YNAB übernommen, N Kategorien", "In YNAB zugewiesen, <Kategorie>". Der Satz "Was direkt in YNAB zugewiesen wurde, steht hier nicht." ist entfernt.

**Client**: Markierung "noch nicht in YNAB" (`Month.NotInYnabText`) am Handy in der zweiten Zeile (`mobileFiguresText`), am PC als Badge hinter dem Namen, im Tab Geld mit Knopf "Erneut senden" (neue Nachricht `RetryWriteBack`, läuft über `retryMoveCmd`/`MoveRetried`).

**Tests**: rein in `src/Tests/BudgetAssignmentsTests.fs` (Echo je Zeile, Faltung je Ereigniszeile, A/B mit `Withdrawn`, Verschiebung in YNAB, Raster, Übernahme, `planRetry`, Monat mit Soll und Ablehnung); Handler in `src/Tests/BudgetLeadTests.fs` mit eigenem Monat je Test, Fake-YNAB als Port und Uhr als Parameter: Aufzählung der Reihenfolgen mit Orakeln O1 (Beobachtungen je Szenario von Hand bestimmt), O2 (aus den Ereignissen abgezählt, gegen Seite und Fake) und O3 (Fake prüft den Stream vor jedem PATCH), dazu Übernahme (gleichzeitig, migriert, internal, Uhr, im selben Befehl), YNAB weg, Targets anwenden abgelehnt, Lesen mit scheiterndem Anhängen (Trigger in der Datenbank und werfender Verbindungs-Port, Log über stderr), ein `FetchMonth`, Markierung, gleicher Wert, Append-Reihenfolge mit `first_position`, Historie je Kategorie und allgemein. Bestehende Tests: `movementRows` zählt nur Entscheidungen, `recordedMove` beobachtet vorher, zwei qm5x9-Tests auf die neue Ablehnung angepasst, die Migrations-Retry-Erwartung folgt der Übernahme, `decideRetry`-Test entfernt. Fixtures in `scripts/*.test.mjs` tragen `OpenWriteBack: null` und `ReplacedBy: null`.

### Für den Conductor

- Nicht deployen, bis budgeting-dkjc5 auf `main` ist (Task-Notes). Vor dem Deploy die Produktions-Datenbank sichern: alter Code kann `BudgetTakenOverFromYnab` und `AssignmentChangeObservedInYnab` nicht lesen.
- ADR 0084, Nachtrag vorschlagen: (a) Die Paarregel beim erneuten Senden zieht eine Seite auch zurück, wenn ihr Partner von einer späteren BB-Entscheidung ersetzt wurde (nicht nur in YNAB). (b) Scheitert beim Lesen nur das Festhalten, zeigt der Monat YNABs Zugewiesen ohne Markierung. (c) Beobachtung und Übernahme erscheinen in der Historie als zwei neue `MovementOrigin`-Fälle ohne Stand.
- Override (C): Reviews vorschlagen: `qa-milestone-reviewer` (Tests), `backend-code-quality` (src/Server), `frontend-architecture` (src/Client), `design-quality` (Markierung und Knopf im Tab Geld).
