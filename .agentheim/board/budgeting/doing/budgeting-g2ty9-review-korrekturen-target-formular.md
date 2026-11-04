---
id: budgeting-g2ty9
title: "Review-Korrekturen im Target-Formular: Spinner am auslösenden Knopf, Fokus nach Abbrechen und Speichern bleibt, Datumsfeld nur lesend wie der Betrag, dazu die Befunde der Reviews zu Target anlegen"
status: doing
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
