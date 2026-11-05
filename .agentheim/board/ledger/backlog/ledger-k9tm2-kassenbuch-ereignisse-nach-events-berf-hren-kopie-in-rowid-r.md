---
id: ledger-k9tm2
title: "Kassenbuch-Ereignisse nach `events` überführen: Kopie in rowid-Reihenfolge mit Abbruchprüfung, `appendAndApply` und Ladepfade umstellen, `ledger_entry_events` danach entfernen"
status: backlog
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
