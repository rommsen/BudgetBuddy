---
id: 0083
title: Gemeinsame Ereignistabelle events für ledger und budgeting; Geld ist Ereignis, alles andere Zustand
scope: global
status: accepted
date: 2026-11-05
supersedes: []
superseded_by: []
related_tasks: [infrastructure-c7f53, budgeting-zkbbd, infrastructure-e4vq7, ledger-k9tm2]
related_adrs: [0008, 0012, 0013, 0021, 0065, 0068, 0079, 0080]
related_research: []
---

## Context

BB soll in seinen Kernkontexten selbst Quelle der Wahrheit sein (ADR 0012, Fähigkeit für
Fähigkeit), und Reports sollen als Read Models auf einem einheitlichen Ereignismodell
entstehen. Heute gibt es drei Speichermuster, jedes im eigenen BC entschieden:

- **Kassenbuch:** Event Sourcing nach ADR 0013 (`scope: ledger`). Tabelle
  `ledger_entry_events` mit `PRIMARY KEY (entry_id, sequence)`, synchron fortgeschriebene
  Zustandstabellen, Replay-Test. Gegen UPDATE und DELETE gibt es keinen Trigger
  (`src/Server/Ledger/Persistence.fs`, `createLedgerSchemaOn`).
- **Budget:** append-only Aufzeichnungen mit Triggern (`budget_movements`,
  `target_pause_events`, `target_changes` samt Ergebnistabellen; ADR 0065, 0079, 0080), aber
  ohne `apply` und ohne Replay.
- **Rest:** gewöhnlicher Zustand.

Mit budgeting-zkbbd wird BB im Budget führend, Zuweisen, Verschieben und Targets entstehen dort
als Ereignisse. Bevor das erste Budget-Ereignis gespeichert wird, müssen Umschlag, Reihenfolge,
Sprache und die Grenze zwischen Ereignis und Zustand für alle Kontexte feststehen. Ein
gespeicherter `event_type` bleibt für immer.

Zwei Befunde aus dem Code bestimmen die Entscheidung mit:

- `occurred_at` ist schon heute innerhalb einer Buchung nicht monoton. `recordComdirectPush`
  schreibt den Push-Zeitpunkt (`Ledger/Domain.fs`, `PushedAt`), die Review-Schritte danach tragen
  das frühere `step.At`. Eine Einfügereihenfolge lässt sich aus `occurred_at` also nicht ablesen.
- Die Actor-Spalte `actor_rule_id` ist schon heute eine freie Referenz: `OrderIdMatch` legt dort
  die Bestell-Id ab (`Ledger/Events.fs`, `actorToColumns`).

Entschieden mit Roman am 2026-11-05, Refinement mit architect und tactical-modeler am selben
Tag. Dieses ADR schreibt die Entscheidungen aus und legt die technischen Details fest, die das
Refinement offen ließ (Stream-Namen, Befehls-Id, Transaktionsbeginn).

## Decision

### 1. Eine Tabelle `events` für alle Kontexte mit Event Sourcing

Alle Kontexte, die Ereignisse speichern, schreiben in eine Tabelle. Schema (nur
Standard-SQL-Typen, ADR 0013 Punkt 8):

```sql
CREATE TABLE IF NOT EXISTS events (
    position       INTEGER PRIMARY KEY,
    event_id       TEXT    NOT NULL UNIQUE,
    context        TEXT    NOT NULL,
    stream_id      TEXT    NOT NULL,
    stream_version INTEGER NOT NULL,
    occurred_at    TEXT    NOT NULL,
    actor_kind     TEXT    NOT NULL,
    actor_ref      TEXT,
    event_type     TEXT    NOT NULL,
    schema_version INTEGER NOT NULL,
    payload        TEXT    NOT NULL,
    command_id     TEXT,
    UNIQUE (stream_id, stream_version)
);

CREATE INDEX IF NOT EXISTS idx_events_context_type ON events(context, event_type);
CREATE INDEX IF NOT EXISTS idx_events_command ON events(command_id);

CREATE TRIGGER IF NOT EXISTS events_no_update BEFORE UPDATE ON events
BEGIN SELECT RAISE(ABORT, 'events ist append-only'); END;
CREATE TRIGGER IF NOT EXISTS events_no_delete BEFORE DELETE ON events
BEGIN SELECT RAISE(ABORT, 'events ist append-only'); END;
```

Die Spalten:

- `position`: globale Reihenfolge. Als `INTEGER PRIMARY KEY` ist sie der Alias der rowid,
  SQLite vergibt sie selbst als größte vorhandene plus eins. Ein Zähler oder `AUTOINCREMENT`
  ist nicht nötig: `AUTOINCREMENT` schützt nur vor der Wiederverwendung gelöschter Nummern,
  und der Trigger verbietet DELETE. Ein VACUUM nummeriert eine als `INTEGER PRIMARY KEY`
  deklarierte rowid nicht um.
- `event_id`: eindeutige Id je Ereignis (Guid als Text), vom Schreiber vergeben.
- `context`: Name des Bounded Context in Kleinschreibung (`ledger`, `budgeting`).
- `stream_id`, `stream_version`: Stream und laufende Nummer darin, beginnend bei 1, lückenlos.
- `occurred_at`: fachlicher Zeitpunkt in UTC als ISO-8601-Text (Format `o`, wie heute im
  Kassenbuch).
- `actor_kind`, `actor_ref`: Herkunft als Art plus freie Referenz (siehe Abschnitt 2).
- `event_type`, `schema_version`, `payload`: Typname, Version, JSON-Text (ADR 0013 Punkt 7
  und 8).
- `command_id`: optional, die Befehls-Id, unter der das Ereignis entstand (siehe Abschnitt 3).

`UNIQUE(stream_id, stream_version)` sorgt dafür, dass zwei Schreibvorgänge, die dieselbe
Version in denselben Stream schreiben wollen, scheitern. Keiner überschreibt den anderen still.
Die append-only-Trigger gelten einmal für alle Kontexte.

Die Kontexte bleiben fachlich getrennt: eigener `context`-Wert, eigenes Vokabular, eigener
Decoder je Kontext. Ein Kontext liest nur Zeilen mit seinem `context`-Wert. Ein
kontextübergreifender Report darf alle Zeilen lesen und dekodiert sie mit den Decodern der
jeweiligen Kontexte. ADR 0065 lehnte das *Kassenbuch*-Log für Budget-Bewegungen ab, weil es an
Buchungen hängt. Ein eigener Budget-Stream in der gemeinsamen Tabelle ist damit vereinbar.

**`position` ist die Reihenfolge des Festhaltens, nicht des Geschehens.** Sie sagt, in welcher
Folge BB Ereignisse gespeichert hat. Reports sortieren fachlich nach Buchungsdatum oder
`occurred_at`. Es gibt keine Spalte `recorded_at`, und `occurred_at` wird nicht
zurückdatiert: Ein Ereignis trägt den Zeitpunkt, zu dem das Festgehaltene geschah.

### 2. Stream-Namen und Actor-Referenz

- **Stream-Namen** haben die Form `<context>:<aggregat>:<id>`, alles in Kleinschreibung, die
  Id im Textformat ihres Typs (Guid im Format `D`, wie `Guid.ToString()` ihn liefert). Das
  erste Segment ist gleich dem Wert in `context`. Damit ist `stream_id` ohne `context`
  eindeutig, und `UNIQUE(stream_id, stream_version)` reicht über alle Kontexte.
- **Kassenbuch:** `ledger:entry:<entryId>`, ein Stream je Buchung wie heute.
- **Budget:** Den Schnitt legt das ADR zu budgeting-zkbbd fest (je Kategorie, je Monat oder je
  Monat und Kategorie). Er folgt derselben Form, etwa `budgeting:category:<categoryId>`.
- Der Stream-Name wird an einer Stelle gebaut (Funktion im generischen Anhängen), die Kontexte
  geben Aggregat und Id. Kein Kontext setzt den Text von Hand zusammen.
- **Actor:** `actor_kind` ist der Name der Actor-Variante in PascalCase (heute `YnabMirror`,
  `ComdirectImport`, `Rule`, `UserInBb`, `QuickAdd`, `Bob`, `OrderIdMatch`). `actor_ref` ist eine
  freie Text-Referenz ohne festen Typ: bei `Rule` die RuleId, bei `OrderIdMatch` die
  Bestell-Id, sonst leer. Das ist die heutige Spalte `actor_rule_id` unter einem Namen, der
  sagt, was sie ist. Gefiltert wird über `actor_kind`, ohne JSON-Funktionen (ADR 0013 Punkt 8).

### 3. Befehls-Id

Ein Befehl kann mehrere Ereignisse in mehreren Streams erzeugen (Übertragspaar: zwei Buchungen,
zwei Streams; eine Verschiebung im Budget: zwei Kategorien). Ein `UNIQUE` auf `command_id` in
`events` würde schon das zweite Ereignis desselben Befehls abweisen. Deshalb:

```sql
CREATE TABLE IF NOT EXISTS processed_commands (
    command_id     TEXT    PRIMARY KEY,
    context        TEXT    NOT NULL,
    first_position INTEGER NOT NULL
);
```

- Der Client vergibt je Absicht eine Befehls-Id (Guid) und schickt sie bei einer Wiederholung
  unverändert mit. Die API-Grenze prüft das Format (`src/Server/Validation.fs`).
- Das generische Anhängen schreibt die Ereignisse mit `command_id` und in derselben Transaktion
  eine Zeile in `processed_commands`. Vor dem Anhängen prüft es in der Transaktion, ob die
  Befehls-Id schon dort steht. Steht sie dort, entsteht kein Ereignis, und der Aufrufer meldet
  den festgehaltenen Stand. Rennt eine Wiederholung trotzdem an der Prüfung vorbei, weist der
  Primärschlüssel sie ab und die Transaktion rollt zurück.
- Ein Befehl ohne Ereignis hinterlässt keine Zeile. Seine Wiederholung wird neu ausgewertet.
- `processed_commands` ist ein Read Model nach Kriterium (b) aus Abschnitt 4: Es lässt sich aus
  `events` neu bauen (erste `position` je `command_id`) und steht im Replay-Test.
- `command_id` ist optional. Ereignisse ohne Befehl aus der UI (Spiegel, Import, Startmigration)
  tragen keine. Das Kassenbuch dedupliziert weiter über fremde Ids (Comdirect-Reference,
  ImportId, ADR 0021 Punkt 3).

### 4. Read Models

- Standard ist Falten auf Anfrage aus dem Log.
- Eine Read-Model-Tabelle gibt es nur, wenn
  (a) ein Schreibweg aus ihr entscheidet,
  (b) sie eine Eindeutigkeit trägt (Fremd-Id, Befehls-Id) oder
  (c) ein Lesepfad mit Produktionsdaten gemessen über 200 ms braucht.
- Jede Read-Model-Tabelle lässt sich aus dem Log neu bauen und steht im Replay-Test. Heute sind
  das `ledger_entries`, `ledger_entry_split_categories`, `ledger_entry_split_transfers` und
  `ledger_entry_links`, mit `processed_commands` kommt eine dazu.
- Weiter gilt ADR 0013: Read Models werden synchron in derselben Transaktion wie das Anhängen
  fortgeschrieben. Kein Bus, keine asynchronen Projektionen.

### 5. Transaktionsbeginn

Gepinnt ist `Microsoft.Data.Sqlite` 10.0.14 (`src/Server/Server.fsproj`, `src/Tests/Tests.fsproj`);
ADR 0008 nennt noch 9.0.13 und ist in diesem Punkt veraltet. Die XML-Doku dieser Version sagt
beim parameterlosen `SqliteConnection.BeginTransaction()` nichts darüber, ob die Transaktion
aufgeschoben wird. Nur die Überladung `BeginTransaction(deferred)` dokumentiert, dass `true` die
Transaktion aufschiebt. Daraus schließen wir, dass der parameterlose Aufruf nicht aufschiebt, also
mit `BEGIN IMMEDIATE` öffnet. Den Beleg liefert der Test in infrastructure-e4vq7. Folgen für dieses Schema, sofern der Schluss stimmt:

- Eine Schreibtransaktion hält die Schreibsperre der Datei ab dem Beginn. Eine zweite wartet
  bis zum Befehls-Timeout und scheitert dann mit `SQLITE_BUSY`. Das Lesen der nächsten
  `stream_version` in der Transaktion kann daher von keinem zweiten Prozess oder keiner zweiten
  Connection überholt werden.
- `UNIQUE(stream_id, stream_version)` und der Primärschlüssel von `processed_commands` sind das
  Netz für alles, was an dieser Sperre vorbeigeht: eine aufgeschobene Transaktion, ein
  Schreibweg ohne Transaktion, die `Cache=Shared`-In-Memory-Datenbank der Tests (dort sperrt
  SQLite je Tabelle und meldet `SQLITE_LOCKED`).
- Die fachlichen Sperren im Prozess (`WriteGuard` im Kassenbuch, Sperre je Monat, `TargetGuard`)
  bleiben. Sie reihen Befehle fachlich, die Datenbank sichert die Nummern.
- Das Verhalten ist im Folge-Task zur Tabelle `events` (infrastructure-e4vq7) per Test zu
  belegen: Konflikt bei gleicher Stream-Version, Rollback und der tatsächliche
  Transaktionsbeginn. Weicht er ab, gilt das Netz aus dem vorigen Punkt allein, und dieses ADR
  bekommt einen Nachtrag.

**Nachtrag 2026-11-05 (infrastructure-e4vq7):** Belegt. `BeginTransaction()` öffnet unter
10.0.14 mit `BEGIN IMMEDIATE`. Eine zweite Connection liest während der Transaktion und scheitert
beim Schreiben nach dem Timeout mit `SQLITE_BUSY`. `BeginTransaction(true)` schiebt auf. Test in
`src/Tests/EventStoreTests.fs` gegen eine Datei-Datenbank im Temp-Verzeichnis.

### 6. Übergang des Kassenbuchs

- `ledger_entry_events` wird in einem Startschritt in `events` überführt, in einer Transaktion.
  Gelesen wird in `rowid`-Reihenfolge, das ist die echte Einfügereihenfolge. Die Einfügung in
  `events` vergibt `position` in genau dieser Folge.
- **Abbruchprüfung:** Steigt `sequence` je Buchung in `rowid`-Reihenfolge nicht um genau 1
  (beginnend bei 1), bricht die Migration ab und BB startet nicht. Grund: `ledger_entry_events`
  hat einen zusammengesetzten Primärschlüssel und damit eine verdeckte rowid, die ein früheres
  VACUUM neu nummeriert haben kann. Dann wäre die rowid keine Einfügereihenfolge mehr.
- Abbildung: `context = 'ledger'`, `stream_id = 'ledger:entry:' || entry_id`,
  `stream_version = sequence`, `actor_ref = actor_rule_id`, `command_id` leer, `event_id` neu
  und zufällig. `occurred_at`, `actor_kind`, `event_type`, `schema_version` und `payload` werden
  unverändert übernommen.
- **Ausnahme vom Nicht-Anfassen:** ADR 0013 Punkt 7 sagt, gespeicherte Ereignisse bleiben
  unangetastet. Die einmalige Kopie in die neue Tabelle ist die ausdrückliche Ausnahme. Typ,
  Version und Payload ändern sich dabei nicht, nur der Umschlag.
- Nach erfolgreicher Kopie wird `ledger_entry_events` in derselben Transaktion entfernt. Es gibt
  keinen Zustand mit zwei Logs.
- Umzustellen sind `appendAndApply`, `loadEventsForEntry`, `loadAllEvents`,
  `loadBobBookedAmountsBetween` (Bob-Tagesgrenze, ADR 0068), `backfillTransferAccountId`, der
  Zeilentyp `EventRow` und die Tests in `LedgerTests.fs`, die das Log direkt lesen. Die Grenzen
  aus ADR 0068 dürfen sich durch den Umbau nicht verschieben.
- **Abfolge:** Die Kassenbuch-Migration läuft, bevor das erste Budget-Ereignis in `events`
  geschrieben wird. So ist `position` über die ganze Historie die Reihenfolge des Festhaltens.
  Das ADR zu budgeting-zkbbd kann vorher entstehen, seine Umsetzung wartet.
- Vor dem Deploy läuft die Migration einmal gegen eine Kopie der Produktions-Datenbank
  (außerhalb des Repos).

**Nachtrag 2026-11-05 (Verfeinerung ledger-k9tm2):** Umzustellen ist auch `loadEntryOrigins`
(liest das erste Ereignis je Buchung). Die Kopie läuft per SQL mit den Textspalten unverändert
und nicht über `EventStore.validate`, damit ein alter Zeitpunkt ohne Zonenangabe den Start nicht
blockiert. Vorher läuft ledger-r6wn3, damit Replay auch `ledger_entry_links` vollständig herstellt.

**Nachtrag 2026-11-05 (ledger-k9tm2, umgesetzt):** Die Migration läuft in `createLedgerSchemaOn`
nach dem Anlegen von `events` und vor den Spalten-Backfills, die damit schon `events` lesen.
`appendAndApply` schreibt über `EventStore.appendOn`; neue Kassenbuch-Ereignisse brauchen damit
einen UTC-Zeitpunkt (alle Produktionswege nutzen `DateTime.UtcNow`). Migrierte Zeitpunkte bleiben
Text wie zuvor. `RecordedLedgerEvent.Sequence` bleibt als Feldname und wird aus `stream_version`
gefüllt. Die Probe gegen eine Kopie der Produktion vor dem Deploy war in allen Punkten grün.

### 7. Regel für neue Fähigkeiten

> Eine Fähigkeit bekommt Ereignisse, wenn sie Geld-Fakten oder Entscheidungen über Geld
> festhält (Buchungen, Zuweisungen, Targets) und ein Bericht deren Verlauf lesen soll.
> Konfiguration, Stammdaten aus YNAB und der Arbeitsstand eines Imports sind Zustand. Eine
> Ursache aus der Konfiguration steht im Ereignis als Name und Id.

Kurzform: Geht es um Geld (was passiert ist oder was entschieden wurde), sind es Ereignisse.
Alles andere ist Zustand.

Damit sind YNAB-Beobachtungen im Kassenbuch Ereignisse (sie halten Geld-Fakten fest) und
Kategorisierungsregeln Zustand. Targets sind Entscheidungen über Geld und keine Regeln im Sinn
von `rules.yml`. Name und Id einer Ursache aus der Konfiguration stehen im Ereignis, weil sich
die Konfiguration ändern oder wegfallen kann und der Bericht trotzdem lesbar bleiben muss. Die
Regel gilt für neue Ereignistypen. Gespeicherte Typen bleiben, wie sie sind (ADR 0013 Punkt 7).

### 8. Zustand bleibt

Zustand ohne Ereignisse bleiben: Kategorisierungsregeln (`rules`), `settings`,
`sync_sessions`, `sync_transactions`, `ledger_accounts`, `ledger_categories`. Die heutigen
Budget-Aufzeichnungen (`budget_movements`, `target_pause_events`, `target_changes` samt
Ergebnistabellen) bleiben, bis das ADR zu budgeting-zkbbd ihre Überführung nach `events`
festlegt.

### 9. Sprache der Ereignisse

`event_type` ist englisch, PascalCase, Vergangenheitsform (`EntryRecategorized`).
Payload-Felder sind englisch in camelCase (`categoryId`), gespeicherte Codes englisch
(`changed-in-ynab`). Die README eines Kontexts nennt die Typnamen und erklärt sie auf Deutsch.
Das entspricht dem heutigen Kassenbuch (`Ledger/Events.fs`) und der Regel "Identifier
Englisch" aus CLAUDE.md. Die deutschen Namen der `budgeting`-README (TargetsAngewendet,
TargetPausiert) werden mit budgeting-zkbbd auf englische Typnamen abgebildet, bevor das erste
Budget-Ereignis gespeichert wird.

Durchsetzung:

- **`event_type`, Form:** erzwungen. Das generische Anhängen (infrastructure-e4vq7) weist einen
  `event_type` ab, der nicht auf `^[A-Z][A-Za-z0-9]*$` passt, und schreibt dann nichts. Ein Test
  belegt das. Ob ein Name englisch und in der Vergangenheitsform ist, prüft der Ausdruck nicht;
  das bleibt ein Review-Punkt.
- **Payload-Felder in camelCase, englische Codes, README-Pflicht:** prose-only, unenforced. Kein
  Test und kein Wächter prüft sie; sie gelten über dieses ADR und das Review.

### Verhältnis zu ADR 0013

ADR 0013 gilt weiter, Punkte 1 bis 9. Geändert ist die Tabelle: Das Log liegt in `events`
statt in `ledger_entry_events`. Punkt 7 bekommt die einmalige Ausnahme aus Abschnitt 6. ADR
0013 wird nicht ersetzt und bekommt einen Nachtrag mit Verweis auf dieses ADR.

## Consequences

### Positive
- Ein Umschlag, ein Satz Trigger, ein generisches Anhängen und Laden für alle Kontexte. Ein
  neuer Kontext mit Ereignissen schreibt keine eigene Log-Tabelle.
- Kontextübergreifende Reports lesen eine Tabelle in einer Reihenfolge, ohne `UNION ALL`.
- Das Kassenbuch-Log ist danach gegen UPDATE und DELETE geschützt, was es heute nicht ist.
- Gleichzeitiges Schreiben in denselben Stream scheitert sichtbar am `UNIQUE`.
- Wiederholte UI-Befehle (abgebrochene Anfrage, erneut gesendet) buchen nicht doppelt.
- Die Regel aus Abschnitt 7 beantwortet für jede neue Fähigkeit, ob sie Ereignisse bekommt,
  ohne eigenes ADR.

### Negative
- Startmigration in Produktion: Schlägt die Abbruchprüfung an, startet BB nicht. Deshalb vorher
  die Probe gegen eine Kopie.
- Kein Zurück ohne Backup: Sobald neue Ereignisse in `events` stehen, geht ein Rollback nur
  über das Backup.
- Wer nach `position` statt nach Datum sortiert, zeigt einen falschen fachlichen Verlauf. Das
  steht im Pflichtsatz und muss in jedem Report-Review geprüft werden.
- Die Bob-Tagesgrenze (ADR 0068) hängt an `occurred_at` und `actor_kind`. Ihr Ladepfad wird
  umgebaut und braucht den Vorher-nachher-Vergleich im Folge-Task.
- Eine Tabelle für alle heißt: ein falscher Decoder oder ein fehlender `context`-Filter liest
  fremde Ereignisse. Jeder Ladepfad filtert über `context` oder `stream_id`.
- Der Client muss für Befehle aus der UI eine Befehls-Id erzeugen und bei Wiederholung
  mitschicken.

### Neutral
- `src/Shared` ändert sich durch den Umbau nicht (`LedgerHistoryLine`, `HistoryEntry`). Wandern
  neue Felder nach Shared, sind `scripts/ledger-detail.test.mjs` und
  `scripts/budget-history.test.mjs` im selben Schritt nachzuziehen.
- `linkEntry` schreibt heute `ledger_entry_links` ohne Ereignis. Das ist gegen ADR 0013 und
  0021 zu prüfen (eigener Folge-Task) und ändert an diesem ADR nichts.
- Im Budget fehlt noch eine Echo-Regel wie im Kassenbuch (`...WrittenBackToYnab`, Actor
  `YnabMirror`). Sie gehört zu budgeting-zkbbd.

## Alternatives considered

- **Tabelle je Kontext mit Zählertabelle für die globale Reihenfolge:** verworfen. Die
  Migration wäre kleiner, aber der Umschlag bliebe Konvention, die jeder Kontext neu umsetzt
  (Trigger, Spalten, Unique), und jeder kontextübergreifende Report bräuchte ein `UNION ALL`.
  Der Zähler wäre eine weitere Stelle, die in derselben Transaktion stimmen muss.
- **Regelsatz "Ereignisse, wenn BB den Zustand führt und Historie gebraucht wird":** verworfen.
  Er ist unschärfer ("Historie gebraucht" lässt sich für fast alles behaupten) und passt nicht
  auf YNAB-Beobachtungen: Die führt BB nicht, sie sind trotzdem Ereignisse.
- **`UNIQUE(command_id)` oder `UNIQUE(command_id, stream_id)` in `events`:** verworfen. Das
  erste weist schon das zweite Ereignis eines Befehls ab, das zweite jedes Paar von Ereignissen
  eines Befehls im selben Stream.
- **Spalte `recorded_at` neben `occurred_at`:** verworfen. Die Reihenfolge des Festhaltens trägt
  `position`. Ein zweiter Zeitstempel lädt dazu ein, ihn zum Sortieren zu nehmen oder
  `occurred_at` daran anzupassen.
- **`AUTOINCREMENT` für `position`:** verworfen. Es verhindert nur die Wiederverwendung
  gelöschter Nummern, und DELETE ist durch den Trigger ausgeschlossen.