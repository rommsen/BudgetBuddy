---
id: ledger-k4n2s
title: "Kann-Sammlung ledger"
status: backlog
type: chore
context: ledger
created: 2026-11-06
completed:
depends_on: []
blocks: []
tags: [ledger, review, design, test]
related_adrs: [0020, 0049, 0075]
related_research: []
prior_art: []
---

## Why
Sammelstelle für Befunde, die stimmen, aber die Schwelle der Backlog-Disziplin (CLAUDE.md) nicht erfüllen. Angelegt beim ersten Bestandsreview ledger am 2026-11-06. Roman priorisiert und schneidet; nichts davon wird automatisch gebaut.

## Kann

1. **Handy-Trefferflächen und Farbrollen im Kassenbuch** (Bestandsreview ledger 2026-11-06, Gruppe 6, 29 Befunde, höchste Schwere mittel): Knöpfe am Handy bei 32 bis 44 px statt 48 px (Detail-Kopf, Filter-Knopf, Chip-„×“, Split-Sheet, Konto-Wechsler), von Hand nachgebaute Bausteine (Zeitraum-Schnellwahl, Payee-Vorschläge, Split-Sheet-Knöpfe, Settings-Selects) und Orange für Zustände statt nur für die Hauptaktion (Nicht in YNAB, Push-Fehler, Nicht konfiguriert). Offen für Roman: Ist Orange für „Nicht in YNAB“ gewollt? `View.fs:194` beruft sich auf ADR 0049, das Orange nur „Keine Kategorie“ gibt. Befunde: `.agentheim/knowledge/reviews/2026-11-06-ledger.md`, Abschnitt 6.
2. **Test-Altlasten, N+1, Benennung, Elmish-Muster** (Bestandsreview ledger 2026-11-06, Gruppe 7, 33 Befunde, höchste Schwere mittel): Quelltext- und Klassenstring-Tests nach Test-Doktrin Punkt 6 und 7 löschen oder auf reine Funktionen heben (LedgerViewTests, LedgerSelectionTests, LedgerReviewButtonVisibilityTests und weitere), N+1 in den Sammelbefehlen und im Spiegel-Lauf (`CommandHandlers.fs:1375`, `:444`), Benennungen (`addCashExpense` bucht auch Einnahmen, `reviewMarkEvent`, `SetSortExactly`), Bool-Ladeflags und `Run…`-Zweige mit Logik (ADR 0020). Befunde: `.agentheim/knowledge/reviews/2026-11-06-ledger.md`, Abschnitt 7.
