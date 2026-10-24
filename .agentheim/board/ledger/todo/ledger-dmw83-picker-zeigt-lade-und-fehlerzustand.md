---
id: ledger-dmw83
title: Kategorie-Picker zeigt "Kategorien werden geladen" bzw. "Kategorien konnten nicht geladen werden" mit "Erneut versuchen", statt einer leeren Liste ohne Erklärung
status: todo
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
