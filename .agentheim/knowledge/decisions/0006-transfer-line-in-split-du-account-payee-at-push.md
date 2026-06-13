---
id: 0006
title: Transfer-Zeile im Split — DU-Split-Ziel als Konto, Payee erst beim Push aufgelöst
scope: ynab-sync
status: accepted
date: 2026-06-13
supersedes: []
superseded_by: []
related_tasks:
  - contexts/ynab-sync/done/ynab-001-split-with-transfer-cashback.md
  - contexts/ynab-sync/todo/ynab-002-split-review-ui.md
related_research:
  - ynab-transfer-in-split-subtransaction-2026-06-13
---

# ADR 0006: Transfer-Zeile im Split — DU-Split-Ziel als Konto, Payee erst beim Push aufgelöst

## Context
Eine Comdirect-Buchung mit Barabhebung (z. B. €217, davon €200 abgehoben) muss in einen
Split zerlegbar sein, bei dem **eine Zeile ein Transfer auf ein YNAB-Konto** (Bargeld) ist
und der Rest eine Kategorie. Heute ist `TransactionSplit` (`src/Shared/Domain.fs:98`)
kategorie-only (`{ CategoryId; CategoryName; Amount; Memo }`) und kann einen Transfer nicht
ausdrücken.

Die Research (`ynab-transfer-in-split-subtransaction-2026-06-13`, gegen die offizielle
YNAB-OpenAPI-Spec verifiziert) ergab den entscheidenden Conformist-Zwang: YNABs
`SaveSubTransaction` kennt **kein** `transfer_account_id`/`transfer_payee_id` — ein Transfer
wird **ausschließlich über `payee_id`** (die auto-erzeugte "Transfer : <Konto>"-Payee) kodiert.
Die Transfer-`payee_id` ist nur zur Push-Zeit ermittelbar (`GET /payees`, Join auf
`YnabPayee.TransferAccountId`); Payees werden serverseitig **nicht** gespiegelt.

## Decision
1. **Split-Ziel als Discriminated Union.** Eine Split-Zeile trägt ein
   `SplitTarget = ToCategory of YnabCategoryId * string | ToTransfer of YnabAccountId * string`.
   `TransactionSplit` wird zu `{ Target: SplitTarget; Amount: Money; Memo: string option }`.
   Damit ist "Kategorie *und* Transfer" bzw. "weder noch" auf Typebene unmöglich (XOR im Typ);
   das alte `CategoryId`/`CategoryName`-Paar entfällt.
2. **Die Transfer-Zeile speichert das Ziel-*Konto*, nicht eine Payee-Id.** Die Auflösung
   `YnabAccountId → transfer payee_id` ist eine Push-Concern (Conformist), kein Domänen-Wert.
3. **Auflösung beim Push, einmal pro Batch.** `YnabClient.createTransactions` holt — nur wenn
   der Batch mindestens eine Transfer-Zeile enthält — `GET /payees` **einmal** und baut eine
   `Map<YnabAccountId, payeeId>` über `YnabPayee.TransferAccountId`. Kategorie-only-Batches
   holen keine Payees (schont YNABs ~200 req/h). Die Transfer-Zeile serialisiert als
   `{ amount, payee_id }`, **`category_id` wird weggelassen** (nicht als `null` gesendet —
   bestehendes Omit-when-None-Muster).
4. **Strukturelle Invarianten im geteilten Smart-Constructor.** `mkSplits` (in `src/Shared/`)
   erzwingt ≥2 Zeilen, vorzeichenrichtige Summe == Gesamtbetrag (Transfer-Zeile zählt mit),
   eine Währung; Rückgabe `Result<_, SplitError>`. Der Client kann so gar keinen ungültigen
   Split bauen; der Server prüft erneut. Das XOR-Ziel ist durch die DU bereits typseitig sicher.
5. **Fehlende Transfer-Payee → per-Transaktion-Reject, nie geratenes Payload.** Existiert für
   das Ziel-Konto keine Transfer-Payee, wird die Transaktion `RejectedByYnab (UnknownRejection …)`
   markiert und aus dem Request-Body ausgeschlossen. Ein `GET /payees`-Fehler lässt den ganzen
   Batch fehlschlagen (konsistent mit dem bestehenden Modell).

## Consequences
### Positive
- Illegale Split-Zustände (Kategorie+Transfer / keins) sind unrepräsentierbar; die
  XOR-Validierung verschwindet in den Typ.
- **Keine Daten-Migration:** Splits werden nicht persistiert (`src/Server/Persistence.fs:677`
  rekonstruiert `Splits = None`). Einzige brechende Call-Site: `YnabClient.createSubtransaction`.
- Ein Transfer auf ein Konto ohne Transfer-Payee scheitert sauber pro Transaktion beim Push —
  nie als fehlerhaftes Payload an YNAB.
- DUs queren Fable.Remoting sauber (wie bestehende Wire-DUs `DuplicateStatus`/`YnabImportStatus`);
  kein Flat-Record-Workaround für das Domänen-Payload nötig (die Flat-Record-Regel gilt nur für
  `Api.fs`-Methodensignaturen, nicht für Payloads in `SyncTransaction`).

### Negative
- Ein zusätzlicher `GET /payees`-Call pro Transfer-behaftetem Push — gemildert durch
  Per-Batch-Caching (nur einmal, nur wenn nötig).

### Neutral
- Der JSON-Vertrag wird per Test fixiert: Transfer-Subtransaction enthält `payee_id` und
  **keinen** `category_id`-Key; Betrag als JSON-Zahl in Milliunits mit korrektem Vorzeichen.

## Alternatives considered
- **Record mit optionalen Feldern** (`CategoryId option` + `TransferAccountId option`) —
  abgelehnt: braucht eine Laufzeit-XOR-Invariante und einen `None,None`-Arm an jeder Call-Site.
- **`payee_name = "Transfer : Bargeld"` statt aufgelöster `payee_id`** — abgelehnt: laut
  Research bindet Namensauflösung nicht an die Transfer-Payee und riskiert, einen gewöhnlichen
  Payee mit diesem Literal anzulegen.
- **Payees serverseitig in SQLite spiegeln** — abgelehnt: Stale-Data- und Caching-Infrastruktur,
  die die Aufgabe nicht braucht; ein Live-`GET /payees` pro Batch genügt.

## References
- Research: `../research/ynab-transfer-in-split-subtransaction-2026-06-13.md` (gegen OpenAPI-Spec verifiziert, Review PASS)
- `src/Shared/Domain.fs` — `TransactionSplit` (:98), `SyncTransaction.Splits` (:183), `YnabPayee.TransferAccountId` (:269), `RejectedByYnab`/`UnknownRejection` (:163/:169)
- `src/Server/YnabClient.fs` — `getPayees` (:232), Encoder (:357), `YnabSubtransactionRequest` (:337), `createSubtransaction` (:385), `createTransactions` (:398)
- `src/Server/Persistence.fs` (:677 — Splits nicht persistiert, daher keine Migration)
- ADR 0004 (Konto-Auswahl-Muster), ADR 0005 (Sheet-/Picker-Muster für die UI in ynab-002)
