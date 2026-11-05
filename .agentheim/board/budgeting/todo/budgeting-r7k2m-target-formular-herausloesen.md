---
id: budgeting-r7k2m
title: "Target-Formular aus dem Tab Target herauslösen, damit die Seite Targets prüfen es im Panel nutzen kann, ohne Verhaltensänderung im Tab"
status: todo
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
