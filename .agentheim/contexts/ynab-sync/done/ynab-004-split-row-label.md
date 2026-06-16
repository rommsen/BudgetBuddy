---
id: ynab-004
title: Split-Transaktion in der Liste als "Aufgeteilt" kennzeichnen
status: done                 # backlog | todo | doing | done
type: bug                    # feature | bug | refactor | chore | spike | decision
context: ynab-sync
created: 2026-06-16
completed: 2026-06-16
commit: ae7d448
depends_on: [ynab-002]
blocks: []
tags: [split, review-ux, mobile, display, bug]
related_adrs: [0006]
related_research: []
prior_art: [ynab-002, ynab-003]
---

## Why
Roman-Feedback (2026-06-16, am Gerät): eine aufgeteilte Buchung (REWE, −221,15) zeigte in der
Transaktionsliste weiterhin den orangen Platzhalter **„Kategorie…"**, als wäre sie
unkategorisiert — eine erledigte Aufteilung war optisch nicht von einer unangetasteten Buchung
zu unterscheiden.

## What
Reiner Client-Display-Fix in der Transaktionszeile. Ursache: eine Split-Transaktion hat
bewusst `CategoryId = None` (Server `Api.fs:959` — kein einzelne Kategorie), und die Chip-Logik
prüfte nur `CategoryId`. Jetzt berücksichtigen Label und Badge `tx.Splits`:
- Label: `Some splits (≥1)` → **„Aufgeteilt"** (sonst Kategoriename bzw. „Kategorie…").
- Badge: Split → `badge-ready` (nicht `badge-attention`/orange).
- Die Label-Entscheidung wurde in eine testbare reine Funktion `categoryChipLabel` extrahiert.

## Acceptance criteria
- [x] Eine Transaktion mit `Splits = Some [...]` (und `CategoryId = None`) zeigt „Aufgeteilt"
      statt „Kategorie…" — Test (`categoryChipLabel`).
- [x] `getCategoryBadgeClass` liefert für eine Split-Tx `badge-ready` statt `badge-attention`
      — Regressionstest.
- [x] Unkategorisiert (kein Split, keine Kategorie) zeigt weiter „Kategorie…"; kategorisiert
      zeigt den Namen; Duplikat-Status gewinnt — Tests (intakt).
- [x] `dotnet build` + `dotnet test` grün (577 passed).

## Notes
**Load-bearing:** `src/Client/Components/SyncFlow/Views/TransactionRow.fs`
(`getCategoryBadgeClass`, neue `categoryChipLabel`); `src/Tests/SyncFlowViewTests.fs`.

**Caveat (bewusst out of scope):** Splits werden nicht persistiert (`Persistence.fs:677`
rekonstruiert `Splits = None`). Der Fix gilt für die laufende In-Memory-Session — der reale
Sync→Review→Import-Flow. Ein DB-Reload würde das „Aufgeteilt"-Label verlieren; Splits
persistieren (Schema + Migration) ist eine seit ynab-001 aufgeschobene Folgeaufgabe — bei
Bedarf als eigener Task aufnehmen.
