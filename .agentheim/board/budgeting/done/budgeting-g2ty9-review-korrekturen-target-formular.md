---
id: budgeting-g2ty9
title: "Review-Korrekturen im Target-Formular: Spinner am auslösenden Knopf, Fokus nach Abbrechen und Speichern bleibt, Datumsfeld nur lesend wie der Betrag, dazu die Befunde der Reviews zu Target anlegen"
status: done
type: refactor
context: budgeting
created: 2026-11-04
completed:
depends_on: [budgeting-xp46a, budgeting-c6vn4]
blocks: []
tags: [budget, review, target]
related_adrs: [0072, 0075, 0080]
related_research: []
prior_art: [budgeting-r3wq8, budgeting-k8wq3, budgeting-xp46a]
---

## Why
Die Reviews zu budgeting-r3wq8 (c6bd8bf) haben Befunde im Target-Formular gefunden. budgeting-xp46a baut gerade in denselben Dateien, deshalb kommen diese Befunde hierher und werden zusammen mit den Befunden der Reviews zu budgeting-xp46a behoben, sobald xp46a integriert ist.

## What
**Aus den Reviews zu budgeting-r3wq8**
- F1: Beim Entfernen dreht der Spinner an "Target speichern" (`TargetEditView.fs:153`, `TargetEdit.fs:122`). `TargetSaving` trägt die Art (Speichern oder Entfernen), der Spinner steht nur am auslösenden Knopf.
- F2: "Target speichern" nutzt `IsBusy` statt `IsLoading`, der Fokus bleibt (`TargetEditView.fs:153`).
- F3: Nach "Abbrechen" in `BottomSheet.confirmDanger` geht der Fokus zurück auf "Target entfernen", nach dem Bestätigen auf den Knopf mit dem laufenden Aufruf (`BottomSheet.fs:577-590`, `TargetEditView.fs:168`). Escape schließt die Rückfrage. Die Id im Baustein entsteht einmal, nicht je Render (`BottomSheet.fs:579`). Regel im Doc-Kommentar und in `/styleguide` Abschnitt 18. Die Rolle `alertdialog` ohne `aria-modal` ist in Styleguide §5 benannt.
- F4: Betrag und Datum sind beim Speichern gleich gesperrt: `DateField` bekommt `ReadOnly` (Textfeld `readOnly`, Kalender-Knopf `aria-disabled`), dazu sieht der gesperrte Kalender-Knopf gesperrt aus (`disabled:opacity-50 disabled:cursor-not-allowed`, kein Hover). Zustand in `/styleguide` 8a gezeigt (`DateField.fs:171-173`).
- F5 (kann): Der Satz "Target ändern geht nur im laufenden Monat." erscheint nicht während eines Monatswechsels im laufenden Monat (`TargetEditView.fs:43`). Der Hinweis unter dem Betragsfeld hängt per `aria-describedby` und `aria-invalid` am Feld (`TargetEditView.fs:90-93`, auch Tab Geld).

**Aus den Reviews zu budgeting-xp46a (24c4a48)**
- X1 (MUSS, Client): Am Handy können zwei Rückfragen gleichzeitig offen sein. Steht die Wechsel-Warnung an der Stelle des Speichern-Knopfs, bleiben "Target pausieren" und "Target entfernen" bedienbar, ein Tipp öffnet zusätzlich `confirmDanger` (im Browser nachgewiesen, zwei `alertdialog` im Sheet). Höchstens eine Rückfrage im Tab: `removeTargetLocked` beachtet `ConfirmingChange`, `askRemoveTarget` wirkt bei offener Warnung nicht, der Pause-Knopf ist dann gesperrt; umgekehrt liefert `canSaveTarget` bei `ConfirmingRemove` false. Getestet als Aufzählung aller Reihenfolgen im echten `update` (Doktrin Punkt 20), Regressionskommentar.
- X2 (Client): Ändert ein stilles Neulesen bei offener Warnung das gezeigte Target so, dass keine Verluste mehr bleiben oder `FormUnchanged` gilt, schließt die Warnung (`confirmTargetChange` bei `None`, oder in `syncTargetForm`). Sonst bleibt "Wechseln" wirkungslos und Speichern gesperrt.
- X3 (DS, Client): `Segmented.Segment` bekommt `Disabled` (gedämpft, `disabled`), gezeigt in `/styleguide`; `rhythmChoice` übergibt `targetFieldsLocked model`.
- X4 (Darstellung): Ein Teilergebnis ("Target angelegt, Datum … nicht gesetzt") steht als gelber Hinweis (`ErrorDisplay.notice`, eigener Fall `TargetPartlyNotice`), nicht als rote Fehlerkarte, mit dem Satz "Speichern setzt das Datum erneut."
- X5 (Darstellung): Ohne Target in anderen Monaten steht unter "Kein Target" der Satz "Target anlegen geht nur im laufenden Monat." (Konstante neben `TargetEditMonthText`). "wieder herstellen" wird "wiederherstellen" (`TargetEdit.fs`). Der Titel der Warnung heißt "Art wechseln?", wenn `LosesKind` darunter ist.
- X6 (Darstellung): Im 340-px-Panel am PC wird der Platzhalter des Datumsfelds nicht abgeschnitten (Raster ändern oder eigene Zeile), belegt durch einen `[layout]`-Test (`scrollWidth <= clientWidth`).
- X7 (Backend): Die Historie zeigt ein Teilergebnis richtig: Ergebnisse je Schritt lesen (`COALESCE(step, 1)`), eine Änderung mit geschriebenem Schritt 1 und gescheitertem Schritt 2 erscheint als "Target angelegt, … Datum nicht gesetzt" mit eigenem Stand, nicht als ganz fehlgeschlagen (ADR 0080, Negative). Test gegen In-Memory-SQLite.
- X8 (Backend, Validation): Anlegen ohne Rhythmus und Anlegen mit 0 € werden an der API-Grenze mitgesammelt (`validateTargetChange`); `planTargetWrite` bleibt zweite Sicherung.
- X9 (Backend): Ab einem geschriebenen Schritt werden alle folgenden Ergebnis-Zeilen best-effort geschrieben (mit Log samt Schrittnummer), das Teilergebnis kommt trotzdem zurück. Das Datum in der Meldung kommt aus dem zurückgehaltenen Patch, kein `Option.defaultValue ""`. Bei unerwartetem Rhythmus eigener Text ("Target in YNAB mit anderem Rhythmus angelegt …").
- X10 (Tests): Der Walk der Warnung kennt "Betrag" und "Datum" als Ereignisse; `targetRhythmValue` für `Months 3`, `Years 2`, `NoRhythm`, MF/TB (None) und wöchentliches NEED (Some Weekly); Konstantentest `TargetStepHeldBack` als Literal in der `stepRows`-Erwartung; Task-Ids aus `testList`-Namen in Kommentare.
- X11 (kann): `targetDateNotSetText` nimmt `TargetChangeKind` statt Bool; neue Spalten auch in `CREATE TABLE`, Migration über `pragma_table_info`; `appendTargetChangeResult` entfernen; ein gemeinsamer `rhythmText` in Shared; Währung aus einer Konstante statt `"EUR"`; `data-testid "confirm-sheet"`; Verlustsätze als Liste; Negativzeile mit "Anlegen ohne Rhythmus".

## Acceptance criteria
- [ ] F1 bis F4 umgesetzt, je mit Test auf der Heimat-Ebene (`[fokus]` für die Fokusführung).
- [ ] X1 behoben, mit Regressionstest (Aufzählung der Reihenfolgen), vor dem Fix rot.
- [ ] X2 bis X10 umgesetzt, je mit Test auf der Heimat-Ebene; neue DS-Zustände in `/styleguide`.
- [ ] F5 und X11 umgesetzt oder im Outcome mit Grund ausgelassen.
- [ ] README budgeting: Teilergebnis in der Historie und an der Grenze geprüfte Anlege-Regeln beschrieben, Delta geprüft mit `npm run check:readme-delta`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Review-Berichte im Scratchpad der Sitzung vom 2026-11-04. Wird erst nach der Integration von budgeting-xp46a verfeinert und befördert.

## Outcome

Alle Befunde F1 bis F5 und X1 bis X10 sind umgesetzt, X11 bis auf einen Punkt. `./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 3776 grün, 11 opt-in übersprungen, node 299 grün, beide Builds und beide Wächter grün). README-Delta mit `npm run check:readme-delta -- --bc budgeting` geprüft (Exit 0). Kein Live-Aufruf.

**Aus den Reviews zu budgeting-r3wq8**
- F1, F2: `TargetCall.TargetSaving` trägt `TargetAction` (`SavingTarget | RemovingTarget`, `Components/Budget/Types.fs`). `saveButtonState` und `removeButtonState` (`TargetEdit.fs`) liefern `Busy`/`Disabled`; "Target speichern" und "Target entfernen" nutzen `IsBusy`, der Spinner steht nur am auslösenden Knopf ("Wechseln" dreht am Speichern-Knopf), der Fokus bleibt. Test in `TargetChangeClientTests.fs` ("Beim Speichern dreht nur Target speichern ...").
- F3: `BottomSheet.ConfirmDangerProps.ReturnFocus` (CSS-Selektor). Nach dem Schließen geht der Fokus auf den Knopf, der die Rückfrage geöffnet hat (nach Abbrechen frei, nach dem Bestätigen mit dem laufenden Aufruf). Escape bricht ab, die Id entsteht einmal (`useMemo`). Doc-Kommentar, `/styleguide` Abschnitt 18 und Styleguide §5 nennen die Regel und `alertdialog` ohne `aria-modal`. `[fokus]`-Test in `scripts/target-change.test.mjs` (Abbrechen, Escape, Bestätigen mit `aria-busy`). Vor dem Fix rot (`ReturnFocus = None`: Timeout).
- F4: `DateField.ReadOnly` (Textfeld `readOnly`, Kalender-Knopf `aria-disabled`, öffnet nichts), `calendarButtonClass` dämpft gesperrt und nur lesend und hat dann kein Hover. Zustände "nur lesend" und "gesperrt" in `/styleguide` 8a, Styleguide-Absatz Datumsfeld. Test in `ButtonSizeTests.fs`.
- F5: `targetMonthHint` zeigt den Satz nur in vergangenen und künftigen Monaten, nicht während eines Wechsels aus dem laufenden. Die Betragsfelder im Tab Target und Tab Geld hängen ihren Hinweis per `aria-describedby` und `aria-invalid` an (`Input.DescribedBy`, `Input.AriaInvalid`, `invalidHint` mit Id). Die ARIA-Verdrahtung hat keinen eigenen Test (reine View-Attribute).

**Aus den Reviews zu budgeting-xp46a**
- X1 (Bug): `confirmOpen` sperrt Speichern (`canSaveTarget`, `startTargetSave`), `removeTargetLocked` und `askRemoveTarget` beachten die offene Warnung, `Pause.pauseHeldByConfirm` sperrt im Tab Target den Pause-Knopf und `startPause`. Regressionstest "Tab Target: Reihenfolgen der Rückfragen" (Aufzählung aller Reihenfolgen bis Tiefe 5 durch das echte `update`, eigenes Orakel). Vor dem Fix rot: "Rhythmus, Speichern, Entfernen" öffnete beide Rückfragen; nur der Pause-Wächter zurückgenommen: "Speichern, Pausieren" sendete.
- X2: `syncTargetForm` schließt eine offene Warnung, wenn der Wechsel nichts mehr verliert oder keiner mehr ist. Test mit stillem Neulesen (`Reloaded`).
- X3: `Segmented.Segment.Disabled` (`disabled`, gedämpft, kein Hover), `rhythmChoice` übergibt `targetFieldsLocked`. Gezeigt in `/styleguide` 23b, Styleguide §5. Tests in `ButtonSizeTests.fs` und im Client.
- X4: eigener Fall `TargetPartlyNotice`, gelb über `ErrorDisplay.notice`, Text mit "Speichern setzt das Datum erneut." (`TargetDateRetryText`).
- X5: `TargetCreateMonthText` ("Target anlegen geht nur im laufenden Monat.") unter "Kein Target" (`TargetEditView.noTarget`), "wiederherstellen", Titel "Art wechseln?" bei `LosesKind` (`targetChangeTitle`).
- X6: im Panel steht das Datumsfeld in eigener Zeile. `[layout]`-Test am PC (scrollWidth gegen clientWidth und gemessene Breite des Platzhalters gegen den Innenraum). Vor dem Fix rot.
- X7: `HistoryTargetChange.DateStep`, `queryTargetChanges` liest je Schritt (`COALESCE(step, 1)`), sortiert nach `rowid` und liefert `Result` (jetzt auch der Port `LoadTargetChanges`, Lesefehler als "Target-Änderungen nicht lesbar"; aus budgeting-c6vn4 mitgenommen). Neuer Stand `HistoryPartlyWritten` ("teilweise geschrieben", gelbes Badge mit Grund, in `/styleguide` 4), Text "..., Datum 01.06.2027 nicht gesetzt" (`targetChangeStatus`, `targetChangeText`). Tests: rein, gegen In-Memory-SQLite (Ergebnis ohne Schritt zählt als Schritt 1) und im Ablauf "Schritt 2 scheitert".
- X8: `validateTargetChange` sammelt beim Anlegen `TargetRhythmMissingText` und `TargetCreateZeroText`; der Client sperrt Anlegen mit 0 € mit demselben Hinweis. Tests in Validation, Client und Negativtabelle ("Anlegen ohne Rhythmus").
- X9: `recordBestEffort` (Log mit Schrittnummer) für jedes Ergebnis nach einem geschriebenen Schritt; `StepsOutcome.LaterNotWritten`/`LaterHeldBack` tragen den Patch, das Datum der Meldung kommt aus ihm; eigener Text `targetRhythmUnexpectedText` ("Target in YNAB mit anderem Rhythmus angelegt, ..."). Regressionstest mit Trigger nur für Schritt 2, vor dem Fix errored (SqliteException).
- X10: Warn-Walk mit "Betrag" und "Datum" (Orakel um "getippt" ergänzt), `targetRhythmValue` für `Months 3`, `Years 2`, `NoRhythm`, MF, TB und wöchentliches NEED, `TargetStepHeldBack` als Literal in der `stepRows`-Erwartung, Task-Ids aus den `testList`-Namen in Kommentare.
- X11: `targetDateNotSetText` nimmt `TargetChangeKind`; neue Spalten in `CREATE TABLE`, Migration über `pragma_table_info` (SQL ohne eingesetzte Werte); `appendTargetChangeResult` entfernt; `Shared.Budgeting.rhythmWordText` für Server und Client (`frequencyText` baut darauf); `TargetCurrency` statt `"EUR"`; `data-testid "confirm-sheet"`; Negativzeile "Anlegen ohne Rhythmus". Ausgelassen: Verlustsätze als Liste. `ConfirmDangerProps.Message` ist ein Text, den `Modal.confirm` und `BottomSheet.confirm` teilen; eine Liste bräuchte eine neue DS-Variante in beiden.

**Tests:** 14 neue .NET-Tests, 1 neuer Browser-Test (`[layout]` PC), der `[fokus]`-Test erweitert. Aus `scripts/target-change.test.mjs` ist die Logikaussage im `[layout]`-Test gestrichen, `inView` prüft auch die Oberkante.

**Offen:** Der `[layout]`-Test "Handy 375 px: die Suche teilt ihre Zeile ..." aus budgeting-c6vn4 war im ersten Lauf einmal rot (Historie 47,99999 px hoch, Rundung bei gebrochener Lage) und einzeln und im zweiten Gate-Lauf grün; Folgeaufgabe im Backlog. Ob Schritt 1 nach einem unerwarteten Rhythmus erneut gehen darf (Backend-Review xp46a Punkt 5, ADR-Satz), ist nicht entschieden. Am PC fängt `Modal.confirm` den Fokus weiter nicht (Design-Review xp46a K11, eigener DS-Task). `[human-eye]` für den gelben Hinweis, das Badge "teilweise geschrieben" und den gesperrten Rhythmus nicht per Screenshot angesehen. Sinnvolle Reviews: `qa-milestone-reviewer`, `backend-code-quality`, `frontend-architecture`, `design-quality`.
