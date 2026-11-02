---
id: infrastructure-h4q8v
title: "Wächter ledger-read-retry: mögliches Lastrennen bei abortWindowMs 200"
status: backlog
type: bug
context: infrastructure
created: 2026-11-02
completed:
depends_on: []
blocks: []
tags: [test, retry]
related_adrs: []
related_research: []
prior_art: []
---

## Why
`scripts/ledger-read-retry.test.mjs:223` schlug einmal in einem Gate-Lauf von budgeting-p4n6r fehl ("getLedgerAccountBalances: keine Wiederholung", Fall `/ledger`, "übersteht einen Abbruch"). Die Datei war dort unverändert. In den folgenden Läufen trat es nicht wieder auf.

Vermutung des Verifiers, unbelegt: Der zweite Startaufruf kommt unter Last nach mehr als 200 ms und wird beantwortet statt abgebrochen.

## What
Ursache klären. Trifft die Vermutung zu, hängt der Wächter nicht mehr von der Zeit ab, sondern bricht etwa nach Aufrufzahl ab.

## Acceptance criteria
- [ ] Die Ursache ist belegt, zum Beispiel durch Reproduktion unter CPU-Last, oder die Vermutung ist widerlegt.
- [ ] Der Wächter besteht unter Last wiederholt und wird rot, wenn die Wiederholung im Produktionscode fehlt.
## Beobachtung
Zweites Auftreten am 2026-11-02 im Verifier-Lauf von budgeting-h3k9d: `getAllRules (/rules) übersteht einen Abbruch`, 5-s-Timer in `scripts/ledger-read-retry.test.mjs:252`, einzeln dreimal grün. Das Gate meldet damit ab und zu rot, ohne dass der Code es ist. Vor den nächsten großen Tasks klären.
