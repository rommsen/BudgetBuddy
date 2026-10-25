---
id: ADR-0062
title: Memo-Schreibweg: Ereignis mit Alt und Neu, Fehlschlag mit Baseline, Grenze 500 Zeichen
scope: ledger
status: accepted
date: 2026-10-25
supersedes: []
superseded_by: []
related_tasks: [ledger-fa1fp]
related_adrs: [0028, 0050, 0055, 0056]
related_research: []
---

# ADR-0062: Memo-Schreibweg

## Context
Das Memo ließ sich in BB nicht bearbeiten (Detail zeigte "Kein Memo", Tafeln 5.2 und 7.8 sehen "Memo hinzufügen" vor). Der Spiegel liest das Memo schon (`EntryDetailsChangeObservedInYnab`). Der Schreibweg folgt ADR 0050, 0055 und 0056.

## Decision
1. **Ein Ereignis für jede Memo-Änderung aus BB:** `EntryMemoChangedInBb { OldMemo; NewMemo }` (Actor `UserInBb`, Version 1) für Setzen, Ändern, Leeren und "Rückgängig". Rückgängig ist derselbe Befehl `setEntryMemo` mit dem alten Memo, es gibt keinen eigenen Wiederherstellungs-Befehl.
2. **Eingabe:** `Shared.Domain.normalizeMemo` trimmt, leer heißt kein Memo (`None`), über der Grenze wird abgelehnt (nie gekürzt). Client und Server nutzen dieselbe Regel. Ziel gleich Ist (auch bis auf Leerraum) ergibt `MemoUnchanged`: kein Ereignis, kein Push.
3. **Grenze 500 Zeichen** (`ynabMemoMaxLength`) nach YNABs OpenAPI-Schema. Nicht online geprüft (Annahme). Das weicht bewusst von `MemoFormatting.memoLimit = 300` ab, das nur den Comdirect-Import und Split-Memos kürzt ("testing with 300"); ein von Roman getipptes Memo wird nie still gekürzt.
4. **Reihenfolge und Push wie ADR 0050/0056:** erst `appendAndApply`, dann `PUT /budgets/{id}/transactions/{tid}` mit `{"transaction":{"memo": ...}}` bzw. `null`. Eigener Name (`YnabClient.updateMemo`), kein `approved`, `cleared`, Flag oder Kategorie. Unter der Schreib-Sperre. Gelöschte Buchungen werden abgelehnt.
5. **Fehlschlag als eigener Zustand:** `EntryMemoWriteBackFailed { Reason; BaselineMemo }` und `EntryMemoWrittenBackToYnab`, Feld `MemoWriteBackFailure` an `Entry` (Spalte `memo_write_back_failure`) und `LedgerEntryView`. Erste Baseline bleibt bei weiteren Gesten, eine neue Geste setzt den Zustand zurück, "Erneut senden" (`retryMemoWriteBack`) sendet das aktuelle Memo.
6. **Spiegel:** das Memo gehört weiter zur Gruppe der Kopfdaten, aber `observe` wendet bei offenem Fehlschlag die Regel von ADR 0056 an: YNAB gleich BB-Stand ergibt `EntryMemoWrittenBackToYnab` (Actor `YnabMirror`), gleich Baseline ergibt kein Memo-Ereignis (ein Kopfdaten-Ereignis aus anderem Grund trägt dann das BB-Memo als `NewMemo`, damit `apply` es nicht überschreibt), jeder andere Stand ist eine echte Änderung, die den Fehlschlag in `apply` beendet.
7. **Historie nach dem Aufteilen:** `describeRecategorized` nennt die Zeilen mit Ziel und Betrag ("Aufgeteilt in Möbel -23,40 EUR, Haushalt -58,50 EUR", Tafel 4.7). Die Sätze der YNAB-Beobachtung bleiben unverändert.

## Consequences
- Drei neue Ereignistypen, eine Spalte, ein Feld an `LedgerEntryView` (Antworten älterer Server: `None`).
- Neue Record-Literale von `LedgerEntryView` und `Entry` brauchen `MemoWriteBackFailure`.
- Die Rückschreibe-Hinweise bleiben je Feld getrennt (Prüfvermerk, Kategorie, Flag, Memo), höchstens eine Leiste, Memo zuletzt.
