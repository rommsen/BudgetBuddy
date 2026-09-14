---
id: ledger-tpbe7
title: Randfälle, in denen die Kassenbuch-Buchung nach dem Push von YNAB abweicht, und ungetesteter Klebecode im Import-Push
status: backlog
type: bug
context: ledger
created: 2026-09-11
completed:
depends_on: []
blocks: []
tags: [ledger, push, spiegel, captured]
related_adrs: [0013, 0021]
related_research: []
prior_art: [ledger-qvt84]
---

## Why
Fund aus der Verifikation von `ledger-qvt84` (Iteration 3, PASS, als nicht blockierend
markiert). Auf dem normalen Weg stimmt die Kassenbuch-Buchung nach dem Push mit dem
überein, was YNAB speichert. In einigen Randfällen weicht sie ab, und jede Abweichung
erzeugt beim nächsten Spiegel-Lauf ein falsches "in YNAB geändert". Das Log ist
append-only (ADR 0013), eine solche Zeile bleibt für immer in der Historie.

## What
Raw capture, beim Verfeinern zu prüfen und zu schneiden:
1. `categorizeTransaction` speichert `CategoryName = None`, wenn `getCategories`
   scheitert (z. B. 429); `recordComdirectPush` verwirft dann die Kategorie, YNAB bekommt
   sie aber.
2. Setzt eine Regel einen Payee-Override und Roman entfernt ihn in der Review, behält
   `NewPayee` den alten Override (`Option.orElse`), YNAB bekommt den Bank-Payee.
3. Split-Memos werden nicht so gekürzt wie in `truncateSplitMemo`.
4. Ein zwischengespeicherter Kategorie-Name einer Regel veraltet nach einer Umbenennung
   in YNAB.
5. Der Klebecode in `src/Server/Api.fs` (Payee und Memo aus `PreparedTransaction.Request`
   übernehmen, die Sperre `ledgerWriteFailureSummary.IsNone` vor `completeSession`) hat
   keinen Test, der ihn durchläuft; der Spiegel-Test baut Request und Zuordnung von Hand.
   Die Zuordnung in eine reine, getestete Funktion ziehen.
6. Der Doc-Kommentar zu `ComdirectPushInput.Payee`/`Memo` in `Ledger/Domain.fs` sagt noch
   "roh", seit Iteration 3 kommen die gesendeten Werte.
7. Auf dem Fehlerweg lädt der Client die Session nicht neu; im selben Batch importierte
   Zeilen und Duplikat-Ids erscheinen erst nach einem Neuladen.

## Acceptance criteria
To be defined during refinement.

## Notes
Quelle: Verifier-Bericht zu `ledger-qvt84`, Iteration 3 (Protokoll 2026-09-11 18:24).
Punkt 1 bis 4 sind fachlich zu gewichten (wie oft kommen sie vor), Punkt 5 bis 7 sind
kleine Aufräumarbeiten.
