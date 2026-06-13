# ynab-sync

## Purpose
Pusht kategorisierte Transaktionen nach YNAB, respektiert dabei YNABs eigenes
Import-ID-Dedup, und stellt YNAB-Strukturen (Budgets/Accounts/Categories/Payees) als
Spiegel zum Auswählen bereit.

## Classification
**core** — ohne sauberen, dedup-sicheren Push wäre das Tool wertlos. Zugleich
**Conformist** gegenüber der YNAB-API: BB ordnet sich YNABs Datenmodell unter.

## Actors
- **Roman** — stößt den Push an, wählt Ziel-Budget/Account.
- **YNAB** — externes Upstream/Downstream-System (über Personal Access Token).

## Ubiquitous language
- **ImportId** — BBs Dedup-Schlüssel, aus der TransactionId erzeugt (fester Prefix); YNAB
  nutzt ihn, um wiederholte Importe derselben Buchung zu erkennen. Die **Brücke** zwischen
  BBs Vor-Import-Dedup (`banking-import`) und YNABs eigenem Dedup.
- **YnabImportStatus** — Ergebnis des Push-Versuchs: `NotAttempted | YnabImported |
  RejectedByYnab`. Streng zu trennen vom `DuplicateStatus` (Vor-Import) im banking-import.
- **YnabRejectionReason** — warum YNAB ablehnte: `DuplicateImportId | UnknownRejection`.
- **YnabTransaction** — die an YNAB gesendete/gespiegelte Buchung (Amount als Integer-
  Milliunits, Date, Payee, Memo, CategoryId, ImportId).
- **Splits / TransactionSplit** — Aufteilung einer Buchung in mehrere Zeilen
  (`{ Target; Amount; Memo }`); Splits müssen ≥2 sein, eine Währung teilen und in
  vorzeichenrichtiger Summe dem Transaktionsbetrag entsprechen. Erzwungen durch den
  geteilten Smart-Constructor **`mkSplits`** (`src/Shared/Domain.fs`), der
  `Result<_, SplitError>` (`TooFewLines | SumMismatch | CurrencyMismatch`) liefert —
  Client und Server prüfen damit gleich (ADR 0006).
- **SplitTarget** — das Ziel einer Split-Zeile als DU: `ToCategory (Kategorie) |
  ToTransfer (Ziel-Konto)`. XOR auf Typebene — eine Zeile ist *entweder* Kategorie
  *oder* Transfer, nie beides/keins. Die Transfer-Zeile speichert das **Ziel-Konto**,
  nicht eine Payee-Id (ADR 0006).
- **Transfer-Zeile** — eine `ToTransfer`-Split-Zeile: eine Konto-Verschiebung (z.B.
  Barabhebung) ohne Budget-Wirkung. Beim Push als Subtransaction mit `payee_id`
  serialisiert, **ohne** `category_id`-Key (YNAB-Conformist).
- **Cashback-Split** — der häufigste Fall (~80%): eine Buchung in 1 Kategorie-Zeile
  (echter Einkauf) + 1 Transfer-Zeile (Barabhebung, nimmt den Rest auf). Gebaut über
  **`buildCashbackSplit`** (`splitRemainder` berechnet den Rest), validiert durch `mkSplits`.
- **Payee / PayeeOverride** — der an YNAB gemeldete Zahlungsempfänger; optional überschrieben.
- **Transfer-Payee** — YNABs auto-erzeugte "Transfer : <Konto>"-Payee. Ein Transfer wird
  bei YNAB **ausschließlich** über deren `payee_id` kodiert (kein `transfer_account_id`).
  BB spiegelt Payees nicht; die `YnabAccountId → payee_id`-Auflösung passiert **beim Push**
  (einmal pro Batch via `GET /payees`, Join auf `TransferAccountId`). Fehlt die
  Transfer-Payee fürs Ziel-Konto, wird die Transaktion `RejectedByYnab (UnknownRejection)`
  und aus dem Request-Body ausgeschlossen (ADR 0006). *Für Split-Transfer-Zeilen
  implementiert (Push-Fundament, ynab-001); Review-UI offen (ynab-002).*
- **YnabBudget / YnabAccount / YnabCategory / YnabPayee** — gespiegelte YNAB-Strukturen
  zur Auswahl.
- **ManualTransaction (Quick Add)** — manuell erfasste Buchung (z.B. Bar-Ausgabe), direkt
  und synchron nach YNAB gepusht. Bewusst **ohne ImportId** (kollidiert nie mit dem
  Import-Dedup) und auf das eigene **Quick-Add-Konto** statt des Bank-Import-Kontos
  (ADR 0004). Payee optional. Scope-Herkunft: ADR 0003 (Phase-0-Experiment).
- **Quick-Add-Konto** — konfigurierbares Ziel-Konto für manuelle Einträge
  (`ynab_quickadd_account_id`), strikt getrennt vom Standard-/Import-Konto.

## Aggregates
- **YnabTransaction** — Invariante: gültige ImportId; bei Splits Summe == Amount und ≥2 Teile.
- **YnabImportBatch** (implizit) — ein Push-Vorgang; trackt pro Transaktion den YnabImportStatus.

## Key events
- *TransactionImportedToYnab* — Push erfolgreich.
- *YnabImportRejected* — YNAB lehnte ab (mit YnabRejectionReason).

## Key commands
- *PushToYnab* (Batch kategorisierter SyncTransactions).
- *RefreshYnabStructures* (Budgets/Accounts/Categories/Payees laden).
- *AddManualTransaction* (Quick Add: einzelne manuelle Buchung aufs Quick-Add-Konto).

## Relationships with other contexts
- **Downstream of:** `categorization` — erhält kategorisierte SyncTransactions.
- **Conformist zu:** YNAB-API (Datenmodell, Dedup, Cleared-/Transfer-Semantik).
- **Shared kernel:** ImportId teilt sich der Context konzeptionell mit `banking-import`s
  Duplikat-Erkennung.
- Voller Kontext-Überblick: `../../context-map.md`.

## Open questions
- **Transfer-Payee Modell** — *gelöst (ADR 0006, ynab-001):* Transfer-Zeile speichert das
  Ziel-Konto, `payee_id` erst beim Push aufgelöst. Offen bleibt nur die **UI** (ynab-002):
  wie elegant "Transfer to/from" neben der Kategorie im Split-Editor anbieten?
- **Split UI**: Push-Fundament steht (Domain + Transfer-Push, ynab-001); die Review-/Editor-UI
  fehlt noch (ynab-002 — Cashback-Split komfortabel erfassen).
- Rate-Limit-Handling bei größeren Batches (YNAB ~200 req/h)?
