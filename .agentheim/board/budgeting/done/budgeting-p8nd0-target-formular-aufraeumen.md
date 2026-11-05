---
id: budgeting-p8nd0
title: "Target-Formular aufräumen: Wrapper, die nur Tests aufrufen, streichen und die Tab-Tests auf Kern und Sperrlage umstellen"
status: done
type: refactor
context: budgeting
created: 2026-11-05
completed:
depends_on: [budgeting-r7k2m]
blocks: []
tags: [budget, target, client, tests]
related_adrs: [0082]
related_research: []
prior_art: [budgeting-r7k2m]
---

## Why
Nach budgeting-r7k2m (Formular in `TargetFormCore` und `TargetFormView` herausgelöst) fand das Frontend-Review vom 2026-11-05 sieben Wrapper in `TargetEdit.fs`, die nur noch Tests aufrufen. Damit prüfen die Tab-Tests einen Pfad, den die View nicht nimmt; heute gleichwertig, kann aber auseinanderlaufen. r7k2m durfte die Tests nicht inhaltlich ändern, diese Task darf ihre Aufrufe umstellen. Roman hat die Umsetzung am 2026-11-05 freigegeben.

## What
`src/Client/Components/Budget/TargetEdit.fs`, `TargetFormCore.fs`, `TargetFormView.fs`, Tests in `src/Tests/TargetChangeClientTests.fs` (und weitere, die die Wrapper rufen).

1. Die Wrapper `canSaveTarget`, `targetFieldsLocked`, `removeTargetLocked`, `saveButtonState`, `removeButtonState`, `saveWaitHint`, `removeWaitHint` streichen (`TargetEdit.fs:104, 121, 204, 221, 230, 234, 240`). Ihre Tests rufen stattdessen `targetFormLocks model categoryId` plus die `form*`-Funktionen des Kerns, also denselben Pfad wie die View. `targetBusy` (Zeile 60) wird privat, wenn es außerhalb nur Tests nutzen. Der Kommentar in `Pause.fs:99` zieht mit.
2. Der `None`-Zweig von `saveButtonState` (Zeile 225-227) entfällt damit.
3. `chooseTargetRhythm` (Zeile 187-188) über `changeForm` wie die Geschwister.
4. `open Components.Budget.Month` in `TargetFormView.fs:11` entfernen, wenn ungenutzt.
5. Doc-Kommentare und Texte im Kern (`TargetFormCore.fs:11, 21, 40, 373-387`) sprechen vom "Formular" statt vom "Tab Target", wo es um den Kern geht.

## Acceptance criteria
- [ ] Keiner der sieben Wrapper existiert mehr; `grep` im Repo findet sie nur noch in Kommentaren zur Historie oder gar nicht.
- [ ] Die umgestellten Tests prüfen dasselbe Verhalten wie vorher (gleiche Fälle, gleiche Erwartungen); geändert sind nur die Aufrufe. Keine Kopie, kein neuer Test ohne neues Verhalten (Doktrin Punkt 2).
- [ ] Gegenprobe: M1 (Speichern trotz offener Rückfrage) und M2 (`formCanSave` ohne `not locks.Waits`) aus dem Outcome von budgeting-r7k2m machen nach dem Umbau weiter mindestens einen Test rot, im Outcome vermerkt.
- [ ] Keine sichtbare Änderung: die Browser-Wächter in `scripts/budget-page.test.mjs` laufen unverändert grün.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Quelle: Frontend-Review Warnungen 3 und 4, Vorschläge 3, 4 und 7. Die Punkte für die Seite Targets prüfen (Sperrlage je Monat, Antwort an beide Formulare, feste ids, vorbelegtes Formular) gehören zu budgeting-q4v8n, nicht hierher.

Läuft parallel zu budgeting-gv74q (Shared/Server) und ledger-cy1j8 (Kassenbuch).

## Outcome

Die Wrapper im Tab Target, die nur Tests aufriefen, sind weg. Die Tab-Tests prüfen jetzt denselben Pfad wie die View: `targetFormLocks model categoryId` plus die `form*`-Funktionen des Kerns. Keine sichtbare Änderung.

- **Anbindung** `src/Client/Components/Budget/TargetEdit.fs`: `canSaveTarget`, `targetFieldsLocked`, `removeTargetLocked`, `saveButtonState` (samt `None`-Zweig), `removeButtonState`, `saveWaitHint` und `removeWaitHint` sind gestrichen. `targetBusy` ist privat (außerhalb nutzte es nur ein Test). `chooseTargetRhythm` läuft über `changeForm` wie `typeTargetAmount` und `typeTargetDate`. `grep -w` über `src` und `scripts` findet keinen der sieben Namen mehr.
- **Kern** `TargetFormCore.fs`: die Doc-Kommentare zu `TargetNotice`, `TargetForm`, `TargetAction` und `formRemoveWaitHint` sprechen jetzt vom Formular. Was nur im Tab gilt, steht als Zusatz da (`syncTargetForm`, `startPause` "im Tab Target").
- **View** `TargetFormView.fs`: das ungenutzte `open Components.Budget.Month` ist entfernt.
- `Pause.fs:99`: der Kommentar nennt jetzt `formSaveButton` im Kern als Vorbild.
- **Tests** `src/Tests/TargetChangeClientTests.fs`: nur die Aufrufe sind umgestellt, Fälle und Erwartungen sind gleich, kein Test kam dazu. Neu sind zwei private Helfer: `tabForm model category` holt gezeigtes Target, Sperrlage und Formular über `targetTabMode` und `targetFormLocks`, wie die View sie an den Kern gibt (ohne bearbeitbaren Tab bricht der Test ab, statt still `false` zu liefern). `locksOf model` liefert die Sperrlage der Kategorie des Formulars. Aufrufe wie `tabForm m c |||> formCanSave`, `formSaveButton`, `formSaveWaitHint` und `formRemoveButton (locksOf m) form`, `formRemoveLocked`, `formRemoveWaitHint`, `formFieldsLocked`. Die Prüfung "Target und Pause zugleich" in der Reihenfolgen-Wanderung liest `TargetCall` direkt statt über `targetBusy`.
- **Gegenprobe** im Kern, Lauf über `--filter-test-list "Tab Target"` (52 Tests), danach zurückgesetzt und per `cmp` gegen das Original geprüft:
  - M1 Speichern trotz offener Rückfrage (`if confirmOpen form then None` → `if false then None`): rot, 2 Tests ("Reihenfolgen der Warnung", "Reihenfolgen der Rückfragen"). Gleich wie in r7k2m.
  - M2 `formCanSave` ohne `&& not locks.Waits`: rot, 3 Tests ("Reihenfolgen der Rückfragen" und zwei Tests in "Sperre je Kategorie"). Gleich wie in r7k2m.
- `./verify.sh`: `VERIFY: PASS` (dotnet 3856 grün, node 310 grün). Der erste Lauf war rot an einem einzelnen `[layout]`-Test in `scripts/move-money.test.mjs:198` (Popover im Panel 1280 × 800). Der hat mit dem Target-Formular nichts zu tun, die Datei allein lief grün (3 von 3), und der zweite volle Lauf war grün. Das spricht für Last durch die parallelen Worktrees. Einen UNIQUE-Fehler aus ledger-cy1j8 gab es nicht.
- README_DELTA: in der Bullet "Target-Änderung" heißen die Wartesätze und Knöpfe jetzt `formSaveWaitHint`, `formRemoveWaitHint`, `formSaveButton` und `formRemoveButton` im Kern. Geprüft mit `npm run check:readme-delta` (Exit 0).
- Review-Vorschläge: `frontend-architecture` (src/Client), `qa-milestone-reviewer` (umgestellte Tests).
