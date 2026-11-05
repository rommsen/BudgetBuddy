---
id: ledger-k9tm2
title: "Kassenbuch-Ereignisse nach `events` überführen: Kopie in rowid-Reihenfolge mit Abbruchprüfung, `appendAndApply` und Ladepfade umstellen, `ledger_entry_events` danach entfernen"
status: backlog
type: feature
context: ledger
created: 2026-11-05
completed:
depends_on: [infrastructure-e4vq7]
blocks: []
tags: [event-sourcing, migration, persistence]
related_adrs: [0013, 0021, 0068, 0083]
related_research: []
prior_art: []
---

## Why
ADR 0083 Abschnitt 6: Das Kassenbuch-Log zieht von `ledger_entry_events` in die gemeinsame
Tabelle `events`. Das muss geschehen, bevor das erste Budget-Ereignis geschrieben wird, damit
`position` über die ganze Historie die Reihenfolge des Festhaltens ist. Die Umsetzungs-Tasks aus
budgeting-zkbbd warten darauf.

## What
- Startmigration in einer Transaktion: `ledger_entry_events` in `rowid`-Reihenfolge lesen, je
  Buchung prüfen, dass `sequence` ab 1 um genau 1 steigt, sonst Abbruch (BB startet nicht).
  Abbildung nach ADR 0083 Abschnitt 6 (`context = 'ledger'`, `stream_id = 'ledger:entry:<id>'`,
  `stream_version = sequence`, `actor_ref = actor_rule_id`, neue zufällige `event_id`).
  Danach `ledger_entry_events` in derselben Transaktion entfernen. Läuft nur, wenn die alte
  Tabelle existiert.
- Umstellen auf das generische Anhängen und Laden aus infrastructure-e4vq7: `appendAndApply`,
  `loadEventsForEntry`, `loadAllEvents`, `loadBobBookedAmountsBetween`,
  `backfillTransferAccountId`, `EventRow`, die Tests in `LedgerTests.fs`, die das Log direkt
  lesen.
- `src/Shared` bleibt unverändert (`LedgerHistoryLine`).

## Acceptance criteria
- [ ] Replay vor und nach der Migration ergibt denselben Zustand, und beide sind gleich
      `ledger_entries` (samt Split- und Link-Tabellen).
- [ ] Payload, `event_type` und `schema_version` sind je Ereignis vor und nach der Migration
      gleich, ebenso `occurred_at` und Actor.
- [ ] `stream_version` ist je Buchung lückenlos ab 1.
- [ ] Ein Test mit vertauschter rowid-Reihenfolge (sequence fällt) lässt die Migration abbrechen,
      ohne dass `events` oder `ledger_entry_events` verändert sind.
- [ ] Die Bob-Tagesgrenze (`loadBobBookedAmountsBetween`) liefert vor und nach der Migration
      dieselben Summen für denselben Zeitraum; die Grenzen aus ADR 0068 verschieben sich nicht.
- [ ] Probe gegen eine Kopie der Produktions-Datenbank vor dem Deploy, außerhalb des Repos;
      Ergebnis (Zahl der Ereignisse vorher und nachher, Replay gleich) steht im Outcome ohne
      echte Daten.
- [ ] `./verify.sh` endet mit VERIFY: PASS.

## Notes
- Risiko Startmigration: Schlägt die Abbruchprüfung in Produktion an, startet BB nicht. Deshalb
  die Probe vorher. Sobald neue Ereignisse in `events` stehen, geht ein Rollback nur über das
  Backup.
- Reports und Ladepfade sortieren fachlich nach Datum oder `occurred_at`, nie nach `position`
  (ADR 0083, Pflichtsatz).
- Wandern neue Felder nach `src/Shared`, im selben Schritt `scripts/ledger-detail.test.mjs`
  nachziehen.
- Aus infrastructure-e4vq7 (Conductor, 2026-11-05): `EventStore.initializeSchema ()` läuft bisher nur in `Program.fs`, in `src/Tests/EventStoreTests.fs` und in den beiden Tests, die den Serverstart nachbauen (`RemotingPipelineTests`, `RemotingEndpointNegativeTests`). Beim Umzug in jedes Testmodul aufnehmen, das `initializeLedgerSchema` aufruft. Für die Startmigration gibt es `EventStore.appendOn conn tx` (offene Transaktion, kein Commit).
