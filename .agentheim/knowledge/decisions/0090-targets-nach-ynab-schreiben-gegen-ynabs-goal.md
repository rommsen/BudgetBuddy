---
id: 0090
title: Targets nach YNAB schreiben, geplant gegen YNABs heutiges Goal, Ergebnis als Ereignis im Target-Stream
scope: budgeting
status: accepted
date: 2026-11-08
supersedes: []
superseded_by: []
related_tasks: [budgeting-e2z7v]
related_adrs: [0072, 0083, 0084, 0087, 0088]
related_research: [ynab-reihenfolge-monate-target-schreiben-2026-11-02]
---

# ADR 0090: Targets nach YNAB schreiben, geplant gegen YNABs heutiges Goal

## Context
Der Nachtrag zu ADR 0084 vom 2026-11-08 lässt BB jede Target-Entscheidung beim Speichern nach YNAB schreiben (`PATCH .../categories/{id}` mit `goal_*`). ADR 0072 setzt die Grenzen: `goal_frequency` ersetzt YNABs Goal und löscht Tag und Datum, `goal_day` ist nicht schreibbar, `goal_frequency` und `goal_target_date` gehen nicht in einem Aufruf, "immer alle Felder senden" ist dort verworfen. Offen waren: wogegen geplant wird, ob Erfolg und Fehlschlag Ereignis oder Zustand sind, und wie ein Teilfehler aussieht. Geklärt mit dem tactical-modeler am 2026-11-08.

## Decision
1. **Geplant wird gegen YNABs heutiges Goal**, gelesen mit einem `GET /categories` vor jedem Senden (Speichern, Erneut senden, einmaliger Lauf). Die reine Funktion `TargetRules.planGoalWrite inYnab rule` sendet bei gleicher Art und gleichem Rhythmus nur die geänderten Felder `goal_target`, `goal_target_date`, `goal_needs_whole_amount` in einem Aufruf (ADR 0072 Punkt 3), sonst `goal_target` mit `goal_frequency` (NEED, nächster Rhythmus) und bei einem Datum einen zweiten Aufruf mit `goal_target_date` (ADR 0072 Punkt 5). Ohne Target `goal_target: null`. Steht YNAB schon auf BBs Target, geht nichts hinaus.
2. **Erfolg und Fehlschlag sind Ereignisse** (ADR 0083, Push-Ergebnis einer Geld-Entscheidung, Vorbild `EntryPushToYnabFailed` und `AssignmentWriteBackFailed`): `TargetWrittenToYnab {decisionEventId}` und `TargetWriteToYnabFailed {decisionEventId, reason}` im Stream `budgeting:target:<categoryId>`, Actor `YnabMirror`, ohne Befehls-Id. Sie stehen nicht in `EventTypes.targetRules`.
3. **Die letzte Entscheidung gewinnt.** Offen ist die letzte `TargetSet`/`TargetRemoved`, solange kein `TargetWrittenToYnab` auf sie verweist, auch ohne Ergebnis ("noch nicht gesendet"). Kein Zurückziehen-Ereignis: `TargetGuard` ordnet Festhalten, Senden und Ergebnis je Kategorie. Die Übernahme gilt als in YNAB.
4. **Ein Fehlschlag im zweiten von zwei Aufrufen** ist ein `TargetWriteToYnabFailed` mit "Aufruf 2 von 2: …". Erneut senden plant wieder gegen YNABs Stand und sendet nur den fehlenden Teil.
5. **Lücken werden nie gespeichert.** Was YNAB nicht zeigt (Wochentag, Tag, Rhythmus, Art), vergleicht `goalGaps` aus BBs Regel und YNABs Goal; die Pause zeigt "In YNAB nicht gesnoozt" aus dem Pausenzustand.

## Consequences
### Positive
- Tag und Wochentag eines aus YNAB übernommenen Targets bleiben in YNAB, solange Art und Rhythmus gleich bleiben (belegt im Nachtrag 2026-11-05 zu ADR 0072); der einmalige Lauf löscht sie nicht bei den 42 Monats-Targets mit Tag.
- Ein Teilfehler und eine in YNAB inzwischen geänderte Form werden beim nächsten Senden ohne Sonderfall richtig.
### Negative
- Ein zusätzlicher YNAB-Aufruf (`GET /categories`) je Speichern und Erneut senden.
- Eine Änderung direkt in YNAB beobachtet BB nicht; sie bleibt bis zum nächsten Schreiben stehen (hingenommen, BB führt).
- Nach einem Wechsel der Art ist ein Wochentag in YNAB weg und bleibt dort leer.

## Alternatives considered
- **Gegen die zuletzt bestätigte Regel planen** (Faltung "confirmed", Vorschlag des tactical-modelers): spart den Lesezugriff, sieht aber einen Teilfehler oder eine Änderung in YNAB nicht und meldete Lücken falsch. Verworfen zugunsten des tatsächlichen Stands.
- **Immer alle Felder mit `goal_frequency` senden:** verworfen wie in ADR 0072, es löscht Tag und Datum bei jedem Speichern.
- **Erfolg und Fehlschlag als Zustand:** verworfen, ADR 0083 verlangt Ereignisse für das Ergebnis einer Geld-Entscheidung.
- **Eigenes `TargetWriteToYnabWithdrawn`:** nicht nötig, die letzte Entscheidung gewinnt unter `TargetGuard`.

## Note on ADR numbering

Minted provisionally as ADR-0089 in its worker worktree. A sibling task's ADR already claimed that number (or the guess overshot the true count) by the time this task's conductor finalized numbering at squash-merge integration (`lib/adr-allocation.mjs`'s `finalizeAdrNumbering`, ADR-0058) — this ADR was renumbered to **ADR-0090**, the true next-free number on `main` at that moment. No content besides this identity changed.
