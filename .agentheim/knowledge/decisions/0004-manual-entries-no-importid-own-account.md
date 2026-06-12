---
id: 0004
title: Manuelle Einträge ohne ImportId, auf eigenes Quick-Add-Konto
scope: ynab-sync
status: accepted
date: 2026-06-12
supersedes: []
superseded_by: []
related_tasks:
  - contexts/ynab-sync/done/2026-06-11-quick-add-manual-entry.md
  - contexts/ynab-sync/done/2026-06-12-quick-add-feedback-round.md
related_research: []
---

# ADR 0004: Manuelle Einträge ohne ImportId, auf eigenes Quick-Add-Konto

## Context
Quick Add (ADR 0003) pusht manuell erfasste Transaktionen nach YNAB. Der bestehende
Push-Pfad (`createTransactions`) hängt an zwei Mechanismen, die für manuelle Einträge
falsch wären: der **ImportId** (BBs Dedup-Schlüssel, den YNAB zum Erkennen wiederholter
Bank-Importe nutzt) und dem **Standard-Konto** (`ynab_default_account_id`, das
Comdirect-Import-Konto). Eine Bar-Ausgabe ist aber weder ein Bank-Import noch gehört
sie aufs Girokonto. Der erste Wurf nutzte das Import-Konto — Romans Feedback vom ersten
Android-Test: "macht nur Sinn, wenn es auf den Bar-Account geht".

## Decision
1. **Keine ImportId für manuelle Einträge.** `buildManualTransactionBody` setzt bewusst
   kein `import_id`-Feld. YNAB behandelt Transaktionen ohne import_id als user-entered
   (wie seine eigene Mobile-Eingabe); sie können nie mit dem Bank-Import-Dedup
   kollidieren — weder BBs Vor-Import-Urteil noch YNABs eigenem.
2. **Eigenes, konfigurierbares Quick-Add-Konto.** Neuer Settings-Key
   `ynab_quickadd_account_id` (Settings → YNAB → "Quick-Add-Konto (z. B. Bar)"),
   strikt getrennt vom Bank-Import-Konto. Ist keins konfiguriert, lehnt der Server mit
   einer klaren Meldung ab — **kein Fallback** auf das Import-Konto, eine stille
   Fehlbuchung wäre schlimmer als ein Fehler.
3. **Optionale Felder werden weggelassen, nicht leer gesendet.** `payee_name` und `memo`
   fehlen im JSON, wenn leer — ein leerer String würde in YNAB einen Payee namens ""
   anlegen. Payee ist fachlich optional (Bar-Buchung braucht nur einen Betrag).
4. Manuelle Einträge sind `uncleared` (Projekt-Konvention, wie der Import-Pfad).

## Consequences
### Positive
- Die zwei Duplikat-Welten (DuplicateStatus / YnabImportStatus) bleiben unberührt —
  manuelle Einträge existieren außerhalb beider, per Konstruktion.
- Bar-Ausgaben landen auf dem richtigen Konto; das Import-Konto bleibt sauber.
- Doppel-Tap erzeugt zwar potenziell zwei Buchungen (kein Dedup-Schlüssel), aber das
  entspricht YNABs eigenem Verhalten bei manueller Eingabe — akzeptiert.

### Negative
- Ein zusätzlicher Pflicht-Konfigurationsschritt nach dem Deploy (Quick-Add-Konto
  einmalig wählen), sonst Fehlermeldung.

### Neutral
- Der JSON-Vertrag ist durch Tests fixiert (`QuickAddTests.fs`: Betrag als
  JSON-Zahl in Milliunits, import_id abwesend, payee_name/memo weggelassen wenn leer).

## Alternatives considered
- **ImportId mit eigenem Prefix (z. B. `BB:QA:`)** — abgelehnt: schützt nicht gegen
  Doppel-Tap (frische UUID je Aufruf) und verheddert manuelle Einträge unnötig mit der
  Dedup-Maschinerie.
- **Fallback aufs Standard-Konto wenn Quick-Add-Konto fehlt** — abgelehnt: stille
  Fehlbuchung aufs Girokonto ist schwerer zu bemerken und zu korrigieren als eine
  Fehlermeldung.

## References
- `src/Server/YnabClient.fs` (`buildManualTransactionBody`, `createManualTransaction`)
- `src/Server/Api.fs` (`addManualTransaction`), `src/Server/Validation.fs`
- `src/Tests/QuickAddTests.fs` (Vertragstests)
- ADR 0003 (Scope-Entscheidung)
