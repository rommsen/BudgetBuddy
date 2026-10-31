---
id: banking-import-v8n3k
title: "Verbindungstest bei Connected scheitert vermutlich am Kontenabruf (404)"
status: backlog
type: bug
context: banking-import
created: 2026-10-31
completed:
depends_on: [banking-import-h2n8q]
blocks: []
tags: [backend, comdirect]
related_adrs: [0071]
related_research: []
prior_art: [banking-import-tshq9]
---

## Why
ADR 0071 Punkt 2 lässt den Verbindungstest bei `Connected` die Konten lesen. Laut Live-Befund
lieferte `getAccounts` in allen sechs Runden 404. Roman sieht dann einen Fehler, obwohl die
Verbindung lebt und Umsätze kommen.

## What
Nach `banking-import-h2n8q` die echte Ursache klären und den Kontenpfad korrigieren. Falls die
Konten nicht lesbar werden, den Test bei `Connected` auf einen anderen leichten Abruf stellen.

## Acceptance criteria
- [ ] Kaputt: GIVEN `Connected` und eine lebende Verbindung WHEN `testComdirectConnection` THEN Erfolg statt 404.
- [ ] Intakt: GIVEN `Connected` und die Bank lehnt ab THEN Fehler ohne Challenge.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Die Prüfung braucht eine echte Verbindung, also Romans TAN. Das Verhalten stammt aus der Messung
in `banking-import-p5mcp`, nach `banking-import-tshq9` ist es nicht erneut beobachtet.