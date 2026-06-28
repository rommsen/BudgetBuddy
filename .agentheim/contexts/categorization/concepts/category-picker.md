---
name: category-picker
description: Gemeinsamer Bottom-Sheet zum Zuordnen einer YNAB-Kategorie — mobile-fest, mit Available pro Zeile
context: categorization
created: 2026-06-28
last_updated: 2026-06-28
derived_from:
  - 0005
  - 0011
  - 2026-06-11-mobile-category-picker-keyboard-ghostclick
  - ynab-k7m3q
max_lines: 60
---

# Category Picker — concept

## What it is
Der **Category Picker** ist die gemeinsame Bottom-Sheet-Komponente, mit der eine YNAB-
Kategorie ausgewählt wird. Eine Komponente (`BottomSheet.categoryPicker` /
`categoryPickerLayered` / `CategoryPickerInternal`, `src/Client/DesignSystem/BottomSheet.fs`),
drei Aufrufer im Zuordnungs-Moment: Import-Zuweisung (`SyncFlow/Views/TransactionList.fs`),
Split-Sheet (`SyncFlow/Views/SplitSheet.fs`) und Quick-Add (`Views/QuickAddPage.fs`).
Bietet Vorgeschlagen / Zuletzt verwendet / Suche / Auswahl nach Gruppen.

## Why it exists
Kategorisieren ist BudgetBuddys Kernarbeit (categorization = core) und passiert primär am
Handy. Der Picker ist der Engpass *jeder* Zuordnung; seine mobile Bedienbarkeit entscheidet,
ob sich der Workflow besser anfühlt als YNAB-Web (Vision-Ziel). Darum sammeln sich hier
mehrere Arbeiten an derselben Komponente.

## Current shape
- **Mobile-fest:** Visual-Viewport-Anker (nicht von der Tastatur verdeckt) + Click-Commit
  (Taps fallen nicht auf die Transaktion dahinter durch) — ADR 0005,
  `done/2026-06-11-mobile-category-picker-keyboard-ghostclick`.
- **Sheet-Stacking** nur über die Layer-Klasse (`.layer-2`), eine Ebene tief (Picker über
  Form-Sheet, z.B. Split/Quick-Add) — ADR 0005 §4.
- **Available pro Zeile:** reicherer Option-Typ
  `CategoryPickerOption { Id; Label; Available }`; jede Zeile zeigt rechtsbündig das
  aktuelle YNAB-Available farbcodiert (grün >0 / rot <0 / neutral =0), optionaler Decode
  ohne Extra-Request — ynab-k7m3q, ADR 0011.
- **Label-Form** "Gruppe: Name".

## Open questions
- Available bewusst **nicht** im Regel-Editor (`Rules/View.fs`) — für eine Config-Regel
  kontextlos (ynab-k7m3q).
- Datenfrische des Available: Stand des letzten Kategorien-Loads (`model.Categories`,
  RemoteData), kein Live-Refresh pro Zeile — akzeptiert.
- Mobile-Mechanik nur auf Code-Ebene verifizierbar (keine Browser-E2E im Repo, ADR 0005);
  Regressionen fallen erst beim Gerätetest auf.
- Visuelle Abnahme des Available am Gerät (reale Werte) steht aus — Human-Gate.

## See also
- `[ADR 0005]` — Visual-Viewport-Sheets + Click-Commit (Picker-Mechanik)
- `[ADR 0011]` — Available im Picker, optionaler Decode, Drei-Farben-Semantik
- `[done/2026-06-11-mobile-category-picker-keyboard-ghostclick]` — Mobile-Tauglichkeit
- `[done/ynab-k7m3q-category-balance-in-picker]` — Available pro Zeile
