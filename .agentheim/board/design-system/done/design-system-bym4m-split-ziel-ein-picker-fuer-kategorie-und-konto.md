---
id: design-system-bym4m
title: "Split-Ziel in einem Picker: Kategorie oder Konto über dieselbe Suche (\"Bar\" findet das Bar-Konto), Sync-Symbol für den Übertrag entfällt, in Kassenbuch und Import"
status: done
type: feature
context: design-system
created: 2026-10-27
completed: 2026-10-30
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
- [x] Eine Split-Zeile hat in beiden Editoren genau ein Ziel-Feld; der Knopf mit `Icons.sync` neben dem Ziel ist in Kassenbuch und Import entfernt.
- [x] Antippen des Ziel-Felds öffnet den gemeinsamen Picker. Ohne Suche zeigt er die Kategorien und darunter den Abschnitt "Übertrag auf Konto" mit den wählbaren Konten (offene Budget-Konten ohne das Konto der Buchung selbst, wie heute).
- [x] Suche nach einem Kontonamen ("Bar") liefert das Konto als Übertrag-Zeile "→ Bar" neben passenden Kategorien.
- [x] Präfix "Konto:": "Konto:" allein zeigt nur die Konten, "Konto: Bar" nur die passenden Konten, keine Kategorien. Groß-/Kleinschreibung und Leerzeichen nach dem Doppelpunkt spielen keine Rolle.
- [x] Konten erscheinen nie unter "Zuletzt verwendet" (ledger-d4msf) und nie unter den Vorschlägen; diese Abschnitte bleiben Kategorien vorbehalten.
- [x] Wahl eines Kontos setzt das Ziel der Zeile auf den Übertrag (ADR 0006); das Ziel-Feld zeigt danach "→ <Kontoname>". Wahl einer Kategorie verhält sich wie heute.
- [x] Tastatur-Navigation (Pfeiltasten, Enter, Escape) erreicht auch die Konto-Zeilen.
- [x] Der Kategorie-Picker außerhalb des Split-Editors (Quick Add, Kategorie im Detail) zeigt weiterhin keine Konten.
- [x] Barabhebung (ledger-s8t3s) ohne hinterlegtes Quick-Add-Konto öffnet den gemeinsamen Picker statt des alten Konto-Pickers.
- [x] Die reinen Funktionen (Filtern mit und ohne Präfix, Layout mit Konto-Abschnitt, navigierbare Zeilen) haben Unit-Tests in `src/Tests/`.
- [x] Browser-Wächter: im Kassenbuch und im Import "Bar" tippen, "→ Bar" wählen, die Zeile zeigt den Übertrag; der Sync-Knopf existiert in keinem der beiden Editoren.
- [x] Der lebende Styleguide (`/styleguide`) zeigt den Picker mit Konto-Abschnitt.
- [x] Nicht mehr verwendete Picker (`AccountPicker`, `BottomSheet.accountPickerLayered`, ggf. `BottomSheet.categoryPickerLayered`) sind entfernt, samt ihrer CSS-Klassen.
- [x] `dotnet build`, `dotnet test` und `npm run test:scripts` grün.

## Notes
Captured via `quick-capture` on 2026-10-27, verfeinert am 2026-10-27. **Entscheidungen (Roman, 2026-10-27):**
- Der Abschnitt "Übertrag auf Konto" steht **unten**, unter den Kategorien.
- Die Suche findet ein Konto über seinen Namen ("Bar") und über das Präfix "Konto:" (z. B. "Konto: Bar" bzw. "Konto:" allein zeigt nur die Konten).
- Konten erscheinen **nicht** unter "Zuletzt verwendet" (ledger-d4msf); die Liste bleibt Kategorien vorbehalten.
- Kassenbuch und Import in **einem** Task.

Bezug: ledger-pafkp (Split-Editor Kassenbuch), ADR 0006 (Übertrag im Split), ledger-s8t3s (Barabhebung setzt das Konto-Ziel direkt).
Hängt an design-system-y2bdj, weil beide die Split-Editor-Karte in `Styleguide/View.fs` ändern. design-system-j6dnj (Touch-Fläche) ändert nur `Button.fs` und läuft unabhängig.
Für Browser-Wächter: neue Pflichtfelder an geteilten View-Typen brechen Playwright-Fixtures; Fixtures mitziehen.

## Verifier note (iteration 1)

REASONS:
- Check 5, BC-README: `OnSelect` meldet jetzt `PickerChoice`, das Delta enthält aber nur ein `append`. Der Bullet "Kategorie-Picker (Einfachauswahl)" (`.agentheim/knowledge/contexts/design-system/README.md:49`) sagt weiter "`OnSelect` meldet `YnabCategoryId option` (`None` = Ohne Kategorie)" und widerspricht damit dem neuen Bullet.
- Check 5, derselbe Bullet: Er nennt `layout` als Funktion mit den Listenregeln, ohne Bezug zu `Accounts`. Er muss mindestens auf den neuen Vertrag und den Ziel-Picker verweisen.
- Check 5, Nebenbefund Nachbar-BC: `.agentheim/knowledge/contexts/ledger/README.md:224` sagt "Ohne Quick-Add-Konto bleibt das Ziel leer und die Konto-Auswahl der Zeile geht auf". Diese Konto-Auswahl gibt es nicht mehr, es öffnet `SplitTargetPickerFor`.
- Ohne Befund: Checks 1 bis 4, `./verify.sh` VERIFY: PASS (dotnet 2585, node 321). Checks 6, 6b, 6c und 7 liefen nicht.
- Hinweis zu Kriterium 2, kein FAIL-Grund: Im Import bleibt das Konto der Buchung wählbar. Das entspricht "wie heute", weicht aber vom Wortlaut "ohne das Konto der Buchung selbst" ab.

SUGGESTED_FIX: Ein `replace` auf den Bullet "Kategorie-Picker (Einfachauswahl)" ergänzen, das `OnSelect` als `PickerChoice` beschreibt und auf den Ziel-Picker verweist, dann durch `npm run check:readme-delta -- --bc design-system --delta <datei>` prüfen. Die Ledger-Zeile als Backlog-Item melden. Code und Tests bleiben unverändert.

ITERATION_HINT: likely-fixable

## Outcome

Eine Split-Zeile hat in Kassenbuch und Import genau ein Ziel-Feld, der Knopf mit `Icons.sync` ist in beiden Editoren entfernt. Beide öffnen den gemeinsamen `CategoryPicker`.

- `src/Client/DesignSystem/CategoryPicker.fs`: neue Props `Accounts`, `CurrentAccount`, `OfferWithout`; `OnSelect` meldet `PickerChoice` (`ChoseCategory` / `ChoseAccount`), `PickerRow` hat `Account`. Reine Funktionen: `searchScope` (Präfix "Konto:"), `filterAccounts`, `layoutForPicker`, `accountLabel`, `categoryChoice`, `isWithoutCurrent`. Tastatur und Layout liegen damit an einer Stelle.
- Kassenbuch: `SplitPicker` hat nur noch `SplitTargetPickerFor`, Nachricht `OpenSplitTargetPicker`. Barabhebung ohne Quick-Add-Konto öffnet diesen Picker und frischt dabei die Kategorien auf.
- Import: `Views/SplitSheet.fs` nutzt `CategoryPicker.sheetLayered`, `SplitPicker` hat `TargetPickerFor`.
- Entfernt: `DesignSystem/AccountPicker.fs`, `BottomSheet.accountPickerLayered` samt Interna, CSS `.split-transfer-btn` und `.split-target-row`. `BottomSheet.categoryPickerLayered` bleibt, Quick Add nutzt es noch.
- Styleguide: Karte 27 zeigt den Picker mit Konto-Abschnitt, `standards/frontend/styleguide.md` ist nachgezogen.

Ergänzungen gegenüber dem Form-Vorschlag, mit Grund:
- `CurrentAccount`: sonst trüge eine Übertrag-Zeile im gemeinsamen Picker den Haken bei "Ohne Kategorie".
- `OfferWithout`: der Import bot "Ohne Kategorie" nie an und sein `SelectSplitCategory` kennt es nicht; so bleibt es dort wie heute.
- Ein Lade- oder Fehlerhinweis nimmt nur die Kategorien, Konten bleiben wählbar: ein Übertrag braucht keine Kategorien, und die alte Konto-Auswahl hing nicht an ihnen. Der Picker zeigt in diesem Fall Hinweis und Konto-Abschnitt zusammen.
- Das Sheet heißt mit Konten "Ziel wählen" statt "Kategorie wählen", das Suchfeld "Kategorie oder Konto suchen…".
- Ohne wählbare Konten gilt das Präfix nicht: "Konto:" sucht dann in den Kategorien, damit Quick Add und das Detail unverändert bleiben.

Offen: im Import filtert der Picker wie bisher nur `openOnBudgetAccounts`. Der Import kennt das Konto der Buchung dort nicht, das eigene Konto ist also weiter wählbar. Das entspricht "wie heute", weicht aber vom Wortlaut des Kriteriums ab, das für das Kassenbuch gilt (`transferTargetsFor`).

Tests: `src/Tests/CategoryPickerTests.fs` (Liste "Ziel-Picker"), `src/Tests/LedgerSplitEditorClientTests.fs`, Browser-Wächter `scripts/import-split-target.test.mjs` (neu, PC und Handy) und `scripts/ledger-split-editor.test.mjs`. `./verify.sh`: VERIFY: PASS (dotnet 2585, node 321). Danach änderte sich nur der Text zweier Kommentare in `scripts/ledger-split-cashback.test.mjs`. Die Tests entstanden nach dem Code im selben Schritt; die Gegenprobe (Präfix, Konto-Zeilen in der Navigation und Kontofilter ausgebaut) machte 5 von 15 Picker-Tests rot.
