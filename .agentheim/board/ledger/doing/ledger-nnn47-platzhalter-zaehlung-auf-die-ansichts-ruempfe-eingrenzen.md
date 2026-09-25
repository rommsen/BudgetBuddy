---
id: ledger-nnn47
title: "Platzhalter-Zählung in LedgerViewTests auf Code eingrenzen (Kommentarzeilen ausnehmen), damit ein Doc-Kommentar den Platzhalter wieder nennen darf"
status: doing
type: chore
context: ledger
created: 2026-09-24
completed:
depends_on: []
blocks: []
tags: [tests, kassenbuch, quelltext-zusicherung]
related_adrs: [0031]
related_research: []
prior_art: [ledger-n3pqw]
---

## Why

Die in `ledger-n3pqw` ausgelieferten Quelltext-Zusicherungen in
`src/Tests/LedgerViewTests.fs` (Tests "'ohne Kategorie' steht genau einmal, in
categoryLabel" und "'ohne Payee' steht genau einmal, in payeeLabel", Zeile 274 bis 283)
zählen die Platzhaltertexte über den gesamten Text von
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

Die Zählung auf Code eingrenzen: ein privater Helfer `withoutCommentLines : string -> string`
in `src/Tests/LedgerViewTests.fs` entfernt jede Zeile, deren getrimmter Anfang `//` ist
(deckt `///`-Doc-Kommentare und `//`-Zeilenkommentare ab; Blockkommentare `(* *)` kommen in
`View.fs` nicht vor und werden nicht behandelt). Die beiden Zähl-Tests zählen danach über
`withoutCommentLines view` statt über `view`, Erwartungswert bleibt 1.

**Warum Kommentarzeilen ausnehmen und nicht auf die Rümpfe von `entryRow` und
`entryTableRow` einschränken** (die Erfassung ließ beides offen): die Eigenschaft ist
"genau eine Stelle im Modul". Eine Einschränkung auf die zwei bekannten Ansichten würde
eine dritte Kopie des Platzhalters (etwa im `historySheet`) durchlassen, und genau so eine
stille Abweichung soll der Test fangen. Kommentarzeilen auszunehmen behält die Modulweite
und löst trotzdem den Fund.

Der Helfer bekommt einen eigenen Test gegen synthetischen Quelltext, damit er selbst
festgenagelt ist und nicht nur durch die Zähl-Tests indirekt geprüft wird.

**Nicht in dieser Aufgabe:** der Doc-Kommentar an `categoryLabel`
(`src/Client/Components/Ledger/View.fs`, Zeile 203 bis 210) bleibt unverändert. `ledger-h6ptn`
ersetzt ihn ohnehin und darf ihn wieder wörtlich formulieren, sobald diese Aufgabe in
`done/` liegt (keine harte Abhängigkeit, nur Dateiüberschneidung in der Testdatei). Diese Aufgabe fasst ausschließlich `src/Tests/LedgerViewTests.fs` an.

## Acceptance criteria

- [ ] `src/Tests/LedgerViewTests.fs` enthält einen privaten Helfer `withoutCommentLines`,
      und die beiden Zähl-Tests für `"ohne Kategorie"` und `"ohne Payee"` zählen über sein
      Ergebnis, weiterhin mit Erwartungswert 1.
- [ ] Ein Test pinnt den Helfer an synthetischem Quelltext: eine Quelle mit dem Literal je
      einmal in einer `///`-Zeile, einer `//`-Zeile und einer Codezeile ergibt nach
      `withoutCommentLines` und `countOccurrences` genau 1; dieselbe Quelle ohne die
      Codezeile ergibt 0. Eine Codezeile, in der der String nach einem Code-Anteil steht
      (`let x = "ohne Payee" // Kommentar`), zählt weiterhin mit.
- [ ] Mutationsprobe im Ergebnis dokumentiert, nicht committet: ein zusätzlich in den Rumpf
      von `entryRow` eingebautes Literal `"ohne Kategorie"` macht den Zähl-Test rot; eine
      zusätzlich eingefügte `///`-Zeile mit demselben Literal lässt ihn grün.
- [ ] `src/Client/Components/Ledger/View.fs` ist im Diff dieser Aufgabe unverändert.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes

Erfasst per `quick-capture` am 2026-09-24, verfeinert am 2026-09-25.

Kein dringender Defekt: die Suite ist grün, und die eigentliche Regression (ein abweichender
Platzhalter, wieder in eine der beiden Ansichten eingebaut) wird weiterhin gefangen, sogar
doppelt. Es ist eine Falle für spätere Arbeit an der Datei.

Herkunft: der Verifier der Iteration 1 von `ledger-n3pqw` hat den Befund als
Konstruktionsfehler der Kriteriumsformulierung eingeordnet, nicht der Umsetzung, und per
Mutation belegt, dass der Regressionsschutz erhalten bleibt. Details stehen im `## Outcome`
von `.agentheim/board/ledger/done/ledger-n3pqw-kassenbuch-zeilen-die-kategorie-farbe-an-die-css-variable-bi.md`.

Die Testdatei hat bereits `countOccurrences` und `readLedgerView` als Helfer (Zeile 227 ff.
liegt `sliceFunctionBody`, das für ein anderes Kriterium Rümpfe herausschneidet); der neue
Helfer gehört daneben.
