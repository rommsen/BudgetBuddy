---
id: ledger-t7v2q
title: Übertrag: die beiden Buchungen miteinander verknüpfen
status: backlog
type: feature
context: ledger
created: 2026-09-06
depends_on: [ledger-m8r2v]
blocks: []
tags: [ledger, spiegel, uebertrag]
related_adrs: [0013, 0015]
related_research: []
prior_art: [ledger-e3k7p]
---

## Why
ADR 0015 sagt: ein Übertrag entsteht im Kassenbuch als **zwei** Buchungen, und "die
Verbindung zwischen ihnen wird als eigene Verknüpfung mitgeführt (Buchung-zu-Buchung
statt Buchung-zu-Fremdsystem)". Das Fundament (`ledger-e3k7p`) hat das Feld angelegt
(`Entry.TransferEntryId`, Spalte `ledger_entries.transfer_entry_id`), füllt es aber nie.

Grund: `EntryObservedInYnab` trägt laut verbindlichem Ereignis-Katalog YNABs
`transfer_account_id` und `transfer_transaction_id`, also Fremd-Ids. Die Übersetzung in
die eigene Buchungs-Id der Gegenseite braucht einen Lookup über die
Fremd-Id-Verknüpfungen, und die Gegenbuchung ist zum Zeitpunkt der ersten Beobachtung
oft noch gar nicht angelegt. `apply` ist rein und kann das nicht. Es fehlt also ein
Ereignis, das die Verknüpfung ausspricht, sobald beide Seiten existieren. Der Katalog
sieht dafür heute keins vor, deshalb ist das Modellierungsarbeit, kein Implementierungs-
Detail.

Solange das offen ist, zeigt das Kassenbuch einen Übertrag als zwei unverbundene
Buchungen. Für v1 ist das laut ADR 0015 hinnehmbar ("Die Sicht 'ein Übertrag ist eine
Bewegung' ist v2-Arbeit"), aber die Verknüpfung selbst ist ausdrücklich v1.

## What
1. Mit Roman das Ereignis modellieren, das die Verknüpfung stiftet: Name (Beobachtung
   oder Entscheidung?), Payload, Auslöser. Kandidat für die Beobachtungs-Familie, weil
   YNAB die Verbindung feststellt, nicht BB. Den Katalog im `ledger/README.md` unter
   "Ereignisse" entsprechend ergänzen.
2. Das Ereignis in `src/Server/Ledger/Events.fs` ergänzen und in `apply` behandeln
   (setzt `TransferEntryId` auf beiden Seiten).
3. Im Spiegel-Lauf (`ledger-m8r2v`) erzeugen: nach dem Verarbeiten eines Snapshots die
   Gegenseite über `findEntryByLink YnabSystem <transfer_transaction_id>` suchen und,
   wenn sie da ist, für beide Buchungen das Verknüpfungs-Ereignis anhängen.
4. Idempotenz: eine bereits gesetzte Verknüpfung erzeugt kein weiteres Ereignis.

## Acceptance criteria
- [ ] Der Ereignis-Katalog im `ledger/README.md` nennt das neue Ereignis mit Payload.
- [ ] Zwei gespiegelte Buchungen, die YNAB als Übertrag führt, tragen nach dem
      Spiegel-Lauf wechselseitig die `TransferEntryId` der jeweils anderen.
- [ ] Ein zweiter Spiegel-Lauf über dieselben Daten erzeugt kein weiteres Ereignis.
- [ ] Der Replay-Test aus ADR 0013 bleibt grün: `replayAll` reproduziert auch die
      gesetzte Verknüpfung.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Vorarbeit siehe `ledger-e3k7p`: Feld, Spalte und Fremd-Id-Lookup
(`Ledger.Persistence.findEntryByLink`) stehen bereits, es fehlt nur das Ereignis und
sein Auslöser.
