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
- **Refresh-Urteil** (`RefreshFailure`): Ergebnis eines gescheiterten Token-Refresh. `ReloginRequired` (400, 401, jedes andere 4xx außer 429) verlangt eine neue TAN, `RetryLater` (Status 0, 429, 5xx, nicht lesbare Antwort) lässt die Verbindung stehen.
- **Beobachtungssonde**: Testfall hinter `RUN_INTEGRATION_TESTS` und `COMDIRECT_PROBE` (1, 2 oder 3, nur als Prozess-Variable), der die Trageweite der Session mit einer echten TAN misst. Nur dort darf ein Testlauf eine TAN auslösen.

## Aggregates
- **BankTransaction** — unveränderliche Repräsentation einer geholten Buchung; Reference
  ist innerhalb einer Quelle eindeutig.
- **ComdirectAuthSession** — hält den OAuth/Push-TAN-Zustand über einen Abruf hinweg.
- **Invariante Fehlertexte:** Comdirect-Fehler tragen Status und `error`-Code, nie den Antwortrumpf. Das gilt auch für Decoder-Meldungen, weil Thoth darin den gelesenen Wert zitiert.

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

## Szenarien und Tests

- **Bug-Tasks:** mindestens ein „kaputt“- und ein „intakt“-Szenario als Akzeptanzkriterium (GIVEN/WHEN/THEN). Der Regressionstest zum kaputt-Szenario ist vor dem Fix rot.
- **Fachliches Verhalten** (Zustandswechsel, Grenzen, Berechtigungen, was an den Client geht) steht in den ACs als Szenario; jedes Szenario hat einen Test. Einfache Fakten-ACs (Datei existiert, Zahl im Outcome) brauchen keins.
- **Maßstab für Tests:** `.claude/rules/test-doctrine.md` (Heimat-Ebene, Pflichtliste, kein Test für Tautologien).

## Test command

`./verify.sh` im Repo-Wurzelverzeichnis. Läuft dotnet build, dotnet test, npm run build, guard:classes, guard:tokens und test:scripts; grün nur bei der Schlusszeile `VERIFY: PASS` und Exit 0. Bei `VERIFY: FAIL` stehen die roten Schritte und die Namen der roten Tests am Ende der Ausgabe.

## Open questions
- **ING** als zweite Quelle: eigener Adapter oder Generalisierung des Comdirect-Musters?
- Lohnt ein generischer "Bank-Source"-Port, sobald >1 Quelle existiert?
