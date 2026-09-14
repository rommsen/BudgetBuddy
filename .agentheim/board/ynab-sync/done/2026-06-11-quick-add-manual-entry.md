---
title: "Quick Add: manuelle Transaktions-Eingabe → YNAB (Phase 0)"
status: done
bc: ynab-sync
created: 2026-06-11
completed: 2026-06-11
captured: retroaktiv 2026-06-12 — Arbeit lief außerhalb von agentheim (Session feature/ux-wow)
related_adrs: [0003, 0004]
commits: [37a00b5, 320327e]
branch: feature/ux-wow
---

# Quick Add: manuelle Transaktions-Eingabe → YNAB (Phase 0)

## Problem
Bar-Ausgaben (und der Frau-Workflow) hatten keinen Weg in YNAB außer YNAB Mobile selbst.
`docs/idea-ynab-replacement.md` dokumentiert genau das als Phase 0 der Ersatz-Idee —
bis dato explizites Non-Goal (ADR 0001). Scope-Entscheidung: ADR 0003 (Amendment,
manuelle Eingabe als enthaltenes Phase-0-Experiment in-scope).

## Acceptance criteria
- [x] Mobile-first Formular: Betrag (Dezimal-Tastatur, deutsche Komma-Eingabe),
      Ausgabe/Einnahme, Kategorie, Datum, Memo
- [x] Push direkt und synchron an die YNAB-API — kein lokaler Store, YNAB bleibt Source of Truth
- [x] Keine ImportId — manuelle Einträge kollidieren nie mit dem Import-Dedup (ADR 0004)
- [x] Validierung an der API-Grenze (Betrag > 0, Datum nicht in der Zukunft, Längen-Caps)
- [x] Vertrags- und Validierungs-Tests (Milliunits-Vorzeichen, JSON-Zahl statt String,
      import_id abwesend, optionale Felder weggelassen)

## Outcome
Voller Stack: `ManualTransactionRequest` + `manualTransactionMilliunits` (Shared),
`YnabApi.addManualTransaction`, `validateManualTransaction`,
`buildManualTransactionBody`/`createManualTransaction` (Server), QuickAdd-Sheet +
SyncFlow-State (Client). 48 Tests in `QuickAddTests.fs`; QA-Review
(qa-milestone-reviewer): ADEQUATE. Erste Fassung nutzte noch das Import-Konto und
eine native Selectbox — korrigiert in der Feedback-Runde (Folge-Task 2026-06-12).

## Verification
- 514/514 Tests grün; Endpoint im Dev-Setup mit invalidem Request verifiziert
  (Validierung greift, kein YNAB-Write); deployed auf docker-host
