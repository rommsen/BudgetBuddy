---
id: budgeting-r7k2m
title: "Target-Formular aus dem Tab Target herauslösen, damit die Seite Targets prüfen es im Panel nutzen kann, ohne Verhaltensänderung im Tab"
status: done
type: refactor
context: budgeting
created: 2026-11-05
completed:
depends_on: [budgeting-k8wq3, budgeting-xp46a, budgeting-c2t9w, budgeting-d7r4k, budgeting-f8w2n]
blocks: [budgeting-q4v8n]
tags: [budget, target, client]
related_adrs: [0072, 0080, 0082]
related_research: []
prior_art: [budgeting-k8wq3, budgeting-c2t9w]
---

## Why
Roman hat am 2026-11-05 entschieden: "Anpassen …" auf der Seite Targets prüfen öffnet das bestehende Target-Formular in einem Panel (ADR 0082 Punkt 11). Das Formular hängt heute am Modell der Budget-Seite und lässt sich so nicht wiederverwenden.

## What
Befund (Tactical-Modeler): `src/Client/Components/Budget/TargetEdit.fs` ist nur zum Teil wiederverwendbar.
- **Schon frei vom Modell**, bleiben wie sie sind: alles über `(shown: Target option) (form: TargetForm)`, etwa `targetFormState`, `targetAmountText`, `targetAmountOf`, `targetDateValue`, `targetRhythmValue`, `targetDateShown`, `targetDateLabel`, `targetDateHint`, `targetFormLosses`, `confirmOpen`, `targetChangeWarningText`, `targetChangeTitle`, `targetNoticeText`.
- **An `Model` der Budget-Seite gebunden** (Auswahl, geladener Monat, Tab, `TargetCall`, Pause): `formKey`, `targetTabMode`, `selectedEditable`, `shownTarget`, `start`, `startTargetSave`, `confirmTargetChange`, `startTargetRemove`, `chooseTargetRhythm`, `typeTargetAmount`, `typeTargetDate`, `targetFieldsLocked`, `saveButtonState`, `removeButtonState`, die Wartesätze und `applyTargetAnswered`.
- **View:** `TargetEditView.editableTarget` nimmt `model: Model` und schickt Budget-`Msg`s (`SaveTarget`, `TargetAmountChanged`, `ConfirmTargetChange`, `AskRemoveTarget` …).

Herauszulösen:
1. Ein Formular-Kern ohne `Model`: die Übergänge auf `TargetForm` (Betrag tippen, Datum tippen, Rhythmus wählen, Speichern planen samt Warnung, Wechsel bestätigen und abbrechen, Entfernen fragen, bestätigen, abbrechen, Antwort einnehmen) mit dem gezeigten Target und einer Sperrlage als Eingabe (läuft ein Aufruf, welcher Knopf dreht, welcher Wartesatz). Ergebnis: neues Formular und gegebenenfalls der `TargetChangeRequest`.
2. Eine View des Formulars mit einem Props-Record (gezeigtes Target, Formular, Sperrlage, `isPanel`, Rückrufe statt Budget-`Msg`). Die Rückfragen (`Modal.confirm` am PC, `BottomSheet.confirm` im Sheet) bleiben darin.
3. Der Tab Target ruft Kern und View über eine dünne Anbindung an `Model` und `Msg`; `syncTargetForm`, `targetTabMode` und die Sperren je Kategorie und global (budgeting-c2t9w, budgeting-f8w2n, budgeting-n7h3x) bleiben dort.

Namen und Dateien legt der Worker fest. Die Seite Targets prüfen (budgeting-q4v8n) ist der zweite Nutzer.

## Acceptance criteria
- [ ] Der Formular-Kern kennt `Components.Budget.Types.Model` nicht (er öffnet den Typ nicht und nimmt ihn nicht als Parameter).
- [ ] Die Formular-View nimmt kein `Model` und keine Budget-`Msg`, nur Props mit Rückrufen.
- [ ] Der Tab Target verhält sich unverändert: die bestehenden Tests zum Target-Formular (Client-`update`, Zustandsregeln, Browser-Wächter in `scripts/budget-page.test.mjs`) laufen ohne inhaltliche Änderung grün; angepasst werden dürfen nur Aufrufnamen.
- [ ] Neue Tests nur für Verhalten, das der Kern zusätzlich hat und das die Tab-Tests nicht erreichen (Doktrin Punkt 2); keine Kopie bestehender Tests.
- [ ] Gegenprobe: eine vertauschte Zeile im Kern (etwa Speichern trotz offener Rückfrage) macht einen bestehenden Tab-Test rot, im Outcome mit Mutantenliste vermerkt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Abgetrennt von budgeting-q4v8n (Tactical-Modeler, 2026-11-05), damit die Seite nicht zugleich ein Refactoring des Tabs Target trägt und der Umbau für sich prüfbar bleibt. Keine Oberfläche ändert sich, deshalb kein `depends_on` auf design-system-001. Alle depends_on liegen in done. Läuft unabhängig von budgeting-p2zr6.

Danach bietet sich `frontend-architecture` als Review an.

## Outcome

Das Target-Formular ist in Kern und View getrennt, der Tab Target nutzt beide über eine dünne Anbindung. Keine sichtbare Änderung.

- **Kern** `src/Client/Components/Budget/TargetFormCore.fs` (kompiliert vor `Types.fs`, öffnet `Components.Budget.Types` nicht): die Typen `TargetForm`, `TargetNotice`, `TargetAction` liegen jetzt hier (aus `Types.fs` verschoben, `Types.fs` öffnet den Kern), dazu die neue Sperrlage `TargetFormLocks` (`Waits`, `Running`, `WaitHint`), `TargetFormSend` (`Action`, `Request`) und `TargetButtonState`. Übergänge mit `(shown: Target option) (locks) (form)`: `formTypeAmount`, `formTypeDate`, `formChooseRhythm`, `formPlanSave` (samt Warnung), `formConfirmChange`, `formCancelChange`, `formAskRemove`, `formCancelRemove`, `formConfirmRemove`, `formTakeAnswer`; Sperren und Knöpfe: `formCanSave`, `formSaveableApartFromCalls`, `formFieldsLocked`, `formRemoveLocked`, `formSaveButton`, `formRemoveButton`, `formSaveWaitHint`, `formRemoveWaitHint`; `emptyTargetForm`. Die schon modellfreien Helfer (`targetFormState`, `targetAmountText`, `targetFormLosses`, `confirmOpen`, `targetChangeWarningText`, `targetNoticeText`, Wartesätze usw.) sind unverändert mit hierher gezogen.
- **View** `src/Client/Components/Budget/TargetFormView.fs`: `TargetFormProps` (gezeigtes Target, Formular, `Locks`, `IsPanel`, `Status`, `Pause` als Elemente, Rückrufe `OnAmountChanged` … `OnCancelRemove`) und `view`. Die Rückfragen (`Modal.confirm`/`confirmDanger` am PC, `BottomSheet.confirm`/`confirmDanger` im Sheet) bleiben darin, ebenso die `data-testid`s. `figure` und `refillFact` liegen jetzt hier, die lesende Anzeige im Tab nutzt sie mit.
- **Anbindung** `TargetEdit.fs`: `targetFormLocks model categoryId` baut die Sperrlage (globaler Target-Aufruf, Pause derselben Kategorie, Wartesatz nach budgeting-f8w2n/n7h3x). `syncTargetForm`, `targetTabMode`, `targetMonthHint`, `withTarget`, `applyTargetAnswered` (Aufrufstand und Monat, Formular über `formTakeAnswer`) und alle bisherigen öffentlichen Namen (`startTargetSave`, `saveButtonState`, `canSaveTarget` …) bleiben, sie rufen nur den Kern. `TargetEditView.editableTarget` hat dieselbe Signatur und füllt die Props aus `Model` und `Msg`; `InspectorView.fs` ist unverändert.
- **Tests:** `TargetChangeClientTests.fs` bekam nur die Zeile `open Components.Budget.TargetFormCore`, kein Testinhalt geändert. Neu `src/Tests/TargetFormCoreTests.fs` mit zwei Kern-Tests für Sperren, die die Tab-Tests nicht erreichen (im Tab schließt `syncTargetForm` die Bestätigung vorher, ohne Target steht kein Entfernen-Knopf): Entfernen fragen ohne Target öffnet nichts, Entfernen bestätigen ohne Target sendet nichts.
- **Gegenprobe** (je Mutant im Kern, Lauf über `--filter-test-list "Tab Target"`, danach zurückgesetzt und gegen das Original verglichen):
  - M1 Speichern trotz offener Rückfrage (`if confirmOpen form then None` → `if false`): rot, "Reihenfolgen der Warnung" und "Reihenfolgen der Rückfragen".
  - M2 `formCanSave` ohne `not locks.Waits`: rot (3 Tests, u. a. "Sperre je Kategorie").
  - M3 `start` ohne Waits-Sperre: rot (5, u. a. "ein zweiter Tipp sendet nicht").
  - M4 Ablehnung ohne Meldung: rot (2).
  - M5 Felder nie gesperrt: rot (4).
  - M6 Entfernen fragen ohne Target: in den Tab-Tests grün, jetzt rot im neuen Kern-Test.
  - M7 Entfernen neben offener Rückfrage: rot (2).
  - M8 Speichern dreht beim Entfernen: rot (2).
  - M9 Wartesatz unter Speichern immer: rot (3).
  - M10 Warnung öffnet nicht: rot (5).
  - M11 Bestätigen ohne offene Warnung: rot (3).
  - M12 Rhythmus wöchentlich behält getipptes Datum: rot (1).
  - M13 Teilweise geschrieben lässt `ConfirmingChange` stehen: überlebt. Gleichwertig: während eines eigenen Aufrufs öffnet keine Warnung (`formPlanSave` wartet, `start` schließt Rückfragen), also ist `ConfirmingChange` bei der Antwort immer `false`.
  - M14 Entfernen bestätigt ohne Target (`&& shown.IsSome` weg): in den Tab-Tests grün, jetzt rot im neuen Kern-Test.
- `./verify.sh`: `VERIFY: PASS` (dotnet 3809 grün, node 310 grün). Unter der PASS-Zeile stand im Log noch eine fremde ✗-Zeile zu `Persistence.fs` (AC13 `hasAmountFilter`) mit einem anderen Log-Verzeichnis. Sie stammt aus dem parallelen verify.sh im Worktree budgeting-p2zr6 (gemeinsamer Lock für test:scripts) und gehört nicht zu diesem Lauf; Expecto hier: 0 Fehler. Der Hinweis betrifft p2zr6 (Server-Persistenz).
- Für budgeting-q4v8n: das Panel ruft `TargetFormView.view` mit eigenen Rückrufen und eine Sperrlage `TargetFormLocks` aus dem eigenen Modell; Übergänge über die `form*`-Funktionen, den Aufruf startet es aus `TargetFormSend`.
- Review-Vorschlag: `frontend-architecture` (src/Client), `qa-milestone-reviewer` (neue Tests).
