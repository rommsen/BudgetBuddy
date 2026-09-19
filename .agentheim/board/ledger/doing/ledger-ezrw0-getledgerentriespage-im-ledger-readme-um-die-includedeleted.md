---
id: ledger-ezrw0
title: "getLedgerEntriesPage im ledger-README um die IncludeDeleted-true-Hälfte ergänzen"
status: doing
type: chore
context: ledger
created: 2026-09-19
completed:
depends_on: []
blocks: []
tags: [doku, kassenbuch, readme]
related_adrs: [0017]
related_research: []
prior_art: [ledger-d4kx8, ledger-b1xwe, ledger-p3n8k]
---

## Why
`ledger-d4kx8` hat am 2026-09-19 Romans Entscheidung festgehalten: sind gelöschte Buchungen
eingeblendet, zählen sie bewusst in Trefferzahl **und** Summe, während der Kontostand sie
immer ausklammert. Die Regel steht seitdem im Absatz `**Gelöschte Buchungen**` des
ledger-README.

Der Eintrag zu `getLedgerEntriesPage` unter `## Sichten` beschreibt aber weiterhin nur die
andere Hälfte. Er sagt heute:

> Ohne `IncludeDeleted` bleiben gelöschte Buchungen aus Seite, Trefferzahl und Summe außen
> vor, sodass die sichtbare Liste sich zum Kontostand summiert

Das ist richtig, aber unvollständig: was bei eingeschaltetem Schalter gilt, steht dort nicht.
Wer die Sicht nachschlägt, um sie zu benutzen, liest die halbe Regel und muss den zweiten
Absatz kennen, um die andere Hälfte zu finden.

Der Verifier von `ledger-d4kx8` hat das ausdrücklich als Backlog-Kandidaten benannt und
zugleich festgestellt, dass daran kein Kriterium jener Aufgabe hängt: der Eintrag wird
unvollständig, nicht falsch.

## What
Den `getLedgerEntriesPage`-Eintrag unter `## Sichten` im ledger-README um die
`IncludeDeleted = true`-Hälfte ergänzen: eingeblendet zählen gelöschte Buchungen in
Trefferzahl und Summe mit, der Kontostand (`getLedgerAccountBalances` /
`ledgerBalancesByAccount`) lässt sie weiterhin immer draußen, und die Abweichung zwischen
Summenzeile und Kontostand ist dadurch gewollt.

**Ein Werkzeug-Hinweis, der eine Iteration spart.** Dieser Eintrag ist ein eingerückter
Unterpunkt, der mit Inline-Code beginnt (`` `getLedgerEntriesPage` (ledger-b1xwe) — … ``).
`applyReadmeDelta` adressiert ausschließlich Zeilen, die an Spalte 0 mit `- ` beginnen
(`BULLET_START_RE`). Ein `replace` auf diesen Eintrag findet seinen Anker nie, liefert
`anchor-missing` und hängt den Text stattdessen ans Ende der letzten Bullet-Liste des
Abschnitts. Genau das ist `ledger-d4kx8` in Iteration 2 passiert, weshalb dort von Hand
gesetzt wurde. Wer diese Aufgabe umsetzt, sollte das einkalkulieren und die Grenze im
Ergebnis benennen, statt gegen sie zu laufen.

## Acceptance criteria
- [ ] Der `getLedgerEntriesPage`-Eintrag unter `## Sichten` nennt beide Hälften: das
      Verhalten ohne und mit `IncludeDeleted`.
- [ ] Die Aussage deckt sich wörtlich mit dem Verhalten im Code (`getLedgerEntriesPage` in
      `src/Server/Ledger/QueryHandlers.fs`) und mit dem Absatz `**Gelöschte Buchungen**`
      weiter unten im selben README. Kein dritter, abweichender Wortlaut.
- [ ] Die bestehende Aussage über den Kontostand bleibt erhalten und wird nicht
      abgeschwächt: gelöschte Buchungen bleiben dort immer draußen.
- [ ] Falls das Delta-Werkzeug den Eintrag nicht erreichen kann, steht das im Ergebnis
      benannt, statt dass der Text an einer anderen Stelle landet.

## Notes
Erfasst am 2026-09-19 aus der Verifikation von `ledger-d4kx8` (Iteration 2, PASS). Reine
Doku-Aufgabe: am Code ändert sich nichts, die Regel selbst ist entschieden und umgesetzt.

Zusammenhang: die Summenzeile über der Liste ist `ledger-vgfw3`. Diese Aufgabe hier stellt
sicher, dass deren Worker die Regel auch dann findet, wenn er über die Sichten-Liste
einsteigt und nicht über den Oberflächen-Abschnitt.
