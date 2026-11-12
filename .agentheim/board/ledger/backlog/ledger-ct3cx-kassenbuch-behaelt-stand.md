---
id: ledger-ct3cx
title: "Kassenbuch behält seinen Stand: kein pauschales Neuladen nach dem Spiegel-Lauf, Historie und Filter an Versionen gebunden"
status: backlog
type: bug
context: ledger
created: 2026-11-12
completed:
depends_on: []
blocks: []
tags: [bestandsreview, captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger vom 2026-11-12, Gruppe G2. Roman hat die Gruppe als Task gewählt.

## What
Alle Befunde der Gruppe in Befundform, wörtlich aus dem Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`:

### G2 Kassenbuch verliert Stand: pauschales Neuladen und ungebundene Antworten (hoch, 6 Befunde, ledger)
Nach jedem Spiegel-Lauf und bei überholten Antworten gehen Liste, Auswahl und Historie verloren oder zeigen alten Stand.
- src/Client/Components/Ledger/State.fs:1273 | elmish-state.md; standards/frontend/overview.md „Gelerntes“ | hoch | `AccountsLoaded (Ok …)` ruft nach jedem Spiegel-Lauf (auch dem automatischen beim Öffnen) `resetEntries`: Auswahl, Auswahl-Modus, nachgeladene Seiten und Scrollstand sind weg; nur bei geändertem Filter neu aufsetzen, sonst im Hintergrund nachladen.
- src/Client/Components/Ledger/State.fs:1499 | overview.md „Gelerntes“ | mittel | `HistoryLoaded` nur an die Buchungs-Id gebunden; ältere Antwort überschreibt neuere, Nachladen flackert; Generation mitgeben, alten Stand halten.
- src/Client/Components/Ledger/State.fs:2089 | styleguide.md §7.11 | mittel | Kategorie, Übertrag, Split, Lena und Prüfvermerk laden die Historie des offenen Details nicht neu (Memo, Betrag, Payee schon); gemeinsamer Helfer „nach Änderung“.
- src/Client/Components/Ledger/State.fs:1810 | overview.md „Gelerntes“ | mittel | `RemoveMemoFilter`, `RemoveAmountFilter`, `ClearAllFilters` erhöhen die Versionen nicht; ein laufendes Commit setzt den entfernten Filter wieder.
- src/Client/Components/Ledger/State.fs:3954 | README „Durchgehen“ | mittel | Scheitert „Lena fragen“ im Durchgehen, kommt die Karte nicht zurück; im Fehlerzweig `returnToReviewCard`.
- src/Client/Components/Ledger/State.fs:1348 | Lesbarkeit | niedrig | 20 Zweige rufen `resetEntries` und danach `LoadEntries`, das es erneut ruft (Generation +2).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-11-12, raw, unrefined. Bewusst groß; im Refinement (`agentheim:modeling`) kleiner schneiden, wenn Roman das will.
