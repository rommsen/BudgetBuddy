---
id: ynab-k7m3q
title: Aktuellen Budgetwert (balance) hinter Kategorienamen im Picker anzeigen
status: backlog
type: feature
context: ynab-sync
created: 2026-06-27
completed:
depends_on: []
blocks: []
tags: [captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Beim Import in der Budgetzuordnung soll direkt sichtbar sein, wie viel in einer
Kategorie aktuell verfügbar ist, ohne in YNAB nachzuschauen.

## What
Im Kategorie-Picker (Zuweisungs-Dialog beim Import) hinter dem Kategorienamen den
aktuellen Budgetwert anzeigen: YNAB `balance` der Kategorie (Available des aktuellen
Monats, in Milliunits). Mit schicker grafischer Aufbereitung, nicht nur als Text im
Label: rechtsbündig, farbcodiert (grün bei Plus, rot bei Minus) über Design-Tokens
(`text-success` / `text-danger`, keine rohen Tailwind-Farben).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-06-27 — raw, unrefined. Needs a `modeling` refine
pass before it can be promoted.

Kontext aus der vorangegangenen `inquire`-Analyse (zur Orientierung, nicht als finale
Lösung):
- Daten sind schon da: `GET /budgets/{id}/categories` liefert `balance` pro Kategorie
  mit, wird aktuell nur nicht decodiert (`src/Server/YnabClient.fs:188`). Kein
  zusätzlicher YNAB-Request nötig.
- `YnabCategory` (`src/Shared/Domain.fs:354`) trägt heute nur Id/Name/GroupName, müsste
  ein Wert-Feld bekommen (z.B. `Available: int64` Milliunits).
- Decoder: `categoryDecoder` (`src/Server/YnabClient.fs:41`), `categoryInGroupDecoder`
  (:58) und der Budget-Detail-Pfad (`budgetDetailDecoder`) müssten `balance` lesen.
- Anzeige: der Picker bekommt die Optionen heute als `(string * string)` (id, Label)
  herein (`BottomSheet.categoryPicker` / `CategoryPickerInternal`,
  `src/Client/DesignSystem/BottomSheet.fs:394`). Für die grafische Aufbereitung
  (rechtsbündig, farbcodiert) reicht das Label-Tupel nicht — der Picker braucht einen
  reicheren Option-Typ. Das ist der größere Teil der Arbeit.
- Label-Stellen, die heute `$"{cat.GroupName}: {cat.Name}"` bauen:
  `src/Client/Components/SyncFlow/Views/TransactionList.fs:182` (Import-Zuordnung),
  `src/Client/Components/SyncFlow/Views/SplitSheet.fs`,
  `src/Client/Components/Rules/View.fs:325` (Regel-Editor).
- Frische: Werte sind so aktuell wie der letzte Kategorien-Load (`model.Categories`,
  RemoteData), kein Live-Refresh pro Zeile.
- Cross-cutting: die Datenseite (YNAB-Mirror) gehört zu ynab-sync, der Anzeige-Ort
  (Zuweisungs-Dialog) berührt auch `categorization`. Hier in ynab-sync gefilt, weil der
  Kern das Anreichern der gespiegelten YnabCategory ist.
