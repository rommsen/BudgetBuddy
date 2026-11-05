---
id: budgeting-p8nd0
title: "Target-Formular aufräumen: Wrapper, die nur Tests aufrufen, streichen und die Tab-Tests auf Kern und Sperrlage umstellen"
status: doing
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
