# banking-import

## Purpose
Holt Transaktionen aus Bankquellen (Comdirect via OAuth + Push-TAN; perspektivisch ING)
und beurteilt **vor** dem Push, ob eine Transaktion ein Duplikat ist. Übersetzt rohe
Bank-JSON in das saubere interne Modell.

## Classification
**core** — der zuverlässige Comdirect-Import (den YNAB selbst nicht gut hinbekommt) ist
ein Hauptgrund, warum BudgetBuddy existiert.

## Actors
- **Roman** — löst einen Sync aus, bestätigt die Push-TAN auf dem Handy.
- **Comdirect** — externe Datenquelle (Anticorruption Layer übersetzt ihre JSON).

## Ubiquitous language
- **BankTransaction** — eine roh geholte Buchung: Id, BookingDate, Amount, Payee (optional),
  Memo, **Reference** (Comdirect-Dedup-Schlüssel), RawData (Original-JSON).
- **Reference** — der von Comdirect mitgelieferte Schlüssel, der eine Buchung über
  wiederholte Abrufe hinweg identifiziert.
- **DuplicateStatus** — BBs *Vor-Import*-Urteil: `NotDuplicate | PossibleDuplicate |
  ConfirmedDuplicate`, jeweils mit **DuplicateDetectionDetails**. Streng zu trennen vom
  `YnabImportStatus`-Dedup im `ynab-sync`-Context.
- **DuplicateDetectionDetails** — Belege fürs Urteil: FuzzyMatch auf Amount/Date/Payee,
  ob die ImportId oder Reference bereits in YNAB gefunden wurde.
- **Push-TAN** — Comdirects Zwei-Faktor-Bestätigung; während eines Sync wird auf sie
  gewartet (`AwaitingTan`).
- **ComdirectAccount** — ein Bankkonto bei Comdirect (Iban, Balance, AccountType).

## Aggregates
- **BankTransaction** — unveränderliche Repräsentation einer geholten Buchung; Reference
  ist innerhalb einer Quelle eindeutig.
- **ComdirectAuthSession** — hält den OAuth/Push-TAN-Zustand über einen Abruf hinweg.

## Key events
- *TransactionsFetched* — neue BankTransactions liegen vor.
- *DuplicateDetected* — eine Transaktion wurde als (möglicher/bestätigter) Duplikat erkannt.

## Key commands
- *FetchTransactions* (für einen Zeitraum / DaysToFetch).
- *AnalyzeDuplicates* gegen bereits bekannte/importierte Buchungen.

## Relationships with other contexts
- **Upstream of:** `categorization` — gibt geholte, dedup-beurteilte BankTransactions weiter.
- **Anticorruption layer zu:** Comdirect (rohe JSON → BankTransaction).
- Voller Kontext-Überblick: `../../context-map.md`.

## Open questions
- **ING** als zweite Quelle: eigener Adapter oder Generalisierung des Comdirect-Musters?
- Lohnt ein generischer "Bank-Source"-Port, sobald >1 Quelle existiert?
