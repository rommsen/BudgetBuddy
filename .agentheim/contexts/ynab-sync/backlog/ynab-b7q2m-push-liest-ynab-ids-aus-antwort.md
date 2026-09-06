---
id: ynab-b7q2m
title: Push wertet YNABs Antwort aus und verknüpft Transaction-Ids (Import-Push über ImportId-Zuordnung, Quick Add über die Antwort des Einzel-Posts)
status: backlog
type: feature
context: ynab-sync
created: 2026-09-06
depends_on: [ledger-z5vw6]
blocks: []
tags: [push, quick-add, import, identity]
related_adrs: [0015, 0004]
related_research: []
prior_art: []
---

## Why
ADR 0015 (`ledger`) legt fest, dass jede Buchung im Kassenbuch eine eigene Id hat und
Fremd-Ids (YNAB, ImportId, Comdirect) als Verknüpfungen mitgeführt werden. Damit eine
Quick-Add-Buchung nach dem Push sofort mit ihrer YNAB-Buchung verknüpft ist, muss
`ynab-sync` die Antwort von `POST /transactions` auswerten. Heute tut es das nicht:
`TransactionCreateResult` in `src/Server/YnabClient.fs` trägt nur `CreatedCount`,
`DuplicateImportIds` und `RejectedTransferTransactionIds`, keine Transaction-Ids. Ohne
diese Auswertung sieht der nächste Spiegel-Lauf eine Quick-Add-Buchung ohne ImportId (ADR
0004) als kein Match und legt eine zweite Buchung für dieselbe Bar-Ausgabe an.

## What
`createTransactions` (Import-Push) und `createManualTransaction` (Quick Add) sollen die
von YNAB zurückgegebenen Transaction-Ids aus der Antwort lesen und im Kassenbuch als
`ynab`-Verknüpfung ablegen:
- **Import-Push:** Zuordnung der zurückgegebenen Ids zu den gepushten Buchungen über die
  ImportId, die BB selbst gesetzt hat.
- **Quick Add:** Zuordnung über die Antwort des Einzel-Posts, da hier keine ImportId
  existiert (ADR 0004).

## Acceptance criteria
- [ ] `TransactionCreateResult` (oder ein neuer Rückgabetyp) trägt die von YNAB
      zurückgegebenen Transaction-Ids, zuordenbar zur jeweils gepushten Buchung.
- [ ] Import-Push verknüpft die zurückgegebene YNAB-Id über die ImportId mit der Buchung im
      Kassenbuch.
- [ ] Quick Add verknüpft die zurückgegebene YNAB-Id direkt aus der Antwort des
      Einzel-Posts mit der Buchung im Kassenbuch.
- [ ] Tests für beide Pfade (Import-Push, Quick Add) belegen die Verknüpfung.

## Notes
Entstanden aus ADR 0015 (`ledger`), Punkt "Beim Push". Voraussetzung ist das Kassenbuch
und seine Fremd-Id-Verknüpfungstabelle aus `ledger-z5vw6` / ADR 0015.
