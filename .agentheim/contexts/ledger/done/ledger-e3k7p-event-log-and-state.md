---
id: ledger-e3k7p
title: Kassenbuch-Fundament: Ereignis-Log, Zustandsmodell, appendAndApply und Replay-Test
status: done
type: feature
context: ledger
created: 2026-09-06
depends_on: [ledger-fsz0a, ledger-z5vw6]
blocks: [ledger-m8r2v, ledger-kt0wq]
tags: [foundation, backend, persistence]
related_adrs: [0013, 0015, 0016, 0008, 0017]
related_research: []
prior_art: []
---

## Why
Slice 1 von 3 des Walking Skeletons (`ledger-kt0wq`, Aufteilung 2026-09-06). Alles Weitere
(Spiegel, Seite) setzt auf diesem Fundament auf: dem persistierten Log semantischer
Ereignisse (ADR 0013) und dem daraus abgeleiteten Zustandsmodell. Kein YNAB-Aufruf, keine
UI, nur das Gedächtnis selbst, mit Tests.

## What
1. **Ereignis-Typen** als F#-DU exakt nach dem Ereignis-Katalog in den Notes (verbindlich,
   mit Roman modelliert): Umschlag, Actor, die fünf Beobachtungs-Ereignisse mit ihren
   Payloads, die Entscheidungs-Namen reserviert.
2. **Zustandsmodell**: Buchung (eigene Id per `Guid.CreateVersion7()`, Konto, Datum, Betrag,
   Payee, Memo, Kategorie-Id und -Name, Splits, Cleared, Übertrags-Verknüpfung,
   `DeletedInYnab`-Flag), Konto (minimal: Id, Name, YnabAccountId, Art nach ADR 0016 darf
   vorerst nur `Bankkonto` sein), Fremd-Id-Verknüpfungen (Tabelle Buchungs-Id, System
   `ynab | importid | comdirect`, Wert, unique je System) nach ADR 0015.
3. **`apply : Entry option -> LedgerEvent -> Entry`** rein in `src/Server/Ledger/Domain.fs`
   (Dateilayout siehe Notes).
4. **Persistence** in `src/Server/Ledger/Persistence.fs`: Tabellen per Migration (Muster
   der bestehenden Tabellen im Kern-`Persistence.fs`), `appendAndApply : LedgerEvent list -> Async<unit>` schreibt Ereignisse und
   Zustand auf **einer** Connection in **einer** SQLite-Transaktion (ADR 0013 Punkt 5, kein
   Widerspruch zu ADR 0008), `loadEventsForEntry`, `loadAllEvents`, `replayAll` (baut den
   Zustand aus dem Log neu). Payload als JSON-Text, keine SQLite-JSON-Funktionen in Queries.
5. **Tests** (Expecto, In-Memory-SQLite wie die bestehenden Persistence-Tests): Ereignis
   anhängen und Zustand lesen; zwei Ereignisse auf eine Buchung ergeben zwei Historie-
   Einträge mit steigender Nummer; Löschung setzt Flag statt zu löschen; Replay aus dem Log
   ergibt denselben Zustand wie die Tabellen; Transaktion: schlägt das Zustands-Update fehl,
   steht auch kein Ereignis im Log; Fremd-Id ist unique je System.

## Acceptance criteria
- [x] Ereignis-DU, Actor-DU, Zustands-Records und Verknüpfungs-Typ existieren, semantisch
      benannt (kein CRUD-Naming), Ereignisse tragen Schema-Version.
- [x] `appendAndApply` schreibt Log und Zustand in einer SQLite-Transaktion; Test beweist
      Atomarität.
- [x] `replayAll` reproduziert den Zustand aus dem Log; Test vergleicht beide.
- [x] Buchungs-Id ist `Guid.CreateVersion7()`; Fremd-Ids liegen in eigener Tabelle, unique
      je System.
- [x] Keine Änderung an bestehenden Tabellen, Endpunkten oder UI.
- [x] `dotnet build` und `dotnet test` grün; Tests nur gegen In-Memory-SQLite.
- [x] `ledger/README.md`: Abschnitt "Ereignisse" mit der Liste der Ereignis-Typen und dem
      Actor-DU, damit die Sprache dokumentiert ist.

## Notes
Snapshot-Felder im Contract siehe ADR 0014 (Kategorie- und Payee-Name zum
Beobachtungszeitpunkt mitführen). Der Spiegel (`ledger-m8r2v`) erzeugt später aus
Snapshot-Vergleich die Ereignisse; hier reicht es, dass die Typen und der Schreibpfad stehen.

### Ereignis-Katalog (mit Roman modelliert, 2026-09-06)
Der Katalog ist **verbindlich**. Der Worker erfindet keine Ereignisse und keine Gruppen;
Abweichungen nur mit `## Worker note` und BOUNCE. Vokabular nach ADR 0013 und
`.claude/rules/semantic-messaging.md`. Das gleiche Kapitel steht im `ledger/README.md`
unter "Ereignisse" und wird dort gepflegt.

**Umschlag** (für jedes gespeicherte Ereignis gleich): Buchungs-Id, laufende Nummer je
Buchung (ab 1), Zeitpunkt (UTC), Actor, Ereignistyp (Name), Schema-Version (ab 1), Payload
als JSON-Text.

**Actor** (DU): `YnabMirror | ComdirectImport | Rule of RuleId | UserInBb | QuickAdd`.
"UserInBb" statt "Roman": Single-User-Modell, die Frau nutzt mit.

**Beobachtungs-Familie** (dieser Slice definiert die Typen; der Spiegel in `ledger-m8r2v`
erzeugt sie):

| Ereignis | Payload | Wann |
|---|---|---|
| `EntryObservedInYnab` | voller Snapshot: Konto-Id, YNAB-Transaction-Id, ImportId (optional), Datum, Betrag, Payee, Memo, Kategorie (Id und Name, optional), Splits (je Betrag, Kategorie Id und Name, Memo, Übertrags-Konto optional), Cleared-Status, Übertrags-Konto und Übertrags-Buchung (optional) | Buchung zum ersten Mal gesehen |
| `EntryDetailsChangeObservedInYnab` | alt und neu für Datum, Betrag, Payee, Memo, Konto-Id | Kopfdaten in YNAB geändert, auch Verschiebung auf ein anderes Konto |
| `EntryCategorizationChangeObservedInYnab` | alte und neue Kategorie (Id und Name, optional), alte und neue Splits | Kategorie oder Split in YNAB geändert |
| `EntryClearedStateChangeObservedInYnab` | alt, neu (Uncleared, Cleared, Reconciled) | Cleared-Status in YNAB geändert; eigenes Ereignis, damit die Seite dieses Rauschen ausblenden kann |
| `EntryDeletionObservedInYnab` | nur Umschlag | `deleted: true` gesehen; Zustand behält die Buchung mit Flag `DeletedInYnab` |

Ändert sich in einer Beobachtung mehr als eine Gruppe, entstehen mehrere Ereignisse in
dieser Reihenfolge: Details, Kategorisierung, Cleared. Gleiche Daten wie im Zustand: kein
Ereignis (Idempotenz, ADR 0015).

**Entscheidungs-Familie** (spätere Slices, hier nur als Namen im DU reserviert, damit die
Sprache vollständig ist; ohne Payload-Definition, ohne Verwendung): `EntryImportedFromComdirect`,
`EntryCategorizedByRule`, `EntryRecategorized`, `CashExpenseAdded`, `ValuationRecorded`.
Falls das DU ohne Payload nicht sauber kompiliert, die Namen als Kommentar-Block im
`Events.fs` festhalten statt sie zu erfinden.

**Konten:** reiner Zustand ohne Historie in v1: Id (Guid v7), Name, Art (`Bankkonto | Bar |
Wertpapierkonto`), Verknüpfung (`YnabMirrored of YnabAccountId | BbOwned`), geschlossen
(bool). ADR 0013 fordert Historie pro Buchung, nicht pro Konto.

**Zustandsmodell `Entry`:** Id (Guid v7), Konto-Id, Datum, Betrag (`Money`), Payee, Memo,
Kategorie (Id und Name, optional), Splits, Cleared-Status, Übertrags-Verknüpfung
(Buchung-zu-Buchung, optional, ADR 0015), `DeletedInYnab`. Kontostand ist eine Query über
die nicht gelöschten Buchungen, keine Spalte. Dazu die Fremd-Id-Tabelle aus ADR 0015
(Buchungs-Id, System `ynab | importid | comdirect`, Wert; unique je System und Wert).

**Commands und Queries:** dieser Slice hat keine. Er stellt `apply` (rein) und
`appendAndApply` / `loadEventsForEntry` / `loadAllEvents` / `replayAll` (Persistence)
bereit. `MirrorYnabBudget` ist der Command in `ledger-m8r2v`; die Queries kommen in
`ledger-q5w9c`.

**Dateilayout** nach souls-Muster in einem Unterordner: `src/Server/Ledger/Events.fs`
(Umschlag, Actor, Ereignis-DU, Payload-Records), `src/Server/Ledger/Domain.fs` (`apply`;
später `observe`), `src/Server/Ledger/Persistence.fs` (Tabellen, `appendAndApply`, Loads,
Replay; nutzt `getConnection` aus dem Kern-`Persistence.fs`). Compile-Reihenfolge in
`Server.fsproj`: nach `Persistence.fs`, vor `YnabClient.fs`. Später `Ledger/CommandHandlers.fs`
und `Ledger/QueryHandlers.fs`. Der übrige Server bleibt flach.

## Outcome

Slice 1 des Walking Skeletons steht: das Kassenbuch hat ein Gedächtnis, aber noch keinen
Spiegel und keine Seite.

**Ereignis-Sprache** (`src/Server/Ledger/Events.fs`): der Katalog aus den Notes ist 1:1
umgesetzt. Umschlag (`LedgerEvent`: Buchungs-Id, Zeitpunkt UTC, Actor, Body;
`RecordedLedgerEvent` ergänzt beim Lesen laufende Nummer, Typname und Schema-Version),
`Actor` als DU (`YnabMirror | ComdirectImport | Rule of RuleId | UserInBb | QuickAdd`),
die fünf Beobachtungs-Ereignisse mit genau den Payload-Feldern aus dem Katalog. Payload
liegt als JSON-Text mit handgeschriebenen Thoth-Codecs; Typname und Schema-Version
bestimmen zusammen den Decoder, eine unbekannte Kombination ist ein Fehler statt eines
Rateversuchs (ADR 0013 Punkt 7).

**Zwei bewusste Feinentscheidungen, beide nicht-erfindend:**
1. Die **laufende Nummer** steht nicht im `LedgerEvent`, sondern im `RecordedLedgerEvent`
   und wird vom Store beim Anhängen vergeben. Sonst müsste jeder Aufrufer (später der
   Spiegel) die Historie-Länge kennen, bevor er ein Ereignis bauen darf. Der Umschlag im
   Katalog ist damit vollständig, nur an der richtigen Stelle geteilt: der Task-Vertrag
   `appendAndApply : LedgerEvent list -> Async<unit>` bleibt exakt so, wie geschrieben.
2. Die **Entscheidungs-Namen** (`EntryImportedFromComdirect`, `EntryCategorizedByRule`,
   `EntryRecategorized`, `CashExpenseAdded`, `ValuationRecorded`) liegen als eigenes DU
   `ReservedDecisionEventName` im selben `Events.fs`, nicht als Fälle in
   `LedgerEventBody`. Grund: als Fälle ohne Payload müssten `apply`, Encoder und Decoder
   sie behandeln, und die einzige ehrliche Behandlung wäre dreimal `failwith` in
   ansonsten totalen Funktionen. So sind die Namen im Typsystem reserviert, ohne dass
   ein Payload erfunden wird, und ohne Landmine. Der Doc-Kommentar sagt, dass ein Name
   mit seiner Payload nach `LedgerEventBody` wandert, sobald sein Slice ihn modelliert.

**Zustandsmodell** (`src/Server/Ledger/Domain.fs`, rein, kein I/O): `Entry` mit allen
Feldern aus dem Katalog, `LedgerAccount` (Art × Verknüpfung nach ADR 0016; v1 nutzt nur
`BankAccount`), `EntryLink` mit `LinkSystem = YnabSystem | ImportIdSystem |
ComdirectSystem`. `apply : Entry option -> LedgerEvent -> Entry` ist die einzige Stelle,
die Ereignis auf Zustand faltet; ein Änderungs-Ereignis ohne vorangehende Erstbeobachtung
wird laut abgelehnt statt still geschluckt. `linksFrom` sagt rein, welche Fremd-Ids ein
Ereignis stiftet, damit der Store das nicht selbst wissen muss.

**Schreibpfad** (`src/Server/Ledger/Persistence.fs`): vier neue Tabellen per
`initializeLedgerSchema()` nach dem Muster von `initializeDatabase` (`ledger_accounts`,
`ledger_entry_events` mit PK `(entry_id, sequence)`, `ledger_entries`,
`ledger_entry_links` mit PK `(system, value)` = die Unique-Forderung aus ADR 0015). Keine
bestehende Tabelle, kein Endpunkt und keine UI wurden angefasst. `appendAndApply` holt
eine frische Connection (ADR 0008), öffnet **eine** Transaktion und schreibt darin
Ereignis, Verknüpfungen und Zustands-Upsert; mehrere Ereignisse in einem Aufruf falten
aufeinander auf. Bei jedem Fehler rollt die Transaktion alles zurück. Dazu
`loadEventsForEntry`, `loadAllEvents`, `loadEntry`, `loadAllEntries`, `linkEntry`,
`linksForEntry`, `findEntryByLink` (Spiegel-Matching nach ADR 0015), `replayAll` (baut den
Zustand nur aus dem Log, schreibt nichts) und die Konten-Funktionen.

Einzige Änderung an bestehendem Code: `Persistence.getConnection` ist von `private` auf
`internal` gewechselt (gleiche Assembly), plus ein Aufruf von `initializeLedgerSchema()`
in `Program.fs` und die Compile-Reihenfolge in `Server.fsproj` (nach `Persistence.fs`, vor
`YnabClient.fs`, wie im Task vorgegeben).

**Tests** (`src/Tests/LedgerTests.fs`, 25 Stück, Expecto, In-Memory-SQLite über den
bestehenden `USE_MEMORY_DB`-Harness): alle im Task genannten plus die Guid-v7-Prüfung, der
JSON-Roundtrip aller Payloads, der Actor-Roundtrip und ein Präzisionstest, der mit
`REAL`-Speicherung durchfallen würde. Der Pflicht-Replay-Test aus ADR 0013 Punkt 6
vergleicht `replayAll()` gegen `loadAllEntries()` über **alle** Buchungen, nicht nur die
eine des Tests, und fängt damit jede Drift des Zustandsmodells vom Log. Die Atomarität ist
zweifach belegt: einmal über ein `apply`, das nach dem bereits geschriebenen Ereignis
scheitert, einmal über eine kollidierende YNAB-Fremd-Id; danach steht jeweils weder
Ereignis noch Zustand.

**Nicht erledigt, bewusst:** `Entry.TransferEntryId` bleibt in v1 immer `None`. Der
Spiegel sieht nur YNABs `transfer_transaction_id`; das zur eigenen Buchungs-Id aufzulösen
braucht einen Lookup und damit ein Ereignis, das der verbindliche Katalog nicht vorsieht.
Als Folge-Task in `BACKLOG_ITEMS` gemeldet (ledger-t7v2q).

**Akzeptanzkriterium 7** (README-Abschnitt "Ereignisse"): der bestehende Abschnitt deckt
das Gebaute vollständig ab, es war nichts zu korrigieren. Der gemeldete `README_DELTA`
ergänzt nur zwei Stellen, die die Umsetzung präzisiert hat: einen neuen Begriff
**Fremd-Id-Verknüpfung** unter "Ubiquitous language" und die Ergänzung am Begriff
**Kontostand**, dass er in BB summiert wird und nicht per SQL-`SUM` (ADR 0017).

**Werkzeug-Hinweis:** die Serena-MCP-Tools waren in dieser Session nicht verfügbar (nur
Read/Write/Edit/Bash/Agent), deshalb wurden die `.fs`-Dateien mit den gewöhnlichen
Werkzeugen bearbeitet.

Verifikation: `dotnet build BudgetBuddy.sln` grün, 0 Warnungen. `dotnet test` grün, 653
bestanden / 7 übersprungen / 0 Fehler (zweimal gelaufen, stabil).
