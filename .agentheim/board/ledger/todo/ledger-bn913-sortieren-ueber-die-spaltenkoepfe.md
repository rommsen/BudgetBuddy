---
id: ledger-bn913
title: "Kassenbuch sortieren: Klick auf einen Spaltenkopf sortiert serverseitig nach Datum, Betrag, Payee, Kategorie oder Konto; außer bei Datum fallen die Tagesköpfe weg"
status: todo
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001, ledger-qh9qx]
blocks: []
tags: [frontend, backend, kassenbuch, sortierung, tabelle]
related_adrs: [0036, 0037, 0049]
related_research: []
prior_art: [ledger-b1xwe, ledger-be1yh, ledger-p3n8k]
---

## Why
Die Liste ist heute fest nach Datum absteigend sortiert (`ORDER BY date DESC, id DESC` in
`Ledger.Persistence`). Roman will "mit Klick auf Datum etc." sortieren (2026-10-20), etwa
die größten Ausgaben eines Treffers oder alle Buchungen eines Payees beisammen sehen.

## What
Entscheidung Roman 2026-10-20:

- **Sortierbar:** Datum, Betrag, Payee, Kategorie, in der Gesamtsicht Konto. Memo und
  Status nicht.
- **PC:** Klick auf einen sortierbaren Spaltenkopf sortiert danach; erneuter Klick dreht
  die Richtung. Der aktive Kopf zeigt Richtung per Pfeil und `aria-sort`.
- **Handy:** eine Sortier-Auswahl am Ende der Filterleiste, öffnet ein `BottomSheet` mit
  denselben Optionen und der Richtung.
- **Server sortiert.** Die Sortierung wird Teil von `LedgerEntryPageQuery`; die Seiten,
  Trefferzahl und Summe bleiben über den ganzen Treffer korrekt. Jede Sortierung hat `id`
  als letzten Tiebreaker, damit Seitenwechsel und `LocallyRemoved` (ledger-be1yh) stabil
  bleiben. Payee und Kategorie sortieren über dieselbe Faltung wie die Suche (`bb_lower`,
  ADR 0037), leere Werte ans Ende.
- **Tagesköpfe:** Bei Sortierung nach Datum (auf- oder absteigend) bleiben Tagesgruppen
  und Tagessummen. Bei jeder anderen Sortierung wird die Liste flach, ohne Tagesköpfe; das
  Datum steht dann in jeder Zeile (PC: Datumsspalte; Handy: in der Zeile).
- Standard ist Datum absteigend. Die Sortierung lebt im Client-Zustand, übersteht Konto-
  und Filterwechsel, wird nicht dauerhaft gespeichert. Ein Sortierwechsel leert die
  Auswahl (sie bezieht sich auf geladene Zeilen) und lädt ab Seite 1 neu.

## Acceptance criteria
- [ ] `LedgerEntryPageQuery` trägt Sortierfeld und Richtung; Validierung lehnt unbekannte
      Werte ab.
- [ ] Persistenz-Tests (In-Memory-SQLite) zeigen für jedes Sortierfeld und beide
      Richtungen die richtige Reihenfolge über eine Seitengrenze hinweg, ohne doppelte oder
      fehlende Buchungen; gleiche Werte werden stabil über `id` geordnet.
- [ ] Payee- und Kategorie-Sortierung ordnen `rewe` und `REWE` gleich ein und setzen leere
      Werte ans Ende.
- [ ] Trefferzahl und Summe sind von der Sortierung unabhängig (Test).
- [ ] `LocallyRemoved` liefert bei Nicht-Datums-Sortierung keine fehlende Buchung an der
      Seitengrenze (Regressionstest analog ledger-be1yh).
- [ ] PC: Klick auf einen sortierbaren Kopf sortiert, zweiter Klick dreht die Richtung;
      `aria-sort` ist gesetzt; Memo und Status sind nicht klickbar.
- [ ] Bei Nicht-Datums-Sortierung zeigt die Liste keine Tagesköpfe und keine
      Tagessummen; bei Datum beide Richtungen mit Tagesköpfen.
- [ ] Handy: die Sortier-Auswahl in der Filterleiste setzt dieselbe Sortierung.
- [ ] Update-Tests: Sortierwechsel setzt Seite auf 1, leert die Auswahl und behält die
      Filter.
- [ ] Die Sortier-Pfeile im Kopf fügen sich in die ruhige Tabelle (ADR 0049). [human-eye]

## Notes
- Hängt an `ledger-qh9qx`, weil beide die Spaltenköpfe anfassen und die Kopf-Zellen
  dort neu verteilt werden.
- `Table.calmHeaderCell` braucht eine klickbare Variante (oder eine neue DS-Funktion
  `calmSortableHeaderCell`); neue DS-Elemente gehören in die `/styleguide`-Route.
- Sortierung nach Kategorie: welcher Name gilt bei Split-Buchungen (ADR 0038, 0045)? Beim
  Bau festlegen, vorgeschlagen: Splits als eigene Gruppe am Ende, nach Datum geordnet.
- Der Index auf `date` trägt die Standardsortierung; für die anderen Felder bei 14.000
  Zeilen Laufzeit messen, bevor ein Index angelegt wird.
- README-Satz "Kategoriewechsel sortiert nie um" bleibt gültig: eine Änderung an einer
  Zeile sortiert die geladene Liste nicht neu, erst ein Neuladen.
