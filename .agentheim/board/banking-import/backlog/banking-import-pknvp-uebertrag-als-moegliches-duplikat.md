---
id: banking-import-pknvp
title: "Ein YNAB-Übertrag mit gleichem Betrag und Tag gilt beim Import nicht als mögliches Duplikat, weil der Payee nicht passt (DuplicateDetection.fs)"
status: backlog
type: bug
context: banking-import
created: 2026-11-13
completed:
depends_on: []
blocks: []
tags: [laufend, money, banking-import, server, tests]
related_adrs: [0093]
related_research: []
prior_art: [banking-import-d0fey]
---

## Why
Teil des Vorfalls ledger-nkwqb: YNAB hatte den Übertrag von der ING auf dem Girokonto schon angelegt, Payee
„Transfer : <Konto>“. Die Comdirect-Buchung trug den Namen des Kontoinhabers. `matchesByDateAmountPayee` verlangt, dass
ein Payee den anderen enthält, deshalb kein mögliches Duplikat und kein Warten. Die Buchung ging nach YNAB, und YNAB legte
sie still mit dem Übertrag zusammen.

## What
Entschieden von Roman (2026-11-13):
- Eine YNAB-Buchung auf dem Importkonto mit `transfer_account_id`, gleichem Betrag und Tag ±1 macht die Bankbuchung zum
  `PossibleDuplicate`, egal welcher Payee. Für alles andere gilt die Payee-Bedingung weiter.
- `Shared.Domain.YnabTransaction` bekommt `TransferAccountId: YnabAccountId option`. Der Typ geht nicht über Remoting
  (architect), die Fixtures sind nicht betroffen. `YnabClient.Decoders.transactionDecoder` liest das Feld mit
  `get.Optional.Field "transfer_account_id" Decode.guid |> Option.map YnabAccountId`.
- `detectDuplicate` und `possibleMatchOf` nutzen ein gemeinsames Prädikat (etwa
  `matchesPossibly = matchesByDateAmountPayee || matchesTransferByDateAmount`), damit `holdOf` den Treffer wiederfindet
  und `MatchYnabTransactionId` setzt.
- `possibleDuplicateReason` bleibt („… bei Transfer : <Konto>.“).

## Acceptance criteria
- [ ] 1. Regressionstest, rot vor dem Fix (Kommentar nennt den Bug), Daten erfunden: Ein YNAB-Übertrag mit gleichem
      Betrag am Tag ±1 und anderem Payee ergibt `PossibleDuplicate`.
- [ ] 2. Grenzen: bei +2 Tagen oder anderem Betrag kein Treffer. Ein Nicht-Übertrag mit anderem Payee bleibt
      `NotDuplicate` (intakt-Szenario).
- [ ] 3. `holdOf` findet den Übertragstreffer: `MatchYnabTransactionId` ist gesetzt, der Schnappschuss hat Payee und
      Datum des Übertrags.
- [ ] 4. Decoder-Test für `transfer_account_id` (gesetzt, fehlt, null).
- [ ] 5. Die Bullets **Unsicheres Duplikat** (ledger) und die Duplikaterkennung im README `banking-import` sind
      nachgezogen und mit `check:readme-delta` geprüft (dazu `grep` nach „ähnlichem Payee“).
- [ ] 6. `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Der Compiler meldet jede Stelle, an der der Record gebaut wird (`BankImport.holdOf`, Test-Helfer in
  `DuplicateDetectionTests`, `BankImportTests`, `QuickAddTests`, `OrderIdMatcherTests`). Überall `None` eintragen.
- Läuft parallel zu ledger-nkwqb (andere Dateien), beide gemeinsam deployen.
- Nachtrag ADR 0093 (Erkennung bei Überträgen) ist geschrieben.
- Kann-Punkt: `YnabTransaction` in einen reinen Server-Typ umziehen.
