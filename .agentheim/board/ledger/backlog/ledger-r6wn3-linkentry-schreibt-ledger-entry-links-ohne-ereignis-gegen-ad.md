---
id: ledger-r6wn3
title: "`linkEntry` schreibt `ledger_entry_links` ohne Ereignis: gegen ADR 0013 und 0021 prüfen"
status: backlog
type: bug
context: ledger
created: 2026-11-05
completed:
depends_on: []
blocks: []
tags: [event-sourcing, replay]
related_adrs: [0013, 0021, 0083]
related_research: []
prior_art: []
---

## Why
`linkEntry` (`src/Server/Ledger/CommandHandlers.fs`) schreibt eine Zeile in `ledger_entry_links`,
ohne ein Ereignis anzuhängen. ADR 0021 Punkt 4 verlangt für die YNAB-Verknüpfung ein eigenes
Ereignis, weil ein Tabelleneintrag ohne Ereignis sich per Replay nicht rekonstruieren lässt
(ADR 0013 Punkt 6). ADR 0083 nennt `ledger_entry_links` als Read Model, das sich aus dem Log neu
bauen lassen muss. Aufgefallen im Refinement von infrastructure-c7f53.

## What
- Prüfen, welche Aufrufer `linkEntry` hat und welche Verknüpfungen (System, Wert) es schreibt.
- Klären, ob diese Verknüpfungen aus einem Ereignis entstehen müssen oder ob es einen Grund für
  den Weg ohne Ereignis gibt. Ergebnis als Nachtrag oder Fix.
- Falls ein Fix nötig ist: Verknüpfung über ein Ereignis schreiben, Replay deckt die Zeile ab.

## Acceptance criteria
- [ ] Für jeden Aufrufer von `linkEntry` ist festgehalten, ob die Zeile aus einem Ereignis
      ableitbar ist.
- [ ] Ist sie es nicht: ein Regressionstest, der vor dem Fix rot ist, zeigt, dass Replay
      `ledger_entry_links` nicht vollständig herstellt; nach dem Fix ist er grün.
- [ ] `./verify.sh` endet mit VERIFY: PASS.

## Notes
- Unabhängig von der Migration nach `events` (ledger-k9tm2), kann davor oder danach laufen.