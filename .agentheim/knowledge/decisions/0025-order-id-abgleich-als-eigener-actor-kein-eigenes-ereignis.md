---
id: 0025
title: Order-ID-Abgleich als eigener Actor im Kassenbuch, kein eigenes Ereignis
scope: ledger
status: accepted
date: 2026-09-11
supersedes: []
superseded_by: []
related_tasks: [ledger-qvt84, categorization-gdrx8]
related_adrs: [0013, 0021]
related_research: []
---

## Context
`categorization-gdrx8` hat der Review-Spur (`Shared.Domain.ReviewStep`) einen dritten
`ReviewActor`-Fall gegeben: `ByOrderIdMatch of orderId: string`. Er markiert eine
Kategorie, die beim Abruf aus der YNAB-Historie derselben Amazon-Bestellung übernommen
wurde, ohne dass eine Regel oder Roman sie für DIESE Buchung entschieden hätte
(Entscheidung Roman, 2026-09-11). `ledger-qvt84` baut aus der Review-Spur beim Import-Push
die Kassenbuch-Ereignisfolge (`recordComdirectPush`, ADR 0021) und musste dafür einen
passenden Urheber im Kassenbuch finden.

Der bestehende Ereignis-Katalog (`ledger-cscw1`) kennt für Kategorisierungen zwei
Entscheidungs-Ereignisse: `EntryCategorizedByRule` (Actor `Rule`, nur auf einer noch
unkategorisierten Buchung) und `EntryRecategorized` (Actor `UserInBb`, Umkategorisierung
oder Erstkategorisierung durch einen Menschen). Für den Order-ID-Abgleich passte keins der
beiden Wortwörtlich: er ist weder ein Regel-Treffer noch Romans eigene Entscheidung für
diese Buchung.

## Decision
- `Ledger.Events.Actor` bekommt einen vierten Fall `OrderIdMatch of orderId: string`
  (Anzeigename "Order-ID-Abgleich", wie in der Review-Spur).
- Fachlich ist der Order-ID-Abgleich eine **Kategorisierung**, keine neue Ereignis-Art: er
  setzt Kategorie oder Splits auf einer Buchung, genau wie `EntryRecategorized`. Er
  bekommt deshalb **kein eigenes Ereignis**, sondern nutzt `EntryRecategorized` mit dem
  neuen Actor. Der Ereignis-**Name** sagt weiterhin "was passiert ist" (Kategorie/Splits
  geändert), der **Actor** sagt "wer": genau die Trennung, die ADR 0013 Punkt 3 für
  dieses Feld vorsieht.
- `Shared.Domain.LedgerOrigin` bekommt den passenden Lese-Fall `MatchedByOrderId`
  (`originLabel`: "Order-ID-Abgleich"), damit die Kassenbuch-Seite die Herkunft anzeigen
  kann, ohne den Actor-Typ des Servers zu kennen.
- Persistenz: die zweite Actor-Spalte (`ledger_entry_events.actor_rule_id`) ist
  freier Text, kein reiner Guid-Slot. Der Order-ID-Abgleich legt dort die Bestell-Id ab.
  Keine Schema-Änderung nötig.

## Consequences
### Positive
- Kein neuer Ereignis-Typ, keine neue Schema-Version, kein zusätzlicher `apply`-Zweig:
  `EntryRecategorized`s bestehende Invarianten (Kategorie/Splits-Ausschluss, `mkSplits`)
  gelten unverändert auch für diesen Fall.
- Die Historie bleibt vollständig: "Order-ID-Abgleich: Büro" ist über den Actor der
  Buchung zurückverfolgbar (ADR 0013, Auditierbarkeit).
- Symmetrisch zur Review-Spur: derselbe dritte Fall (`ByOrderIdMatch`/`OrderIdMatch`)
  auf beiden Seiten der Naht.

### Negative
- `EntryRecategorized`s Payload-Feldnamen ("Old/NewCategory" etc.) sind bewusst
  Actor-neutral formuliert; ein Leser des Codes ohne Actor-Kontext sieht nicht sofort,
  dass ein Ereignis auch vom Order-ID-Abgleich stammen kann. Der Actor im Umschlag
  bleibt die einzige, aber vollständige Quelle dafür.

### Neutral
- Die Historien-**Satzbildung** (`Ledger.QueryHandlers.ReadModel.describeRecategorized`)
  bleibt actor-unabhängig ("In BB umkategorisiert: ..."); die Herkunft zeigt sich über das
  separate `Origin`-Badge, nicht über den Satztext. Eine eigene Satzformulierung für
  diesen Actor ist möglich, aber kein Teil dieser Entscheidung.

## Alternatives considered
- **Eigenes Ereignis `EntryCategorizedByOrderIdMatch`**: abgelehnt. Fachlich identisch zu
  `EntryRecategorized` (Kategorie/Splits ändern sich, dieselben Invarianten gelten), ein
  eigener Typ hätte nur den Actor dupliziert, den das Feld bereits trägt.
- **`EntryCategorizedByRule` mit einer Pseudo-`RuleId`**: abgelehnt. Der Order-ID-Abgleich
  ist keine Regel; `EntryCategorizedByRule`s Invariante ("nur auf unkategorisierter
  Buchung") passt fachlich nicht, wenn ein Order-ID-Vorschlag später korrigiert wird.
