---
id: ledger-e3k7p
title: Kassenbuch-Fundament: Ereignis-Log, Zustandsmodell, appendAndApply und Replay-Test
status: todo
type: feature
context: ledger
created: 2026-09-06
depends_on: [ledger-fsz0a, ledger-z5vw6]
blocks: [ledger-m8r2v, ledger-kt0wq]
tags: [foundation, backend, persistence]
related_adrs: [0013, 0015, 0016, 0008]
related_research: []
prior_art: []
---

## Why
Slice 1 von 3 des Walking Skeletons (`ledger-kt0wq`, Aufteilung 2026-09-06). Alles Weitere
(Spiegel, Seite) setzt auf diesem Fundament auf: dem persistierten Log semantischer
Ereignisse (ADR 0013) und dem daraus abgeleiteten Zustandsmodell. Kein YNAB-Aufruf, keine
UI, nur das Gedächtnis selbst, mit Tests.

## What
1. **Ereignis-Typen** als F#-DU nach ADR 0013 und `.claude/rules/semantic-messaging.md`,
   Vergangenheitsform, gruppierte Granularität, reicher Payload. Für diesen Slice reicht die
   Beobachtungs-Familie des Spiegels (die Entscheidungs-Familie kommt mit späteren Slices):
   `EntryObservedInYnab` (Erstbeobachtung, voller Snapshot), Änderungs-Beobachtungen in
   sinnvollen Gruppen (Kategorie/Splits; Datum/Betrag/Payee/Memo; Cleared), und
   `EntryDeletionObservedInYnab`. Jedes Ereignis trägt Buchungs-Id, laufende Nummer pro
   Buchung, Zeitpunkt, **Actor** (DU: YnabMirror | ComdirectImport | Rule of RuleId |
   RomanInBb | QuickAdd; nur YnabMirror wird hier benutzt) und Schema-Version.
2. **Zustandsmodell**: Buchung (eigene Id per `Guid.CreateVersion7()`, Konto, Datum, Betrag,
   Payee, Memo, Kategorie-Id und -Name, Splits, Cleared, Übertrags-Verknüpfung,
   `DeletedInYnab`-Flag), Konto (minimal: Id, Name, YnabAccountId, Art nach ADR 0016 darf
   vorerst nur `Bankkonto` sein), Fremd-Id-Verknüpfungen (Tabelle Buchungs-Id, System
   `ynab | importid | comdirect`, Wert, unique je System) nach ADR 0015.
3. **`apply : EntryState option -> LedgerEvent -> EntryState`** rein in `src/Server/Domain.fs`
   (oder einem neuen `LedgerDomain.fs` vor `Persistence.fs`, wenn Domain.fs zu voll wird).
4. **Persistence**: Tabellen per Migration in `Persistence.fs` (Muster der bestehenden
   Tabellen), `appendAndApply : LedgerEvent list -> Async<unit>` schreibt Ereignisse und
   Zustand auf **einer** Connection in **einer** SQLite-Transaktion (ADR 0013 Punkt 5, kein
   Widerspruch zu ADR 0008), `loadEventsForEntry`, `loadAllEvents`, `replayAll` (baut den
   Zustand aus dem Log neu). Payload als JSON-Text, keine SQLite-JSON-Funktionen in Queries.
5. **Tests** (Expecto, In-Memory-SQLite wie die bestehenden Persistence-Tests): Ereignis
   anhängen und Zustand lesen; zwei Ereignisse auf eine Buchung ergeben zwei Historie-
   Einträge mit steigender Nummer; Löschung setzt Flag statt zu löschen; Replay aus dem Log
   ergibt denselben Zustand wie die Tabellen; Transaktion: schlägt das Zustands-Update fehl,
   steht auch kein Ereignis im Log; Fremd-Id ist unique je System.

## Acceptance criteria
- [ ] Ereignis-DU, Actor-DU, Zustands-Records und Verknüpfungs-Typ existieren, semantisch
      benannt (kein CRUD-Naming), Ereignisse tragen Schema-Version.
- [ ] `appendAndApply` schreibt Log und Zustand in einer SQLite-Transaktion; Test beweist
      Atomarität.
- [ ] `replayAll` reproduziert den Zustand aus dem Log; Test vergleicht beide.
- [ ] Buchungs-Id ist `Guid.CreateVersion7()`; Fremd-Ids liegen in eigener Tabelle, unique
      je System.
- [ ] Keine Änderung an bestehenden Tabellen, Endpunkten oder UI.
- [ ] `dotnet build` und `dotnet test` grün; Tests nur gegen In-Memory-SQLite.
- [ ] `ledger/README.md`: Abschnitt "Ereignisse" mit der Liste der Ereignis-Typen und dem
      Actor-DU, damit die Sprache dokumentiert ist.

## Notes
Die genaue Gruppierung der Änderungs-Beobachtungen ist Modellierungsarbeit in diesem Slice
(ADR 0013 Punkt 4); Leitlinie: eine Gruppe pro fachlich zusammengehöriger Änderung, nicht
pro Feld. Snapshot-Felder im Contract siehe ADR 0014 (Kategorie- und Payee-Name zum
Beobachtungszeitpunkt mitführen). Der Spiegel (`ledger-m8r2v`) erzeugt später aus
Snapshot-Vergleich die Ereignisse; hier reicht es, dass die Typen und der Schreibpfad stehen.
