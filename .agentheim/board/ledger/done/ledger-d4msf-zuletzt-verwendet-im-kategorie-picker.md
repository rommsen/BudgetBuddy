---
id: ledger-d4msf
title: "Zuletzt verwendete Kategorien im Kategorie-Picker des Kassenbuchs: beim Ändern der Kategorie und bei Kategorie für die Auswahl stehen die letzten Kategorien einen Tipp entfernt"
status: done
type: feature
context: ledger
created: 2026-10-26
completed:
depends_on: [design-system-001]
blocks: []
tags: [kassenbuch, kategorie, picker, komfort]
related_adrs: []
related_research: []
prior_art: [ledger-xa7jf, ledger-v9rq3, ledger-k2rbn, ledger-w29y6]
---

## Why
Beim Durchgehen vieler Buchungen vergibt Roman oft dieselben wenigen Kategorien. Quick Add
und Import prüfen zeigen sie im Picker als "Zuletzt verwendet", das Kassenbuch nicht
(Spike ledger-xa7jf, Lücke 1). Entscheidung Roman, 2026-10-26: soll rein.

## What
Der Kategorie-Picker im Kassenbuch (`src/Client/DesignSystem/CategoryPicker.fs`, Props in
`src/Client/Components/Ledger/View.fs` `categoryField`) bekommt einen Abschnitt "Zuletzt
verwendet", sowohl beim Ändern der Kategorie einer Buchung als auch bei "Kategorie für die
Auswahl". Heute kennt er nur "Vorgeschlagen" (Kategorien zum Payee).

Die Daten gibt es im Ledger-Modell schon: `RecentCategories` mit `pushRecentCategories`,
`maxRecentCategories = 5`, im Gerätespeicher (`readRecentCategories`), heute nur im
Kategorie-Filter genutzt (ledger-v9rq3).

Vorbild: Quick Add und Import (`RecentlyUsedCategoryIds` in `SyncFlow.State`,
`BottomSheet.categoryPickerLayered` mit Abschnitt "Zuletzt verwendet").

**Entscheidungen (Roman, 2026-10-26):** Picker und Filter führen **eine gemeinsame Liste**:
jede vergebene Kategorie rückt nach vorne, egal ob im Picker (einzeln oder für die Auswahl)
oder im Filter gewählt. Eine Kategorie darf zugleich unter "Vorgeschlagen" und "Zuletzt
verwendet" stehen. Der Hauptnutzen liegt am Handy.

## Acceptance criteria
- [ ] Der Picker beim Ändern der Kategorie zeigt einen Abschnitt "Zuletzt verwendet" mit höchstens fünf Kategorien, neueste zuerst, am Handy (BottomSheet) und am PC.
- [ ] Derselbe Abschnitt erscheint bei "Kategorie für die Auswahl".
- [ ] Eine im Picker vergebene Kategorie (einzeln oder für die Auswahl) rückt an die Spitze derselben Liste, die der Kategorie-Filter nutzt; eine im Filter gewählte Kategorie erscheint ebenso im Picker (reiner Test auf `pushRecentCategories` bzw. dem Übergang).
- [ ] Eine Kategorie darf in "Vorgeschlagen" und "Zuletzt verwendet" zugleich stehen.
- [ ] Ohne bisherige Kategorien fehlt der Abschnitt.
- [ ] Playwright am echten Picker (Handy und PC): nach dem Vergeben einer Kategorie steht sie beim nächsten Öffnen oben unter "Zuletzt verwendet".

## Notes
Offene Fragen geklärt am 2026-10-26 (Roman): gemeinsame Liste mit dem Filter, Doppelungen
mit "Vorgeschlagen" erlaubt.

Herkunft: Spike ledger-xa7jf, Outcome-Tabelle Lücke 1.

## Outcome

Der Kategorie-Picker (`src/Client/DesignSystem/CategoryPicker.fs`) hat das neue Props-Feld `Recent` und einen Abschnitt "Zuletzt verwendet" unter "Vorgeschlagen" (`data-testid="category-picker-recent"`). Reine Funktion `recentCategories` (höchstens fünf, Reihenfolge der Eingabe, ohne Dubletten, unbekannte, versteckte und aktuelle Kategorie entfallen), `layoutForRecent` (`layoutFor`/`layout` bleiben als Hüllen mit leerer Liste), Pfeiltasten laufen über die neuen Zeilen, "Alle Kategorien" als Kopf auch bei nur diesem Abschnitt. Bei Suche und ohne Einträge fehlt der Abschnitt. Eine Kategorie darf in Vorgeschlagen und Zuletzt verwendet zugleich stehen.

Gemeinsame Liste: `Model.RecentCategories` (Filter-Liste aus ledger-v9rq3). Neu in `Ledger/Types.fs`: `rememberPickedCategory` (Einzelwahl und Auswahl-Wahl rücken nach vorn, "Ohne Kategorie" ändert nichts) und `recentPickerIds`. `State.fs`: `StartSetEntryCategory` und `StartCategoryBatch` setzen die Liste und schreiben sie in den Gerätespeicher. `View.fs`: `categoryField` und der Auswahl-Picker reichen die Liste durch; `splitCategoryPickerProps` setzt `Recent = []` (Split zählt nicht). `/styleguide` setzt `Recent = []` (Aussehen des Pickers unverändert, kein Beispielabschnitt ergänzt).

Tests: 9 neue Expecto-Tests in `src/Tests/LedgerRecentCategoriesTests.fs`, 3 Playwright-Tests in `scripts/ledger-recent-categories.test.mjs` am echten Picker (PC-Popover im Panel: kein Abschnitt zu Beginn, danach oben, Reihenfolge nach zweiter Wahl; Auswahl-Picker am PC mit gesetzter Liste, Wahl steht danach an der Spitze des localStorage-Eintrags; Handy-Sheet über dem Detail). Läufe: `dotnet build` 0 Warnungen, 0 Fehler; `dotnet test` 2400 bestanden, 7 übersprungen, 0 Fehler; `npm run build` ok; `npm run guard:classes` ok; `npm run test:scripts` (seriell) 260 von 260 bestanden. Stilprüfung (Gedankenstriche, Umschreibungen) ohne Treffer.

### README von Hand
Nicht nötig, das README_DELTA ist gegen das Gate geprüft (Exit 0).
