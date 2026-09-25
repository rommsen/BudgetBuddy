---
id: ledger-jkb0f
title: "Kassenbuch-Ereignis beim Comdirect-Push entspricht nicht immer dem tatsächlichen YNAB-Request"
status: done
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

## Verifier note (iteration 1)

REASONS:
- Criterion 1 is not met in general. The requirement is that `NewPayee` equals the `Request.PayeeName` that is actually sent. The new fallback in `src/Server/Ledger/Domain.fs:441` is `step.PayeeOverride |> Option.orElse input.RawPayee`, and `RawPayee` is filled in `src/Server/Api.fs:765` with the raw `tx.Transaction.Payee`. `YnabClient.buildTransactionRequest` (`src/Server/YnabClient.fs:581-584`) sends `tx.PayeeOverride |> Option.orElse tx.Transaction.Payee |> Option.defaultValue "Unknown"`. So when the bank payee is `None`, YNAB gets `"Unknown"`, but every ByUser or OrderIdMatch step without an override now writes `NewPayee = None`. `apply` turns that into `Payee = None` (Domain.fs:271). The next mirror run then sees `"Unknown"` against `None` and logs a false "changed in YNAB" event, which is the same error class this task is meant to fix.
- This is a new regression, not just an old gap. Before the change the fallback was `prevPayee`, which started from `input.Payee = Some p.Request.PayeeName` (`Some "Unknown"`), so the no-bank-payee case was correct. It is a real path: `ComdirectClient.fs:135-138` still yields `Payee = None` whenever `extractCardMerchant` finds no merchant in the memo.
- The regression test for criterion 1 (`LedgerTests.fs`, "Regel setzt Payee-Override, Roman entfernt ihn ...") compares against a hard-coded `Some "dm"`. It never derives the expected value from what `buildTransactionRequest` actually sends, so it cannot catch the `"Unknown"` mismatch. No test covers a `None` bank payee.

SUGGESTED_FIX: Make the no-override fallback match exactly what the request sends, including the `"Unknown"` default. Ideally extract one shared pure payee-fallback function used by both `buildTransactionRequest` and `buildComdirectImportCandidates`/`toEvent`, the same way `MemoFormatting` is shared. Add a regression test with a `None` bank payee and a ByUser step without an override, asserting `NewPayee` equals the sent `PayeeName` (`Some "Unknown"`).

ITERATION_HINT: likely-fixable

## Outcome

Die vier in der Verfeinerung von `ledger-tpbe7` gefundenen Randfälle beim Comdirect-Import-Push
(`Ledger.Domain.recordComdirectPush`) sind über zwei Iterationen behoben, jeweils mit einem
Regressionstest, der vor dem jeweiligen Fix rot war (per Sabotage-Check gegen den fertigen Fix
erneut bestätigt).

**Iteration 1.**

1. Payee-Override-Entfernung: `ComdirectPushInput` (und `Ledger.CommandHandlers.ComdirectImportCandidate`)
   bekamen ein neues Feld `RawPayee: string option`, der rohe Bank-Payee (`BankTransaction.Payee`),
   unabhängig von jedem Override, befüllt in `Server.Api.buildComdirectImportCandidates` aus
   `tx.Transaction.Payee` (nicht aus `PreparedTransaction.Request.PayeeName`, dort steckt ein
   aktiver Override schon drin).
2. Split-Memo-Kürzung: `memoLimit`, `compressWhitespace` und `truncateSplitMemo` wurden aus
   `Server/YnabClient.fs` in ein reines Modul `Server/MemoFormatting.fs` gezogen (in `Server.fsproj`
   vor `Ledger/Domain.fs` einsortiert, beide Nutzer hängen davon ab). `toEvent` wendet dieselbe
   Kürzung jetzt auf jede `split.Memo` an, bevor sie ins `NewSplits`-Payload geschrieben wird.
3. und 4. Kategorie-Name-Auflösung: `recordComdirectPush` bekam einen injizierten Parameter
   `resolveCategoryName: YnabCategoryId -> string option`. `categoryRefOf` fragt zuerst diesen
   Resolver, fällt nur auf `step.CategoryName` zurück, wenn der Resolver `None` liefert. Neue
   Persistence-Funktion `Ledger.Persistence.loadCategoryNames()` liest dieselbe Tabelle
   `ledger_categories` wie `loadCategoryGroups` (`ledger-p3wnq`), nur die Spalte `name` statt
   `group_name`, leere Tabelle liefert eine leere Map, nie einen Fehler. `pushComdirectImports`
   lädt die Map einmal vorab und reicht den daraus gebauten Resolver an `recordComdirectPush` durch.

Die Iteration-1-Tests (`src/Tests/LedgerTests.fs`, vier neue Fälle in
`Ledger Domain (recordComdirectPush)` plus zwei Fälle für `loadCategoryNames`) sind unverändert
gültig geblieben.

**Iteration 2 (Verifier-Fund zu Kriterium 1).** Der ursprüngliche Fallback in Iteration 1,
`step.PayeeOverride |> Option.orElse input.RawPayee`, ergab bei fehlendem Bank-Payee
`NewPayee = None`. `YnabClient.buildTransactionRequest` sendet in genau diesem Fall aber den
festen Text "Unknown" als `PayeeName` (`tx.PayeeOverride |> Option.orElse tx.Transaction.Payee
|> Option.defaultValue "Unknown"`). Ein Review-Schritt ohne Override und ohne bekannten
Bank-Payee schrieb also ein Ereignis, das dem tatsächlichen Request widersprach; der nächste
Spiegel-Lauf hätte daraus fälschlich eine "in YNAB geändert"-Beobachtung gemacht, exakt die
Fehlerklasse, die dieser Task beheben soll.

Fix: neues, reines Modul `Server/PayeeFormatting.fs` (in `Server.fsproj` direkt nach
`MemoFormatting.fs` einsortiert, vor `Ledger/Domain.fs`) mit einer Funktion
`resolvePayeeName (payeeOverride: string option) (bankPayee: string option) : string`, die
genau die Logik aus `buildTransactionRequest` trägt. `YnabClient.buildTransactionRequest` ruft
jetzt diese Funktion auf, statt die Fallback-Kette selbst zu berechnen (kein Doppel mehr).
`Ledger.Domain.recordComdirectPush`s `toEvent` setzt `NewPayee` jetzt über
`Some (resolvePayeeName step.PayeeOverride input.RawPayee)`, also über dieselbe Funktion, statt
über eine eigene, unabhängige Fallback-Kette. Die Doc-Kommentare um `ComdirectPushInput` und
`recordComdirectPush` wurden entsprechend präzisiert.

Neuer Regressionstest in `src/Tests/LedgerTests.fs`
("kein Bank-Payee und kein Override: NewPayee entspricht genau dem PayeeName, den YnabClient
tatsächlich sendet (Unknown), nicht None"): ein `ByUser`-Schritt ohne `PayeeOverride` bei
`RawPayee = None` erwartet `NewPayee = Some (Server.PayeeFormatting.resolvePayeeName step.PayeeOverride None)`,
leitet den erwarteten Wert also aus derselben Funktion ab, die auch die Produktionslogik nutzt,
statt eines hart kodierten Literals. Mit der alten Fallback-Kette (`Option.orElse` ohne
"Unknown"-Default) ist dieser Test rot beobachtet worden (per Sabotage-Check), mit dem Fix wieder
grün.

**Unverändert (AC, per Diff belegt):** `src/Server/Ledger/Events.fs` und `CategoryRef` (kein
Schema-Eingriff, ADR 0013 Punkt 7); alle Erweiterungen bleiben auf `ComdirectPushInput`,
`ComdirectImportCandidate` und die zwei neuen reinen Module beschränkt.

**Build/Test (Endstand über beide Iterationen):** `dotnet build` (ganze Solution) 0 Warnungen,
0 Fehler. `dotnet test src/Tests/Tests.fsproj`: 979 erfolgreich, 7 übersprungen (bestehende
`.env`-Integrationstests, unverändert), 0 Fehler, Replay-Test bleibt grün.

**Nicht mitgelöst** (laut Task-Notes bewusst ausgeklammert): der Fund zu `observe`s
Kategorie-Vergleich (`src/Server/Ledger/Domain.fs`, volle Record-Gleichheit inklusive
`CategoryName`) ist bereits als `ledger-ggzdw` erfasst, hier nicht erneut angelegt.
