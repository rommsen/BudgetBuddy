---
id: design-system-bym4m
title: "Split-Ziel in einem Picker: Kategorie oder Konto über dieselbe Suche (\"Bar\" findet das Bar-Konto), Sync-Symbol für den Übertrag entfällt, in Kassenbuch und Import"
status: todo
type: feature
context: design-system
created: 2026-10-27
completed:
depends_on: [design-system-001, design-system-y2bdj]
blocks: []
tags: [captured, split-editor, picker, transfer]
related_adrs: [0006]
related_research: []
prior_art: []
---

## Why
Roman konnte im Split-Editor des Kassenbuchs kein Konto wählen. Das Ziel-Feld heißt "Kategorie oder Konto wählen", öffnet aber nur die Kategorien. Die Konto-Wahl steckt hinter einem unbeschrifteten Knopf mit `Icons.sync`; dieses Symbol steht in der App sonst für Neuladen und Synchronisieren (Settings, Rules, Kassenbuch, Sync-Ablauf) und führt hier in die Irre. Der Split-Editor im Import (`SyncFlow/Views/SplitSheet.fs`) hat dasselbe Problem.

## What
Das Ziel einer Split-Zeile wird in einem einzigen Picker gewählt. Die Suche läuft über Kategorien und Budget-Konten: wer "Bar" tippt, sieht das Bar-Konto als Übertrag ("→ Bar"). Ohne Suche zeigt der Picker unter den Kategorien einen eigenen Abschnitt "Übertrag auf Konto" mit den wählbaren Konten. Der Knopf mit dem Sync-Symbol neben dem Ziel-Feld entfällt.

Gilt für beide Split-Editoren in diesem einen Task (Entscheidung Roman, 2026-10-27):
- **Kassenbuch:** `src/Client/DesignSystem/SplitEditor.fs` (`line`, `targetButton`, Feld `OnPickAccount`) und `Components/Ledger/View.fs` / `State.fs` (`OpenSplitAccountPicker`).
- **Import:** `SyncFlow/Views/SplitSheet.fs` hat eigene Ziel-Knöpfe (`split-transfer-btn`) und die alten Picker `BottomSheet.categoryPickerLayered` / `BottomSheet.accountPickerLayered`. Der Import-Split zieht auf den gemeinsamen DS-Picker um.

Form-Vorschlag (der Worker darf abweichen, wenn er es im Outcome begründet): `CategoryPicker` bekommt optionale Konten (`Accounts`, leer = heutiges Verhalten) und eine eigene Zeilenart `Account of YnabAccount` in `PickerRow`; die Auswahl meldet Kategorie oder Konto über einen Ziel-Typ. So bleiben Tastatur-Navigation, Suche und Layout an einer Stelle. `AccountPicker.fs` entfällt, wenn ihn danach nichts mehr braucht.

Erwogen und verworfen (Gespräch 2026-10-27): zwei Select-Boxen nebeneinander. Eine Zeile hat genau ein Ziel; zwei Felder erlauben beides zugleich und sind am Handy zu eng.

## Acceptance criteria
- [ ] Eine Split-Zeile hat in beiden Editoren genau ein Ziel-Feld; der Knopf mit `Icons.sync` neben dem Ziel ist in Kassenbuch und Import entfernt.
- [ ] Antippen des Ziel-Felds öffnet den gemeinsamen Picker. Ohne Suche zeigt er die Kategorien und darunter den Abschnitt "Übertrag auf Konto" mit den wählbaren Konten (offene Budget-Konten ohne das Konto der Buchung selbst, wie heute).
- [ ] Suche nach einem Kontonamen ("Bar") liefert das Konto als Übertrag-Zeile "→ Bar" neben passenden Kategorien.
- [ ] Präfix "Konto:": "Konto:" allein zeigt nur die Konten, "Konto: Bar" nur die passenden Konten, keine Kategorien. Groß-/Kleinschreibung und Leerzeichen nach dem Doppelpunkt spielen keine Rolle.
- [ ] Konten erscheinen nie unter "Zuletzt verwendet" (ledger-d4msf) und nie unter den Vorschlägen; diese Abschnitte bleiben Kategorien vorbehalten.
- [ ] Wahl eines Kontos setzt das Ziel der Zeile auf den Übertrag (ADR 0006); das Ziel-Feld zeigt danach "→ <Kontoname>". Wahl einer Kategorie verhält sich wie heute.
- [ ] Tastatur-Navigation (Pfeiltasten, Enter, Escape) erreicht auch die Konto-Zeilen.
- [ ] Der Kategorie-Picker außerhalb des Split-Editors (Quick Add, Kategorie im Detail) zeigt weiterhin keine Konten.
- [ ] Barabhebung (ledger-s8t3s) ohne hinterlegtes Quick-Add-Konto öffnet den gemeinsamen Picker statt des alten Konto-Pickers.
- [ ] Die reinen Funktionen (Filtern mit und ohne Präfix, Layout mit Konto-Abschnitt, navigierbare Zeilen) haben Unit-Tests in `src/Tests/`.
- [ ] Browser-Wächter: im Kassenbuch und im Import "Bar" tippen, "→ Bar" wählen, die Zeile zeigt den Übertrag; der Sync-Knopf existiert in keinem der beiden Editoren.
- [ ] Der lebende Styleguide (`/styleguide`) zeigt den Picker mit Konto-Abschnitt.
- [ ] Nicht mehr verwendete Picker (`AccountPicker`, `BottomSheet.accountPickerLayered`, ggf. `BottomSheet.categoryPickerLayered`) sind entfernt, samt ihrer CSS-Klassen.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün.

## Notes
Captured via `quick-capture` on 2026-10-27, verfeinert am 2026-10-27. **Entscheidungen (Roman, 2026-10-27):**
- Der Abschnitt "Übertrag auf Konto" steht **unten**, unter den Kategorien.
- Die Suche findet ein Konto über seinen Namen ("Bar") und über das Präfix "Konto:" (z. B. "Konto: Bar" bzw. "Konto:" allein zeigt nur die Konten).
- Konten erscheinen **nicht** unter "Zuletzt verwendet" (ledger-d4msf); die Liste bleibt Kategorien vorbehalten.
- Kassenbuch und Import in **einem** Task.

Bezug: ledger-pafkp (Split-Editor Kassenbuch), ADR 0006 (Übertrag im Split), ledger-s8t3s (Barabhebung setzt das Konto-Ziel direkt).
Hängt an design-system-y2bdj, weil beide die Split-Editor-Karte in `Styleguide/View.fs` ändern. design-system-j6dnj (Touch-Fläche) ändert nur `Button.fs` und läuft unabhängig.
Für Browser-Wächter: neue Pflichtfelder an geteilten View-Typen brechen Playwright-Fixtures; Fixtures mitziehen.
