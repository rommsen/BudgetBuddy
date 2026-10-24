---
id: ledger-kwwxz
title: "Available-Betrag je Kategorie im Kassenbuch frisch halten und fortschreiben: Kategorien neu laden bei Seitenaufruf, PWA-Vordergrund und Picker-Öffnung, lokale Fortschreibung nach einer Änderung, Text des Hinweises mit alt und neu"
status: todo
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
3. **Text des Hinweises.** Eine reine Funktion `categoryChangeHint` liefert die Zeilen für
   den Toast nach der Änderung: "Alt: <Gruppe: Name> 120,00 € zu 170,00 €" und
   "Neu: <Gruppe: Name> 80,00 € zu 30,00 €". Ohne Zahlen (andere Monate, kein Available)
   nur die Namen. Rendern und Auslösen gehören zu `ledger-sfab0`.
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
- [ ] `categoryChangeHint`-Tests: Zeilen mit Zahlen im laufenden Monat, nur Namen im
      Vormonat und ohne Available; Beträge im deutschen Format.
- [ ] `availableAgeLabel`-Tests: unter einer Minute, Minuten, über eine Stunde.
- [ ] `dotnet build`, `dotnet test`, `npm run build` grün.

## Notes
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
