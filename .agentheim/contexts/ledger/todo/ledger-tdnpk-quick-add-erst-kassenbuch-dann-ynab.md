---
id: ledger-tdnpk
title: "Quick Add: erst Kassenbuch (CashExpenseAdded), dann Push nach YNAB, dann Verknüpfung über die Id aus der Push-Antwort"
status: todo
type: feature
context: ledger
created: 2026-09-11
completed:
depends_on: [ledger-cscw1]
blocks: []
tags: [ledger, ynab-sync, quick-add, identity]
related_adrs: [0004, 0013, 0015, 0021]
related_research: []
prior_art: []
---

## Why
Roman hat entschieden (2026-09-11): Quick Add schreibt künftig zuerst eine Buchung ins
Kassenbuch, pusht dann nach YNAB und verknüpft danach die YNAB-Id aus der Push-Antwort.
Heute pusht Quick Add direkt nach YNAB (ADR 0004) und kommt nur über den Spiegel zurück,
ohne eigene Herkunft im Kassenbuch. Das ist das Muster der Vision ("BB entscheidet, YNAB
wird nachgezogen") am kleinsten Fall.

Quick-Add-Buchungen tragen keine ImportId (ADR 0004). Die Antwort des Einzel-Posts ist
deshalb die einzige Stelle, an der BBs Buchung und YNABs Buchung verbunden werden können
(ADR 0015). Heute gibt `createManualTransaction` nur `unit` zurück. Dieser Task nimmt den
Quick-Add-Teil des früheren Tasks `ynab-b7q2m` in sich auf (Entscheidung Roman,
2026-09-11).

## What
1. `YnabClient.createManualTransaction` gibt die YNAB-Transaction-Id aus der
   Einzel-Post-Antwort zurück statt `unit`.
2. Neuer Command-Handler in `Ledger/CommandHandlers.fs` (z. B. `addCashExpense`) nimmt die
   Quick-Add-Eingabe (Konto "Bar", Betrag, Payee, Memo, Kategorie) und schreibt zuerst
   `CashExpenseAdded` (Actor `QuickAdd`) via `appendAndApply`.
3. Danach ruft er `createManualTransaction` auf.
4. Bei Erfolg: `EntryLinkedToYnab` mit der zurückgegebenen Id auf derselben Buchung.
5. Bei Push-Fehler: die Kassenbuch-Buchung bleibt stehen (ADR 0021 Punkt 2), kein
   Rollback; Fehlermeldung an Roman wie heute.
6. Die bestehende `AddManualTransaction`-API-Signatur bleibt für den Client unverändert.

## Acceptance criteria
- [ ] `createManualTransaction` gibt die YNAB-Transaction-Id aus der Antwort zurück (Test
      gegen eine aufgezeichnete Antwort).
- [ ] Eine Quick-Add-Eingabe erzeugt zuerst eine Kassenbuch-Buchung mit Herkunft
      "Quick Add", danach eine YNAB-Buchung, danach eine Verknüpfung; die Historie zeigt
      `CashExpenseAdded` und `EntryLinkedToYnab`.
- [ ] Ein simulierter Push-Fehler lässt die Kassenbuch-Buchung stehen, ohne Verknüpfung
      (Test).
- [ ] Der nächste Spiegel-Lauf trifft die verknüpfte Buchung über die YNAB-Id und legt
      keine zweite an (Test).
- [ ] Bestehende Quick-Add-Vertragstests (`QuickAddTests.fs`, ADR 0004) bleiben grün.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
- Das kurze Zeitfenster zwischen erfolgreicher Push-Antwort und `EntryLinkedToYnab`
  (Absturz genau dazwischen ergibt beim nächsten Spiegel-Lauf eine zweite Buchung, weil
  Quick Add keine ImportId hat) ist laut ADR 0021 für v1 hingenommen.
- Für `ledger-h4p6c` (Spiegel nach jedem Push) vermerkt: der Lauf nach Quick Add darf erst
  nach `EntryLinkedToYnab` starten.
- `ledger-qvt84` ändert parallel `createTransactions` in derselben Datei, aber eine andere
  Funktion.
