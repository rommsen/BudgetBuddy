---
id: ledger-m7k2d
title: "Split einer Barbuchung lässt sich nicht ändern: Ersatz-import_id wäre länger als 36 Zeichen"
status: backlog
type: bug
context: ledger
created: 2026-11-08
completed:
depends_on: []
blocks: []
tags: [ynab, split, quick-add, bot]
related_adrs: [0004, 0058, 0059]
related_research: []
prior_art: [ledger-p3vq8]
---

## Why
Seit ledger-p3vq8 tragen Quick-Add- und Bob-Buchungen die feste `import_id` `BB:` plus EntryId ohne Bindestriche (35 Zeichen, ADR 0004 Nachtrag). Das Ändern oder Aufheben eines Splits einer schon aufgeteilten Buchung läuft über DELETE und POST mit der Ersatz-`import_id` `<aktuelle>:r<n>` (ADR 0058 Punkt 2, `replacementImportId`). Bei 35 Zeichen ergibt das 38 Zeichen, und `setEntrySplits` lehnt mit "Die import_id wäre mit dem Suffix länger als 36 Zeichen" ab (`ImportIdMaxLength`, `src/Server/Ledger/CommandHandlers.fs`). Eine neue Barbuchung, die aufgeteilt wurde, lässt sich danach also nicht mehr ändern oder aufheben. Vorher hatten Barbuchungen keine `import_id`, die Ersatzbuchung ging ohne. Dasselbe trifft schon heute Comdirect-Buchungen aus einem erzwungenen Import (`BB:` plus Guid, ebenfalls 35 Zeichen).

## What
Offen, braucht eine Entscheidung (Nachtrag zu ADR 0058): wie die Ersatz-`import_id` aussieht, wenn die aktuelle schon 33 oder mehr Zeichen hat. Mögliche Wege: der Stamm wird gekürzt, bevor das Suffix angehängt wird (BB-eigene `import_id`s sind ohnehin eindeutig über die Guid), oder ein kürzeres Suffix-Schema. `currentImportId` muss die Ersatz-Kette danach weiter erkennen.

## Acceptance criteria
- [ ] Kaputt: eine Barbuchung mit `import_id` `BB:` plus 32 Zeichen, aufgeteilt, wird geändert. Der Ersatz läuft durch, die Ersatz-`import_id` hat höchstens 36 Zeichen und unterscheidet sich von der alten. Test rot vor dem Fix.
- [ ] Intakt: Comdirect-Buchungen mit kurzer `import_id` bekommen weiter `<alt>:r<n>`.
- [ ] Eine zweite Änderung derselben Buchung ergibt eine dritte, wieder andere `import_id` (`currentImportId` findet die aktuelle).

## Notes
- Gefunden in ledger-p3vq8 (Worker), nicht dort behoben, weil das Format der Ersatz-`import_id` eine Entscheidung aus ADR 0058 ist.