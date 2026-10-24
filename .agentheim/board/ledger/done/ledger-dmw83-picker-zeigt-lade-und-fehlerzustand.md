---
id: ledger-dmw83
title: Kategorie-Picker zeigt "Kategorien werden geladen" bzw. "Kategorien konnten nicht geladen werden" mit "Erneut versuchen", statt einer leeren Liste ohne Erklärung
status: done
type: bug
context: ledger
created: 2026-10-24
completed:
depends_on: [design-system-001]
blocks: []
tags: [captured, kategorie, picker, frontend]
related_adrs: []
related_research: []
prior_art: [ledger-sfab0, ledger-kwwxz]
---

## Why
Live-Befund 2026-10-24: Waren die Kategorien nicht geladen, zeigte der Picker nur "Ohne
Kategorie". Ein Ladefehler sah aus wie "es gibt keine Kategorien", und es gab keinen Weg,
es erneut zu versuchen.

## What
Der DS-Baustein `CategoryPicker` (`src/Client/DesignSystem/CategoryPicker.fs`) bekommt in
`CategoryPickerProps` einen Ladezustand mit drei Fällen: bereit, lädt, fehlgeschlagen (mit
Rückruf für "Erneut versuchen").

- **Lädt und noch keine Kategorien da:** statt der Liste (auch statt "Ohne Kategorie") ein
  Ladehinweis "Kategorien werden geladen" mit Spinner aus `Loading`. Suche ohne Wirkung.
- **Fehlgeschlagen:** statt der Liste "Kategorien konnten nicht geladen werden" und ein
  Knopf "Erneut versuchen" (DS-Button).
- **Bereit**, und auch "lädt" bei schon vorhandener Liste (Nachladen nach ledger-kwwxz):
  unverändert, kein Hinweis.

Das Kassenbuch (`src/Client/Components/Ledger/View.fs`, `pickerProps`) leitet den Zustand
aus `Ledger.Categories` ab: `NotAsked` und `Loading` ohne Liste ergeben "lädt", `Failure`
ergibt "fehlgeschlagen" mit `LoadCategories` als Rückruf (der lädt dank `ledger-k9tq7` bei
Bedarf auch die Settings nach), `Success` ergibt "bereit". `/styleguide` zeigt beide neuen
Zustände.

## Acceptance criteria
- [ ] Test auf der reinen Layout-Ebene des Bausteins: im Zustand "lädt" mit leerer Liste
      enthält das Layout keine Zeilen (auch nicht "Ohne Kategorie") und den Ladehinweis.
- [ ] Test: im Zustand "fehlgeschlagen" enthält das Layout keine Zeilen und den Fehlertext mit
      "Erneut versuchen".
- [ ] Test: im Zustand "lädt" mit nicht leerer Liste ist das Layout gleich dem Zustand
      "bereit" (kein Hinweis, Liste sichtbar).
- [ ] Test der reinen Ableitung im Kassenbuch: `NotAsked` und `Loading` (ohne Liste) ergeben
      "lädt", `Failure` ergibt "fehlgeschlagen", `Success` ergibt "bereit".
- [ ] Reducer- oder Ansichts-Test: "Erneut versuchen" löst `LedgerMsg LoadCategories` aus.
- [ ] Alle Aufrufer von `CategoryPickerProps` (Kassenbuch, `/styleguide`) setzen das neue
      Feld; `dotnet build` grün.
- [ ] `/styleguide` (`src/Client/Components/Styleguide/View.fs`) zeigt den Picker zusätzlich
      im Zustand "lädt" und im Zustand "fehlgeschlagen".
- [ ] Optik nur aus Design-System und Tokens (Styleguide §3); `npm run guard:tokens` grün,
      sofern der Wächter integriert ist.
- [ ] Die Regressionstests tragen einen Kommentar zum Befund vom 2026-10-24.
- [ ] `dotnet test` grün.
- [ ] Lade- und Fehlerzustand fügen sich optisch in Popover und Sheet des Pickers ein.
      [human-eye]

## Notes
Captured via `quick-capture` on 2026-10-24, verfeinert am 2026-10-24.

Zusammenspiel mit `ledger-k9tq7`: erst dort entsteht der `Failure`-Zustand auch nach
gescheitertem Settings-Laden, und "Erneut versuchen" lädt dann die Settings mit nach. Ohne
k9tq7 greift der Fehlerzustand nur, wenn `getCategories` selbst scheitert. Keine harte
Abhängigkeit, aber nicht parallel zu k9tq7 und `infrastructure-y6atr` bauen (dieselbe
Stelle in `src/Client/State.fs`); Reihenfolge y6atr, k9tq7, dmw83.

## Verifier note (iteration 1)

**REASONS:**
- scripts/category-picker.test.mjs:231 legt den Scratchpad-Pfad der Worker-Sitzung fest als Ziel für Screenshots fest (`const shots = "/private/tmp/claude-501/.../scratchpad/ledger-dmw83"`); Zeilen 244 und 256 rufen bei jedem Lauf `page.screenshot({ path: ... })` dorthin. Damit steht ein einmaliges Hilfsmittel für das [human-eye]-Kriterium in einem dauerhaften Test von `npm run test:scripts`, schreibt bei jedem Lauf PNGs in ein sitzungsgebundenes macOS-Temp-Verzeichnis und bricht, wo `/private/tmp` fehlt. Muster im Repo: `screenshotIfRequested` mit `REVIEW_MARK_SCREENSHOTS` (scripts/ledger-review-mark.test.mjs:124-129) bzw. `SHOTS_DIR` (scripts/import-pruefen-duplikat-details.test.mjs:100).
- Klein, dieselbe Stelle: die zwei neuen Playwright-Tests rufen kein `page.close()`, anders als die bestehenden Tests der Datei (z. B. Zeile 225).
- Prüfungen 1 und 2 bestanden (alle Kriterien belegt, Läufe grün).

**SUGGESTED_FIX:** Den festen `shots`-Pfad entfernen: Screenshot-Aufrufe aus dem Test streichen oder hinter eine Umgebungsvariable mit Helfer legen, der ohne Variable nichts tut (Muster `screenshotIfRequested`). In beiden neuen Tests `await page.close()` ergänzen. `node --test --test-concurrency=1 scripts/*.test.mjs` erneut laufen lassen.

**ITERATION_HINT:** likely-fixable

## Outcome

`CategoryPickerProps` (`src/Client/DesignSystem/CategoryPicker.fs`) hat zwei neue Felder: `LoadState: PickerLoadState` (`PickerReady | PickerLoading | PickerFailed`) und `OnRetry`. Reine Funktionen `noticeFor` und `layoutFor` leiten daraus `Layout.Notice` ab (`LoadingNotice | FailedNotice`). Ist ein Hinweis gesetzt, hat das Layout keine Zeilen (auch kein "Ohne Kategorie", kein Vorgeschlagen, keine Gruppen). "Lädt" zählt nur bei leerer Liste, mit Liste (Nachladen nach ledger-kwwxz) ist das Layout gleich "bereit". `layout` bleibt als Kurzform für `PickerReady`. Die Hülle rendert den Ladehinweis mit `Loading.spinner` (role status) und den Fehlerhinweis mit DS-Button "Erneut versuchen" (role alert); die Suche bleibt sichtbar, ohne Wirkung.

Kassenbuch (`Components/Ledger/View.fs`): `categoryLoadState` leitet aus `Ledger.Categories` ab (NotAsked und Loading lädt, Failure fehlgeschlagen, Success bereit); `OnRetry = fun () -> dispatch LoadCategories` (lädt dank ledger-k9tq7 bei Bedarf auch die Settings nach). `/styleguide` Sektion 26 zeigt zwei neue Popover "Popover lädt" und "Popover fehlgeschlagen" mit Zähler für OnRetry. `standards/frontend/styleguide.md` führt den neuen Zustand im Picker-Absatz.

Tests: 7 Werttests auf der Layout-Ebene (`CategoryPickerTests.fs`, Gruppe "Lade- und Fehlerzustand (dmw83)", plus Galerie-Zusicherung), 2 im Kassenbuch (`LedgerCategoryChangeClientTests.fs`: Ableitung, Retry löst LoadCategories aus und schaltet auf Loading), 2 Playwright-Tests am echten Element in `scripts/category-picker.test.mjs` (kein Ohne Kategorie, Spinner, Fehlertext, Knopf ruft OnRetry genau einmal; beide schließen die Seite mit `page.close()`, keine Screenshot-Aufrufe im dauerhaften Test). Regressionstests tragen den Kommentar zum Befund vom 2026-10-24.

Läufe aus dem Worktree: `dotnet build` 0 Warnungen, 0 Fehler; `dotnet test` 1938 bestanden, 0 fehlgeschlagen, 7 übersprungen; `npm run build` grün; `npm run guard:classes` grün; `npm run guard:tokens` grün; `npm run test:scripts` seriell 200 von 200 grün.

Screenshots für das [human-eye]-Kriterium (PC-Popover, Galerie `/#/styleguide`, einmalig in Runde 1 aufgenommen und liegen geblieben): `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/68a4651f-b06d-48b5-a63b-e0e341d4f031/scratchpad/ledger-dmw83/pc-laedt.png` und `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/68a4651f-b06d-48b5-a63b-e0e341d4f031/scratchpad/ledger-dmw83/pc-fehlgeschlagen.png`. Das Sheet am Handy nutzt denselben Inhalt, ist aber nicht eigens fotografiert. Hinweis: die Stand-Zeile der Galerie steht auch im Fehlerzustand unter dem Hinweis (im Kassenbuch fehlt sie, solange kein Abruf gelang).
