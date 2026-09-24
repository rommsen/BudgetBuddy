---
id: ledger-nnn47
title: "Platzhalter-Zählung in LedgerViewTests auf die beiden Ansichts-Rümpfe eingrenzen, statt über die ganze Datei"
status: backlog
type: chore
context: ledger
created: 2026-09-24
completed:
depends_on: []
blocks: []
tags: [captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why

Die in `ledger-n3pqw` ausgelieferten Quelltext-Zusicherungen in
`src/Tests/LedgerViewTests.fs` zählen die Platzhaltertexte `"ohne Payee"` und
`"ohne Kategorie"` über den gesamten Text von
`src/Client/Components/Ledger/View.fs`, Kommentare eingeschlossen. Gemeint ist die
Eigenschaft "der Platzhalter lebt an genau einer Stelle im Modul", gemessen wird aber
zusätzlich die Prosa.

Zwei Folgen, eine davon schon eingetreten:

- Der Worker musste den Doc-Kommentar von `categoryLabel` umformulieren, damit er den
  String nicht mehr nennt, den er beschreibt: aus "sonst `"ohne Kategorie"`" wurde "sonst
  der Platzhaltertext für eine fehlende Kategorie (siehe Rückfallzeile unten)". Die
  Dokumentation wurde von ihrer eigenen Messung geformt.
- Wer künftig einen harmlosen Kommentar schreibt, der einen der beiden Platzhalter nennt,
  bekommt einen grundlos roten Test.

## What

Die Zählung auf die Rümpfe von `entryRow` und `entryTableRow` eingrenzen (oder
Kommentarzeilen ausnehmen), sodass sie weiterhin genau die gemeinte Eigenschaft prüft, ohne
die Prosa zu binden. Die Maschinerie liegt bereits in derselben Testdatei: für ein anderes
Kriterium schneidet der Test die beiden Rümpfe schon über `sliceFunctionBody` heraus.

Danach kann der `categoryLabel`-Kommentar wieder den String nennen, den er dokumentiert.

## Acceptance criteria

- [ ] To be defined during refinement.

## Notes

Captured via `quick-capture` on 2026-09-24 — raw, unrefined. Needs a `modeling` refine pass
before it can be promoted.

Kein dringender Defekt: die Suite ist grün, und die eigentliche Regression (ein abweichender
Platzhalter, wieder in eine der beiden Ansichten eingebaut) wird weiterhin gefangen, sogar
doppelt. Es ist eine Falle für spätere Arbeit an der Datei.

Herkunft: der Verifier der Iteration 1 von `ledger-n3pqw` hat den Befund als
Konstruktionsfehler der Kriteriumsformulierung eingeordnet, nicht der Umsetzung, und per
Mutation belegt, dass der Regressionsschutz erhalten bleibt. Details stehen im `## Outcome`
von `.agentheim/board/ledger/done/ledger-n3pqw-kassenbuch-zeilen-die-kategorie-farbe-an-die-css-variable-bi.md`.
Beim Verfeinern sind `prior_art: [ledger-n3pqw]` und `related_adrs: [0031]` die naheliegenden
Verknüpfungen.
