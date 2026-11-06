---
id: budgeting-hbhzf
title: "Fehlbetrag aus BB: Targets anwenden und die Anzeige rechnen mit BBs Fehlbetrag gegen das Soll, Rückbau der Parallelrechnung"
status: done
type: feature
context: budgeting
created: 2026-11-06
completed:
depends_on: [budgeting-eh8xe, budgeting-r4kqm, budgeting-b9869, budgeting-pf8n7, budgeting-k5q2w]
blocks: [budgeting-m3x7f]
tags: [targets, fehlbetrag, rueckbau, fuehrung]
related_adrs: [0083, 0084]
related_research: []
prior_art: [budgeting-v7kfw, budgeting-dgrp7]
---

## Why
Nach budgeting-eh8xe führt BB die Targets, die Goals in YNAB sind eingefroren. YNABs
`goal_under_funded` rechnet dann mit veralteten Goals. "Targets anwenden" und die Anzeige "fehlt"
brauchen BBs eigenen Fehlbetrag, gerechnet gegen das Soll aus der Faltung (budgeting-dgrp7).

Teil 2 von 2 der Teilung von budgeting-eh8xe (2026-11-06).

## What
- Der Fehlbetrag je Kategorie kommt aus der reinen Funktion `underfunded` (budgeting-v7kfw) über
  BBs Target-Faltung und das Soll, statt aus `goal_under_funded`.
- "Targets anwenden" nutzt BBs Fehlbetrag. Die Ablehnung bei offener Rückschreibung aus dgrp7
  entfällt, weil gegen das Soll gerechnet wird.
- Die Anzeige "fehlt" auf der Budget-Seite nutzt BBs Fehlbetrag.
- Rückbau der Parallelrechnung: `underfunded_comparisons`, `saveUnderfundedComparisons*`,
  `compareUnderfunded` und das Speichern beim Lesen eines Monats.

### Verfeinerung 2026-11-06 (nach budgeting-k5q2w)
- **Eine Stelle:** Der Fehlbetrag entsteht in `Domain.toBudgetCategory` (heute `Underfunded = c.Underfunded`).
  Dort `underfunded target month assigned carryover activity earlier` mit Zugewiesen = BBs Soll, Übertrag
  live = YNABs Verfügbar − YNABs Zugewiesen − Aktivität (unabhängig vom Soll), Aktivität = YNABs Aktivität des
  Monats. `Computed x` wird `Some x`, `NotComputed` wird `None` (wie heute ein fehlender YNAB-Wert: "fehlt"
  zeigt nichts, Targets anwenden lässt die Kategorie weg). Ohne Target `None`.
- **Ausgaben der Periode aus dem Kassenbuch** über `LedgerActivityPort` und `periodActivityFromLedger`
  (budgeting-k5q2w). `toBudgetMonthWith` bekommt die Aktivität der Periodenmonate als Parameter, bleibt rein.
  Die nötigen Monate hängen nur an den Targets und am Monat: `periodMonthsNeeded` und `periodCategoriesNeeded`
  so umstellen, dass sie ohne fertigen `BudgetMonth` auskommen (sonst Henne und Ei).
- **Alle Aufrufer laden die Aktivität vorher**, je eine Abfrage für alle Kategorien: `QueryHandlers.budgetMonthWith`
  (Seite), die Vorschau und Targets anwenden in `CommandHandlers` (heute `toBudgetMonthWith (Some ...) targets
  paused raw`), Targets prüfen (`toBudgetMonth`), sofern es den Fehlbetrag nutzt. Jede Stelle, die den Live-Port
  anschließt, ist belegt (Doktrin Punkt 3). Scheitert das Lesen des Kassenbuchs beim Lesen der Seite: der Monat
  kommt ohne BB-Fehlbetrag (alle `None`) und der Grund steht im Log; vor einem Befehl (Targets anwenden) ist es
  ein Fehler mit konkreter Meldung, damit nichts mit fehlenden Ausgaben zugewiesen wird.
- **Rückbau:** `underfunded_comparisons` (Schema wie bei `category_month_activity` über `sqlite_master` entfernen,
  nicht bei jedem Start ein DROP), `saveUnderfundedComparisons*`, `compareUnderfunded`, `comparisonSummary`,
  `runUnderfundedComparison`, der `save`-Parameter von `budgetMonthWith`, `YnabWithoutValueText` und was nur der
  Vergleich braucht. `YnabMonthCategory.Underfunded` und das Lesen von `goal_under_funded` entfallen, wenn kein
  Leser bleibt. Das Ereignis `TargetsApplied` behält seinen Fehlbetrag je Zeile (Historie).
- **Ablehnung bei offener Rückschreibung entfällt** (`TargetsOpenWriteBackText`): gerechnet wird gegen das Soll.
- Kommentare, die "der Fehlbetrag ist YNABs" sagen (`toBudgetCategory`, `planTargetApplication`,
  `Shared/Budgeting.fs` am Feld), nachziehen.

## Acceptance criteria
- [ ] GIVEN eine Kategorie mit in BB geändertem Target, WHEN "Targets anwenden" läuft, THEN weist
      es BBs Fehlbetrag zu, nicht YNABs `goal_under_funded` (Handler-Test mit abweichendem
      YNAB-Wert).
- [ ] GIVEN eine offene Rückschreibung im Monat, WHEN "Targets anwenden" läuft, THEN rechnet es
      gegen das Soll und lehnt nicht ab.
- [ ] Die Zeile zeigt "fehlt" aus BBs Fehlbetrag (Test der Projektion).
- [ ] GIVEN ein Target "Mit Fälligkeit" mit Ausgaben der Periode im Kassenbuch, WHEN der Monat gelesen wird bzw.
      Targets anwenden läuft, THEN zählen diese Ausgaben als gedeckt (Handler-Test mit Kassenbuch-Buchungen).
- [ ] GIVEN BB kann den Fehlbetrag nicht rechnen (`NotComputed`), THEN zeigt die Zeile kein "fehlt" und Targets
      anwenden lässt die Kategorie weg.
- [ ] GIVEN das Kassenbuch ist nicht lesbar, WHEN die Seite gelesen wird, THEN kommt der Monat ohne BB-Fehlbetrag
      und der Grund steht im Log; WHEN Targets anwenden läuft, THEN wird mit konkreter Meldung abgelehnt und nichts
      zugewiesen.
- [ ] `underfunded_comparisons` wird nicht mehr geschrieben; `compareUnderfunded` und
      `saveUnderfundedComparisons*` sind aus Code und Tests verschwunden.
- [ ] Die Umschaltung ist im Outcome als von Roman freigegeben vermerkt, mit der Toleranz aus den
      Notes.
- [ ] Die Fixtures in `scripts/*.test.mjs` werden im selben Schritt an geänderte Typen aus
      `src/Shared` angepasst.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- **Toleranz offen (Owner).** Bedingung für die Umschaltung nach ADR 0084: Parallelrechnung aus
  budgeting-v7kfw und Toleranz von Roman. Stand 2026-11-06: In Prod läuft die Parallelrechnung erst
  seit dem Deploy von dgrp7 (09:25), `underfunded_comparisons` ist leer. Sie füllt sich, wenn ein
  Monat auf der Budget-Seite gelesen wird. Die Daten müssen vor dem Deploy von budgeting-eh8xe
  entstehen, danach weichen YNABs Goals absichtlich ab und der Vergleich sagt nichts mehr.
- **Toleranz (Roman, 2026-11-06):** Umschalten, wenn jede Abweichung im laufenden Monat einen
  bekannten Grund hat (etwa ein YNAB-Sonderfall). Die Liste wird einmal mit Roman durchgegangen,
  das Ergebnis kommt hierher. Roman öffnet dafür in Prod den laufenden und zwei, drei frühere Monate.
- **Vergleich 2026-11-06 (Mai bis Dezember, 100 Kategorien mit Target):** "Nicht gerechnet" nur bei
  monatlichen Targets in vergangenen Monaten, YNAB liefert dort keinen Wert (erklärt, YNAB-Sonderfall).
  Alle Abweichungen betreffen "Mit Fälligkeit" und gehen auf einen BB-Fehler zurück: budgeting-r4kqm.
  Nach dessen Deploy wird erneut verglichen.
- **Vor dem Umschalten zu klären (Review zu budgeting-b9869, Backend W1):** Die Aktivität eines Monats in
  `category_month_activity` gilt für immer, sobald sie einmal gelesen wurde. Wurde ein Monat zuletzt vor
  seinem Ende gelesen, zählt BB nur einen Teil seiner Ausgaben und rechnet einen falschen Betrag statt
  "nicht gerechnet". Bob bucht bis 31 Tage zurück, Import und Spiegel bringen ältere Buchungen nach.
  Optionen: `read_at` prüfen (Monat gilt als fehlend, solange er nicht nach Monatsende plus 31 Tagen
  gelesen wurde) oder die Aktivität aus dem Kassenbuch nehmen
  (`Ledger.QueryHandlers.getMonthlyActivityByCategory`, immer aktuell, ohne YNAB). **Entschieden von Roman
  am 2026-11-06: Kassenbuch.** Umgesetzt in zwei Schritten: budgeting-k5q2w stellt die Parallelrechnung um
  und baut `category_month_activity` zurück, danach fünfter Vergleich in Prod, erst dann schaltet diese Task um.
- **Vierter Vergleich 2026-11-06 (nach budgeting-pf8n7, November 2026, Monate ab Dezember 2024 gelesen):** 99
  von 100 gleich, keine Abweichung, eine Kategorie "nicht gerechnet" (erste Periode vor Dezember 2024). Von
  Roman als erfüllte Toleranz angenommen.
- ~~Nach der Integration von budgeting-eh8xe (Review zu b9869): Reihenfolge in `observeUnguarded`, Name
  `logReadFailure`.~~ Entfällt mit dem Rückbau von `category_month_activity` in budgeting-k5q2w.
- ~~Deploy nur zusammen mit budgeting-eh8xe.~~ eh8xe und k5q2w sind seit 2026-11-06 15:02 in Prod.
- **Umschaltung freigegeben von Roman am 2026-11-06** nach dem fünften Vergleich (November 2026, Aktivität aus
  dem Kassenbuch, BBs Regeln aus der Übernahme): 100 von 100 gleich, keine Abweichung, kein "nicht gerechnet".
  Die Toleranz (jede Abweichung hat einen bekannten Grund) ist erfüllt.
- Bis zum Deploy dieser Task ändert Roman kein Target in BB.

## Outcome

Die Umschaltung ist von Roman am 2026-11-06 freigegeben, nach dem fünften Vergleich in Prod (November 2026, Aktivität aus dem Kassenbuch, BBs Regeln aus der Übernahme): 100 von 100 gleich, keine Abweichung, kein "nicht gerechnet". Die Toleranz aus den Notes (jede Abweichung im laufenden Monat hat einen bekannten Grund) ist damit erfüllt.

Targets anwenden, die Vorschau und "fehlt" rechnen jetzt mit BBs Fehlbetrag. YNABs `goal_under_funded` wird nicht mehr verwendet. Die Formel ist unverändert.

- `src/Server/Budgeting/Domain.fs`: Der Fehlbetrag wird an einer Stelle gerechnet, in `toBudgetCategory` über `toBudgetMonthWith assignments periodActivity targets paused raw`. Die Eingaben: Zugewiesen ist das Soll, der Übertrag ist YNABs Verfügbar − YNABs Zugewiesen − Aktivität (unabhängig vom Soll), die Aktivität ist YNABs Aktivität des Monats. `Computed x` wird `Some x`, `NotComputed` und "ohne Target" werden `None`. Bei `periodActivity = None` hat keine Zeile einen Fehlbetrag. `toBudgetMonth` (Targets prüfen, nutzt den Fehlbetrag nicht) übergibt `Some Map.empty`. `periodMonthsNeeded` und `periodCategoriesNeeded` brauchen nur noch BBs Targets und den Monat, die Monate kommen aufsteigend. Der reine Adapter `periodActivityFromLedger` liegt jetzt hier. `planTargetApplication` lehnt bei offener Rückschreibung nicht mehr ab. Entfernt: `TargetsOpenWriteBackText`, `ComparisonOutcome`, `comparisonOutcomeText`, `YnabWithoutValueText`, `UnderfundedComparison`, `compareUnderfunded`, `comparisonSummary`, `underfundedRuleText`.
- `src/Server/Budgeting/CommandHandlers.fs`: Hierher verschoben sind `LedgerActivityPort` und `liveLedgerActivityPort`. Neu sind `loadPeriodActivity` (eine Abfrage für alle Kategorien) und `PeriodActivityUnreadableText`. Neu sind auch `previewWithLedger` und `applyWithLedger`; `previewWith` und `applyWith` rufen sie mit dem Live-Port auf. Ist das Kassenbuch nicht lesbar, lehnen Vorschau und Targets anwenden mit `LocalReadFailed` ab: "Die Ausgaben der Periode aus dem Kassenbuch sind nicht lesbar. BB rechnet den Fehlbetrag deshalb nicht und weist nichts zu." Der Grund steht im Log, nichts wird festgehalten oder gesendet. `preparedMonth` bekommt den Port als `option`. Verschieben, Zuweisen und Pausieren übergeben `None` und lesen das Kassenbuch nicht.
- `src/Server/Budgeting/QueryHandlers.fs`: `budgetMonthWithLedger ledger connect port now monthKey` lädt die Ausgaben nach den Targets. Scheitert das, steht der Grund im Log und der Monat kommt ohne Fehlbetrag. Der Parameter `save` ist weg, ebenso `runUnderfundedComparison`.
- `src/Server/Budgeting/Persistence.fs`: `underfunded_comparisons` wird nicht mehr angelegt. `dropUnderfundedComparisonsOn` entfernt die Tabelle nur, wenn `sqlite_master` sie zeigt (wie `dropMonthActivityOn`, kein DROP bei jedem Start). `saveUnderfundedComparisons*` ist entfernt.
- `src/Shared/Budgeting.fs` und `src/Server/YnabClient.fs`: `YnabMonthCategory.Underfunded` und das Lesen von `goal_under_funded` sind entfernt, weil kein Leser bleibt. `YnabMonthCategory` geht nicht über Remoting. Deshalb ändert sich keine Fixture in `scripts/*.test.mjs`, und `BudgetCategory` bleibt unverändert. Die Kommentare am Feld, in `toBudgetCategory`, in `planTargetApplication` und in `src/Client/Components/Budget/Month.fs` sind angepasst.
- Tests: `src/Tests/UnderfundedComparisonTests.fs` heißt jetzt `src/Tests/UnderfundedTests.fs`. Die Formeltests sind unverändert. Neu sind Projektionstests: "fehlt" gegen das Soll, Übertrag unabhängig vom Soll, Ausgaben der Periode, "nicht gerechnet" ohne "fehlt" und weggelassen, ohne Target, ohne Ausgaben. Die Kassenbuch-Tests prüfen jetzt BBs Fehlbetrag der Seite statt der Abweichungsliste. Dazu kommen ein Handler-Test für Vorschau und Targets anwenden mit Kassenbuch-Buchungen, das nicht lesbare Kassenbuch für die Seite (mit Log, auch ein monatliches Target ohne Fehlbetrag) und für Vorschau und Targets anwenden (Meldung, nichts gesendet, nichts festgehalten) und der Schema-Rückbau auf einer eigenen DB. In `src/Tests/TargetsApplyTests.fs` steht ein neuer Test: ein in BB geändertes Target weist BBs 80 zu, YNABs Goal zeigt 50. In `src/Tests/BudgetLeadTests.fs` ersetzt "mit offener Rückschreibung rechnet gegen das Soll und lehnt nicht ab" den Ablehnungstest. Die Fake-Ports bekommen Goals statt eines YNAB-Fehlbetrags. Entfallen sind die Tests, die nur Vergleich und Speichern prüften (Vergleich, Speicherfunktion, `underfunded_comparisons`), der Decoder-Test für `goal_under_funded` und der reine Ablehnungstest in `BudgetAssignmentsTests`. Die Tests, die das Server-Log über `Console.SetError` fangen, laufen in der Gruppe `testSequencedGroup "Server-Log"`. Grund: Parallel liefen sie sich in die Quere, ein Lauf war rot.
- Regressionstests vor der Änderung rot gesehen, mit den alten Signaturen: Targets anwenden mit geändertem Target (Vorschau 50 statt 80), offene Rückschreibung (Vorschau abgelehnt), die Zeile zeigt "fehlt" aus BBs Wert (YNAB 30), Übertrag unabhängig vom Soll, "nicht gerechnet" ohne "fehlt", ohne Target kein Fehlbetrag, die Seite mit Kassenbuch-Ausgaben (YNAB-Wert statt 0), Targets anwenden mit Kassenbuch-Ausgaben (YNAB-Wert statt 5). Das sind 8 von 8 rot.
- Live-Anschluss (Doktrin Punkt 3). `CommandHandlers.liveLedgerActivityPort` wird an drei Stellen angeschlossen: `QueryHandlers.budgetMonthWith` (`budgetMonth` ruft es auf, `Api.budgetMonthFor` ruft `budgetMonth` auf, unverändert), `CommandHandlers.previewWith` (über `preview` aus `Api`) und `CommandHandlers.applyWith` (über `apply` aus `Api`). Der Endpoint ist unverändert. Gegenprobe mit Mutanten, jeweils der ganze Expecto-Lauf:
  - M1, Seite mit leerem Port: 2 Tests rot.
  - M2, Vorschau mit leerem Port: 1 rot.
  - M3, Anwenden mit leerem Port: 1 rot.
  - M4, `liveLedgerActivityPort.LoadMonthlyActivity` leer: 3 rot.
  - M5, Übertrag mit dem Soll: 1 rot.
  - M6, `preparedMonth None` bei Targets anwenden: 3 rot (dazu Folgefehler).
  - M7, Seite bei Kassenbuch-Fehler mit `Some Map.empty` statt `None`: zunächst überlebt. Daraufhin bekam der Test ein monatliches Target, danach 1 rot.
  Kein Überlebender in dieser Liste. `LoadFirstEntryDate` belegt weiter "Die erste Buchung des Kassenbuchs zählt ohne gelöschte" über den Live-Port.
- `./verify.sh`: VERIFY: PASS (dotnet 4140 grün, 13 übersprungen "(requires .env)", node 311 grün). Der erste Lauf war rot an einem `[layout]`-Test in `scripts/move-money.test.mjs` (Popover 778 gegen 775 px). Dieser Test berührt diese Änderung nicht. Allein lief die Datei grün, der zweite ganze Lauf war grün.
- Offen in der README: Die offene Frage zu "Monatlich mit `goal_day`" nennt `goal_under_funded` des Vormonats. Sie ist kein Bullet mit fettem Kopf und per Delta nicht erreichbar, sie blieb unverändert.

### Integration (2026-11-06)
- Verifier: Iteration 1 und 2 FAIL nur wegen der Doku (README-Delta, ADR-Nachtrag, ein Doc-Kommentar), Code und Tests in beiden PASS. Korrigiert: "entscheiden nichts" statt "halten nichts fest", nach Befehl getrennt (Targets anwenden hat Übernahme und Beobachtung schon festgehalten, die Vorschau höchstens eine fällige Target-Übernahme); Bullet "Vorschau" an BBs Fehlbetrag und an die Target-Übernahme angepasst. Integriert mit den Formulierungen des Verifiers, ohne dritte Runde.
- Offene Frage "Monatlich mit `goal_day`" als gegenstandslos durchgestrichen (kein Goal-Schreibweg mehr, Fehlbetrag ist BBs).
- Nachtrag zu ADR 0084 Punkt 7 (Umschaltung) angehängt.
- Kann-Funde aus der Verifikation in budgeting-h9c3w.
