---
id: budgeting-xp46a
title: "Target anlegen und Art wechseln im Tab Target: NEED monatlich, wöchentlich oder jährlich, optional mit Datum in zwei Aufrufen, Warnung vor dem Verlust von Tag und Datum"
status: done
type: feature
context: budgeting
created: 2026-11-04
completed:
depends_on: [design-system-001, budgeting-k8wq3, budgeting-r3wq8]
blocks: []
tags: [budget, target, ynab-write]
related_adrs: [0072, 0074, 0080]
related_research: [ynab-reihenfolge-monate-target-schreiben-2026-11-02, ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-k8wq3, budgeting-q7m2c]
---

## Why
Teil C von budgeting-t6p8z. Kategorien ohne Target bekommen bisher nur in YNAB eins. Roman will sie in BB anlegen und den Rhythmus eines Targets wechseln. Die API lässt nur NEED zu, über `goal_frequency` monatlich, wöchentlich oder jährlich. `goal_frequency` ersetzt das Target: Das Datum geht immer verloren, der Tag beim Wechsel der Art (ADR 0072 Punkte 4 und 5).

## What
Baut auf dem Endpoint `changeCategoryTarget`, `planTargetWrite`, `TargetPatch` und den Tabellen aus budgeting-k8wq3 auf. Kein neuer Endpoint.

**Anlegen**
- `Shown = None`, `Wanted = Some draft` mit `Rhythm = SetRhythm Monthly | Weekly | Yearly`, Betrag und optional Datum. `NeedsWholeAmount` sendet BB nicht, YNAB legt dann "Set aside another" an (Spec: ohne Feld NEED; F6).
- `planTargetWrite` liefert: ohne Datum einen Patch `GoalTarget = SetTo`, `Frequency = Some f`. Mit Datum zwei Patches: erst `GoalTarget` und `Frequency`, dann nur `TargetDate`. Das gilt für alle drei Rhythmen: belegt ist die Ablehnung im einen Aufruf nur für jährlich (Research Abschnitt 3), zwei Aufrufe sind für alle gefahrlos.

**Wechsel der Art**
- Ein echter Wechsel der Art liegt vor, wenn `SetRhythm f` vom gelesenen Rhythmus abweicht (Woche, Monat, Jahr) oder die heutige Art kein NEED ist (TB, TBD, MF). Gleicher Rhythmus mit anderem Betrag oder Datum ist kein Wechsel, dann sendet BB nie `goal_frequency` (sonst ginge auch bei "jährlich auf jährlich" das Datum verloren).
- "Alle n Monate" mit n größer 1, "alle 2 Jahre" und Rhythmen ohne Kadenz kann BB nicht herstellen. Ein Wechsel weg davon ist endgültig, die Warnung sagt das.
- Reine Funktion `targetChangeLosses : Target -> TargetDraft -> TargetLoss list` nennt, was am gelesenen Stand verloren geht: `LosesDay` (wenn `Day` gesetzt), `LosesDate` (wenn `Date` gesetzt und der Entwurf kein Datum hat), `LosesKind` (bei TB, TBD, MF), `LosesRhythm` (bei nicht herstellbarem Rhythmus). Leer heißt keine Warnung.
- Vor dem Senden zeigt der Client die Warnung mit dieser Liste als Bestätigung (Styleguide: `Modal.confirm` am PC, am Handy das Gegenstück im Sheet). Ein Wochentag lässt sich danach in BB nicht setzen, das steht in der Warnung beim Wechsel auf wöchentlich.
- Mit Datum im Entwurf: dieselben zwei Aufrufe wie beim Anlegen.

**Zwei Aufrufe**
- Die Target-Änderung wird mit beiden Schritten in einer Transaktion festgehalten, danach Schritt 1, dann Schritt 2. Schritt 2 geht nur hinaus, wenn Schritt 1 geschrieben ist und YNABs Antwort den neuen Rhythmus zeigt. Je Schritt ein Ergebnis in `target_change_results`.
- Scheitert Schritt 2, steht das Target ohne Datum in YNAB, sein Fehlbetrag ist dann der volle Betrag. Meldung: "Target angelegt, Datum 01.06.2027 nicht gesetzt: <Grund>". Das Formular behält das Datum, der gezeigte Stand ist der neue aus Schritt 1. Ein erneutes Speichern ergibt über `planTargetWrite` nur `TargetDate`, also genau den fehlenden Schritt, ohne eigenen Erneut-Weg. Schritt 1 wird nie wiederholt.

**Client**
- Ohne Target zeigt der Tab Target statt "Kein Target" das Formular zum Anlegen: `Segmented` Monat, Woche, Jahr, Betragsfeld, `DateField` (optional), Knopf "Target anlegen" (Orange).
- Bei vorhandenem Target wird der Rhythmus aus budgeting-k8wq3 zum `Segmented`. Abweichung vom gelesenen Rhythmus ist ein Wechsel und löst die Warnung aus.
- Nur im laufenden Monat (`targetEditAllowed`, Roman F4). Im vergangenen und im künftigen Monat zeigt eine Kategorie ohne Target "Kein Target" ohne Formular, eine mit Target den Rhythmus als Text.

## Acceptance criteria
**Domain**
- [ ] (Domain) `planTargetWrite` Anlegen: monatlich, wöchentlich, jährlich ohne Datum je ein Patch mit `Frequency`. Mit Datum zwei Patches, der erste ohne Datum, der zweite nur mit Datum, für alle drei Rhythmen.
- [ ] (Domain) `planTargetWrite` Wechsel: monatlich auf wöchentlich ist ein Patch mit `Frequency`; jährlich mit Datum auf jährlich mit neuem Betrag ist nur `GoalTarget` (kein `Frequency`); TB auf monatlich ist ein Wechsel; ein Wechsel mit Datum im Entwurf ergibt zwei Patches.
- [ ] (Domain) `targetChangeLosses`: Tag gesetzt und Wechsel ergibt `LosesDay`; Datum gesetzt und Wechsel ergibt `LosesDate`, auch bei gleichem Rhythmus, wenn `Frequency` gesendet würde; MF ergibt `LosesKind`; alle 3 Monate ergibt `LosesRhythm`; reine Betragsänderung ergibt eine leere Liste.
- [ ] (Encoder) Ein Body mit `goal_frequency` und `goal_target_date` zugleich kann nicht entstehen (`buildTargetBody` lehnt ab).

**Handler (In-Memory-SQLite, YNAB als Port)**
- [ ] GIVEN Kategorie ohne Target WHEN anlegen monatlich 50 € THEN eine Target-Änderung, ein PATCH mit `goal_target` und `goal_frequency: monthly`, Ergebnis geschrieben.
- [ ] GIVEN anlegen jährlich mit Datum THEN zwei PATCHes in dieser Reihenfolge, je ein Ergebnis, Historie zeigt eine Änderung.
- [ ] GIVEN Schritt 1 scheitert THEN Schritt 2 geht nicht hinaus und ist fehlgeschlagen mit Grund "Schritt 1 fehlgeschlagen" (wie das Ziel einer Verschiebung, ADR 0069 Punkt 3), Schritt 1 fehlgeschlagen mit YNABs Grund, die Meldung nennt ihn.
- [ ] GIVEN Schritt 2 scheitert THEN Schritt 1 geschrieben, Schritt 2 fehlgeschlagen, Antwort trägt den neuen Stand ohne Datum und die Meldung "Datum ... nicht gesetzt". Ein folgender Aufruf mit diesem Stand als `Shown` sendet nur `goal_target_date`.
- [ ] GIVEN YNABs Antwort auf Schritt 1 zeigt nicht den erwarteten Rhythmus THEN Schritt 2 geht nicht hinaus, Meldung konkret.
- [ ] GIVEN Kategorie hat in YNAB inzwischen ein Target, gezeigt war keins THEN nichts gesendet, `TargetChangedInYnab`.
- [ ] GIVEN künftiger oder vergangener Monat WHEN anlegen oder Art wechseln THEN Fehler ohne YNAB-Aufruf, keine Zeile.

**Client-`update`**
- [ ] GIVEN Wechsel der Art mit Verlusten WHEN Speichern THEN erst die Warnung, gesendet wird nur nach Bestätigung; Abbrechen sendet nichts und lässt die Eingabe stehen.
- [ ] GIVEN reine Betragsänderung THEN keine Warnung.
- [ ] GIVEN Kategorie ohne Target im künftigen Monat THEN "Kein Target" ohne Formular (reine Funktion wie in budgeting-k8wq3).
- [ ] GIVEN Warnung offen WHEN Monatswechsel THEN Warnung geschlossen, nichts gesendet.
- [ ] GIVEN Teilergebnis (Datum nicht gesetzt) THEN Meldung, Formular mit Datum und neuem gezeigten Stand, Speichern aktiv.

**Browser und Sonstiges**
- [ ] Neuer Flow `target-anlegen` in `FLOWS` ("Am Handy für eine Kategorie ohne Target ein Monats-Target anlegen.") mit genau einem `[flow:target-anlegen]`-Test, API per `page.route` gemockt.
- [ ] Anlegen und Warnung am Handy im 500-px-Sheet bedienbar. [human-eye]
- [ ] README budgeting: Target anlegen, Wechsel der Art, Teilergebnis bei zwei Aufrufen. Delta geprüft.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Verfeinert am 2026-11-04 (Orchestrator mit tactical-modeler und architect).

Abweichung vom Architect-Vorschlag: Er schlug für Schritt 2 einen eigenen Erneut-Weg vor (wie `retryFailedTargets`). Der Tactical-Modeler empfahl die Reparatur über das normale Speichern. Übernommen ist die Reparatur über das normale Speichern: der Vergleich gegen den gezeigten Stand liefert genau den fehlenden Schritt, ein zweiter Endpoint und ein Hinweis wie bei der Offenen Verschiebung entfallen. Nach einem Neuladen der App ist die Meldung weg; das Target steht dann ohne Datum, die Historie zeigt den fehlgeschlagenen Schritt.

**Entscheidungen (Roman, 2026-11-04):** F4 (nur laufender Monat), F6 a (Anlegen immer "Set aside another"), F7 a (Knopf "Target anlegen").

Ob YNAB auch monatlich und wöchentlich mit Datum im einen Aufruf ablehnt, ist offen. Die zwei Aufrufe gelten deshalb für alle Rhythmen.

## Verifier note (iteration 1)
**REASONS:** `./verify.sh` endet mit `VERIFY: FAIL (rote Schritte: dotnet build, dotnet test)`. Das Testprojekt kompiliert nicht: `src/Tests/TargetChangeTests.fs(755,96): error FS0001: This expression was expected to have type 'HistoryStatus' but here has type 'HistoryStatus option'`. Ursache: semantischer Merge-Konflikt mit main. budgeting-h7q4v hat `HistoryLine.Status` auf `HistoryStatus option` umgestellt (Pausen ohne Stand); der neue Helfer `historyOf` (`TargetChangeTests.fs:752`, `(string * HistoryStatus) list`) und seine Erwartungen in den createFlowTests sind gegen den alten Typ geschrieben. Der Conductor hat main vor der Prüfung in den Branch gemergt (Merge-Commit im Worktree). Alles andere ist in Ordnung (Marker, FLOWS, README-Delta, Migration wiederholbar, ADR 0080 Punkte 4 und 6, Extras 1 bis 3).
**SUGGESTED_FIX:** `historyOf` liefert `(string * HistoryStatus option) list`, die Erwartungen werden `Some HistoryWritten` (wie der bestehende Test `"Target entfernt", Some HistoryWritten`). Dann `./verify.sh` bis `VERIFY: PASS` und bestätigen, dass die neuen .NET-Tests von xp46a wirklich grün laufen.
**ITERATION_HINT:** likely-fixable

## Outcome

Target anlegen und Art wechseln ist gebaut (ADR 0072, ADR 0080). `./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 3728 grün, 11 opt-in übersprungen, node 295 grün, beide Builds und beide Wächter grün). README-Delta mit `npm run check:readme-delta -- --bc budgeting` geprüft (Exit 0). Kein Live-Aufruf, `RUN_INTEGRATION_TESTS` nicht gesetzt.

**Shared** (`src/Shared/Budgeting.fs`): `TargetChangeResult.TargetPartlyWritten of Target option * message`; `TargetLoss` (`LosesDay | LosesDate | LosesKind | LosesRhythm`) und die reine `targetChangeLosses`; `targetKindChange` (echter Wechsel: anderer Rhythmus oder Art kein NEED), `rhythmOfFrequency`, `frequencyText`, `targetDateNotSetText` ("Target angelegt, Datum 01.06.2027 nicht gesetzt: <Grund>", beim Wechsel "Target geändert, ..."), `TargetRhythmMissingText`. `TargetChangeKind` um `TargetCreated of TargetFrequency` und `TargetKindChanged of TargetFrequency` erweitert, `targetChangeText` nennt "Target angelegt, 50,00 € monatlich, Datum ..." und "Rhythmus wöchentlich, Target ... → ...". Die Texte mit Task-Ids (`TargetRhythmLaterText`, `TargetCreateLaterText`) sind weg.

**Server:** `Domain.planTargetWrite : Target option -> TargetDraft option -> ...` plant Anlegen und Wechsel als `goal_target` mit `goal_frequency`, ein Datum als eigenen zweiten Patch nur mit `goal_target_date`, für alle drei Rhythmen; gleicher Rhythmus bei NEED sendet nie `goal_frequency`. `continueAfter` und `unexpectedRhythmText` (Punkt 4 von ADR 0080). `targetChangeKindOf/Text/OfText` mit Rhythmus-Spalte. `YnabClient.buildTargetBody` lehnt `goal_frequency` zusammen mit `goal_target_date` (gesetzt oder null) ab. `CommandHandlers.changeTarget` plant vor dem Lesen gegen `Shown` (ersetzt `targetChangeSupported`), hält eine Änderung als eine Zeile fest und sendet die Schritte über `sendSteps`: je Schritt ein Ergebnis mit Schrittnummer, Schritt 2 nur nach Ok und passendem Rhythmus; sonst `Failed` mit "Schritt 1 fehlgeschlagen" (`TargetStepHeldBack`) oder dem konkreten Rhythmusgrund; Antwort `TargetWritten`, `TargetPartlyWritten` oder innerer `Error`. `Persistence.migrateTargetChangeColumns` ergänzt `target_changes.new_frequency` und `target_change_results.step` (wiederholbar), `appendTargetChangeStepResult`.

**Client:** `TargetEdit`: neuer Modus `TargetCreatable` (laufender Monat ohne Target), alle Formularfunktionen auf `Target option`, `targetRhythmValue`, `chooseTargetRhythm`, `targetFormLosses`, `confirmTargetChange`, `cancelTargetChange`, `targetLossText`, `targetChangeWarningText` (mit "Ein Wochentag lässt sich danach in BB nicht setzen." beim Wechsel auf wöchentlich). `startTargetSave` öffnet bei Verlusten erst die Warnung. `TargetForm` trägt `RhythmInput` und `ConfirmingChange`; Nachrichten `TargetRhythmChanged`, `ConfirmTargetChange`, `CancelTargetChange`. Tippen, Rhythmuswahl und Monatswechsel schließen die Warnung. Ein Teilergebnis setzt den neuen Stand, behält die Eingabe mit Datum und zeigt die Meldung; abseits des Tabs als Warn-Toast. `TargetEditView`: `Segmented` Monat, Woche, Jahr vor den Feldern (bei nicht herstellbarem Rhythmus kein Segment aktiv, der bisherige als Text), "Target anlegen" (Orange) ohne Target, Warnung am PC per `Modal.confirm`, am Handy per neuem `BottomSheet.confirm` an der Stelle des Knopfs. `BottomSheet.confirm` teilt sich die Komponente mit `confirmDanger`, steht in `/styleguide` und in Styleguide §5.

**Tests (39 neu):** `src/Tests/TargetChangeTests.fs` (Planung Anlegen und Wechsel je Fall, Verluste je Fall, `continueAfter`, Encoder mit `goal_frequency` und Ablehnung mit Datum ohne Senden; Abläufe gegen In-Memory-SQLite und Fake-YNAB: anlegen monatlich, jährlich mit Datum in zwei PATCHes mit Historie als eine Änderung, Schritt 1 scheitert, Schritt 2 scheitert mit Nachtrag nur des Datums, falscher Rhythmus in der Antwort hält Schritt 2 zurück, gezeigt kein Target bei Target in YNAB, vergangen und künftig ohne YNAB-Aufruf, Historientext beim Wechsel; Migration auf einer alten Datenbank). `src/Tests/TargetChangeClientTests.fs` (Anlegen sichtbar nur im laufenden Monat, Auftrag mit Rhythmus und Datum, Warnung nur bei Verlusten, Bestätigen, Abbrechen, Monatswechsel, Warntext, Teilergebnis, Toast, Aufzählung aller Reihenfolgen der Warnung bis Tiefe 5 mit eigenem Orakel). `scripts/target-change.test.mjs`: `[flow:target-anlegen]` (neu in `FLOWS`) und ein `[layout]`-Test, dass "Target anlegen" und "Wechseln" im 500-px-Sheet im Bild liegen. In `src/Tests/MoveMoneyClientTests.fs` baut ein Test seinen Monatswechsel jetzt über `update`, weil auch eine Kategorie ohne Target ein Formular hat.

**Gegenprobe:** je Mutante gesetzt und zurückgenommen: Warnung in `startTargetSave` abgeschaltet (2 rot), `continueAfter` immer wahr (2 rot), Ablehnung im Encoder abgeschaltet (1 rot). Die Aussage gilt nur für diese Mutanten.

**Offen:** `[human-eye]` für Anlegen und Warnung im Sheet nicht per Screenshot angesehen, der `[layout]`-Test misst nur die Lage der Knöpfe. Ob YNAB beim Wechsel mit `goal_frequency` `goal_needs_whole_amount` zurücksetzt ("Auffüllen bis"), ist nicht belegt und steht nicht in der Warnung. Die Historie zeigt bei einem Teilergebnis die ganze Änderung mit dem Stand des jüngsten Ergebnisses (fehlgeschlagen), nicht je Schritt. Sinnvolle Reviews: `qa-milestone-reviewer`, `backend-code-quality`, `frontend-architecture`, `design-quality`.

**Nachtrag Iteration 2:** Nach dem Merge von budgeting-h7q4v (`HistoryLine.Status` ist jetzt `HistoryStatus option`) kompilierte das Testprojekt nicht. In `src/Tests/TargetChangeTests.fs` gibt der Helfer `historyOf` jetzt `(string * HistoryStatus option) list` zurück, die drei Erwartungen nutzen `Some HistoryWritten`. Sonst war nichts zu ändern. `./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 3749 grün, 11 opt-in übersprungen, node 296 grün, beide Builds und beide Wächter grün). Die neuen Testlisten "Target anlegen und Art wechseln: ..." liefen einzeln über Expecto mit `--filter`: 37 grün, 0 rot.
