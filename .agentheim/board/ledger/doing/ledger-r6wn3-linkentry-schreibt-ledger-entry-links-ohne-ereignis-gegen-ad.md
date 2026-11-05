---
id: ledger-r6wn3
title: "Spiegel verknüpft eine über die ImportId gefundene Buchung mit `EntryLinkedToYnab` statt über `linkEntry` ohne Ereignis; alte Verknüpfungen ohne Ereignis bekommen einmalig ihr Ereignis nachgetragen"
status: doing
type: bug
context: ledger
created: 2026-11-05
completed:
depends_on: []
blocks: [ledger-k9tm2]
tags: [event-sourcing, replay]
related_adrs: [0013, 0015, 0021, 0083]
related_research: []
prior_art: []
---

## Why
`linkEntry` (`src/Server/Ledger/Persistence.fs`) schreibt eine Zeile in `ledger_entry_links`,
ohne ein Ereignis anzuhängen. Einziger Aufrufer ist `resolveEntry` in
`src/Server/Ledger/CommandHandlers.fs`: Findet der Spiegel eine Buchung über ihre ImportId, trägt
er die YNAB-Id als Verknüpfung nach. Diese Zeile lässt sich per Replay nicht herstellen
(ADR 0013 Punkt 6), obwohl ADR 0083 `ledger_entry_links` als Read Model führt, das aus dem Log
neu gebaut werden kann. Für diesen Fall gibt es schon das Ereignis `EntryLinkedToYnab`
(ADR 0021), und `Domain.apply` sieht den Weg über die ImportId ausdrücklich vor (Kommentar am
Fall `EntryLinkedToYnab`). ledger-k9tm2 soll danach prüfen können, dass Replay die Link-Tabelle
vollständig herstellt.

## What
1. **Laufender Weg:** `resolveEntry` hängt bei einem Treffer über die ImportId
   `EntryLinkedToYnab { YnabTransactionId }` mit Actor `YnabMirror` an, über den gewohnten
   Schreibweg (`appendAndApply`, unter derselben Schreibsperre wie die übrigen Spiegel-Schreibvorgänge).
   Die Zeile in `ledger_entry_links` entsteht über `linksFrom` beim Anhängen. `linkEntry` hat
   danach keinen Aufrufer mehr und wird gelöscht.
2. **Alte Zeilen (Startschritt):** Für jede Zeile in `ledger_entry_links` mit `system = 'ynab'`,
   die kein gespeichertes Ereignis über `linksFrom` stiftet, hängt ein einmaliger Startschritt
   ein `EntryLinkedToYnab` an: Actor `YnabMirror`, `occurred_at` = Zeitpunkt des Startschritts
   (UTC), Payload die YNAB-Id aus der Zeile, `sequence` = nächste Nummer der Buchung. Der Schritt
   schreibt nur das Ereignis; die Zeile in `ledger_entry_links` und der Zustand in
   `ledger_entries` stehen schon (die Projektion würde am `UNIQUE` der Link-Tabelle scheitern).
   Alles in einer Transaktion. Fehlt danach nichts mehr, schreibt ein zweiter Start nichts.
3. Nachtrag in ADR 0021 (steht schon, siehe Notes): Herkunft dieser nachgetragenen Ereignisse.

## Acceptance criteria
- [ ] Regressionstest, vor dem Fix rot (Kommentar nennt den Bug): GIVEN eine Buchung, die eine
      ImportId-Verknüpfung trägt, WHEN der Spiegel eine Beobachtung mit dieser ImportId und einer
      YNAB-Id verarbeitet, THEN enthält das Log der Buchung ein `EntryLinkedToYnab` mit dieser
      YNAB-Id, und die Verknüpfungen aus `linksFrom` über das Log der Buchung enthalten die
      YNAB-Verknüpfung.
- [ ] Intakt-Szenario: Eine Beobachtung, die über die YNAB-Id trifft, hängt kein
      `EntryLinkedToYnab` an; eine neue Buchung aus dem Spiegel bekommt ihre Verknüpfung weiter
      über `EntryObservedInYnab`.
- [ ] `linkEntry` ist gelöscht; kein Aufrufer schreibt `ledger_entry_links` außerhalb des
      Anhängens.
- [ ] Startschritt: GIVEN eine YNAB-Verknüpfung ohne stiftendes Ereignis, WHEN der Startschritt
      läuft, THEN steht genau ein neues `EntryLinkedToYnab` mit Actor `YnabMirror` am Ende des
      Logs dieser Buchung, `ledger_entry_links` und `ledger_entries` sind unverändert, und ein
      zweiter Lauf hängt nichts an. Eine Verknüpfung, die schon aus einem Ereignis stammt, bekommt
      kein zusätzliches Ereignis.
- [ ] Nach dem Startschritt stimmen die YNAB-Verknüpfungen aus `linksFrom` über das ganze Log mit
      denen in `ledger_entry_links` überein (Test mit gemischtem Bestand).
- [ ] `./verify.sh` endet mit VERIFY: PASS.

## Notes
- Aufrufer-Prüfung (Verfeinerung 2026-11-05): `linkEntry` hat genau einen Aufrufer,
  `resolveEntry` in `src/Server/Ledger/CommandHandlers.fs` (Treffer über ImportId). Weitere
  Schreiber von `ledger_entry_links` gibt es nur im Anhängen (`linksFrom`) und in diesem Weg.
- `Domain.apply` für `EntryLinkedToYnab` setzt `LinkedToYnab = true` und löst einen offenen
  `PushToYnabFailure` auf. Für eine über die ImportId gefundene Buchung ist beides richtig; der
  Start-Backfill `backfillLinkedToYnab` setzt `linked_to_ynab` für diese Zeilen heute schon.
- Der Startschritt läuft in Produktion einmal, bevor ledger-k9tm2 das Log umzieht. ledger-k9tm2
  baut ihn danach zurück (Reihenfolge: r6wn3 deployen, dann k9tm2).
- Reihenfolge im Startschritt: nach dem Anlegen des Schemas, vor `rebuildSplitIndexes`. Die
  Ereignisse eines Laufs lassen sich über `linksFrom` aus `loadAllEvents` bestimmen; ein Laden
  je Buchung in einer Schleife ist zu vermeiden (Query Safety, kein N+1).
- Keine echten YNAB-Ids in Tests (öffentliches Repo).
