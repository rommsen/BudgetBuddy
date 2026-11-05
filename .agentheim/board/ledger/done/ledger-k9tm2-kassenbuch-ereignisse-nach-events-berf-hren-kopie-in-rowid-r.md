---
id: ledger-k9tm2
title: "Kassenbuch-Ereignisse nach `events` überführen: Kopie in rowid-Reihenfolge mit Abbruchprüfung, `appendAndApply` und Ladepfade umstellen, `ledger_entry_events` danach entfernen"
status: done
type: feature
context: ledger
created: 2026-11-05
completed:
depends_on: [infrastructure-e4vq7, ledger-r6wn3]
blocks: []
tags: [event-sourcing, migration, persistence]
related_adrs: [0013, 0021, 0068, 0083]
related_research: []
prior_art: []
---

## Why
ADR 0083 Abschnitt 6: Das Kassenbuch-Log zieht von `ledger_entry_events` in die gemeinsame
Tabelle `events`. Das muss geschehen, bevor das erste Budget-Ereignis geschrieben wird, damit
`position` über die ganze Historie die Reihenfolge des Festhaltens ist. budgeting-hyn77 und die
Tasks dahinter warten darauf.

## What

### 1. Startmigration
- Funktion auf einer offenen Connection (`migrateLedgerEventsOn conn`), damit Tests und die Probe
  sie auf eigener Datenbank aufrufen können. Läuft nur, wenn `ledger_entry_events` existiert.
- In einer Transaktion: `ledger_entry_events` nach `rowid` lesen. Je Buchung muss `sequence` ab 1
  um genau 1 steigen, sonst Abbruch mit Meldung (Buchung, erwartete und gefundene Nummer); BB
  startet dann nicht, und keine der beiden Tabellen ist verändert.
- Kopie per SQL-Parameter, ohne `EventStore.validate`: `occurred_at`, `actor_kind`,
  `event_type`, `schema_version` und `payload` gehen als **unveränderter Text** nach `events`
  (ADR 0083: "unverändert übernommen"). `context = 'ledger'`,
  `stream_id = EventStore.streamIdOf "ledger" "entry" (GuidId entryId)`,
  `stream_version = sequence`, `actor_ref = actor_rule_id`, `command_id` leer, `event_id` neu
  und zufällig (im F#-Code erzeugt). Einfügen in rowid-Reihenfolge, so vergibt SQLite `position`
  in der echten Einfügereihenfolge. Kein INSERT je Zeile in eigener Abfrage-Schleife ohne
  Transaktion; ein `Execute` mit Parameter-Array ist in Ordnung.
- Danach `DROP TABLE ledger_entry_events` (samt Index) in derselben Transaktion.

### 2. Schema
- `createLedgerSchemaOn` legt `ledger_entry_events` nicht mehr an; sonst entstünde sie bei jedem
  Start neu und leer.
- `initializeLedgerSchema` ruft zuerst `EventStore.initializeSchema ()`, ebenso legt
  `createLedgerSchemaOn conn` die Ereignistabelle über `EventStore.createSchemaOn conn` auf der
  übergebenen Connection an. Damit braucht kein Testmodul einen eigenen Aufruf (das Kassenbuch
  hängt an `events`).
- Reihenfolge beim Start: Schema, Migration, dann die Backfills, dann `rebuildSplitIndexes`.

### 3. Umstellen auf `events`
- `appendAndApply` schreibt über `EventStore.append` (oder `appendOn` in seiner Transaktion) mit
  `context = "ledger"`, `aggregate = "entry"`, `GuidId entryId`. Links (`linksFrom`), Zustand und
  Split-Indizes laufen als Read-Model-Funktion in derselben Transaktion. Die fachliche
  Schreibsperre (`WriteGuard`) bleibt.
- Lesen: `loadEventsForEntry` (nach `stream_version`), `loadAllEvents` (je Stream nach
  `stream_version`), `loadBobBookedAmountsBetween` (Filter auf `context = 'ledger'`, Actor `Bob`,
  `CashExpenseAdded`, `occurred_at`-Bereich wie bisher), `loadEntryOrigins` (erstes Ereignis je
  Buchung, `stream_version = 1`; fehlte in ADR 0083), `backfillTransferAccountId`.
- `EventRow` und `sequence` werden durch den Zeilentyp und `StreamVersion` aus `EventStore`
  ersetzt, soweit `RecordedLedgerEvent` sie braucht. `src/Shared` bleibt unverändert
  (`LedgerHistoryLine`).
- Der Startschritt aus ledger-r6wn3 (nachgetragene `EntryLinkedToYnab`) hat in Produktion
  gelaufen und wird zurückgebaut, samt seinen Tests.
- Tests, die `ledger_entry_events` direkt anlegen, lesen oder leeren
  (`LedgerTests.fs`, `LedgerWithoutCategoryTests.fs`, `LedgerLenaFlagTests.fs` und weitere per
  `grep`), lesen künftig `events` oder bauen den alten Zustand für den Migrationstest gezielt auf.

### 4. Probe gegen Produktionsdaten
- Eine Probe-Funktion auf einer Connection meldet: Zahl der Ereignisse vorher und nachher, Zahl
  der Buchungen, ob jedes Ereignis Typ, Version, Payload, `occurred_at` und Actor behalten hat, ob
  `stream_version` je Buchung lückenlos ist, ob Replay vorher und nachher gleich ist und ob das
  Replay danach `ledger_entries`, die Split-Tabellen und `ledger_entry_links` ergibt.
- Ein Opt-in-Test ruft sie auf einer **Kopie** der Datei aus der Umgebungsvariable
  `LEDGER_MIGRATION_PROBE_DB` auf und schreibt die Zahlen ins Testprotokoll, ohne Beträge oder
  Namen. Ohne die Variable läuft er nicht; er steht in einer Liste "(requires .env)" wie die
  übrigen Opt-in-Tests.

## Acceptance criteria
- [ ] Migrationstest: Replay vor und nach der Migration ergibt denselben Zustand, und dieser ist
      gleich `ledger_entries`, `ledger_entry_split_categories`, `ledger_entry_split_transfers` und
      `ledger_entry_links`.
- [ ] Je Ereignis sind `event_type`, `schema_version`, `payload`, `occurred_at` (als Text),
      `actor_kind` und `actor_rule_id`/`actor_ref` vor und nach der Migration gleich; die Zahl der
      Ereignisse ist gleich; `position` folgt der rowid-Reihenfolge.
- [ ] `stream_version` ist je Buchung lückenlos ab 1.
- [ ] Ein Test mit vertauschter rowid-Reihenfolge (sequence fällt) lässt die Migration
      abbrechen, ohne dass `events` oder `ledger_entry_events` verändert sind.
- [ ] Ein Ereignis mit `occurred_at` ohne Zonenangabe wird unverändert kopiert und blockiert die
      Migration nicht.
- [ ] Ein zweiter Start nach der Migration ändert nichts (die alte Tabelle fehlt, es entsteht
      keine neue).
- [ ] Die Bob-Tagesgrenze (`loadBobBookedAmountsBetween`) liefert vor und nach der Migration
      dieselben Summen für denselben Zeitraum; die Grenzen aus ADR 0068 verschieben sich nicht
      (bestehende Bot-Tests bleiben grün).
- [ ] `loadEntryOrigins` liefert vor und nach der Migration dieselbe Herkunft je Buchung.
- [ ] Neue Kassenbuch-Ereignisse landen in `events` mit `context = 'ledger'` und fortlaufender
      `stream_version` hinter den migrierten; `ledger_entry_events` existiert nicht mehr.
- [ ] Kein Code und kein Test in `src/` nennt `ledger_entry_events` außer der Migration und
      ihren Tests.
- [ ] Probe gegen eine Kopie der Produktions-Datenbank vor dem Deploy (Conductor, außerhalb des
      Repos): Ergebnis (Ereignisse vorher und nachher, Buchungen, Replay gleich, Zustand gleich)
      steht im Outcome ohne echte Daten.
- [ ] `./verify.sh` endet mit VERIFY: PASS.

## Notes
- Risiko Startmigration: Schlägt die Abbruchprüfung in Produktion an, startet BB nicht. Deshalb
  die Probe vorher. Sobald neue Ereignisse in `events` stehen, geht ein Rollback auf eine ältere
  Version nur über das Backup (eine alte Version legte `ledger_entry_events` leer neu an).
- Warum keine Kopie über `appendOn`: `EventStore.validate` verlangt seit dem Review von
  infrastructure-e4vq7 einen Zeitpunkt in UTC. Ein altes Ereignis, dessen Text beim Einlesen nicht
  als UTC ankommt, würde sonst den Start blockieren, und der Text ginge über Parsen und Formatieren
  nicht mehr unverändert hinüber.
- Probe-Ablauf (Conductor, nach PASS des Verifiers, vor dem Deploy, Entscheidung Roman
  2026-11-05): Datenbank per ssh vom Docker-Host in den Session-Scratchpad kopieren,
  `LEDGER_MIGRATION_PROBE_DB=<pfad> ` Opt-in-Test laufen lassen, Kopie danach löschen. Keine Host-
  oder Pfadangaben ins Repo.
- Voraussetzung: ledger-r6wn3 ist in Produktion deployt und sein Startschritt gelaufen, sonst
  stimmt `ledger_entry_links` nicht mit dem Replay überein.
- Reports und Ladepfade sortieren fachlich nach Datum oder `occurred_at`, nie nach `position`
  (ADR 0083, Pflichtsatz).
- Wandern neue Felder nach `src/Shared`, im selben Schritt `scripts/ledger-detail.test.mjs`
  nachziehen.
- Aus infrastructure-e4vq7: Für Startmigrationen gibt es `EventStore.appendOn conn tx`; hier wird
  bewusst direkt kopiert (siehe oben). Für neue Ereignisse ist `append`/`appendOn` der Weg.

## Outcome

Das Kassenbuch-Log ist von `ledger_entry_events` nach `events` umgezogen (ADR 0083 Abschnitt 6). `./verify.sh` endet mit VERIFY: PASS: dotnet 3928 grün (main vorher 3922; minus 5 Tests des zurückgebauten r6wn3-Startschritts, plus 11 neue Migrationstests), 12 Opt-in-Tests übersprungen (vorher 11, neu ist die Probe), node 310 grün.

**Produktionscode**
- `src/Server/Ledger/Persistence.fs`
  - `migrateLedgerEventsOn conn` (internal): läuft nur, wenn `ledger_entry_events` existiert. Die Prüfung auf die Tabelle passiert zuerst ohne Transaktion und danach noch einmal in der Transaktion. Die Funktion liest nach `rowid` und prüft, dass die Buchungs-Id eine Guid im Format D ist und dass `sequence` je Buchung ab 1 um genau 1 steigt. Bei einer Abweichung wirft sie `Kassenbuch-Migration abgebrochen: Buchung <id>: erwartet laufende Nummer N, gefunden M (rowid R)`, und das Dispose der nicht bestätigten Transaktion lässt beide Tabellen unverändert. Die Kopie läuft als ein `Execute` mit Parameter-Array, unverändert als Text: `occurred_at`, `actor_kind`, `event_type`, `schema_version` und `payload`. Außerdem `actor_ref = actor_rule_id`, `stream_version = sequence`, `command_id` NULL und eine neue `event_id`. Danach folgen `DROP INDEX` und `DROP TABLE` in derselben Transaktion.
  - `createLedgerSchemaOn` legt die alte Tabelle nicht mehr an. Es ruft `EventStore.createSchemaOn conn`, dann die Migration, dann `migrateLedgerEntryColumns` mit den Backfills. `initializeLedgerSchema` ruft zuerst `EventStore.initializeSchema ()`, dann `createLedgerSchemaOn` und danach `rebuildSplitIndexes`.
  - `appendAndApplyOn` schreibt über `EventStore.appendOn` mit einem Stream-Abschnitt je Ereignis, damit die Reihenfolge des Aufrufers erhalten bleibt. Die Read Models (`linksFrom`-Links, Zustand, Split-Indizes) laufen in `applyLedgerReadModels` als `ApplyReadModels` in derselben Transaktion; sie dekodieren die gespeicherten Ereignisse. `WriteGuard` ist unverändert.
  - Ladepfade: `loadEventsForEntry` über `EventStore.loadStream`. `loadAllEvents` und `loadAllEventsOn` lesen `context = 'ledger'`, sortiert nach `stream_id, stream_version`. `loadBobBookedAmountsBetween` (neu mit `...On conn`) filtert auf `context = 'ledger'`, Actor `Bob`, `CashExpenseAdded` und denselben Textvergleich auf `occurred_at`. `loadEntryOrigins` (neu mit `...On conn`) liest `stream_version = 1`. `backfillTransferAccountId` liest `events`. Kein Pfad sortiert nach `position`, außer der Probe, die die Einfügereihenfolge prüft.
  - `EventRow` ist entfernt. Gelesen wird `EventStore.StoredEventRow` (in Ledger über einen Typalias, damit der Dapper-Wächter den Namen erkennt). `RecordedLedgerEvent.Sequence` bleibt als Feld bestehen und wird aus `StreamVersion` gefüllt, `src/Shared` ist unverändert.
  - Der Startschritt `recordMissingYnabLinkEvents` ist zurückgebaut, ebenso `insertEventRow`.
  - `probeLedgerEventMigrationOn conn` (internal) liefert `LedgerEventMigrationProbe`: Ereignisse vorher und nachher, Buchungen, Zeilen in `ledger_entries` sowie Wahrheitswerte für unveränderten Umschlag (inklusive der Prüfung, dass `position` der rowid folgt), lückenlose `stream_version`, gleiches Replay vorher und nachher, Replay gleich `ledger_entries`, gleich den Split-Tabellen, gleich `ledger_entry_links` und entfernte alte Tabelle.
- `src/Server/EventStore.fs`: `StoredEventRow` und `toRecordedEvent` (vorher `rowToRecorded`) sind öffentlich, damit Kontexte eigene gefilterte Abfragen auf `events` lesen können.

**Tests**
- Neu: `src/Tests/LedgerEventMigrationTests.fs`, Liste "Kassenbuch-Migration nach events" mit 10 Fällen: Replay und Read Models mit Probe; Gegenprobe, dass die Probe veränderten Zustand und fehlende Links meldet; Umschlag je Ereignis und rowid-Reihenfolge; lückenlose Versionen; Abbruch bei fallender `sequence` ohne Änderung an beiden Tabellen; `occurred_at` ohne Zone; zweiter Start; `initializeLedgerSchema` ohne alte Tabelle; Bob-Summen vorher (alte SQL-Abfrage) und nachher, inklusive eines fremden Kontexts, der nicht mitzählt; `loadEntryOrigins` vorher und nachher; neue Ereignisse hinter den migrierten. Der Stand vor der Migration entsteht über `toLegacyLog`, das die alte Tabelle aus `events` füllt und `events` per DROP neu anlegt.
- Opt-in-Liste "Kassenbuch-Migration: Probe gegen eine Kopie (requires .env)": ohne `LEDGER_MIGRATION_PROBE_DB` wird sie übersprungen. Mit der Variable kopiert sie die Datei samt eventuell vorhandenem `-wal`/`-shm` ins Temp-Verzeichnis, migriert nur die Kopie, gibt nur Zahlen und Wahrheitswerte aus und löscht die Kopie danach. Lokal geprüft gegen eine synthetische Datei mit leerer alter Tabelle: alles true, die Originaldatei blieb unverändert.
- Angepasst: `LedgerMirrorTests.fs` (Liste des r6wn3-Startschritts entfernt). `LedgerLenaFlagTests.fs`: die Vorbereitung löscht nicht mehr Log und Zustand, weil `events` append-only ist. Sie hängt stattdessen je Lena-Buchung früherer Fälle ein `EntryFlagChangedInBb` ohne Flag an, so bleiben Log und Zustand konsistent. `LedgerWithoutCategoryTests.fs`: der Backfill-Test schreibt sein Ereignis nach `events`. `LedgerPageTests.fs`: 13 Ereignis-Zeitpunkte in Testdaten sind jetzt UTC, weil `EventStore.validate` UTC verlangt. Das Buchungsdatum ist unverändert.
- `grep ledger_entry_events src` trifft nur noch den Migrationsabschnitt in `Ledger/Persistence.fs` und `LedgerEventMigrationTests.fs`. Einen Strukturwächter dafür gibt es bewusst nicht, weil die Test-Doktrin Punkt 7 Rückbau-Wächter verbietet.

**Probe (Conductor, vor dem Deploy)** im Repo-Root, nach dem Build:
```
dotnet build src/Tests/Tests.fsproj --nologo -v q
LEDGER_MIGRATION_PROBE_DB=<pfad-zur-kopie.db> USE_MEMORY_DB=true TZ=Europe/Berlin dotnet src/Tests/bin/Debug/net10.0/Tests.dll --filter-test-list "Kassenbuch-Migration: Probe gegen eine Kopie"
```
Die Ausgabe "Kassenbuch-Migrationsprobe:" mit Zählern und Wahrheitswerten gehört ohne echte Daten in dieses Outcome. Das Ergebnis gegen die Produktionskopie steht noch aus (Akzeptanzkriterium für den Conductor).

**Hinweise**
- Neue Kassenbuch-Ereignisse brauchen jetzt einen UTC-Zeitpunkt. Produktion nutzt `DateTime.UtcNow`, nur Testdaten waren betroffen.
- Rollback auf eine ältere Version geht nach dem Deploy nur über das Backup.

### Für den Conductor
- ADR 0083 Abschnitt 6 könnte einen kurzen Nachtrag bekommen. Die Migration läuft innerhalb von `createLedgerSchemaOn` vor den Spalten-Backfills, und `appendAndApply` verlangt über `EventStore.validate` jetzt UTC-Zeitpunkte. `RecordedLedgerEvent.Sequence` blieb als Feldname bestehen und wird aus `stream_version` gefüllt.

### Probe gegen die Produktionskopie (Conductor, 2026-11-05 16:41, vor dem Deploy)
Kopie der Produktions-Datenbank im Session-Scratchpad, Opt-in-Test mit `LEDGER_MIGRATION_PROBE_DB`,
Kopie danach gelöscht; die Eingabedatei blieb unverändert (gleiche Prüfsumme vorher und nachher).
- Ereignisse vorher 15696, nachher 15696; Buchungen im Log 14221, Zeilen in `ledger_entries` 14221.
- EnvelopeUnchanged, StreamVersionsGapless, ReplayUnchanged, ReplayMatchesEntries,
  ReplayMatchesSplitIndexes, ReplayMatchesLinks, LegacyTableGone: alle true.
