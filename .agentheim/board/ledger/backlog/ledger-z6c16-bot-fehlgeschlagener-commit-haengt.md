---
id: ledger-z6c16
title: "Bot-Commit, der mit einer Ausnahme endet, bleibt als fehlerhafte Aufgabe gespeichert und liefert bis zum Purge 500"
status: backlog
type: bug
context: ledger
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
`src/Server/BotApi.fs:76-78`, `:362-374`: weitere Versuche mit derselben Vorschau bekommen 500 ohne Angabe, ob schon gebucht wurde. Über eine neue Vorschau kann Bob doppelt buchen (Audit B1 M1, C B5).

## What
Ein gescheiterter Commit hinterlässt einen definierten Zustand, den eine Wiederholung erkennt und beantwortet.

## Acceptance criteria
- [ ] Kaputt-Szenario: Commit wirft, zweiter Commit derselben Vorschau liefert eine definierte Antwort statt 500; Test rot vor dem Fix.
- [ ] Die Antwort sagt, ob eine Buchung angelegt wurde.

## Notes
Migrationsschritt P5. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Hängt fachlich mit dem entry_id-Bug zusammen; im Refine prüfen, ob beide in einem Task besser liegen.
