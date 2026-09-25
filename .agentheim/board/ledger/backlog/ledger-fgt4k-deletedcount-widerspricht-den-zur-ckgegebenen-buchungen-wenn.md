---
id: ledger-fgt4k
title: "DeletedCount widerspricht den zurückgegebenen Buchungen, wenn OnlyNeedsReview und IncludeDeleted zusammen aktiv sind"
status: backlog
type: bug
context: ledger
created: 2026-09-25
completed:
depends_on: []
blocks: []
tags: [backend, kassenbuch, lesepfad]
related_adrs: [0032]
related_research: []
prior_art: [ledger-qns4j]
---

## Why
Beim manuellen Verifizieren von `ledger-qns4j` (PC-Statusspalte, AC5) gegen eine
Kopie der echten Entwicklungsdatenbank fiel eine Diskrepanz in
`getLedgerEntriesPage` auf: mit `OnlyNeedsReview = true` und
`IncludeDeleted = true` gleichzeitig für ein Konto lieferte die Antwort
`DeletedCount = 1`, aber keine der zwei zurückgegebenen `Entries` trug
`DeletedInYnab = true`. Laut Doc-Kommentar auf `LedgerEntryPage.DeletedCount`
(`src/Shared/Domain.fs`) soll die Zahl "unabhängig von `IncludeDeleted`" die in
YNAB gelöschten Buchungen im *aktiven Filter* zählen (Konto plus
`OnlyNeedsReview`); bei aktivem `IncludeDeleted` sollten also die gezählten
gelöschten Buchungen auch tatsächlich unter den `Entries` auftauchen.

## What
Nachvollziehen, ob `DeletedCount` und `Entries` bei der Kombination
`OnlyNeedsReview = true` + `IncludeDeleted = true` tatsächlich auseinanderlaufen
(oder ob es an diesem Tag lediglich Paginierung/Sortierung war) und je nach
Befund `Ledger.Persistence`/den Lesepfad korrigieren oder den Doc-Kommentar
präzisieren.

Reproduktionsschritte (gegen eine Kopie der Entwicklungsdatenbank, NIE gegen
das Original):
1. Kassenbuch öffnen, ein Konto mit YNAB-Spiegel wählen, das sowohl ungeprüfte
   als auch in YNAB gelöschte Buchungen enthält.
2. Chip "Zu prüfen" aktivieren (`OnlyNeedsReview = true`).
3. Schalter "Gelöschte zeigen" aktivieren (`IncludeDeleted = true`).
4. Netzwerk-Antwort von `getLedgerEntriesPage` ansehen: `DeletedCount` stand
   bei 1, `Entries` enthielt zwei Buchungen, beide mit `DeletedInYnab: false`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Nicht selbst untersucht oder behoben im Rahmen von `ledger-qns4j`: das liegt im
Server-Lesepfad (`src/Server/`), außerhalb des Umfangs jener (reinen
Client-)Aufgabe, und im selben Bereich, den der parallele Worker
`ledger-xk9wq` im selben Batch anfasst (Server-Ledger-Persistenz/-Domäne).
Die konkreten Konto- und Buchungs-IDs sowie der Kontoname aus dem ursprünglichen
Repro sind hier absichtlich nicht wiedergegeben: sie stammten aus Romans echter
Entwicklungsdatenbank vom 2026-09-25. Wer diesen Task verfeinert, kann den Repro
mit eigenen (Test-)Daten gegen eine Kopie der Entwicklungsdatenbank nachstellen.