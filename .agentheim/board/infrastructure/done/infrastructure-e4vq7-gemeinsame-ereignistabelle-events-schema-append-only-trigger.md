---
id: infrastructure-e4vq7
title: "Gemeinsame Ereignistabelle `events`: Schema, append-only-Trigger, `UNIQUE(stream_id, stream_version)`, Befehls-Id, generisches Anhängen und Laden; Tests belegen Konflikt bei gleicher Stream-Version, Rollback und das Verhalten von `BeginTransaction`"
status: done
type: feature
context: infrastructure
created: 2026-11-05
completed:
depends_on: [infrastructure-c7f53]
blocks: [ledger-k9tm2]
tags: [event-sourcing, persistence, sqlite]
related_adrs: [0008, 0013, 0083]
related_research: []
prior_art: []
---

## Why
ADR 0083 legt eine gemeinsame Tabelle `events` für alle Kontexte mit Event Sourcing fest. Bevor
das Kassenbuch umzieht (ledger-k9tm2) und das Budget Ereignisse schreibt (budgeting-zkbbd), muss
es die Tabelle und einen generischen Weg zum Anhängen und Laden geben.

## What
- Schema `events` und `processed_commands` genau wie in ADR 0083 Abschnitt 1 und 3, mit Index und
  append-only-Triggern, angelegt beim Start wie die übrigen Schemata.
- Generisches Anhängen: nimmt Kontext, Aggregat, Id, optionale Befehls-Id und Ereignisse im
  Umschlag (Typ, Version, Payload, `occurred_at`, Actor) und schreibt sie auf einer Connection in
  einer Transaktion (ADR 0008, ADR 0013 Punkt 5). Vergibt `stream_version` als größte plus eins,
  baut `stream_id` als `<context>:<aggregat>:<id>` an dieser einen Stelle, vergibt `event_id`.
  Nimmt eine Funktion entgegen, die in derselben Transaktion die Read Models fortschreibt.
  Prüft vor dem Schreiben die Form jedes `event_type` gegen `^[A-Z][A-Za-z0-9]*$` (ADR 0083
  Abschnitt 9) und weist einen abweichenden Typ ab.
- Befehls-Id: Prüfung gegen `processed_commands` in der Transaktion, Zeile schreiben, bei
  vorhandener Id keine Ereignisse und ein Ergebnis, das das sagt.
- Generisches Laden: je Stream nach `stream_version`, je Kontext nach `position`.
- Kein Kontext wird in diesem Task umgestellt.

### Schnittstelle (Refinement 2026-11-05; Namen darf der Worker anpassen, die Form nicht)
- Neues Modul `src/Server/EventStore.fs`, im `Server.fsproj` direkt nach `Persistence.fs` (vor
  `Ledger/Events.fs`), damit Kassenbuch und Budget es nutzen können. Schema-Anlage
  `EventStore.initializeSchema ()`, aufgerufen in `src/Server/Program.fs` vor
  `Ledger.Persistence.initializeLedgerSchema ()` und überall dort, wo Tests die übrigen Schemata
  anlegen.
- Typen:
  - `NewEvent`: `EventType`, `SchemaVersion`, `Payload` (JSON-Text, vom Kontext kodiert),
    `OccurredAt` (UTC, gespeichert im Format `o`), `ActorKind`, `ActorRef: string option`.
  - `StreamAppend`: `Aggregate` (Kleinschreibung), `Id` (Guid oder Text, als Text im Format `D`
    bei Guid), `Events: NewEvent list`.
  - `AppendRequest`: `Context` (Kleinschreibung), `CommandId: Guid option`,
    `Streams: StreamAppend list`. Ein Befehl darf mehrere Streams in einer Transaktion beschreiben.
  - `RecordedEvent`: alle Spalten von `events` (`Position`, `EventId`, `Context`, `StreamId`,
    `StreamVersion`, `OccurredAt`, `ActorKind`, `ActorRef`, `EventType`, `SchemaVersion`,
    `Payload`, `CommandId`).
  - `AppendResult`: `Appended of RecordedEvent list` oder
    `CommandAlreadyProcessed of firstPosition: int64`.
- Anhängen: `append (request) (applyReadModels: SqliteConnection -> SqliteTransaction ->
  RecordedEvent list -> unit) : Async<AppendResult>`. Eine frische Connection, eine Transaktion
  (ADR 0008). Reihenfolge in der Transaktion: Form von `context`, `aggregate` und jedem
  `event_type` prüfen, Befehls-Id gegen `processed_commands` prüfen, je Stream
  `MAX(stream_version)` lesen und fortlaufend vergeben, einfügen, `processed_commands` schreiben
  (nur mit Befehls-Id), `applyReadModels` mit den gespeicherten Ereignissen aufrufen, commit.
  Jede Ausnahme rollt alles zurück. Zusätzlich eine Variante, die eine schon offene Connection und
  Transaktion nimmt (für die Startmigration in ledger-k9tm2).
- `stream_version` vergibt nur das Anhängen. `UNIQUE(stream_id, stream_version)` ist das Netz für
  Schreibwege an der API vorbei; es gibt keine erwartete Version als Parameter (Konkurrenz im
  Prozess regeln die fachlichen Sperren, ADR 0083 Abschnitt 5).
- Laden: `loadStream context aggregate id` (nach `stream_version`), `loadContext context` (nach
  `position`). Kein `loadAll` ohne `context`-Filter in diesem Task.

## Acceptance criteria
- [ ] Ein zweites Einfügen derselben (`stream_id`, `stream_version`) direkt per SQL scheitert am
      `UNIQUE`; im Log steht nur das erste Ereignis.
- [ ] Zwei aufeinanderfolgende `append` in denselben Stream ergeben die Versionen 1 und 2.
- [ ] Ein `append` mit zwei Streams speichert beide in einer Transaktion; scheitert der zweite
      Stream (etwa an einem ungültigen `event_type`), steht auch vom ersten nichts im Log.
- [ ] Ein ungültiger `context` oder `aggregate` (Großbuchstaben, Doppelpunkt, leer) wird
      abgewiesen, ohne zu schreiben.
- [ ] `loadContext` liefert nur Ereignisse des genannten Kontexts, nach `position`; `loadStream`
      nur den genannten Stream, nach `stream_version`.
- [ ] `RecordedEvent` übersteht Schreiben und Laden unverändert, auch `ActorRef = None` und
      `CommandId = None` (Dapper-`OptionHandler` für `string`, Guid-Spalten als Text).
- [ ] Scheitert die Read-Model-Funktion, steht danach weder ein Ereignis in `events` noch eine
      Zeile in `processed_commands` (Rollback).
- [ ] UPDATE und DELETE auf `events` werden vom Trigger abgewiesen.
- [ ] Ein Befehl mit zwei Ereignissen in zwei Streams wird gespeichert; seine Wiederholung mit
      derselben Befehls-Id erzeugt kein weiteres Ereignis.
- [ ] `stream_version` beginnt je Stream bei 1 und ist lückenlos; `position` steigt in
      Einfügereihenfolge.
- [ ] Ein Test belegt, ob `BeginTransaction()` mit `BEGIN IMMEDIATE` öffnet (gegen eine
      Datei-Datenbank im Temp-Verzeichnis, nicht nur gegen die In-Memory-DB), und wie sich eine
      zweite Connection dabei verhält. Weicht das Ergebnis von ADR 0083 Abschnitt 5 ab, wird das
      ADR per Nachtrag korrigiert.
- [ ] Das generische Anhängen weist einen `event_type` ab, der nicht auf `^[A-Z][A-Za-z0-9]*$`
      passt (etwa `entryRecategorized`, `Entry_Recategorized`, leerer Text), und schreibt dann
      weder ein Ereignis noch eine Zeile in `processed_commands`. Ein Test belegt das. Die
      Vergangenheitsform prüft der Ausdruck nicht, sie bleibt ein Review-Punkt.
- [ ] `./verify.sh` endet mit VERIFY: PASS.

## Notes
- ADR 0083 Abschnitt 5: Gepinnt ist `Microsoft.Data.Sqlite` 10.0.14 (ADR 0008 nennt noch
  9.0.13 und ist dort veraltet). Die XML-Doku dokumentiert nur bei `BeginTransaction(deferred)`,
  dass `true` aufschiebt; dass die parameterlose Variante mit `BEGIN IMMEDIATE` öffnet, ist ein
  Schluss daraus, den dieser Task per Test belegt. In der `Cache=Shared`-In-Memory-DB der Tests
  sperrt SQLite je Tabelle (`SQLITE_LOCKED`), deshalb der Test gegen eine Datei.
- ADR 0083 Abschnitt 9: Nur die Form von `event_type` wird hier erzwungen. Payload-Felder in
  camelCase, englische Codes und die README-Pflicht sind prose-only, unenforced.
- Keine SQLite-Spezialitäten in Queries (ADR 0013 Punkt 8): kein `json_extract`.
- Tests in einer neuen Datei `src/Tests/EventStoreTests.fs` (in `Tests.fsproj` eintragen). Sie
  laufen gegen die In-Memory-DB aus `USE_MEMORY_DB`; nur der Test zu `BeginTransaction` öffnet
  eine eigene Datei-Datenbank unter `Path.GetTempPath()` und löscht sie danach. Keine echte
  Datenbank, kein `DATA_DIR` (CLAUDE.md, Security-Fallen).
- Vorbild für Anhängen in einer Transaktion: `appendAndApply` in `src/Server/Ledger/Persistence.fs`
  (ab Zeile 661, `MAX(sequence)` bei 698).
- Dapper: neuer Row-Typ braucht `[<CLIMutable>]`, Optionsfelder einen registrierten
  `OptionHandler<'T>` (CLAUDE.md, Laufzeitfallen).
- Ergebnis-Ereignisse (Rückschreib-Ergebnisse im Budget, ADR 0084 Punkt 8) entstehen in einer
  späteren Transaktion als ihre Entscheidung und tragen keine `command_id`. Das generische
  Anhängen muss Ereignisse ohne Befehls-Id zulassen und schreibt dann keine Zeile in
  `processed_commands`.

## Outcome

Neues Modul `src/Server/EventStore.fs` (im `Server.fsproj` direkt nach `Persistence.fs`) mit dem Schema aus ADR 0083 Abschnitt 1 und 3 (Tabelle `events`, beide Indizes, Trigger `events_no_update` und `events_no_delete`, Tabelle `processed_commands`), angelegt über `EventStore.initializeSchema ()` in `src/Server/Program.fs` direkt nach `Persistence.initializeDatabase()` und damit vor `initializeLedgerSchema`. Die beiden Tests, die den Serverstart nachbauen (`src/Tests/RemotingPipelineTests.fs`, `src/Tests/RemotingEndpointNegativeTests.fs`), rufen es ebenfalls auf. Die übrigen Testmodule, die `initializeLedgerSchema` aufrufen, brauchen die Tabelle noch nicht, weil kein Kontext umgestellt ist; ledger-k9tm2 muss das beim Umzug nachziehen.

Typen nach der Schnittstelle im Task: `NewEvent`, `StreamAppend` (mit `Id: AggregateId = GuidId of Guid | TextId of string`), `AppendRequest`, `RecordedEvent`, `AppendResult = Appended | CommandAlreadyProcessed of firstPosition`, dazu `ApplyReadModels` und `noReadModels`. `append` und `appendOn` (offene Connection und Transaktion, ohne Commit) wie beschrieben; Formfehler werfen `InvalidAppend` mit allen gesammelten Meldungen, bevor etwas geschrieben wird. Die gespeicherten Ereignisse werden nach dem Einfügen mit einer Abfrage (`event_id IN @event_ids`) zurückgelesen, damit der Aufrufer genau das sieht, was in der Tabelle steht. Ein Befehl ohne Ereignis schreibt keine Befehlszeile und ruft die Read-Model-Funktion nicht. `OccurredAt` wird im Format `o` gespeichert und mit `RoundtripKind` gelesen; UTC wird erwartet, aber nicht erzwungen. Dapper-Zeilentyp `StoredEventRow` mit `[<CLIMutable>]`, Optionsfelder nur `string option` (Handler vorhanden), Guids als Text.

Tests in `src/Tests/EventStoreTests.fs` (16 Fälle, eigener Kontextname je Fall): UNIQUE-Konflikt per SQL, Versionen 1 und 2, lückenlose Versionen und steigende `position`, Stream-Name, zwei Streams in einer Transaktion, ungültiger Kontext und Aggregat, ungültiger `event_type` (`entryRecategorized`, `Entry_Recategorized`, leer, mit Leerzeichen, mit Ziffer vorn) ohne Ereignis und ohne Befehlszeile, Laden je Kontext und je Stream, Rundlauf von `RecordedEvent` mit und ohne `ActorRef`/`CommandId`, Rollback bei scheiternder Read-Model-Funktion, Read-Model-Funktion sieht die Ereignisse in der Transaktion, Wiederholung derselben Befehls-Id, keine Befehlszeile ohne Befehls-Id und ohne Ereignis, Transaktionsbeginn.

Befund Transaktionsbeginn (Datei-Datenbank unter `Path.GetTempPath()`, danach gelöscht): Das parameterlose `BeginTransaction()` von Microsoft.Data.Sqlite 10.0.14 öffnet mit `BEGIN IMMEDIATE`. Eine zweite Connection liest während der Transaktion, ihr Schreiben scheitert nach dem Timeout mit `SQLITE_BUSY` (Code 5). Gegenprobe: `BeginTransaction(true)` schiebt auf, die zweite Connection schreibt durch. Das deckt sich mit ADR 0083 Abschnitt 5, ein korrigierender Nachtrag ist nicht nötig.

`./verify.sh`: VERIFY: PASS (dotnet 3896 grün, node 310 grün).

### Für den Conductor
Optionaler bestätigender Nachtrag zu ADR 0083 Abschnitt 5: "Nachtrag 2026-11-05 (infrastructure-e4vq7): Belegt. `BeginTransaction()` öffnet mit `BEGIN IMMEDIATE`, eine zweite Connection liest und scheitert beim Schreiben nach dem Timeout mit `SQLITE_BUSY`; `BeginTransaction(true)` schiebt auf. Test: `src/Tests/EventStoreTests.fs`."
