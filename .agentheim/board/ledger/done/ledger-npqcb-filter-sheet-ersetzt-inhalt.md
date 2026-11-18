---
id: ledger-npqcb
title: "Filter-Sheet am Handy: Kategorie, Zeitraum und Sortieren ersetzen den Inhalt des Sheets statt ein zweites Sheet zu öffnen"
status: done
type: feature
context: ledger
created: 2026-11-18
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [etappe-3, frontend, kassenbuch]
related_adrs: [0087]
related_research: []
prior_art: [ledger-d9z3n]
---

## Why
Roman, Prüfliste q-sheet (A, 2026-11-18): Unter 1024 px öffnen Kategorie, Zeitraum und Sortieren aus dem Filter-Sheet
ein zweites Sheet darüber (`View.fs:3168`, `:3479`). Styleguide §7.15 will, dass eine Auswahl den Inhalt ersetzt und
zurückführt, so wie „Neue Buchung“ und das Buchungs-Detail es schon tun.

## What
Das Filter-Sheet wechselt seinen Inhalt zur Auswahl, oben „Zurück“; nach der Wahl springt es zurück zu den Filtern.

## Acceptance criteria
- [ ] Unter 1024 px: „Kategorie“, „Zeitraum“ und „Sortieren“ im Filter-Sheet ersetzen den Inhalt des Sheets durch ihre
      Auswahl, mit „Zurück“ oben. Es geht kein zweites Sheet auf.
- [ ] Nach einer Wahl (Kategorie mit Mehrfachwahl: nach „Übernehmen“ bzw. dem Muster der heutigen Auswahl) zeigt das Sheet
      wieder die Filter mit dem neuen Wert. „Zurück“ führt ohne Änderung zu den Filtern. Schließen schließt das ganze Sheet.
- [ ] Der Inhalt der Auswahl ist derselbe wie heute (dieselben Optionen, Suche, Häkchen); am PC ändert sich nichts.
- [ ] Welche Ansicht das Sheet zeigt, ist ein Zustand im Model; die Übergänge (öffnen, wählen, zurück, schließen) sind im
      Client-`update` unter .NET als Aufzählung aller Reihenfolgen getestet (Doktrin Punkt 20).
- [ ] Layout-Sweep: die Zustände der offenen Auswahl im Filter-Sheet bei 412 px ersetzen die bisherigen Zustände mit
      zweitem Sheet; Tastatur bleibt beim Tippen in der Kategorie-Suche offen.
- [ ] Am Handy (412 px, Touch) nebeneinander mit „Neue Buchung“ verglichen: gleiche Form von Kopf und „Zurück“;
      Screenshot im Outcome. [human-eye]

## Notes
Vorlage für die Form: die Kategorie-Auswahl in „Neue Buchung“ am Handy (ersetzt den Inhalt, führt zurück).

## Outcome

Unter 1024 px zeigt das Filter-Sheet die Auswahl „Kategorie“, „Zeitraum“ oder „Sortieren“ an Stelle der Filter (Styleguide §7.15): Titel der Auswahl im Kopf, darunter „Zurück“ (`PageHeader.backButtonFor`, „Zurück zu den Filtern“), kein Fuß. Es geht kein zweites Sheet mehr auf. Nach „Übernehmen“ (Kategorie, Zeitraum) bzw. der Wahl einer Sortierung stehen wieder die Filter mit dem neuen Wert; „Zurück“ und Esc führen ohne Änderung zu den Filtern; Schließen (×, Tipp daneben) schließt das ganze Sheet und verwirft den Entwurf. Der Inhalt der Auswahlen ist derselbe wie vorher (`categoryPickerBody`, `datePickerBody`, die Sortier-Zeilen); am PC ändert sich nichts. `./verify.sh`: VERIFY: PASS (dotnet 5151 grün, 16 erlaubte übersprungen; node 348 grün; Build, beide Wächter).

Umsetzung:
- `src/Client/Components/Ledger/Types.fs`: `FilterSheetView` (`FilterSheetClosed | FilterSheetFilters | FilterSheetCategory | FilterSheetDate | FilterSheetSort`), Nachricht `BackToFilterSheet`; Doku von `FilterSheetOpen` und `CloseFilterSheet` nachgezogen.
- `src/Client/Components/Ledger/State.fs`: `filterSheetView` leitet die Ansicht aus `FilterSheetOpen`, `OpenPopover` und `SortSheetOpen` ab (diese tragen schon die Entwürfe, so gibt es keinen zweiten Zustand, der auseinanderlaufen kann). `OpenFilterSheet` beginnt immer mit den Filtern, `CloseFilterSheet` schließt samt offener Auswahl, `BackToFilterSheet` führt zu den Filtern (beide ohne offenes Filter-Sheet ohne Wirkung), Esc in der Sortierung des Filter-Sheets führt zu den Filtern (vorher blieb nach Esc das Sortier-Sheet allein stehen).
- `src/Client/Components/Ledger/View.fs`: `filterSheet` rendert je `filterSheetView` den Inhalt; `sortOptionRows` aus `sortSheet` herausgezogen, das Sortier-Sheet am Sortier-Chip bleibt ein eigenes Sheet, aber nur ohne offenes Filter-Sheet. `handyFilterPickers` (die zweiten Sheets) entfällt.

Tests:
- `src/Tests/LedgerHandyFilterTests.fs`, neue Liste „Filter-Sheet unter 1024 px: Auswahl ersetzt den Inhalt (ledger-npqcb)“ (3 Tests): Aufzählung aller Reihenfolgen bis Tiefe 9 (über 68 000 Schritte) aus Filter-Knopf, Kategorie, Zeitraum, Sortieren, Haken, Schnellwahl, Übernehmen, Sortierung wählen, Zurück, Esc und Schließen durch das echte `update`, Orakel als eigene Zustandsmaschine (Ansicht, Kategorien, Zeitraum, Sortierung, Entwürfe), Doktrin Punkt 20. Gegenprobe: ohne das Zurücksetzen von `OpenPopover` in `CloseFilterSheet` rot („Auswahl bleibt nach dem Schließen nicht stehen“). Dazu Schließen aus jeder Auswahl und Wiederöffnen mit den Filtern, und `BackToFilterSheet`/`CloseFilterSheet` ändern ohne Filter-Sheet nichts (Sortier-Chip).
- `scripts/ledger-filter-bar.test.mjs` `[flow:kassenbuch-filtern]`: je Zeitraum, Sortieren, Kategorie genau ein Sheet, die Filter ersetzt, „Zurück“ zu den Filtern, Schließen aus „Sortieren“ schließt alles; danach der bisherige Ablauf.
- `scripts/layout-states.mjs`: `openHandyFilterRow` bricht ab, wenn mehr als ein Sheet offen ist, die Filter noch darunter stehen oder „Zurück“ fehlt; so gelten `kassenbuch-filter-kategorie`, `kassenbuch-filter-zeitraum`, `kassenbuch-sortieren` und `kassenbuch-ausgegraut-handy` jetzt für die ersetzte Form. `kassenbuch-filter-kategorie` tippt in die Kategorie-Suche und prüft, dass der Fokus im Feld bleibt (Tastatur bleibt offen), dazu Invariante 7 mit offener Tastatur (erster Treffer im Bild).

Vergleich am Handy (412 px, Touch), Bilder unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/24b8079f-2912-4751-9c18-a9bb18d50825/scratchpad/shots-bb/`: `ledger-npqcb-filter-kategorie-412.png` neben `ledger-npqcb-neue-buchung-kategorie-412.png`, dazu `ledger-npqcb-filter-zeitraum-412.png`, `ledger-npqcb-filter-sortieren-412.png` und `ledger-npqcb-filter-412.png` (Filter). Gleich: ein Sheet, Kopf mit Titel der Auswahl links und rundem × rechts. Abweichungen: „Neue Buchung“ hat keine Zeile „Zurück“ (dort führt × zum Formular zurück), das Filter-Sheet hat sie nach Kriterium 1 und 2 (× schließt alles); „Neue Buchung“ trägt die Unterzeile „Für die neue Buchung“, das Filter-Sheet keine (die Titel „Kategorie“, „Zeitraum“, „Sortieren“ bleiben wie vorher). Beides für Romans Blick [human-eye].

Ohne ADR: die Ansicht ist aus den bestehenden Feldern abgeleitet statt ein neues Feld; `Viewport.swallowNextClick` bleibt weg, weil alle drei Auswahlen auf dem echten `click` wählen (mit ihm schluckte das Sheet den nächsten Tipp auf „n zeigen“).

README_DELTA: geprüft mit `npm run check:readme-delta -- --bc ledger --delta <datei>`, Exit 0, „Ganz nachlesen“: keine Einträge. Hinweis ohne Änderung: der Eintrag **Kassenbuch unter 1024 px** nennt den Fuß „Zurücksetzen“, der Code beschriftet ihn seit 2026-11-15 „Filter löschen“ (`filterSheetClearLabel`); nicht Teil dieser Task.

Kann-Zeilen (Kann-Sammlung ledger):
- In der Kategorie-Auswahl des Filter-Sheets liegt „Übernehmen“ bei 320 und 390 px mit offener Tastatur unter der Tastatur (wie vorher im zweiten Sheet); der Sweep prüft dort den ersten Treffer.
- Das Sheet ändert beim Wechsel zwischen Filtern und Auswahl seine Höhe (keine feste Höhe, wie „Neue Buchung“).
