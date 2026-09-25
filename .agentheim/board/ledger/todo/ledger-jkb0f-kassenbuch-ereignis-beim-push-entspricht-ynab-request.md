---
id: ledger-jkb0f
title: "Kassenbuch-Ereignis beim Comdirect-Push entspricht nicht immer dem tatsächlichen YNAB-Request"
status: todo
type: bug
context: ledger
created: 2026-09-25
completed:
depends_on: []
blocks: []
tags: [ledger, push, spiegel, kassenbuch]
related_adrs: [0013, 0021]
related_research: []
prior_art: [ledger-qvt84, ledger-p3wnq]
---

## Why

Vier Randfälle beim Comdirect-Import-Push (`Ledger.Domain.recordComdirectPush`) erzeugen
ein Kassenbuch-Ereignis, das nicht zu dem passt, was YNAB im selben Push tatsächlich
bekommt: ein entfernter Payee-Override, ein ungekürztes Split-Memo, ein fehlender oder ein
veralteter Kategorie-Name. Jeder dieser Fälle löst beim nächsten Spiegel-Lauf ein falsches
"in YNAB geändert"-Ereignis aus, das append-only für immer im Log steht (ADR 0013) und eine
BB-geführte Entscheidung fälschlich als YNAB-Änderung ausweist (ADR 0021). Fund aus der
Verfeinerung von `ledger-tpbe7`.

## What

1. **Payee-Override-Entfernung** (`src/Server/Ledger/Domain.fs`, `recordComdirectPush`,
   `toEvent`, Zeile 404 bis 406): `newPayee = step.PayeeOverride |> Option.orElse
   prevPayee` ersetzen durch einen Fallback auf den Payee ohne Override. Dafür
   `ComdirectPushInput` (Zeile 333 bis 346) um ein Feld `RawPayee: string option`
   erweitern (der rohe, von der Bank gelieferte Payee, unabhängig von jedem Override),
   gefüllt vom Aufrufer aus der SyncTransaction beziehungsweise BankTransaction, nicht aus
   `PreparedTransaction.Request.PayeeName` (dort steckt der aktuelle Override schon drin).
   `toEvent`s Fallback wird `step.PayeeOverride |> Option.orElse input.RawPayee`.
2. **Split-Memo-Kürzung** (`src/Server/YnabClient.fs:346-351` und
   `src/Server/Ledger/Domain.fs`): `truncateSplitMemo` und `memoLimit` aus `YnabClient.fs`
   in ein gemeinsames, reines Modul ohne HTTP-Abhängigkeit ziehen (zum Beispiel neben
   `Shared.Domain.TransactionSplit` oder ein neues `src/Server/MemoFormatting.fs`), das
   sowohl `YnabClient.resolveSubtransaction` (Zeile 528 bis 544) als auch
   `recordComdirectPush`s `toEvent` aufrufen. `toEvent` wendet die Kürzung auf jede
   `split.Memo` an, bevor sie ins `NewSplits`-Payload geschrieben wird. Nicht duplizieren,
   sonst laufen die beiden Kopien wieder auseinander, genau die Fehlerklasse dieses Tasks.
3. **Kategorie-Name-Auflösung, Punkte 1 und 4 zusammen** (`src/Server/Ledger/Domain.fs`,
   `src/Server/Ledger/CommandHandlers.fs`, `src/Server/Ledger/Persistence.fs`):
   `recordComdirectPush` bekommt einen injizierten Resolver
   `resolveCategoryName: YnabCategoryId -> string option` (Muster wie der injizierte
   `push`-Parameter in `pushComdirectImports`). `categoryRefOf` (Zeile 387 bis 390) fragt
   für jede Kategorie-Id aus der Review-Spur zuerst den Resolver und fällt nur auf
   `step.CategoryName` zurück, wenn der Resolver `None` liefert. Neue Persistence-Funktion
   `loadCategoryNames () : Async<Map<Guid,string>>` in `Ledger/Persistence.fs`, analog zu
   `loadCategoryGroups` (Zeile 1055), dieselbe Tabelle `ledger_categories`, Spalte `name`
   statt `group_name`. `pushComdirectImports` (oder eine dünne Fassade davor) lädt die Map
   vor dem Aufruf und reicht sie als Resolver durch.
4. **Doc-Kommentar** (`src/Server/Ledger/Domain.fs:340-343`): korrigieren, dass `Payee` und
   `Memo` aus dem tatsächlich gesendeten Request kommen, nicht roh von der Bank. Das neue
   `RawPayee`-Feld aus Schritt 1 bekommt den "roh"-Kommentar, den `Payee` bisher fälschlich
   trug.

## Acceptance criteria

- [ ] Regressionstest in `src/Tests/LedgerTests.fs`, rot vor dem Fix: eine Review-Spur
      "Regel setzt Payee-Override X, Roman entfernt ihn (Schritt ohne Override)" ergibt
      beim Push ein `NewPayee`, das mit dem tatsächlich gesendeten `Request.PayeeName`
      übereinstimmt, nicht mit dem alten Override X.
- [ ] Regressionstest, rot vor dem Fix: eine Split-Zeile mit einem Memo über 300 Zeichen
      ergibt im Kassenbuch-Ereignis denselben gekürzten Text, den `YnabClient` tatsächlich
      an YNAB sendet.
- [ ] Regressionstest, rot vor dem Fix: ein Review-Schritt mit
      `Category = Some catId, CategoryName = None` (simulierter `getCategories`-Fehler)
      erzeugt trotzdem eine vollständige `CategoryRef` im Kassenbuch-Ereignis, wenn der
      Resolver die Id kennt.
- [ ] Regressionstest, rot vor dem Fix: ein Review-Schritt mit einem veralteten,
      gecachten `CategoryName` (Regel-Cache) erzeugt eine `CategoryRef` mit dem
      frischeren, vom Resolver gelieferten Namen.
- [ ] `src/Server/Ledger/Events.fs` und `CategoryRef` bleiben unverändert (ADR 0013
      Punkt 7, kein Schema-Eingriff); Erweiterungen bleiben auf `ComdirectPushInput`
      beschränkt. Per Diff belegt.
- [ ] `src/Server/Ledger/Domain.fs:340-343` beschreibt korrekt, woher `Payee`, `Memo` und
      `RawPayee` kommen.
- [ ] Replay-Test bleibt grün.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes

Nicht mitlösen: der Fund zu `observe`s Kategorie-Vergleich
(`src/Server/Ledger/Domain.fs:514`, volle Record-Gleichheit inklusive `CategoryName`).
Das ist breiter als dieser Task (betrifft jede Kategorie-Umbenennung in YNAB, nicht nur den
Push-Zeitpunkt) und gehört als eigenes Backlog-Item erfasst, sonst wächst dieser Task über
die vier geplanten Fixes hinaus.

Reihenfolge beim Implementieren: erst die beiden lokalen Fixes (Payee, Memo), dann den
Resolver für Kategorie-Namen, weil Letzterer eine Signaturänderung an `recordComdirectPush`
und eine neue Persistence-Funktion braucht.

Ein Fable.Remoting-API-Handler ist unter `dotnet test` nicht direkt aufrufbar (siehe
`ledger-w6tbq`); alle vier Fixes sitzen bewusst in reinen, injizierten Funktionen
(`recordComdirectPush`, `toEvent`, `categoryRefOf`), nicht im `IEntityApi`-Record.

`loadCategoryGroups` (`ledger-p3wnq`) liefert Id zu Gruppenname, nicht Id zu eigenem Namen;
die neue `loadCategoryNames`-Funktion liest dieselbe Tabelle, nur die Spalte `name` statt
`group_name`.

Der Doc-Kommentar-Fix (Punkt 6 der Erfassung, `Ledger/Domain.fs:340-343`) liegt allein
hier; `ledger-na3rq` fasst ihn nicht an.
