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
Entscheidung Roman (2026-11-09, Nachtrag ADR 0058 Punkt 2): **Stamm kürzen, dann das Suffix anhängen.**

- `replacementImportId` (`src/Server/Ledger/CommandHandlers.fs`) bildet die Ersatz-`import_id` als
  Stamm plus `:r<n>`, und kürzt den Stamm dabei von hinten so weit, dass das Ergebnis höchstens
  `ImportIdMaxLength` (36) Zeichen hat. Der Stamm ist wie heute die `import_id` ohne ihr `:r<n>`
  (`splitReplacementSuffix`). Kurze `import_id`s bleiben unverändert (`<alt>:r<n>`).
- Ein gekürzter Stamm ist immer ein Präfix des ursprünglichen. Jede weitere Änderung leitet sich wieder
  daraus ab; `currentImportId` findet die aktuelle weiter über die höchste Ersatz-Zahl.
- Die Ablehnung "Die import_id wäre mit dem Suffix länger als 36 Zeichen" in `setEntrySplits` fällt weg,
  weil sie nicht mehr eintreten kann.
- Warum das die Duplikat-Erkennung nicht stört: Zu lang sind nur `import_id`s mit 33 oder mehr Zeichen.
  Das sind Barbuchungen (`cashEntryImportId`, `BB:` plus EntryId, erkannt über BBs Verknüpfungen, nicht
  per Präfix) und Comdirect-Buchungen aus einem erzwungenen Import (`BB:` plus neue Guid, passen nie per
  Präfix zu einer Bank-Buchung). Normale Comdirect-`import_id`s (`generateImportId`) sind kurz und behalten
  ihren vollen Stamm, `matchesImportId` erkennt sie weiter.
- Nicht ändern: Aufteilen einer unaufgeteilten Buchung (PUT, ADR 0058 Punkt 1), die Sperre für fremde
  `import_id`s, `matchesImportId`.

## Acceptance criteria
- [ ] Rein: `replacementImportId` auf `BB:` plus 32 Zeichen ergibt höchstens 36 Zeichen, endet auf `:r1`,
      ihr Stamm ist ein Präfix des alten Stamms, und sie unterscheidet sich von der alten.
- [ ] Rein: auf die Ersatz-Id angewendet ergibt sich `:r2` mit höchstens 36 Zeichen, wieder anders; auch
      beim Übergang auf eine zweistellige Zahl (`:r9` zu `:r10`) bleibt es bei höchstens 36 Zeichen.
- [ ] Rein, intakt: eine kurze `import_id` (`BB:` plus 20 Zeichen) bekommt weiter genau `<alt>:r<n>`.
- [ ] Rein: `currentImportId` findet unter Verknüpfungen mit gekürztem Stamm die mit der höchsten Zahl.
- [ ] Handler-Test (kaputt): eine aufgeteilte Barbuchung mit `cashEntryImportId` wird geändert; DELETE und
      POST laufen, der POST trägt die gekürzte Ersatz-Id, die Verknüpfung der neuen Buchung ist gespeichert.
      Vor dem Fix rot (heute lehnt `setEntrySplits` ab).
- [ ] Handler-Test: eine zweite Änderung derselben Buchung schickt eine dritte, wieder andere `import_id`.
- [ ] Intakt: Aufheben des Splits einer Comdirect-Buchung mit kurzer `import_id` schickt weiter `<alt>:r1`
      (bestehender oder neuer Test).
- [ ] Die Tests zum Bug sind vor dem Fix rot (Test-Doktrin Punkt 8 und 9).
- [ ] BC-README ledger: die Stelle zur Ersatz-`import_id` nennt das Kürzen. Alte Sätze dazu an allen Stellen
      nachziehen (grep nach `:r<n>`, `import_id`, "36 Zeichen").
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Gefunden in ledger-p3vq8 (Worker), nicht dort behoben, weil das Format der Ersatz-`import_id` eine
  Entscheidung aus ADR 0058 ist.
- Nachtrag ADR 0058 geschrieben (2026-11-09).
- Code: `replacementImportId`, `splitReplacementSuffix`, `currentImportId`, `ImportIdMaxLength` und die
  Prüfung `tooLongImportId` in `src/Server/Ledger/CommandHandlers.fs`; `cashEntryImportId` ebenda;
  `generateImportId`, `matchesImportId` in `src/Shared/Domain.fs`; erzwungener Import in
  `src/Server/YnabClient.fs` (`forceNewImportId`).
- Die UI-Sperre (`splitLocksOf`) prüft die Länge nicht; nach dem Fix braucht sie es auch nicht.
