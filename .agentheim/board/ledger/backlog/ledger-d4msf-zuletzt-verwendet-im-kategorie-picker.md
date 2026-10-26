---
id: ledger-d4msf
title: "Zuletzt verwendete Kategorien im Kategorie-Picker des Kassenbuchs: beim Ändern der Kategorie und bei Kategorie für die Auswahl stehen die letzten Kategorien einen Tipp entfernt"
status: backlog
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

## Acceptance criteria
- [ ] Der Picker beim Ändern der Kategorie zeigt einen Abschnitt "Zuletzt verwendet" mit höchstens fünf Kategorien, neueste zuerst.
- [ ] Derselbe Abschnitt erscheint bei "Kategorie für die Auswahl".
- [ ] Eine im Kassenbuch vergebene Kategorie (einzeln oder für die Auswahl) rückt an die Spitze der Liste.
- [ ] Ohne bisherige Kategorien fehlt der Abschnitt.

## Notes
Offen für die Verfeinerung:
- Speist der Filter dieselbe Liste wie der Picker, oder führen beide eine eigene? Heute
  füllt nur das Anwenden des Filters `RecentCategories`.
- Reihenfolge und Doppelungen mit "Vorgeschlagen": erscheint eine Kategorie in beiden
  Abschnitten?
- Am Handy (BottomSheet) und am PC (Popover) gleich.

Herkunft: Spike ledger-xa7jf, Outcome-Tabelle Lücke 1.
