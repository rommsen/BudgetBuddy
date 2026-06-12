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
- **Splits / TransactionSplit** — Multi-Kategorie-Aufteilung einer Buchung; Splits müssen
  ≥2 sein und in Summe dem Transaktionsbetrag entsprechen.
- **Payee / PayeeOverride** — der an YNAB gemeldete Zahlungsempfänger; optional überschrieben.
- **Transfer-Payee** — "Transfer to/from <Account>" statt Kategorie (z.B. Barabhebung als
  Konto-Verschiebung ohne Budget-Wirkung). *Geplant, noch nicht implementiert.*
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
- **Transfer-Payee** UI/Modell: wie elegant "Transfer to/from" neben der Kategorie anbieten?
- **Split UI**: Backend existiert, UI fehlt — bauen oder Feature streichen (Splits gehen
  auch direkt in YNAB)?
- Rate-Limit-Handling bei größeren Batches (YNAB ~200 req/h)?
