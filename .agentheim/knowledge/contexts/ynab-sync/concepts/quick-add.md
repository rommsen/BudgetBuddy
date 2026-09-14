---
name: quick-add
description: Manuelle Bar-Buchung direkt nach YNAB — eigene Seite, eigenes Konto, kein Dedup, mit Vorlagen
context: ynab-sync
created: 2026-06-27
last_updated: 2026-06-27
derived_from:
  - 0003
  - 0004
  - 2026-06-11-quick-add-manual-entry
  - 2026-06-12-quick-add-feedback-round
  - ynab-q7k3m
  - ynab-t4n8p
max_lines: 60
---

# Quick Add — concept

## What it is
**Quick Add** ist BudgetBuddys schneller Eingabeweg für eine einzelne, manuell erfasste
Buchung (typisch eine Bar-Ausgabe) — direkt und synchron nach YNAB gepusht, ohne lokalen
Store. YNAB bleibt Source of Truth; Quick Add macht nur, was YNAB Mobile auch könnte, nur
schneller und im BB-Workflow. Die erfasste Buchung ist eine **ManualTransaction**.

## Why it exists
Bar-Ausgaben (und der Frau-Workflow) hatten keinen Weg in YNAB außer YNAB Mobile selbst —
in `docs/idea-ynab-replacement.md` als Phase 0 der Ersatz-Idee dokumentiert, lange explizit
Non-Goal (ADR 0001). ADR 0003 amendierte das: manuelle Eingabe ist In-Scope als bewusst
*enthaltenes* Phase-0-Experiment an der Companion-Grenze; Phase 1+ (eigenes Budget) bleibt
Non-Goal. ADR 0004 zog die technischen Grenzen aus Romans erstem Android-Test.

## Current shape
Eine eigenständige Top-Level-Seite, kein sync-flow-gebundenes Sheet mehr:

- Seite `QuickAdd` (Route `#/quickadd`) + Nav-Eintrag; Form-State im Top-Level-Model
  (`src/Client/Views/QuickAddPage.fs`, `src/Client/State.fs`) — **ynab-q7k3m**.
- Push **ohne ImportId**, auf ein eigenes **Quick-Add-Konto** (`ynab_quickadd_account_id`),
  kein Fallback aufs Import-Konto; leere Optionalfelder weggelassen — ADR 0004
  (`buildManualTransactionBody`, `src/Server/YnabClient.fs`).
- Betrag → signierte Milliunits via `manualTransactionMilliunits` (`src/Shared/Domain.fs`);
  Payee optional, Eintrag `uncleared`.
- Bis zu 5 deduplizierte **Vorlagen** aus den jüngsten Quick-Add-Buchungen (Schlüssel
  Payee+Betrag+Kategorie); Tipp füllt das Formular vor (Datum=heute, kein Auto-Push) —
  `recentQuickAddTemplates`/`amountFromMilliunits` (`src/Shared/Domain.fs`), **ynab-t4n8p**.

## Open questions
- Liegt außerhalb beider Duplikat-Welten (DuplicateStatus / YnabImportStatus): Doppel-Tap
  erzeugt zwei Buchungen (wie YNAB Mobile) — bewusst akzeptiert (ADR 0004).
- Der Vorlagen-Read kostet einen zusätzlichen YNAB-Call pro Seitenbesuch → die offene
  ynab-sync-Rate-Limit-Frage (YNAB ~200 req/h) wird konkreter.
- On-Device-Test der Seite + Vorlagen steht aus (braucht konfiguriertes Konto + reale Daten).

## See also
- `[ADR 0003]` — Scope: Quick Add aktiviert Phase 0
- `[ADR 0004]` — kein ImportId, eigenes Quick-Add-Konto
- `[done/2026-06-11-quick-add-manual-entry]` — Erst-Implementierung (voller Stack)
- `[done/2026-06-12-quick-add-feedback-round]` — eigenes Konto, echter Picker, Payee optional
- `[done/ynab-q7k3m-quick-add-in-bottom-nav]` — eigene Seite + Nav + State-Lift
- `[done/ynab-t4n8p-quick-add-recent-templates]` — Vorlagen
