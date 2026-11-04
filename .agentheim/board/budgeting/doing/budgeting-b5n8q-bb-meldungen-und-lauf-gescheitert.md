---
id: budgeting-b5n8q
title: "BB-eigene Ablehnungen nicht als \"YNAB hat abgelehnt\", Targets-Durchlauf mit n von n Fehlschlägen als gescheitert"
status: doing
type: bug
context: budgeting
created: 2026-11-04
completed:
depends_on: []
blocks: [budgeting-c2t9w]
tags: [budget, meldungen, historie]
related_adrs: []
related_research: []
prior_art: [budgeting-c6vn4]
---

## Why
Aus budgeting-h9c3w (Backend-Review zu c6vn4, Punkte 4 und 5). BB soll bald führend werden; Meldungen müssen sagen, wer abgelehnt hat.

## What

### 1. "YNAB hat abgelehnt" für BB-eigene Ablehnungen
`YnabError.InvalidResponse` wird als "YNAB hat abgelehnt: ..." übersetzt (`ynabErrorText`, `src/Shared/Domain.fs`). Zwei Stellen nutzen ihn für Ablehnungen, die BB selbst ausspricht:
- `src/Server/Budgeting/CommandHandlers.fs`, `plan`: `planTargetApplication` lehnt ab (etwa ein Betrag außerhalb des 10-Milliunits-Rasters), Ergebnis `InvalidResponse message`.
- `src/Server/Budgeting/CommandHandlers.fs`, Erneut-Weg (`retryFailed`, etwa Zeile 573): `loadStates` liefert `Ok []`, Ergebnis `InvalidResponse "Unbekannte Anwendung"`.

Entscheidung: neuer Fall `YnabError.RefusedByBb of message: string`, `ynabErrorText` gibt die Meldung unverändert aus (sie ist schon konkret). Beide Stellen nutzen ihn; der Erneut-Weg meldet "Targets-Anwendung nicht gefunden". Alle `match` auf `YnabError` ziehen mit (Compiler), auch die Antwort an Bob, falls betroffen (dort nur Text).

### 2. `RunPartlyFailed` bei n von n
`src/Shared/Budgeting.fs`, `runStatus` (etwa Zeilen 838 bis 850): bei Targets anwenden mit mehreren Kategorien heißt der Durchlauf immer "teilweise fehlgeschlagen", auch wenn alle scheitern ("3 von 3"). Entscheidung: scheitern alle, ist der Durchlauf `RunFailed` mit dem ersten Grund; die Gründe je Kategorie stehen weiter in den Einzelzeilen. Doc-Kommentare an `RunPartlyFailed` und `HistoryRun.Status` korrigieren.

## Acceptance criteria
- [ ] Handler-Test: Vorschau mit einer Planung, die BB ablehnt, liefert `RefusedByBb`, Nutzertext ohne "YNAB".
- [ ] Handler-Test: Erneut-Weg mit unbekannter Anwendung liefert `RefusedByBb`, Text "Targets-Anwendung nicht gefunden".
- [ ] Reine Tests für `runStatus`: 3 von 3 gescheitert ist `RunFailed` mit dem ersten Grund; 1 von 3 bleibt `RunPartlyFailed (1, 3)`; eine Kategorie gescheitert bleibt `RunFailed`.
- [ ] Bestehende Tests, die die alten Texte erwarten, sind auf die neuen Fälle umgestellt, nicht gelöscht.
- [ ] Fable.Remoting: ein neuer Fall in `YnabError` bricht keine Fixture in `scripts/*.test.mjs` (prüfen, im Outcome nennen).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Kein Umbau von `YnabError` zu einem eigenen BudgetError-Typ (Kann-Befund, bleibt in budgeting-h9c3w).
