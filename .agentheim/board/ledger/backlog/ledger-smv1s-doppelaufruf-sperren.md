---
id: ledger-smv1s
title: "Erneut senden und Rückgängig gegen Doppelaufruf sperren, einheitlich in update, mit Aufzählungstests nach Doktrin Punkt 20"
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
Bestandsreview ledger vom 2026-11-12, Gruppe G1. Roman hat die Gruppe als Task gewählt.

## What
Alle Befunde der Gruppe in Befundform, wörtlich aus dem Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`:

### G1 Erneut senden und Rückgängig ohne Sperre gegen Doppelaufruf (mittel, 15 Befunde, ledger)
Ein Teil der Start-Zweige sperrt in `update`, ein Teil verlässt sich auf die gesperrte Schaltfläche; zwei Befehle können gleichzeitig laufen, der zuletzt Antwortende gewinnt.
- src/Client/Components/Ledger/State.fs:2114 | overview.md „Gelerntes“ | mittel | `StartUndoCategory`, `StartUndoSplit` (3303), `StartUndoFlag` (2302), `StartUndoReviewMark` (1989) prüfen den Sende-Zustand nicht.
- src/Client/Components/Ledger/State.fs:1882 | overview.md „Gelerntes“ | mittel | `StartRetryReviewMark`, `StartRetryPush` (1912), `StartRetryCategorization` (2199), `StartRetryFlag` (2333), `StartSetReviewMark` (1855) sperren nur in der View; einheitlicher Helfer `whenRowIdle`.
- src/Client/Components/Ledger/State.fs:2445 | overview.md „Gelerntes“ | mittel | `StartUndoMemo` ohne Sperre (Kann-Sammlung ledger 53, hier aufgenommen).
- src/Client/Components/Ledger/State.fs:3515 | overview.md „Gelerntes“ | mittel | `StartUndoReviewMarkBatch` setzt und prüft keine Laufsperre.
- src/Client/Components/Ledger/State.fs:3484 | overview.md „Gelerntes“; Types.fs:1372 | mittel | `StartReviewMarkBatch` prüft `CategoryBatchRunning` nicht.
- src/Client/Components/Ledger/Types.fs:1374 | elmish-state.md (keine Bool-Flags für Async) | mittel | Drei Bool-Flags für einen gegenseitig ausschließenden Sammellauf; `BatchRunning: BatchAction option`.
- src/Tests/LedgerCategoryChangeClientTests.fs:327 | test-doctrine.md Punkt 20 | mittel | Aufzählung „höchstens ein Aufruf je Buchung“ für Kategorie und Split.
- src/Tests/LedgerReviewMarkClientTests.fs:72 | test-doctrine.md Punkt 20 | mittel | Dieselbe Aufzählung für den Prüfvermerk.
- src/Tests/LedgerLenaFlagClientTests.fs:176 | test-doctrine.md Punkt 20 | mittel | Dieselbe Aufzählung für Lena fragen.
- src/Tests/LedgerMemoClientTests.fs:149 | test-doctrine.md Punkt 20 | mittel | Memo-Sperre als Aufzählung.
- src/Tests/LedgerNotInYnabClientTests.fs:59 | test-doctrine.md Punkt 20 | mittel | „Erneut senden“ bei „Noch nicht in YNAB“ mit zwei Tipps (zweiter POST).
- src/Tests/LedgerReviewMarkBatchClientTests.fs:87 | test-doctrine.md Punkt 20, 4 | mittel | Aufzählung über die drei Sammelaktionen samt Rückgängig.
- src/Tests/LedgerAmountClientTests.fs:375 | test-doctrine.md Punkt 20 | niedrig | Einzelfälle der Betrags-Sperre durch Aufzählung ersetzen.
- src/Client/Components/Ledger/State.fs:3907 | README „Durchgehen“ | niedrig | „Passt, weiter“ prüft eine laufende Prüfvermerk-Aktion aus „Details“ nicht.
- src/Client/Components/Ledger/State.fs:4020 | overview.md „Gelerntes“ | niedrig | `ReviewConfirmedNew`/`ReviewDiscarded` lösen `Deciding` ohne Prüfung der Id.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-11-12, raw, unrefined. Bewusst groß; im Refinement (`agentheim:modeling`) kleiner schneiden, wenn Roman das will.
