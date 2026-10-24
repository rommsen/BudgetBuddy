---
id: ledger-kwwxz
title: "Available-Betrag je Kategorie im Kassenbuch frisch halten und fortschreiben: Kategorien neu laden bei Seitenaufruf, PWA-Vordergrund und Picker-Öffnung, lokale Fortschreibung nach einer Änderung, Zeilen der Karte Budget nach dem Wechsel mit alt und neu"
status: done
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [design-system-001]
blocks: [ledger-sfab0]
tags: [frontend, kassenbuch, kategorie, v2, scheibe]
related_adrs: [0014, 0054, 0055]
related_research: []
prior_art: [ledger-m8r2v, ledger-j8xq4, ledger-h4p6c]
---

## Why
Wunsch von Roman aus seiner YNAB-Beschreibung (2026-09-12): in der Kategorienauswahl soll
je Kategorie der aktuell verfügbare Betrag stehen, und beim Wechsel soll man beide Seiten
sehen (alte Kategorie geht hoch, neue runter). Mit "Kategorie ändern" (ADR 0054) wird das
jetzt gebraucht.

Die Prüfung am Code hat die Ausgangslage verändert: `Model.Categories` im Kassenbuch
trägt `Available` (YNABs `balance` des laufenden Monats) schon heute, geladen über
`getCategories` (ledger-j8xq4). Offen ist nur die Frische. ADR 0055, Punkt 8 entscheidet
deshalb gegen eine persistierte Kopie im Spiegel: sie wäre älter als der Live-Abruf.

## What
1. **Frische.** `Model.Categories` bekommt einen Zeitstempel des letzten erfolgreichen
   Abrufs (`CategoriesLoadedAt`). `getCategories` wird neu geladen beim Seitenaufruf (wie
   heute), beim Wechsel der PWA in den Vordergrund (derselbe Auslöser wie der
   Spiegel-Lauf aus `ledger-h4p6c`) und beim Öffnen des Pickers, wenn der Stand älter als
   zwei Minuten ist. Der vorhandene Stand bleibt sichtbar, bis der neue da ist; ein Fehler
   lässt den alten Stand stehen. Ein Aufruf je Auslöser, kein Timer.
2. **Fortschreibung nach einer Änderung.** Eine reine Funktion
   `shiftAvailable (entryDate, amount, oldCategoryId, newCategoryId, today) categories`:
   für Buchungen im Kalendermonat von `today` sinkt Available der alten Kategorie um den
   Betrag (Ausgabe −50 erhöht um 50) und steigt Available der neuen um den Betrag. Für
   Buchungen anderer Monate, Kategorien ohne Available und "ohne Kategorie" bleibt alles
   unverändert. Der nächste Abruf überschreibt.
3. **Zeilen der Karte.** Eine reine Funktion `categoryChangeHint` liefert die Zeilen der Karte
   "Budget nach dem Wechsel" (Tafel Ablauf 4, 4.3), nicht des Toasts: eine Liste von
   `{ Label: string; OldText: string option; NewText: string option }`, zuerst die alte, dann
   die neue Kategorie, z. B. `("Alltag: Haushalt", Some "412,10 €", Some "494,00 €")` und
   `("Wohnen: Möbel", Some "120,00 €", Some "38,10 €")`. Ohne Zahlen (andere Monate, kein
   Available) `None` in beiden Texten, nur das Label. Für "ohne Kategorie" entfällt die
   Zeile. Die Karte selbst (`CategoryChangeCard`, `design-system-gffps`) und das Auslösen
   (`ledger-sfab0`) gehören nicht hierher; der Toast trägt nur "Kategorie geändert: <Name>".
4. **Alter im Picker.** Eine reine Funktion `availableAgeLabel` ("Stand 10:42" oder
   "Stand vor 3 Min") für die Fußzeile des Pickers; Anzeige in `ledger-sfab0`.

Grenze zur Vision: Budgetieren wird nicht nachgebaut. Die Zahl wird gezeigt, YNAB führt sie.

## Acceptance criteria
- [ ] Reducer-Test: `CategoriesLoaded (Ok ...)` setzt `CategoriesLoadedAt`; ein Fehler beim
      Nachladen lässt `Categories` und `CategoriesLoadedAt` unverändert.
- [ ] Test der Nachlade-Entscheidung (rein): Picker-Öffnung bei Stand jünger als zwei
      Minuten lädt nicht, bei älterem Stand genau einmal; ein laufender Abruf wird nicht
      doppelt angestoßen.
- [ ] Test: der Wechsel der PWA in den Vordergrund auf der Kassenbuch-Seite löst bei
      Stand älter als zwei Minuten einen Abruf aus, sonst keinen.
- [ ] `shiftAvailable`-Tests: Ausgabe im laufenden Monat (alt plus Betrag, neu minus
      Betrag), Einnahme (umgekehrt), Buchung aus einem Vormonat ändert nichts, Wechsel auf
      "ohne Kategorie" erhöht nur die alte, Kategorie ohne Available bleibt ohne.
- [ ] `categoryChangeHint`-Tests: Zeilen mit alt und neu im laufenden Monat (Reihenfolge alt,
      neu; neuer Wert gleich Wert nach `shiftAvailable`), nur Label im Vormonat und ohne
      Available, keine Zeile für "ohne Kategorie"; Beträge im deutschen Format.
- [ ] `availableAgeLabel`-Tests: unter einer Minute, Minuten, über eine Stunde.
- [ ] `dotnet build`, `dotnet test`, `npm run build` grün.
- [ ] Optik nur aus Design-System und Tokens (Styleguide §3, Vorrangregel in den Handoffs):
      keine Farb- oder Maßwerte aus den Design-Tafeln; ein neuer DS-Baustein erscheint in
      `/styleguide` (`src/Client/Components/Styleguide/View.fs`); `npm run guard:tokens` grün
      (Wächter aus `design-system-wfhxj`, sobald integriert).
- [ ] Das Neue fügt sich optisch in die gebauten Screens (Kassenbuch, Buchungs-Detail,
      `/styleguide`) ein und fällt daneben nicht auf. [human-eye]

## Notes
Verfeinert am 2026-10-24 (Sammel-Modeling, Ablauf 4): `categoryChangeHint` liefert die Zeilen
der Karte am Picker, nicht des Toasts (Roman, 2026-10-24). Handoff:
`docs/ux/kassenbuch/handoff-ablauf-4-kategorie-und-split.md`. Rückgängig nutzt `shiftAvailable` mit vertauschten Kategorien, keine neue Funktion.

Verfeinert am 2026-10-24 (Sammel-Modeling): Umgeschrieben von "Spiegel führt Available" auf
"Available live frisch halten". Roman hatte am 2026-10-24 den Spiegel als Weg genannt und
die Frische zu klären gebeten. Befund: `Model.Categories` trägt `Available` bereits, der
Spiegel-Lauf ruft `/categories` zwar auf, verwirft `balance` aber; eine persistierte Kopie
wäre bis zu fünf Minuten älter als der Live-Abruf und bräuchte eine neue Abfrage und einen
Merge im Client. Falls Roman die Zahlen auch ohne YNAB-Verbindung zeigen will, ist die
persistierte Variante additiv nachrüstbar (Spalte in `ledger_categories`, `upsertCategories`
schreibt `Available`). Das ist in ADR 0055 als verworfene Alternative festgehalten.

YNAB-Limit rund 200 Anfragen je Stunde: je Auslöser ein `GET /categories`, kein Timer, keine
Abrufe nach jedem Schreiben (stattdessen die lokale Fortschreibung). Delta über
`server_knowledge` lohnt bei einer Anfrage je Auslöser nicht.

Dateien, die ein Worker anfasst: `src/Client/Components/Ledger/Types.fs`, `State.fs`,
`src/Client/State.fs` (Auslöser `LoadCategories`, PWA-Vordergrund), neue reine Funktionen
im Ledger-Client, Tests. Überschneidung mit `ledger-w4vte` und `ledger-sfab0` in
`Types.fs` und `State.fs`.

Stil-Absicherung (2026-10-24): Tafeln und Handoffs legen nur den Ablauf fest; die Optik
kommt aus `standards/frontend/styleguide.md`, `src/Client/DesignSystem/` und `/styleguide`
(Vorrangregel in `docs/ux/kassenbuch/tafeln/README.md`).

## Outcome

Available je Kategorie wird im Kassenbuch-Client live frisch gehalten, ohne persistierte Kopie (ADR 0055 Punkt 8).

- `Components/Ledger/Types.fs`: Model-Felder `CategoriesLoadedAt: DateTime option` und `CategoriesRefreshing: bool`, neue Msg `EnsureCategoriesFresh` (Auslöser für das Öffnen des Pickers, Verdrahtung in `ledger-sfab0`), reine Funktionen `shouldReloadCategories`/`categoriesReloadDue` (Schwelle `categoriesFreshness` = 2 Minuten, nie bei laufendem Abruf), `shiftAvailable`, `categoryChangeHint` (Record `CategoryChangeHintRow { Label; OldText; NewText }`, Beträge über `Money.formatAmountText`, Format "412,10 €"), `availableAgeLabel` ("Stand gerade eben", "Stand vor 3 Min", ab einer Stunde "Stand 10:42").
- `Components/Ledger/State.fs`: `CategoriesLoaded (Ok)` setzt Stand, Zeitstempel und beendet den Abruf; `CategoriesLoaded (Error)` lässt `Categories` und `CategoriesLoadedAt` bei vorhandenem Stand unverändert.
- `State.fs` (Haupt): `LedgerMsg LoadCategories` setzt `Loading` nur ohne vorhandenen Stand und merkt den laufenden Abruf; `VisibilityChanged` auf der Kassenbuch-Seite löst `LoadCategories` bei Stand über zwei Minuten aus; `LedgerMsg EnsureCategoriesFresh` lädt bei Bedarf. `update` ist dafür `let rec`.
- Tests: `src/Tests/LedgerCategoriesFreshnessTests.fs` (24 Tests, in `Tests.fsproj` direkt nach `LedgerCategoriesSyncTests.fs`).
- Abweichung von der Task: Der Zeitstempel ist `DateTime` (lokale Uhr via `DateTime.Now`), damit die Nachricht `CategoriesLoaded` unverändert bleibt und die Fußzeile die Ortszeit zeigt. Keine neue UI, daher kein Styleguide-Eintrag; `guard:tokens` grün.
- [human-eye]-Kriterium entfällt mangels sichtbarer Oberfläche (Anzeige folgt in `ledger-sfab0`).

Läufe: `dotnet build` 0 Warnungen/0 Fehler; `dotnet test` 1790 bestanden, 7 übersprungen, 0 Fehler; `npm run build` grün; `npm run guard:classes` und `npm run guard:tokens` grün; `npm run test:scripts` (seriell) 178 von 181 beim ersten Gesamtlauf, die drei roten (Seiten-Ladetimeout unter Last in ledger-filter-bar und der Spaltenkopf-Datei) liefen einzeln wiederholt grün (19/19 und 3/3), erster Lauf hatte ebenfalls nur Ladetimeouts.
