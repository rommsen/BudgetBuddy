---
id: ynab-001
title: Split mit Transfer-Zeile — Domain + YNAB-Push (Fundament)
status: done                 # backlog | todo | doing | done
type: feature                # feature | bug | refactor | chore | spike | decision
context: ynab-sync
created: 2026-06-13
completed: 2026-06-13
commit: 688d81c
depends_on: []
blocks: [ynab-002]
tags: [split, transfer, ynab-push, domain, conformist]
related_adrs: [0004, 0005, 0006]
related_research: [ynab-transfer-in-split-subtransaction-2026-06-13]
prior_art: []
---

## Why
Beim Einkaufen mit Barabhebung kommt eine einzige Comdirect-Buchung an (z. B. €217), die
BB als *eine* Kategorie taggt. €200 davon waren eine Barabhebung (Konto-Verschiebung aufs
Bargeld-Konto, keine Budget-Wirkung), nur €17 der eigentliche Einkauf. Damit BB den Split
nach YNAB pushen kann, muss zuerst das **Fundament** stehen: das Domänenmodell muss eine
Split-Zeile als *Transfer* ausdrücken können, und der Push muss sie YNAB-konform senden.
Dieses Task ist das Backend-Fundament; die UI ist `ynab-002` (hängt hieran).

## What
`TransactionSplit` so erweitern, dass eine Zeile *entweder* eine Kategorie *oder* ein
Transfer auf ein bestehendes YNAB-Konto ist, und den YNAB-Push entsprechend serialisieren.
Die Domain-Form und der Push-Mechanismus sind in **ADR 0006** entschieden (gestützt auf die
Research, die gegen die YNAB-OpenAPI-Spec verifiziert wurde): DU-Split-Ziel, das die Zeile
als Ziel-*Konto* speichert; die Transfer-`payee_id` wird erst beim Push aus `GET /payees`
(Join auf `TransferAccountId`) aufgelöst.

```fsharp
type SplitTarget =
    | ToCategory of categoryId: YnabCategoryId * categoryName: string
    | ToTransfer of accountId: YnabAccountId * accountName: string

type TransactionSplit = { Target: SplitTarget; Amount: Money; Memo: string option }
```

## Acceptance criteria
- [x] `TransactionSplit.Target` ist die `SplitTarget`-DU (`ToCategory | ToTransfer`); das alte
      `CategoryId`/`CategoryName`-Paar ist entfernt, `Amount`/`Memo` bleiben.
- [x] `mkSplits` (Smart-Constructor in `src/Shared/`) liefert `Error (TooFewLines _)` bei <2
      Zeilen, `Error (SumMismatch …)` wenn die vorzeichenrichtige Summe ≠ Gesamtbetrag (Transfer-
      Zeile zählt mit), `Error CurrencyMismatch` bei Währungsmix, sonst `Ok` — je ein Unit-Test.
- [x] `splitRemainder total enteredLines` = Gesamt − Σ erfasste Zeilen, inkl. negativem Rest
      (Über-Allokation) — Unit-Test.
- [x] `buildCashbackSplit` erzeugt einen 2-Zeilen-Split (Kategorie + Transfer, der den Rest
      aufnimmt), der `mkSplits` besteht — Unit-Test.
- [x] Push: eine `ToTransfer`-Zeile serialisiert mit `payee_id` (aufgelöst via `GET /payees`,
      Join auf `TransferAccountId`) und **ohne** `category_id`-Key; Betrag als JSON-Zahl in
      Milliunits mit korrektem Vorzeichen — Regressionstest am Encoder prüft Key-Präsenz/-Absenz
      und numerischen Betrag.
- [x] Push: eine `ToCategory`-Zeile serialisiert weiterhin mit `category_id`, ohne `payee_id`
      (keine Regression) — Test.
- [x] Push: existiert für das Ziel-Konto keine Transfer-Payee, wird die Transaktion
      `RejectedByYnab (UnknownRejection …)` markiert und aus dem Request-Body ausgeschlossen —
      Test gegen eine *pure* Auflösungs-Funktion, die eine Payee-`Map` entgegennimmt (ruft nicht
      `getPayees`).
- [x] Der Payee-Fetch passiert höchstens einmal pro Push-Batch und wird für Kategorie-only-
      Batches übersprungen.
- [x] `dotnet build` + `dotnet test` grün; Diary aktualisiert; BC-README-Ubiquitous-Language
      ergänzt (SplitTarget / Transfer-Zeile / Cashback-Split; "Transfer-Payee *geplant*" demoten).

## Notes

**Entscheidungen (ADR 0006 — gelesen, bevor du anfängst):** DU statt optionalem Record;
Transfer-Zeile speichert Ziel-*Konto*, Payee-Auflösung erst beim Push; strukturelle Invarianten
im geteilten `mkSplits`; fehlende Transfer-Payee → per-Transaktion-Reject statt geratenem Payload.

**Backend-Push-Plan (architect):**
- Payees sind **nicht** serverseitig gespiegelt (`getPayees`, `YnabClient.fs:232` holt live). Vor
  dem Map in `createTransactions`: nur wenn der Batch eine Transfer-Zeile hat,
  `let! payees = getPayees token (YnabBudgetId budgetId)` (Error-Pfad behandeln), dann
  `transferPayeeByAccount = payees |> List.choose (fun p -> p.TransferAccountId |> Option.map (fun acc -> acc, p.Id)) |> Map.ofList`. Join auf `TransferAccountId`, **nie** auf `Name`.
- Serialisierung (`encodeSubtransaction` :357, `createSubtransaction` :385): Kategorie-Zeile →
  `category_id` (kein `payee_id`); Transfer-Zeile → `payee_id` (aufgelöst), `category_id`
  **weglassen** (nicht `null`). Betragskonvertierung `int (Amount * 1000m)` und `truncateSplitMemo`
  gelten unverändert; `Encode.int` für den Betrag beibehalten (bestehender String-Bug-Fix).
- Validierung: strukturell im geteilten `mkSplits`; Auflösungs-Fehler-Guard bleibt serverseitig.
- Tests: Proxy ist nicht .NET-testbar → den Encoder/Builder direkt testbar machen (`internal` +
  `InternalsVisibleTo` oder Nicht-private Helper) und auf `Encode.toString`-Output asserten.

**Offene Fragen aus dem Refinement (durch Empfehlung in den AC aufgelöst, hier dokumentiert):**
- Auflösungs-Fehler-Granularität: per-Transaktion `RejectedByYnab` (empfohlen, in AC) statt
  Whole-Batch-Fail.
- Multi-Transfer-Split (>1 Transfer-Zeile): **nicht** einschränken (YNAB erlaubt es; Conformist).
- `YnabSubtransactionRequest`-Form: DU spiegeln (Symmetrie) — AC ist auf die DU geschrieben.

**Load-bearing Code:** `src/Shared/Domain.fs` (TransactionSplit :98, Splits :183,
YnabPayee.TransferAccountId :269, RejectedByYnab/UnknownRejection :163/:169);
`src/Server/YnabClient.fs` (getPayees :232, Encoder :357, YnabSubtransactionRequest :337,
createSubtransaction :385, createTransactions :398); `src/Server/Persistence.fs:677`
(Splits nicht persistiert → keine Migration).

**Reuse / prior art:** Konto-Auswahl-Muster aus Quick Add (ADR 0004); Sheet-/Picker-Muster
(ADR 0005) gehören zur UI in `ynab-002`.

## Outcome

Das Backend-Fundament für Cashback-Splits steht (Domain + YNAB-Push), gebaut strikt nach
ADR 0006. UI bleibt `ynab-002`.

**Domain (`src/Shared/Domain.fs`):**
- `TransactionSplit = { Target: SplitTarget; Amount: Money; Memo: string option }` mit
  `SplitTarget = ToCategory of YnabCategoryId * string | ToTransfer of YnabAccountId * string`
  — XOR auf Typebene, alter `CategoryId`/`CategoryName`-Record entfernt.
- `mkSplits : Money -> TransactionSplit list -> Result<TransactionSplit list, SplitError>`
  (`SplitError = TooFewLines | SumMismatch | CurrencyMismatch`); `splitRemainder`;
  `buildCashbackSplit`.

**Push (`src/Server/YnabClient.fs`):**
- `YnabSubtransactionRequest` als DU `CategorySub | TransferSub`. `encodeSubtransaction`
  (jetzt non-private): Transfer-Zeile → `payee_id`, KEIN `category_id`-Key; Kategorie-Zeile
  → `category_id`, kein `payee_id`; Betrag `Encode.int` (JSON-Zahl).
- Reine Auflösung: `transferPayeeByAccount` (Join auf `TransferAccountId`),
  `resolveSubtransaction` / `resolveSplits` / `buildTransactionRequest` (nehmen eine Payee-Map,
  rufen `getPayees` NICHT → .NET-testbar ohne Proxy). Fehlt die Transfer-Payee →
  `Error accountId` → Transaktion aus dem Body ausgeschlossen.
- `createTransactions` holt `GET /payees` höchstens einmal pro Batch und nur bei
  `batchHasTransferLine`; `TransactionCreateResult.RejectedTransferTransactionIds` neu.

**Wiring (`src/Server/Api.fs`):** ausgeschlossene Transfer-Transaktionen werden
`RejectedByYnab (UnknownRejection …)`; `splitTransaction` re-validiert via `mkSplits`.
**Client (`src/Client/Components/SyncFlow/State.fs`):** `AddSplit` baut `ToCategory`-Zeile.

**Tests:** neu `src/Tests/SplitPushTests.fs` (Encoder-Contract, Payee-Map-Join, pure Auflösung
resolved/rejected, per-Tx-Reject + Batch-Fetch-Gate); `SplitTransactionTests.fs` auf DU
umgestellt + `mkSplits`/`splitRemainder`/`buildCashbackSplit`-Tests; `YnabClientTests.fs`
Subtransaction-Regressionstest auf echten Encoder + DU. **541 passed / 6 skipped / 0 failed**
(32 davon Split-bezogen). Build grün.

**Entscheidungen:** kein neuer ADR — ADR 0006 deckte den Entwurf vollständig. Implementierungs-
Detail festgehalten im Diary: `[<Literal>]` auf einer `decimal`-Konstante bricht den
statischen Modul-Init (102 rote Tests beim ersten Lauf) → plain `let private`.

**Keine Daten-Migration** (Splits werden nicht persistiert, `Persistence.fs:677` rekonstruiert
`Splits = None`).
