# ynab-sync

## Purpose
Vermittelt in **beide Richtungen** zwischen Kassenbuch (`ledger`) und YNAB (seit ADR 0012,
2026-09-06). **Push:** kategorisierte Transaktionen und BB-geführte Entscheidungen nach
YNAB, mit Respekt vor YNABs eigenem Import-ID-Dedup. **Spiegel:** YNABs Zustand (Konten,
Buchungen, Änderungen) laufend ins Kassenbuch holen, damit es vollständig bleibt, solange
YNAB Eingabeort ist. Stellt außerdem YNAB-Strukturen (Budgets/Accounts/Categories/Payees)
zum Auswählen bereit. Die Spiegel-*Mechanik* (Delta, Intervall, Rate-Limit) lebt hier; das
Spiegel-*Ergebnis* und die Abweichung gehören dem `ledger`.

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
  ToTransfer (Ziel-Konto) | Uncategorized`. "Kategorie *und* Transfer" bleibt auf Typebene
  unrepräsentierbar: eine Zeile trägt nie beides. "Keins" ist seit ADR 0019 der benannte
  Fall `Uncategorized` — er entsteht ausschließlich beim Lesen aus YNAB (Spiegel), BBs
  eigener Split-Editor erzeugt ihn nie. Die Transfer-Zeile speichert das **Ziel-Konto**,
  nicht eine Payee-Id (ADR 0006, ergänzt durch ADR 0019).
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
  zur Auswahl. `YnabAccount` trägt `OnBudget`/`Closed` (aus YNABs `on_budget`/`closed`),
  damit der Transfer-Ziel-Picker auf **offene On-Budget-Konten** filtern kann.
- **Transfer-Ziel-Filter** — die Auswahl für eine `ToTransfer`-Split-Zeile bietet nur
  **offene On-Budget-Konten** (`openOnBudgetAccounts` in `src/Shared/Domain.fs`). Reine
  Scope-Begrenzung (Roman, ynab-002): jedes YNAB-Konto trägt eh eine auto-erzeugte
  Transfer-Payee, der Filter schließt nichts technisch Notwendiges aus; die Push-Zeit-
  Auflösung (ADR 0006) bleibt der Backstop. Default-Transfer-Ziel des Cashback-Shortcuts
  ist das konfigurierte **Quick-Add-Konto** (`ynab_quickadd_account_id`, ADR 0004),
  im Picker überschreibbar.
- **ManualTransaction (Quick Add)** — manuell erfasste Buchung (z.B. Bar-Ausgabe), direkt
  und synchron nach YNAB gepusht. Bewusst **ohne ImportId** (kollidiert nie mit dem
  Import-Dedup) und auf das eigene **Quick-Add-Konto** statt des Bank-Import-Kontos
  (ADR 0004). Payee optional. Scope-Herkunft: ADR 0003 (Phase-0-Experiment).
- **Quick-Add-Konto** — konfigurierbares Ziel-Konto für manuelle Einträge
  (`ynab_quickadd_account_id`), strikt getrennt vom Standard-/Import-Konto.
- **Quick-Add-Vorlage (Template)** — eine wiederverwendbare, aus den jüngsten Buchungen
  des Quick-Add-Kontos destillierte Vorlage (`QuickAddTemplate`, `src/Shared/Domain.fs`).
  Dedup-Schlüssel **Payee + Betrag + Kategorie**: wiederkehrende Bar-Buchungen kollabieren
  zu *einer* Vorlage; bis zu 5 verschiedene erscheinen (most-recent-first). Trägt **kein
  Datum** — eine vorausgefüllte Vorlage ist immer auf *heute* datiert, nie auf das
  Original-Datum. Nur *kategorisierte* Buchungen werden Vorlagen (schließt Transfers/Splits
  aus). Quelle ist der bestehende `getAccountTransactions`-Read (kein neuer YNAB-Pfad);
  die Dedup/Projektion ist pur (`recentQuickAddTemplates`). Tippen füllt das
  Quick-Add-Formular vor — gebucht wird nichts automatisch (ynab-t4n8p).
- **Beobachtung (`YnabObservation`)** — der Contract, den `ynab-sync` an das Kassenbuch
  abgibt (ADR 0014): eine gesehene YNAB-Buchung als reine Daten — YNAB-Transaction-Id,
  Konto, ImportId, Datum, Betrag, Payee-Name, Memo, Kategorie **mit Namen**, Split-Zeilen,
  Cleared, `transfer_account_id`, `transfer_transaction_id` und `deleted`. Kein
  Ereignis-Vokabular: ob eine Beobachtung eine Änderung ist, entscheidet der `ledger`.
  Typen und Decoder in `src/Server/YnabObservations.fs`.
- **server_knowledge** — YNABs Delta-Marke. Der Spiegel schickt sie als
  `last_knowledge_of_server` mit (`getTransactionsDelta`) und merkt sich die Marke der
  Antwort je Budget; ohne Marke ist es die Erstbefüllung (Vollabruf). Ein Lauf kostet zwei
  Requests. Die bestehenden `since_date`-Abrufe (Duplikat-Erkennung, Quick-Add-Vorlagen)
  bleiben davon unberührt.
- **Unkategorisierte Split-Zeile** — eine Subtransaction ohne `category_id` und ohne
  `transfer_account_id`. Beim Lesen kommt sie als `SplitTarget.Uncategorized` an und zählt
  beim Betrag voll mit; beim Push wird sie als Subtransaction mit `amount` (und `memo`)
  ohne `category_id` und ohne `payee_id` serialisiert. BBs eigener Split-Editor erzeugt sie
  nie — der Arm ist defensiv (ADR 0019, ergänzt ADR 0006).
- **Transfer-Payee im Lesepfad** — YNAB nennt die Payee einer Übertrags-Zeile
  "Transfer : <Konto>". Der Spiegel schneidet das Präfix ab, damit die Transfer-Split-Zeile
  den **Kontonamen** trägt, wie `SplitTarget.ToTransfer` ihn verlangt.
- **Rückfrage Lena beim Import** (ynab-sync-d9e51): Zustand `AskLena` an der Sitzungs-Transaktion (`SyncTransaction`), gesetzt über `SyncApi.setAskLena` an der einzelnen offenen Importzeile (Pending, AutoCategorized, ManualCategorized, NeedsAttention). Der Import-Push sendet für diese Zeilen `flag_color` mit der Einstellung `lena_flag_color` (ADR 0056, 0061), auch ohne Kategorie; Zeilen ohne Rückfrage senden den Schlüssel nicht. Ohne gültige Farbe ist die Aktion gesperrt (`SyncError.AskLenaRejected`), Überspringen hebt die Rückfrage auf, ist die Farbe bis zum Push entfernt, geht die Zeile ohne Flag und `ImportResult.LenaFlagNotSentCount` nennt das. Der Zustand ist sitzungsgebunden und nicht persistiert (ADR 0064).
- **Konten im Import** (ynab-sync-k3vq8): Das Split-Sheet in Import prüfen bezieht seine Konten über `LedgerApi.getBudgetAccounts` (wie das Kassenbuch, serverseitig gecacht), die Quick-Add-Id getrennt über `getSettings`. Geladen wird beim Seitenaufruf und erneut beim Öffnen des Sheets, solange die Liste fehlt. Ein Fehlschlag von Konten oder Kategorien heißt **Ladefehler** (`ShowLoadFailure`): Er erscheint als Toast mit dem Fehlertext, nur auf Import und Quick-Add und nicht, solange derselbe Text noch sichtbar ist. Die Kategorien laden auch im Hintergrund auf anderen Seiten, dort bleibt ihr Fehler still, das Betreten des Imports lädt neu. Die **wartende Barabhebungs-Zeile** (`AwaitedCashbackLine`) ist die Übertrag-Zeile eines Cashback-Splits, deren Quick-Add-Konto beim Tipp noch nicht bekannt war: Sie wird beim Eintreffen nachgetragen, solange sie leer ist. Lesende Aufrufe im SyncFlow laufen über `Retry.reading`, schreibende nie, weil ein abgebrochener Schreibaufruf beim Server angekommen sein kann und `startSync` oder `confirmTan` nur durch Romans Klick entstehen (ADR 0071). `Retry.reading` hält Antworten zurück, deshalb trägt jede Antwort auf Sitzung, Buchungen, Konten und Quick-Add-Id den Stand ihres Abrufs: nur der aktuelle Stand zählt, Start, TAN-Bestätigung und Abbruch überholen laufende Lesevorgänge.

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
- *MirrorYnabBudget* (Spiegel-Lauf: Konten- und Transaktions-Abruf, Beobachtungen ans
  Kassenbuch; in v1 manuell ausgelöst).
- *SetAskLena* (ynab-sync-d9e51: Rückfrage Lena an einer Importzeile setzen oder zurücknehmen; reine Regeln in `Server.ImportAskLena`, der Push-Teil `YnabClient.applyLenaFlag`).

## Relationships with other contexts
- **Downstream of:** `ledger` für den Push (ADR 0021): gepusht wird, was vorher ins
  Kassenbuch geschrieben wurde. Die kategorisierten SyncTransactions kommen weiter aus
  `categorization`.
- **Conformist zu:** YNAB-API (Datenmodell, Dedup, Cleared-/Transfer-Semantik).
- **Shared kernel:** ImportId teilt sich der Context konzeptionell mit `banking-import`s
  Duplikat-Erkennung.
- Voller Kontext-Überblick: `../../context-map.md`.

## Szenarien und Tests

- **Bug-Tasks:** mindestens ein „kaputt“- und ein „intakt“-Szenario als Akzeptanzkriterium (GIVEN/WHEN/THEN). Der Regressionstest zum kaputt-Szenario ist vor dem Fix rot.
- **Fachliches Verhalten** (Zustandswechsel, Grenzen, Berechtigungen, was an den Client geht) steht in den ACs als Szenario; jedes Szenario hat einen Test. Einfache Fakten-ACs (Datei existiert, Zahl im Outcome) brauchen keins.
- **Maßstab für Tests:** `.claude/rules/test-doctrine.md` (Heimat-Ebene, Pflichtliste, kein Test für Tautologien).

## Test command

`./verify.sh` im Repo-Wurzelverzeichnis. Läuft dotnet build, dotnet test, npm run build, guard:classes, guard:tokens und test:scripts; grün nur bei der Schlusszeile `VERIFY: PASS` und Exit 0. Bei `VERIFY: FAIL` stehen die roten Schritte und die Namen der roten Tests am Ende der Ausgabe.

## Open questions
- **Spiegel-Mechanik** — *gelöst (ADR 0014, ynab-czk34; umgesetzt in ledger-m8r2v):*
  Delta-Sync über `server_knowledge` auf dem budgetweiten Transaktions-Endpoint, Konten je
  Lauf vollständig statt als Delta (ADR 0018), 429 bricht den Lauf ab. Ausgelöst wird in v1
  nur manuell; das Frische-Kriterium (Seitenaufruf/Vordergrund, nach jedem Push) ist
  Folge-Task.
- **Transfer-Payee Modell** — *gelöst (ADR 0006, ynab-001):* Transfer-Zeile speichert das
  Ziel-Konto, `payee_id` erst beim Push aufgelöst.
- **Split UI** — *gelöst (ynab-002):* Push-Fundament (ynab-001) + Review-UI stehen. Das
  Split-Form-Sheet bietet einen Ein-Tipp-Cashback-Shortcut (Transfer + Kategorie-„Rest"-Zeile,
  nur der Transfer-Betrag wird getippt) und einen generischen N-Zeilen-Editor; Validierung/
  Rest-Vorschau über die geteilten Domain-Helper (`mkSplits`/`splitRemainder`). Sheet-Stacking
  nach ADR 0005 §4 (Picker als `.layer-2` über dem Form-Sheet).
- Rate-Limit-Handling bei größeren Batches (YNAB ~200 req/h)?
