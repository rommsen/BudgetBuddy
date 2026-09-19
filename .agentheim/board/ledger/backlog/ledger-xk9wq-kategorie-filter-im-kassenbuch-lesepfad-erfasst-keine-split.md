---
id: ledger-xk9wq
title: "Kategorie-Filter im Kassenbuch-Lesepfad erfasst keine Split-Zeilen-Kategorien"
status: backlog
type: feature
context: ledger
created: 2026-09-19
completed:
depends_on: [ledger-vgfw3]
blocks: []
tags: [backend, kassenbuch, filter, splits]
related_adrs: [0006, 0013, 0036]
related_research: []
prior_art: [ledger-vgfw3]
---

## Why

`ledger-vgfw3` baut den Kategorie-Filter im Lesepfad (`LedgerEntryPageQuery.CategoryIds`)
gegen die `category_id`-Spalte der Zustandstabelle (ADR 0036). Eine aufgeteilte
Buchung (Splits, ADR 0006) trägt dort `NULL` — ihre Kategorien stecken in den
einzelnen Split-Zeilen, die als JSON-Text in der Spalte `splits` liegen. ADR 0013,
Punkt 8 verbietet SQLite-JSON-Funktionen in Abfragen; deshalb filtert der
Kategorie-Filter aus `ledger-vgfw3` bewusst NICHT über Split-Zeilen-Kategorien. Eine
Buchung, deren einzige Kategorie in einem Split steckt, findet der Filter also nicht.
Bei einer geschätzten Kategorie-Filter-Nutzung von 70-80 % (Romans eigene Angabe,
`ledger-vgfw3` Task-Text) ist das eine reale Lücke, sobald Roman eine aufgeteilte
Buchung über ihre Kategorie sucht.

## What

Eine Lösung finden, die Split-Zeilen-Kategorien im Kategorie-Filter erfasst, ohne die
14.000-Buchungen-Performance-Vorgabe aus `ledger-vgfw3` aufzugeben und ohne
SQLite-JSON-Funktionen (ADR 0013). Denkbare Richtungen (nicht vorentschieden, gehört
in die Verfeinerung):
- Eine eigene, normalisierte Tabelle (z. B. `ledger_entry_split_categories`:
  `entry_id`, `category_id`), parallel zur JSON-Spalte `splits` in `appendAndApply`
  gepflegt, mit eigenem Index — macht den Filter wieder SQL-tauglich, kostet aber
  eine zweite Schreibstelle im Schreibpfad.
- Split-Zeilen-Kategorien im Kategorie-Filter über einen F#-seitigen Nachfilter
  einschließen, analog zum Betrags-Filter aus ADR 0036 (SQL-vorgefilterte
  Kandidatenmenge laden, in F# auf Split-Kategorien prüfen) — mit denselben
  Performance-Abwägungen wie dort.

## Acceptance criteria

- [ ] Eine Buchung mit Splits, deren einzige Kategorie in einer Split-Zeile steckt
      (kein `category_id` auf der Buchung selbst gesetzt), erscheint im Treffer, wenn
      diese Kategorie im Kategorie-Filter (`CategoryIds`) steht.
- [ ] Bestehende Kategorie-Filter-Tests aus `ledger-vgfw3`
      (`src/Tests/LedgerPageTests.fs`, Testgruppe "Fünf Such-Filter im Lesepfad")
      bleiben unverändert grün.
- [ ] Performance bei ~14.000 Buchungen bleibt in derselben Größenordnung wie der
      reine `category_id`-Filter aus `ledger-vgfw3` (kein unbedingtes Laden aller
      Buchungen für den häufigsten Fall).
- [ ] `dotnet build` und `dotnet test` grün.

## Notes

Entdeckt und bewusst nicht gelöst bei `ledger-vgfw3` (Server-Hälfte der
Kassenbuch-Filter) — Scope-Grenze laut ADR 0036, Alternativen-Abschnitt.