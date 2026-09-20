---
id: ledger-w6tbq
title: "Force-Import: die Ableitung der alten ImportId gegen den normalen Push festnageln"
status: todo
type: chore
context: ledger
created: 2026-09-20
completed:
depends_on: []
blocks: []
tags: [tests, force-import, ynab, abdeckung]
related_adrs: [0015, 0021]
related_research: []
prior_art: [ledger-fq3kd, ledger-r7dm2]
---

## Why

Lücke 3 aus dem QA-Review nach dem Work-Lauf vom 2026-09-19 (erfasst als
`ledger-r7dm2`), das Risiko mit dem höchsten Schaden.

`ledger-fq3kd` hat Force-Import so umgebaut, dass er die bestehende
Kassenbuch-Buchung über ihre alte, deterministische ImportId wiederfindet und neu
verknüpft, statt eine zweite „Comdirect-Import"-Buchung anzulegen. Die drei Tests dazu
sitzen auf `Ledger.CommandHandlers.pushComdirectImports` und bauen ihren Kandidaten von
Hand. Ungeprüft bleibt, was die API-Ebene neu tut, vor allem
`src/Server/Api.fs:1394`:

    ExistingEntryImportId = Some (Shared.Domain.generateImportId p.TransactionId)

Dieser Ausdruck muss byteweise dieselbe ImportId erzeugen wie der normale Push über
`Ynab.prepareTransactions ... forceNewImportId = false` (`src/Server/YnabClient.fs:661`).
Driften die beiden Ableitungen je auseinander, findet Force-Import die bestehende
Buchung nicht mehr, legt wieder still eine zweite an, und alle Tests bleiben grün, weil
keiner die beiden Ableitungen gegeneinander hält.

Dazu kommt, dass die beiden Aufrufstellen in `src/Server/Api.fs` (Z. 1131 bis 1163 für
den normalen Push, Z. 1369 bis 1394 für Force-Import) denselben 20-Zeilen-Block
Kandidatenbau doppelt führen und sich nur in einem Feld unterscheiden. Die Regel
„Payee/Memo kommen aus `p.Request`, nicht aus den rohen Bank-Feldern" (Verifier-Fund aus
`ledger-qvt84`, Iteration 3) steht deshalb zweimal da und ist an keiner Stelle getestet.

## What

Den Kandidatenbau aus beiden Closures in eine reine Funktion auf Modulebene in
`src/Server/Api.fs` ziehen, etwa:

    let buildComdirectImportCandidates
        (forceImport: bool)
        (transactions: SyncTransaction list)
        (prepared: Ynab.PrepareResult)
        : Ledger.CommandHandlers.ComdirectImportCandidate list

`forceImport = false` setzt `ExistingEntryImportId = None`, `true` setzt
`Some (Shared.Domain.generateImportId p.TransactionId)`. Beide Aufrufstellen rufen
diese Funktion. Derselbe Zug, den `ledger-w9skm` für `entryRowClassName` gemacht hat:
die Entscheidung wird eine reine Funktion, damit sie ohne Netz, ohne Datenbank und ohne
Session testbar ist.

Dazu eine neue Testdatei `src/Tests/ForceImportCandidateTests.fs` samt Eintrag in
`src/Tests/Tests.fsproj`. Kein Test baut eine ImportId von Hand: beide Seiten des
Vergleichs entstehen durch den Produktionscode.

## Acceptance criteria

- [ ] **Die Suchschlüssel-Ableitung deckt sich mit dem normalen Push.** Ein Test in
      `src/Tests/ForceImportCandidateTests.fs` baut drei `SyncTransaction` mit
      verschiedenen `TransactionId` und bildet beide Vorbereitungen über den echten
      Produktionscode:
      `let normal = Ynab.prepareTransactions today acct Map.empty false txs` und
      `let forced = Ynab.prepareTransactions today acct Map.empty true txs`, daraus
      `buildComdirectImportCandidates false txs normal` und
      `buildComdirectImportCandidates true txs forced`. Erwartet: für jede
      `TransactionId` gilt
      `forcedCandidate.ExistingEntryImportId = Some normalCandidate.Prepared.ImportId`.
      Kein Erwartungswert wird im Test hingeschrieben; beide Seiten entstehen aus
      `prepareTransactions`. Fängt jede Drift zwischen `generateImportId` und dem
      Normal-Zweig von `prepareTransactions`, etwa ein `generateImportId`, das die
      Bindestriche der Guid stehen lässt, oder ein `prepareTransactions`, das das Präfix
      ändert.
- [ ] **Gesendete und gesuchte ImportId sind verschieden.** Derselbe Test erwartet für
      jeden Force-Kandidaten
      `forcedCandidate.Prepared.ImportId <> forcedCandidate.ExistingEntryImportId.Value`
      und dass `Prepared.ImportId` mit `"BB:"` beginnt. Fängt eine Vereinfachung, die
      beide Werte gleichsetzt und damit YNABs Dedup wieder ins Messer laufen lässt.
- [ ] **Der normale Pfad sucht nicht.** Derselbe Test erwartet
      `normalCandidates |> List.forall (fun c -> c.ExistingEntryImportId = None)`.
      Fängt eine Umsetzung, die das Flag ignoriert und den Suchschlüssel überall setzt.
- [ ] **Payee und Memo kommen aus dem vorbereiteten Request, nicht aus den Rohdaten.**
      Ein Test baut eine `SyncTransaction`, deren roher Comdirect-Payee sich vom
      aufgelösten `Request.PayeeName` unterscheidet (etwa leerer Payee, der auf
      `"Unknown"` fällt) und deren `Reference` gesetzt ist. Erwartet für den Kandidaten:
      `c.Payee = Some c.Prepared.Request.PayeeName`, `c.Memo = Some c.Prepared.Request.Memo`,
      `c.Payee <> Some rawPayee` und `c.Memo.Value` enthält `"Ref: " + reference`.
      Fängt einen Rückfall auf die rohen Bank-Felder, der den nächsten Spiegel-Lauf ein
      falsches Kopfdaten-Ereignis schreiben ließe (der Verifier-Fund aus `ledger-qvt84`,
      Iteration 3, heute ungetestet).
- [ ] **Die Rohdatenfelder bleiben roh.** Derselbe Test erwartet
      `c.ComdirectReference = tx.Transaction.Reference`,
      `c.Date = tx.Transaction.BookingDate`, `c.Amount = tx.Transaction.Amount` und
      `c.ReviewSteps = tx.ReviewSteps`.
- [ ] **Es gibt nur noch eine Stelle, die `ExistingEntryImportId` setzt.** In
      `src/Server/Api.fs` kommt der Bezeichner `ExistingEntryImportId` nur noch
      innerhalb von `buildComdirectImportCandidates` vor; beide Aufrufstellen (normaler
      Import-Push und `forceImportDuplicates`) rufen diese Funktion.
- [ ] `dotnet build` und `dotnet test` grün, `src/Tests/Tests.fsproj` führt die neue
      Datei.

## Notes

**Herkunft der Testdefinitionen.** Der Elterntask `ledger-r7dm2` behauptet, der QA-Review
liege als Volltext im Diary-Eintrag vom 2026-09-19 20:14 und nenne je Test Datei, Eingabe
und erwartetes Ergebnis. Das stimmt nicht; dort steht nur ein Nachtrag von rund 15
Zeilen. Die Kriterien oben sind aus `src/Server/Api.fs` und `src/Server/YnabClient.fs`
abgeleitet, nicht abgeschrieben.

Ein Fable.Remoting-Proxy ist unter `dotnet test` nicht aufrufbar; deshalb wird nicht
`forceImportDuplicates` selbst getestet, sondern die reine Funktion, die es ab jetzt
benutzt. Das ist dieselbe Trennung, die `ledger-fq3kd` mit `pushComdirectImports`
(injizierter Push, keine Uhr, kein HTTP) bereits gewählt hat.

`Ynab.prepareTransactions` ist rein und nimmt `today` als Parameter, der Test braucht
also keine Systemzeit. Die Payee-Map bleibt `Map.empty`, solange keine Transfer-Zeile im
Spiel ist.
